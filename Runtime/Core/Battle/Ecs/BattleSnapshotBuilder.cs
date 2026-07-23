using System;
using System.Collections.Generic;

namespace Combat.Core.Battle
{
    internal static class BattleSnapshotBuilder
    {
        private static readonly StatusInstance[] EmptyStatuses = Array.Empty<StatusInstance>();

        public static bool TryGetUnitRuntimeSnapshot(
            BattleWorld world,
            UnitId unitId,
            BattleTick tick,
            out UnitRuntimeSnapshot snapshot)
        {
            if (!world.TryFindEntity(unitId, out EntityId entity)
                || !world.UnitComponents.TryGet(entity, out UnitComponent unit)
                || !world.TeamComponents.TryGet(entity, out TeamComponent team)
                || !world.PositionComponents.TryGet(entity, out PositionComponent position)
                || !world.FacingComponents.TryGet(entity, out FacingComponent facing)
                || !world.HealthComponents.TryGet(entity, out HealthComponent health)
                || !world.LifeStateComponents.TryGet(entity, out LifeStateComponent lifeState)
                || !world.AbilityComponents.TryGet(entity, out AbilityComponent abilities))
            {
                snapshot = default;
                return false;
            }

            bool hasBrain = world.BrainComponents.TryGet(entity, out BrainComponent brain);
            UnitId targetUnitId = default;
            bool hasTarget = world.TargetComponents.TryGet(entity, out TargetComponent target)
                && target.Target.IsValid
                && world.TryGetUnitId(target.Target, out targetUnitId);

            IReadOnlyList<StatusInstance> statuses = world.StatusComponents.TryGet(entity, out StatusComponent statusComponent)
                ? statusComponent.Statuses
                : EmptyStatuses;

            snapshot = new UnitRuntimeSnapshot(
                tick,
                unit.UnitId,
                unit.DefinitionId,
                team.TeamId,
                position.Position,
                facing.Direction,
                position.Radius.ToFloat(),
                health.Current,
                BattleStatResolver.ResolveMaxHealth(world, entity),
                lifeState.State.ToString(),
                hasBrain,
                hasBrain ? brain.DefinitionId : string.Empty,
                hasBrain ? brain.Kind.ToString() : string.Empty,
                hasBrain ? brain.State.ToString() : string.Empty,
                hasBrain ? brain.StateEnteredTick : default,
                hasTarget,
                targetUnitId,
                BattleStatResolver.ResolveScalar(world, entity, BattleStatId.MoveSpeed).ToFloat(),
                CreateAbilitySnapshots(abilities),
                CreateStatusSnapshots(world, statuses));
            return true;
        }

        private static AbilityRuntimeSnapshot[] CreateAbilitySnapshots(AbilityComponent component)
        {
            IReadOnlyList<AbilityState> abilities = component.Abilities;
            var snapshots = new AbilityRuntimeSnapshot[abilities.Count];
            for (var i = 0; i < abilities.Count; i++)
            {
                AbilityState ability = abilities[i];
                snapshots[i] = new AbilityRuntimeSnapshot(
                    i,
                    i == AbilityComponent.BasicAbilityIndex,
                    ability.Id,
                    ability.Range.ToFloat(),
                    GetAbilityDamage(ability.EffectFrames),
                    ability.CooldownTicks,
                    ability.CooldownRemainingTicks);
            }

            return snapshots;
        }

        private static int GetAbilityDamage(IReadOnlyList<AbilityEffectFrameData> frames)
        {
            var total = 0;
            for (var frameIndex = 0; frameIndex < frames.Count; frameIndex++)
            {
                IReadOnlyList<BattleEffectData> effects = frames[frameIndex].Effects;
                for (var i = 0; i < effects.Count; i++)
                {
                    BattleEffectData effect = effects[i];
                    if (effect.Type == BattleEffectType.Damage)
                    {
                        total += effect.Amount;
                    }
                }
            }

            return total;
        }

        private static StatusRuntimeSnapshot[] CreateStatusSnapshots(BattleWorld world, IReadOnlyList<StatusInstance> statuses)
        {
            var snapshots = new StatusRuntimeSnapshot[statuses.Count];
            for (var i = 0; i < statuses.Count; i++)
            {
                StatusInstance status = statuses[i];
                bool hasSourceUnit = world.TryGetUnitId(status.Source, out UnitId sourceUnitId);
                snapshots[i] = new StatusRuntimeSnapshot(
                    status.Id,
                    status.Polarity,
                    hasSourceUnit,
                    hasSourceUnit ? sourceUnitId : default,
                    status.DurationRemainingTicks,
                    status.TickIntervalTicks,
                    status.TicksUntilNextPeriodicEffect,
                    status.PeriodicDamage,
                    status.Modifiers.Count,
                    status.Triggers.Count,
                    status.StackCount,
                    status.MaxStacks);
            }

            return snapshots;
        }
    }
}
