using System.Collections.Generic;
using Combat.Foundation.Events;

namespace Combat.Core.Battle
{
    internal static class TargetingSystem
    {
        public static void Run(BattleWorld world)
        {
            Run(world, null, null, default);
        }

        public static void Run(BattleWorld world, EventBuffer<BattleEvent> events, EventSequence eventSequence, BattleTick tick)
        {
            IReadOnlyList<EntityId> entities = world.UnitComponents.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                EntityId entity = entities[i];
                if (!world.IsBattlefieldActiveUnit(entity)
                    || !world.TeamComponents.TryGet(entity, out TeamComponent team))
                {
                    continue;
                }

                if (BattleIntentFilters.TryGetFocusTarget(world, entity, out EntityId focusTarget))
                {
                    if (BattleIntentFilters.IsValidFocusTarget(world, entity, focusTarget))
                    {
                        SetTarget(world, entity, focusTarget);
                        world.TargetingStateComponents.Set(entity, default);
                        TryFaceUnitTowardsTarget(world, entity, focusTarget, events, eventSequence, tick);
                    }
                    else
                    {
                        ClearAutomaticTarget(world, entity);
                    }

                    continue;
                }

                if (!BattleIntentFilters.AllowsAutoTargetSelection(world, entity))
                {
                    ClearPendingAttacker(world, entity);
                    continue;
                }

                TargetingBehaviorComponent behavior =
                    world.TargetingBehaviorComponents.TryGet(
                        entity,
                        out TargetingBehaviorComponent configuredBehavior)
                        ? configuredBehavior
                        : default;
                TargetingStateComponent state = GetActiveState(world, entity, tick);
                if (TryGetValidEnemyTarget(world, entity, team.TeamId, out EntityId currentTarget))
                {
                    if (!ShouldReleaseForNoProgress(
                        world,
                        entity,
                        currentTarget,
                        behavior,
                        state,
                        tick,
                        out state))
                    {
                        world.TargetingStateComponents.Set(entity, WithoutPendingAttacker(state));
                        TryFaceUnitTowardsTarget(
                            world,
                            entity,
                            currentTarget,
                            events,
                            eventSequence,
                            tick);
                        continue;
                    }

                    SetTarget(world, entity, default);
                }

                if (TryConsumePendingAttacker(
                    world,
                    team.TeamId,
                    state,
                    out EntityId attacker))
                {
                    state = BeginTrackingTarget(
                        world,
                        entity,
                        state,
                        attacker,
                        clearRejection: true);
                    world.TargetingStateComponents.Set(entity, state);
                    SetTarget(world, entity, attacker);
                    TryFaceUnitTowardsTarget(
                        world,
                        entity,
                        attacker,
                        events,
                        eventSequence,
                        tick);
                    continue;
                }

                EntityId nearest = FindNearestEnemy(
                    world,
                    entity,
                    team.TeamId,
                    behavior,
                    state);
                SetTarget(world, entity, nearest);
                world.TargetingStateComponents.Set(
                    entity,
                    nearest.IsValid
                        ? BeginTrackingTarget(
                            world,
                            entity,
                            WithoutPendingAttacker(state),
                            nearest,
                            clearRejection: false)
                        : ClearTrackingAndPending(state));
                TryFaceUnitTowardsTarget(world, entity, nearest, events, eventSequence, tick);
            }
        }

        public static void RecordDamageSource(
            BattleWorld world,
            EntityId attacker,
            EntityId defender)
        {
            if (world == null
                || !world.IsBattlefieldActiveUnit(attacker)
                || !world.IsBattlefieldActiveUnit(defender)
                || !world.TryGetTeamId(attacker, out TeamId attackerTeam)
                || !world.TryGetTeamId(defender, out TeamId defenderTeam)
                || attackerTeam.Equals(defenderTeam)
                || !BattleIntentFilters.AllowsAutoTargetSelection(world, defender)
                || TryGetValidEnemyTarget(
                    world,
                    defender,
                    defenderTeam,
                    out _))
            {
                return;
            }

            TargetingStateComponent state =
                world.TargetingStateComponents.TryGet(
                    defender,
                    out TargetingStateComponent existing)
                    ? existing
                    : default;
            if (state.PendingAttacker.IsValid
                && IsValidEnemy(world, defenderTeam, state.PendingAttacker))
            {
                return;
            }

            world.TargetingStateComponents.Set(
                defender,
                new TargetingStateComponent(
                    state.TrackedTarget,
                    state.ProgressBaseline,
                    state.NoProgressTicks,
                    state.RejectedTarget,
                    state.RejectedUntilTick,
                    attacker));
        }

