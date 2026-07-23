using System;
using System.Collections.Generic;
using Combat.Core.LocalAvoidance;
using Combat.Foundation.Events;

namespace Combat.Core.Battle
{
    internal static class MovementSystem
    {
        private const int OverlapRecoveryPassCount = 4;
        private const int InitialCapacity = 16;

        internal sealed class Scratch
        {
            public readonly LocalAvoidanceWorkspace Avoidance =
                new LocalAvoidanceWorkspace();
            public LocalAvoidanceAgent[] Agents = Array.Empty<LocalAvoidanceAgent>();
            public EntityId[] Entities = Array.Empty<EntityId>();
            public BattleScalar[] MaxStepDistances = Array.Empty<BattleScalar>();
            public BattleVector2[] RecoveredPositions = Array.Empty<BattleVector2>();
            public bool[] RecoveredMoved = Array.Empty<bool>();
            public LocalAvoidanceSolveStats LastSolveStats { get; set; }
            public int LastAnchoredAgentCount { get; set; }

            public void EnsureCapacity(int requiredCapacity)
            {
                if (requiredCapacity < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(requiredCapacity));
                }

                if (Agents.Length >= requiredCapacity)
                {
                    return;
                }

                int capacity = Agents.Length == 0 ? InitialCapacity : Agents.Length;
                while (capacity < requiredCapacity)
                {
                    if (capacity > int.MaxValue / 2)
                    {
                        capacity = requiredCapacity;
                        break;
                    }

                    capacity *= 2;
                }

                Array.Resize(ref Agents, capacity);
                Array.Resize(ref Entities, capacity);
                Array.Resize(ref MaxStepDistances, capacity);
                Array.Resize(ref RecoveredPositions, capacity);
                Array.Resize(ref RecoveredMoved, capacity);
            }
        }

        public static void Run(
            BattleWorld world,
            BattleScalar secondsPerTick,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick,
            Scratch scratch)
        {
            if (secondsPerTick <= BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(secondsPerTick));
            }