        public static void ClearAutomaticTarget(BattleWorld world, EntityId entity)
        {
            if (world == null)
            {
                return;
            }

            SetTarget(world, entity, default);
            if (world.TargetingStateComponents.Has(entity))
            {
                world.TargetingStateComponents.Set(entity, default);
            }
        }

        private static void TryFaceUnitTowardsTarget(
            BattleWorld world,
            EntityId entity,
            EntityId target,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            if (UnitControlRules.CanTurn(world, entity))
            {
                world.TryFaceUnitTowards(entity, target, events, eventSequence, tick);
            }
        }

        private static bool TryGetValidEnemyTarget(BattleWorld world, EntityId entity, TeamId teamId, out EntityId targetEntity)
        {
            if (!world.TargetComponents.TryGet(entity, out TargetComponent target)
                || !BattleUnitQuery.TryGetAliveUnit(world, target.Target, out BattleUnitQueryResult targetUnit)
                || !BattleUnitQuery.IsTeamAllowed(teamId, targetUnit.TeamId, BattleTargetTeamFilter.Enemies))
            {
                targetEntity = default;
                return false;
            }

            targetEntity = target.Target;
            return true;
        }

        private static EntityId FindNearestEnemy(
            BattleWorld world,
            EntityId entity,
            TeamId teamId,
            TargetingBehaviorComponent behavior,
            TargetingStateComponent state)
        {
            if (!world.PositionComponents.TryGet(entity, out PositionComponent position))
            {
                return default;
            }

            bool found = behavior.LimitsAcquisitionRange
                ? BattleUnitQuery.TrySelectNearestInRadius(
                    world,
                    position.Position,
                    behavior.AcquisitionRange,
                    teamId,
                    BattleTargetTeamFilter.Enemies,
                    state.RejectedTarget,
                    out BattleUnitQueryResult nearest)
                : BattleUnitQuery.TrySelectNearest(
                    world,
                    position.Position,
                    teamId,
                    BattleTargetTeamFilter.Enemies,
                    state.RejectedTarget,
                    out nearest);
            return found
                ? nearest.Entity
                : default;
        }

        private static bool ShouldReleaseForNoProgress(
            BattleWorld world,
            EntityId entity,
            EntityId target,
            TargetingBehaviorComponent behavior,
            TargetingStateComponent state,
            BattleTick tick,
            out TargetingStateComponent nextState)
        {
            if (behavior.NoProgressTimeoutTicks <= 0
                || !TryGetRemainingDistance(
                    world,
                    entity,
                    target,
                    out BattleScalar remainingDistance))
            {
                nextState = BeginTrackingTarget(
                    world,
                    entity,
                    state,
                    target,
                    clearRejection: false);
                return false;
            }

            if (remainingDistance == BattleScalar.Zero
                || !UnitControlRules.CanMove(world, entity))
            {
                nextState = new TargetingStateComponent(
                    target,
                    remainingDistance,
                    noProgressTicks: 0,
                    state.RejectedTarget,
                    state.RejectedUntilTick,
                    pendingAttacker: default);
                return false;
            }

            if (!state.TrackedTarget.Equals(target))
            {
                nextState = new TargetingStateComponent(
                    target,
                    remainingDistance,
                    noProgressTicks: 0,
                    state.RejectedTarget,
                    state.RejectedUntilTick,
                    pendingAttacker: default);
                return false;
            }

            BattleScalar improvement =
                state.ProgressBaseline - remainingDistance;
            if (improvement >= behavior.MinimumProgressDistance)
            {
                nextState = new TargetingStateComponent(
                    target,
                    remainingDistance,
                    noProgressTicks: 0,
                    state.RejectedTarget,
                    state.RejectedUntilTick,
                    pendingAttacker: default);
                return false;
            }

            int noProgressTicks = state.NoProgressTicks == int.MaxValue
                ? int.MaxValue
                : state.NoProgressTicks + 1;
            if (noProgressTicks < behavior.NoProgressTimeoutTicks)
            {
                nextState = new TargetingStateComponent(
                    target,
                    state.ProgressBaseline,
                    noProgressTicks,
                    state.RejectedTarget,
                    state.RejectedUntilTick,
                    pendingAttacker: default);
                return false;
            }

            int rejectedUntilTick =
                tick.Value > int.MaxValue - behavior.RejectedTargetCooldownTicks
                    ? int.MaxValue
                    : tick.Value + behavior.RejectedTargetCooldownTicks;
            nextState = new TargetingStateComponent(
                trackedTarget: default,
                progressBaseline: BattleScalar.Zero,
                noProgressTicks: 0,
                rejectedTarget: target,
                rejectedUntilTick,
                pendingAttacker: default);
            return true;
        }

        private static TargetingStateComponent GetActiveState(
            BattleWorld world,
            EntityId entity,
            BattleTick tick)
        {
            TargetingStateComponent state =
                world.TargetingStateComponents.TryGet(
                    entity,
                    out TargetingStateComponent existing)
                    ? existing
                    : default;
            return state.RejectedTarget.IsValid
                && tick.Value >= state.RejectedUntilTick
                    ? new TargetingStateComponent(
                        state.TrackedTarget,
                        state.ProgressBaseline,
                        state.NoProgressTicks,
                        rejectedTarget: default,
                        rejectedUntilTick: 0,
                        state.PendingAttacker)
                    : state;
        }

        private static bool TryConsumePendingAttacker(
            BattleWorld world,
            TeamId teamId,
            TargetingStateComponent state,
            out EntityId attacker)
        {
            attacker = state.PendingAttacker;
            return attacker.IsValid && IsValidEnemy(world, teamId, attacker);
        }

        private static bool IsValidEnemy(
            BattleWorld world,
            TeamId sourceTeam,
            EntityId candidate)
        {
            return BattleUnitQuery.TryGetAliveUnit(
                    world,
                    candidate,
                    out BattleUnitQueryResult unit)
                && BattleUnitQuery.IsTeamAllowed(
                    sourceTeam,
                    unit.TeamId,
                    BattleTargetTeamFilter.Enemies);
        }

        private static TargetingStateComponent BeginTrackingTarget(
            BattleWorld world,
            EntityId entity,
            TargetingStateComponent state,
            EntityId target,
            bool clearRejection)
        {
            BattleScalar progressBaseline =
                TryGetRemainingDistance(
                    world,
                    entity,
                    target,
                    out BattleScalar remainingDistance)
                    ? remainingDistance
                    : BattleScalar.Zero;
            return new TargetingStateComponent(
                trackedTarget: target,
                progressBaseline,
                noProgressTicks: 0,
                rejectedTarget: clearRejection ? default : state.RejectedTarget,
                rejectedUntilTick: clearRejection ? 0 : state.RejectedUntilTick,
                pendingAttacker: default);
        }

        private static bool TryGetRemainingDistance(
            BattleWorld world,
            EntityId entity,
            EntityId target,
            out BattleScalar remainingDistance)
        {
            if (!world.PositionComponents.TryGet(
                    entity,
                    out PositionComponent position)
                || !world.PositionComponents.TryGet(
                    target,
                    out PositionComponent targetPosition)
                || !world.AbilityComponents.TryGet(
                    entity,
                    out AbilityComponent abilities)
                || !AbilityEngagement.TryGetMovementRange(
                    abilities,
                    out BattleScalar engagementRange))
            {
                remainingDistance = BattleScalar.Zero;
                return false;
            }

            BattleScalar distance = BattleVector2.DistanceScalar(
                position.Position,
                targetPosition.Position);
            remainingDistance = distance > engagementRange
                ? distance - engagementRange
                : BattleScalar.Zero;
            return true;
        }

        private static TargetingStateComponent WithoutPendingAttacker(
            TargetingStateComponent state)
        {
            return new TargetingStateComponent(
                state.TrackedTarget,
                state.ProgressBaseline,
                state.NoProgressTicks,
                state.RejectedTarget,
                state.RejectedUntilTick,
                pendingAttacker: default);
        }

        private static TargetingStateComponent ClearTrackingAndPending(
            TargetingStateComponent state)
        {
            return new TargetingStateComponent(
                trackedTarget: default,
                progressBaseline: BattleScalar.Zero,
                noProgressTicks: 0,
                state.RejectedTarget,
                state.RejectedUntilTick,
                pendingAttacker: default);
        }

        private static void ClearPendingAttacker(BattleWorld world, EntityId entity)
        {
            if (!world.TargetingStateComponents.TryGet(
                entity,
                out TargetingStateComponent state)
                || !state.PendingAttacker.IsValid)
            {
                return;
            }

            world.TargetingStateComponents.Set(
                entity,
                WithoutPendingAttacker(state));
        }

        private static void SetTarget(
            BattleWorld world,
            EntityId entity,
            EntityId target)
        {
            world.TargetComponents.Set(entity, new TargetComponent(target));
        }
    }
}