            Run(
                world,
                secondsPerTick,
                ticksPerSecond: 0,
                useTickRate: false,
                localAvoidanceEnabled: true,
                events,
                eventSequence,
                tick,
                scratch);
        }

        public static void Run(
            BattleWorld world,
            int ticksPerSecond,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick,
            Scratch scratch)
        {
            if (ticksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            }

            Run(
                world,
                BattleScalar.Zero,
                ticksPerSecond,
                useTickRate: true,
                localAvoidanceEnabled: true,
                events,
                eventSequence,
                tick,
                scratch);
        }

        public static void Run(
            BattleWorld world,
            int ticksPerSecond,
            bool localAvoidanceEnabled,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick,
            Scratch scratch)
        {
            if (ticksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            }

            Run(
                world,
                BattleScalar.Zero,
                ticksPerSecond,
                useTickRate: true,
                localAvoidanceEnabled,
                events,
                eventSequence,
                tick,
                scratch);
        }

        private static void Run(
            BattleWorld world,
            BattleScalar secondsPerTick,
            int ticksPerSecond,
            bool useTickRate,
            bool localAvoidanceEnabled,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick,
            Scratch scratch)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (scratch == null)
            {
                throw new ArgumentNullException(nameof(scratch));
            }

            int agentCount = BuildSnapshot(
                world,
                secondsPerTick,
                ticksPerSecond,
                useTickRate,
                scratch);
            if (!localAvoidanceEnabled)
            {
                CommitPreferredSteps(
                    world,
                    events,
                    eventSequence,
                    tick,
                    scratch,
                    agentCount);
                return;
            }

            var settings = LocalAvoidanceSettings.Default;
            var frame = new LocalAvoidanceFrame(scratch.Agents, agentCount, settings);
            LocalAvoidanceRecoveryResult recovery =
                LocalAvoidanceOverlapRecovery.Resolve(
                    frame,
                    OverlapRecoveryPassCount,
                    scratch.Avoidance);
            ApplyRecoverySnapshot(world, scratch, recovery, agentCount);

            var recoveredFrame = new LocalAvoidanceFrame(
                scratch.Agents,
                agentCount,
                settings);
            LocalAvoidanceSolveResult solve =
                LocalAvoidanceSolver.Solve(recoveredFrame, scratch.Avoidance);
            scratch.LastSolveStats = solve.Stats;
            Commit(
                world,
                events,
                eventSequence,
                tick,
                scratch,
                solve,
                agentCount);
        }

        private static int BuildSnapshot(
            BattleWorld world,
            BattleScalar secondsPerTick,
            int ticksPerSecond,
            bool useTickRate,
            Scratch scratch)
        {
            IReadOnlyList<EntityId> entities = world.UnitComponents.Entities;
            scratch.EnsureCapacity(entities.Count);
            int agentCount = 0;
            for (int i = 0; i < entities.Count; i++)
            {
                EntityId entity = entities[i];
                if (!world.IsBattlefieldActiveUnit(entity)
                    || !world.TryGetUnitId(entity, out UnitId unitId)
                    || !world.TryGetTeamId(entity, out TeamId teamId)
                    || !world.PositionComponents.TryGet(
                        entity,
                        out PositionComponent position))
                {
                    continue;
                }

                BattleScalar maxStepDistance = CalculateMaxStepDistance(
                    world,
                    entity,
                    secondsPerTick,
                    ticksPerSecond,
                    useTickRate);
                BattleVector2 preferredStep = CalculatePreferredStep(
                    world,
                    entity,
                    position.Position,
                    maxStepDistance,
                    scratch,
                    agentCount,
                    useRecoveredTargetPositions: false,
                    out bool stopsAtPreferredStep);
                bool isMoving = preferredStep.SqrMagnitudeScalar > BattleScalar.Epsilon;
                LocalAvoidanceMobility mobility = isMoving
                    ? LocalAvoidanceMobility.Moving
                    : LocalAvoidanceMobility.Anchored;
                if (!isMoving)
                {
                    maxStepDistance = BattleScalar.Zero;
                }

                BattleVector2 heading = world.FacingComponents.TryGet(
                    entity,
                    out FacingComponent facing)
                    ? facing.Direction
                    : BattleVector2.Right;
                scratch.Agents[agentCount] = new LocalAvoidanceAgent(
                    unitId.Value,
                    teamId.Value,
                    position.Position,
                    heading,
                    preferredStep,
                    position.Radius,
                    maxStepDistance,
                    mobility,
                    stopsAtPreferredStep);
                scratch.Entities[agentCount] = entity;
                agentCount++;
            }

            Array.Sort(
                scratch.Agents,
                scratch.Entities,
                0,
                agentCount,
                AgentIdComparer.Instance);
            for (int i = 0; i < agentCount; i++)
            {
                scratch.MaxStepDistances[i] = CalculateMaxStepDistance(
                    world,
                    scratch.Entities[i],
                    secondsPerTick,
                    ticksPerSecond,
                    useTickRate);
            }

            return agentCount;
        }

        private static BattleScalar CalculateMaxStepDistance(
            BattleWorld world,
            EntityId entity,
            BattleScalar secondsPerTick,
            int ticksPerSecond,
            bool useTickRate)
        {
            if (!UnitControlRules.CanMove(world, entity)
                || !BattleStatResolver.TryResolveScalar(
                    world,
                    entity,
                    BattleStatId.MoveSpeed,
                    out BattleScalar effectiveMoveSpeed)
                || effectiveMoveSpeed <= BattleScalar.Zero)
            {
                return BattleScalar.Zero;
            }

            return useTickRate
                ? effectiveMoveSpeed / BattleScalar.FromInt(ticksPerSecond)
                : effectiveMoveSpeed * secondsPerTick;
        }

        private static BattleVector2 CalculatePreferredStep(
            BattleWorld world,
            EntityId entity,
            BattleVector2 position,
            BattleScalar maxStepDistance,
            Scratch scratch,
            int agentCount,
            bool useRecoveredTargetPositions,
            out bool stopsAtPreferredStep)
        {
            stopsAtPreferredStep = false;
            if (maxStepDistance <= BattleScalar.Zero)
            {
                return BattleVector2.Zero;
            }

            if (BattleIntentFilters.TryGetMoveToPosition(
                world,
                entity,
                out BattleVector2 destination))
            {
                return CalculateStep(
                    position,
                    destination,
                    BattleScalar.Zero,
                    maxStepDistance,
                    out stopsAtPreferredStep);
            }

            if (!BattleIntentFilters.AllowsAutoBehavior(world, entity)
                || !world.AbilityComponents.TryGet(
                    entity,
                    out AbilityComponent abilities)
                || !AbilityEngagement.TryGetMovementRange(
                    abilities,
                    out BattleScalar engagementRange)
                || !TryGetMovementTarget(
                    world,
                    entity,
                    position,
                    out EntityId movementTarget,
                    out PositionComponent targetPosition))
            {
                return BattleVector2.Zero;
            }

            BattleVector2 targetWorldPosition = targetPosition.Position;
            if (useRecoveredTargetPositions
                && world.TryGetUnitId(movementTarget, out UnitId targetUnitId)
                && TryFindAgentIndex(scratch.Agents, agentCount, targetUnitId.Value, out int targetIndex))
            {
                targetWorldPosition = scratch.RecoveredPositions[targetIndex];
            }

            return CalculateStep(
                position,
                targetWorldPosition,
                engagementRange,
                maxStepDistance,
                out stopsAtPreferredStep);
        }

        private static bool TryGetMovementTarget(
            BattleWorld world,
            EntityId entity,
            BattleVector2 position,
            out EntityId target,
            out PositionComponent targetPosition)
        {
            if (world.TargetComponents.TryGet(
                    entity,
                    out TargetComponent current)
                && BattleIntentFilters.IsValidFocusTarget(
                    world,
                    entity,
                    current.Target)
                && world.PositionComponents.TryGet(
                    current.Target,
                    out targetPosition))
            {
                target = current.Target;
                return true;
            }

            if (!BattleIntentFilters.AllowsAutoTargetSelection(world, entity)
                || !world.TryGetTeamId(entity, out TeamId teamId)
                || !BattleUnitQuery.TrySelectNearest(
                    world,
                    position,
                    teamId,
                    BattleTargetTeamFilter.Enemies,
                    GetRejectedTarget(world, entity),
                    out BattleUnitQueryResult nearest)
                || !world.PositionComponents.TryGet(
                    nearest.Entity,
                    out targetPosition))
            {
                target = default;
                targetPosition = default;
                return false;
            }

            target = nearest.Entity;
            return true;
        }

        private static EntityId GetRejectedTarget(
            BattleWorld world,
            EntityId entity)
        {
            return world.TargetingStateComponents.TryGet(
                    entity,
                    out TargetingStateComponent state)
                ? state.RejectedTarget
                : default;
        }

        private static BattleVector2 CalculateStep(
            BattleVector2 position,
            BattleVector2 destination,
            BattleScalar stopDistance,
            BattleScalar maxStepDistance,
            out bool stopsAtPreferredStep)
        {
            stopsAtPreferredStep = false;
            BattleVector2 offset = destination - position;
            BattleScalar distance = offset.MagnitudeScalar;
            if (distance <= stopDistance)
            {
                return BattleVector2.Zero;
            }

            BattleScalar remainingDistance = distance - stopDistance;
            stopsAtPreferredStep = remainingDistance <= maxStepDistance;
            BattleScalar moveDistance = maxStepDistance <= remainingDistance
                ? maxStepDistance
                : remainingDistance;
            if (moveDistance <= BattleScalar.Zero)
            {
                return BattleVector2.Zero;
            }

            return offset.Normalized * moveDistance;
        }

        private static void ApplyRecoverySnapshot(
            BattleWorld world,
            Scratch scratch,
            LocalAvoidanceRecoveryResult recovery,
            int agentCount)
        {
            for (int i = 0; i < recovery.Count; i++)
            {
                LocalAvoidanceRecoveredAgent recovered = recovery[i];
                scratch.RecoveredPositions[i] = recovered.Position;
                scratch.RecoveredMoved[i] = recovered.WasMoved;
            }

            scratch.LastAnchoredAgentCount = 0;
            for (int i = 0; i < recovery.Count; i++)
            {
                LocalAvoidanceRecoveredAgent recovered = recovery[i];
                LocalAvoidanceAgent original = scratch.Agents[i];
                BattleScalar maxStepDistance = scratch.MaxStepDistances[i];
                BattleVector2 preferredStep = CalculatePreferredStep(
                    world,
                    scratch.Entities[i],
                    recovered.Position,
                    maxStepDistance,
                    scratch,
                    agentCount,
                    useRecoveredTargetPositions: true,
                    out bool stopsAtPreferredStep);
                bool isMoving = preferredStep.SqrMagnitudeScalar > BattleScalar.Epsilon;
                if (!isMoving)
                {
                    maxStepDistance = BattleScalar.Zero;
                    scratch.LastAnchoredAgentCount++;
                }

                scratch.Agents[i] = new LocalAvoidanceAgent(
                    original.AgentId,
                    original.GroupId,
                    recovered.Position,
                    original.Heading,
                    preferredStep,
                    original.Radius,
                    maxStepDistance,
                    isMoving
                        ? LocalAvoidanceMobility.Moving
                        : LocalAvoidanceMobility.Anchored,
                    stopsAtPreferredStep);
            }
        }

        private static bool TryFindAgentIndex(
            LocalAvoidanceAgent[] agents,
            int agentCount,
            int agentId,
            out int index)
        {
            int low = 0;
            int high = agentCount - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                int middleId = agents[middle].AgentId;
                if (middleId == agentId)
                {
                    index = middle;
                    return true;
                }

                if (middleId < agentId)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            index = -1;
            return false;
        }

        private static void CommitPreferredSteps(
            BattleWorld world,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick,
            Scratch scratch,
            int agentCount)
        {
            scratch.LastSolveStats = default;
            scratch.LastAnchoredAgentCount = 0;
            for (int i = 0; i < agentCount; i++)
            {
                LocalAvoidanceAgent agent = scratch.Agents[i];
                if (agent.Mobility == LocalAvoidanceMobility.Anchored)
                {
                    scratch.LastAnchoredAgentCount++;
                }

                scratch.RecoveredPositions[i] = agent.Position;
                scratch.RecoveredMoved[i] = false;
                CommitStep(
                    world,
                    events,
                    eventSequence,
                    tick,
                    scratch.Entities[i],
                    agent,
                    agent.Position + agent.PreferredStep,
                    agent.PreferredStep,
                    wasRecovered: false);
            }
        }

        private static void Commit(
            BattleWorld world,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick,
            Scratch scratch,
            LocalAvoidanceSolveResult solve,
            int agentCount)
        {
            for (int i = 0; i < agentCount; i++)
            {
                LocalAvoidanceAgent agent = scratch.Agents[i];
                LocalAvoidanceDecision decision = solve[i];
                EntityId entity = scratch.Entities[i];
                BattleVector2 nextPosition = scratch.RecoveredPositions[i]
                    + decision.SelectedStep;
                CommitStep(
                    world,
                    events,
                    eventSequence,
                    tick,
                    entity,
                    agent,
                    nextPosition,
                    decision.SelectedStep,
                    scratch.RecoveredMoved[i]);
            }
        }

        private static void CommitStep(
            BattleWorld world,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick,
            EntityId entity,
            LocalAvoidanceAgent agent,
            BattleVector2 nextPosition,
            BattleVector2 selectedStep,
            bool wasRecovered)
        {
            bool selectedStepMoved = selectedStep.XRaw != 0L
                || selectedStep.YRaw != 0L;
            if (selectedStep.SqrMagnitudeScalar > BattleScalar.Epsilon
                && UnitControlRules.CanTurn(world, entity))
            {
                world.TrySetUnitFacing(
                    entity,
                    selectedStep,
                    events,
                    eventSequence,
                    tick);
            }

            world.PositionComponents.Set(
                entity,
                new PositionComponent(nextPosition, agent.Radius));
            if (!wasRecovered && !selectedStepMoved)
            {
                return;
            }

            if (!world.TryGetUnitId(entity, out UnitId unitId)
                || !world.TryGetTeamId(entity, out TeamId teamId))
            {
                return;
            }

            events.Write(BattleEvent.UnitMoved(
                eventSequence.Next(),
                tick,
                unitId,
                teamId,
                nextPosition));
        }

        private sealed class AgentIdComparer : IComparer<LocalAvoidanceAgent>
        {
            internal static readonly AgentIdComparer Instance = new AgentIdComparer();

            public int Compare(LocalAvoidanceAgent left, LocalAvoidanceAgent right)
            {
                return left.AgentId.CompareTo(right.AgentId);
            }
        }
    }
}
