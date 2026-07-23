using System;
using System.Collections.Generic;
using Combat.Core.Battle;

namespace Combat.Core.LocalAvoidance
{
    internal readonly struct LocalAvoidanceRecoveredAgent
    {
        internal LocalAvoidanceRecoveredAgent(
            int agentId,
            BattleVector2 position,
            bool wasMoved)
        {
            if (agentId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(agentId));
            }

            AgentId = agentId;
            Position = position;
            WasMoved = wasMoved;
        }

        public int AgentId { get; }
        public BattleVector2 Position { get; }
        public bool WasMoved { get; }
    }

    // The view remains valid until the same workspace is passed to Resolve again.
    internal readonly struct LocalAvoidanceRecoveryResult
    {
        private readonly LocalAvoidanceRecoveredAgent[] _agents;

        internal LocalAvoidanceRecoveryResult(
            LocalAvoidanceRecoveredAgent[] agents,
            int count,
            int passCount)
        {
            if (agents == null)
            {
                throw new ArgumentNullException(nameof(agents));
            }

            if (count < 0 || count > agents.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (passCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(passCount));
            }

            _agents = agents;
            Count = count;
            PassCount = passCount;
        }

        public int Count { get; }
        public int PassCount { get; }

        public LocalAvoidanceRecoveredAgent this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _agents[index];
            }
        }
    }

    internal static class LocalAvoidanceOverlapRecovery
    {
        private const int MaximumPassCount = 4;

        internal static LocalAvoidanceRecoveryResult Resolve(
            LocalAvoidanceFrame frame,
            int maxPasses,
            LocalAvoidanceWorkspace workspace)
        {
            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (maxPasses < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPasses));
            }

            int agentCount = frame.AgentCount;
            workspace.EnsureAgentCapacity(agentCount);
            CopyAndSortAgents(frame, workspace, agentCount);
            ValidateUniqueAgentIds(workspace.SortedAgents, agentCount);

            BattleScalar maxRadius = InitializeRecoveryState(workspace, agentCount);
            if (agentCount == 0 || maxPasses == 0)
            {
                return WriteResult(workspace, agentCount, passCount: 0);
            }

            var grid = new LocalAvoidanceUniformGrid(frame.Settings.CellSize);
            int passCount = 0;
            int passLimit = maxPasses <= MaximumPassCount
                ? maxPasses
                : MaximumPassCount;
            for (int pass = 0; pass < passLimit; pass++)
            {
                BuildCurrentGrid(frame.Settings, grid, workspace, agentCount);
                bool changed = RecoverPass(grid, workspace, agentCount, maxRadius);
                passCount++;
                if (!changed)
                {
                    break;
                }
            }

            return WriteResult(workspace, agentCount, passCount);
        }

        private static void CopyAndSortAgents(
            LocalAvoidanceFrame frame,
            LocalAvoidanceWorkspace workspace,
            int agentCount)
        {
            for (int i = 0; i < agentCount; i++)
            {
                workspace.SortedAgents[i] = frame.GetAgent(i);
            }

            Array.Sort(
                workspace.SortedAgents,
                0,
                agentCount,
                AgentIdComparer.Instance);
        }

        private static void ValidateUniqueAgentIds(
            LocalAvoidanceAgent[] sortedAgents,
            int agentCount)
        {
            for (int i = 1; i < agentCount; i++)
            {
                if (sortedAgents[i - 1].AgentId == sortedAgents[i].AgentId)
                {
                    throw new ArgumentException(
                        "Local avoidance agents must have unique AgentId values.",
                        nameof(sortedAgents));
                }
            }
        }

        private static BattleScalar InitializeRecoveryState(
            LocalAvoidanceWorkspace workspace,
            int agentCount)
        {
            BattleScalar maxRadius = BattleScalar.Zero;
            for (int i = 0; i < agentCount; i++)
            {
                LocalAvoidanceAgent agent = workspace.SortedAgents[i];
                workspace.RecoveredPositions[i] = agent.Position;
                workspace.RecoveredMoved[i] = false;
                if (agent.Radius > maxRadius)
                {
                    maxRadius = agent.Radius;
                }
            }

            return maxRadius;
        }

        private static void BuildCurrentGrid(
            LocalAvoidanceSettings settings,
            LocalAvoidanceUniformGrid grid,
            LocalAvoidanceWorkspace workspace,
            int agentCount)
        {
            for (int i = 0; i < agentCount; i++)
            {
                LocalAvoidanceAgent source = workspace.SortedAgents[i];
                workspace.RecoveryAgents[i] = new LocalAvoidanceAgent(
                    source.AgentId,
                    source.GroupId,
                    workspace.RecoveredPositions[i],
                    source.Heading,
                    source.PreferredStep,
                    source.Radius,
                    source.MaxStepDistance,
                    source.Mobility,
                    source.StopsAtPreferredStep);
            }

            var recoveryFrame = new LocalAvoidanceFrame(
                workspace.RecoveryAgents,
                agentCount,
                settings);
            grid.Build(recoveryFrame, workspace);
        }

        private static bool RecoverPass(
            LocalAvoidanceUniformGrid grid,
            LocalAvoidanceWorkspace workspace,
            int agentCount,
            BattleScalar maxRadius)
        {
            bool changed = false;
            BattleScalar maximumDisplacement = BattleScalar.Zero;
            for (int agentIndex = 0; agentIndex < agentCount; agentIndex++)
            {
                LocalAvoidanceAgent agent = workspace.SortedAgents[agentIndex];
                BattleVector2 anchorPosition = workspace.RecoveredPositions[agentIndex];
                while (true)
                {
                    int otherIndex = FindFirstOverlappingHigherIdAgent(
                        grid,
                        workspace,
                        agentIndex,
                        anchorPosition,
                        agent.Radius + maxRadius,
                        maximumDisplacement);
                    if (otherIndex < 0)
                    {
                        break;
                    }

                    LocalAvoidanceAgent other = workspace.SortedAgents[otherIndex];
                    BattleScalar combinedRadius = agent.Radius + other.Radius;
                    BattleVector2 recoveredPosition = SelectSeparationPosition(
                        grid,
                        workspace,
                        otherIndex,
                        anchorPosition,
                        workspace.RecoveredPositions[otherIndex],
                        combinedRadius,
                        maxRadius,
                        maximumDisplacement);
                    workspace.RecoveredPositions[otherIndex] = recoveredPosition;
                    workspace.RecoveredMoved[otherIndex] = true;
                    BattleScalar displacement = BattleVector2.DistanceScalar(
                        workspace.SortedAgents[otherIndex].Position,
                        recoveredPosition);
                    if (displacement > maximumDisplacement)
                    {
                        maximumDisplacement = displacement;
                    }

                    changed = true;
                }
            }

            return changed;
        }

        private static int FindFirstOverlappingHigherIdAgent(
            LocalAvoidanceUniformGrid grid,
            LocalAvoidanceWorkspace workspace,
            int anchorIndex,
            BattleVector2 anchorPosition,
            BattleScalar queryRadius,
            BattleScalar maximumDisplacement)
        {
            LocalAvoidanceAgent anchor = workspace.SortedAgents[anchorIndex];
            int queryCount = grid.QueryRecovered(
                anchorPosition,
                queryRadius,
                maximumDisplacement,
                workspace);
            for (int neighbor = 0; neighbor < queryCount; neighbor++)
            {
                int otherIndex = workspace.GetNeighborAgentIndex(neighbor);
                LocalAvoidanceAgent other = workspace.SortedAgents[otherIndex];
                if (other.AgentId <= anchor.AgentId
                    || other.GroupId == anchor.GroupId)
                {
                    continue;
                }

                BattleScalar combinedRadius = anchor.Radius + other.Radius;
                if (BattleVector2.SqrDistanceScalar(
                        anchorPosition,
                        workspace.RecoveredPositions[otherIndex])
                    < combinedRadius * combinedRadius)
                {
                    return otherIndex;
                }
            }

            return -1;
        }

        private static BattleVector2 SelectSeparationPosition(
            LocalAvoidanceUniformGrid grid,
            LocalAvoidanceWorkspace workspace,
            int movingAgentIndex,
            BattleVector2 anchorPosition,
            BattleVector2 movingPosition,
            BattleScalar combinedRadius,
            BattleScalar maxRadius,
            BattleScalar maximumDisplacement)
        {
            BattleScalar combinedRadiusSquared = combinedRadius * combinedRadius;
            BattleVector2 preferred = SeparateFromAnchor(
                anchorPosition,
                movingPosition,
                combinedRadius,
                combinedRadiusSquared);
            EvaluateLowerIdEnemyOverlaps(
                grid,
                workspace,
                movingAgentIndex,
                preferred,
                maxRadius,
                maximumDisplacement,
                out int preferredCount,
                out BattleScalar preferredDepth);
            if (preferredCount == 0)
            {
                return preferred;
            }

            BattleVector2 alternate = anchorPosition - (preferred - anchorPosition);
            EvaluateLowerIdEnemyOverlaps(
                grid,
                workspace,
                movingAgentIndex,
                alternate,
                maxRadius,
                maximumDisplacement,
                out int alternateCount,
                out BattleScalar alternateDepth);
            if (alternateCount == 0)
            {
                return alternate;
            }

            BattleVector2 best = preferred;
            int bestCount = preferredCount;
            BattleScalar bestDepth = preferredDepth;
            if (IsBetterRecoveryCandidate(
                    alternateCount,
                    alternateDepth,
                    bestCount,
                    bestDepth))
            {
                best = alternate;
                bestCount = alternateCount;
                bestDepth = alternateDepth;
            }

            LocalAvoidanceAgent moving = workspace.SortedAgents[movingAgentIndex];
            BattleVector2 escapeDirection = (preferred - anchorPosition).Normalized;
            BattleScalar escapeSpacing = (moving.Radius + maxRadius)
                * BattleScalar.FromInt(2)
                + BattleScalar.Epsilon;
            int maximumAttempts = movingAgentIndex + 1;
            for (int attempt = 1; attempt <= maximumAttempts; attempt++)
            {
                BattleVector2 candidate = anchorPosition
                    + escapeDirection
                    * escapeSpacing
                    * BattleScalar.FromInt(attempt);
                EvaluateLowerIdEnemyOverlaps(
                    grid,
                    workspace,
                    movingAgentIndex,
                    candidate,
                    maxRadius,
                    maximumDisplacement,
                    out int candidateCount,
                    out BattleScalar candidateDepth);
                if (candidateCount == 0)
                {
                    return candidate;
                }

                if (IsBetterRecoveryCandidate(
                        candidateCount,
                        candidateDepth,
                        bestCount,
                        bestDepth))
                {
                    best = candidate;
                    bestCount = candidateCount;
                    bestDepth = candidateDepth;
                }
            }

            return best;
        }

        private static bool IsBetterRecoveryCandidate(
            int candidateCount,
            BattleScalar candidateDepth,
            int currentCount,
            BattleScalar currentDepth)
        {
            return candidateCount < currentCount
                || (candidateCount == currentCount
                    && candidateDepth < currentDepth);
        }

        private static void EvaluateLowerIdEnemyOverlaps(
            LocalAvoidanceUniformGrid grid,
            LocalAvoidanceWorkspace workspace,
            int movingAgentIndex,
            BattleVector2 candidatePosition,
            BattleScalar maxRadius,
            BattleScalar maximumDisplacement,
            out int overlapCount,
            out BattleScalar totalOverlapDepth)
        {
            LocalAvoidanceAgent moving = workspace.SortedAgents[movingAgentIndex];
            int queryCount = grid.QueryRecovered(
                candidatePosition,
                moving.Radius + maxRadius,
                maximumDisplacement,
                workspace);
            overlapCount = 0;
            totalOverlapDepth = BattleScalar.Zero;
            for (int neighbor = 0; neighbor < queryCount; neighbor++)
            {
                int anchorIndex = workspace.GetNeighborAgentIndex(neighbor);
                LocalAvoidanceAgent anchor = workspace.SortedAgents[anchorIndex];
                if (anchor.AgentId >= moving.AgentId
                    || anchor.GroupId == moving.GroupId)
                {
                    continue;
                }

                BattleScalar combinedRadius = moving.Radius + anchor.Radius;
                BattleScalar combinedRadiusSquared = combinedRadius * combinedRadius;
                BattleScalar distanceSquared = BattleVector2.SqrDistanceScalar(
                    candidatePosition,
                    workspace.RecoveredPositions[anchorIndex]);
                if (distanceSquared >= combinedRadiusSquared)
                {
                    continue;
                }

                overlapCount++;
                totalOverlapDepth += combinedRadiusSquared - distanceSquared;
            }
        }

        private static BattleVector2 SeparateFromAnchor(
            BattleVector2 anchorPosition,
            BattleVector2 otherPosition,
            BattleScalar combinedRadius,
            BattleScalar combinedRadiusSquared)
        {
            BattleVector2 offset = otherPosition - anchorPosition;
            BattleVector2 direction;
            if (offset.XRaw == 0L && offset.YRaw == 0L)
            {
                direction = BattleVector2.Right;
            }
            else
            {
                direction = offset.Normalized;
                if (direction.SqrMagnitudeScalar <= BattleScalar.Epsilon)
                {
                    direction = DominantAxisDirection(offset);
                }
            }

            BattleVector2 separated = anchorPosition + direction * combinedRadius;
            for (int correction = 0; correction < 4; correction++)
            {
                BattleScalar distanceSquared = BattleVector2.SqrDistanceScalar(
                    anchorPosition,
                    separated);
                if (distanceSquared >= combinedRadiusSquared)
                {
                    return separated;
                }

                BattleScalar shortfall = combinedRadius
                    - BattleScalar.Sqrt(distanceSquared)
                    + BattleScalar.Epsilon;
                separated = separated + direction * shortfall;
            }

            return ApplyBoundedRawCorrection(
                anchorPosition,
                separated,
                direction,
                combinedRadius);
        }

        private static BattleVector2 DominantAxisDirection(BattleVector2 offset)
        {
            BattleScalar absoluteX = Absolute(offset.XScalar);
            BattleScalar absoluteY = Absolute(offset.YScalar);
            if (absoluteX >= absoluteY)
            {
                return new BattleVector2(
                    offset.XScalar < BattleScalar.Zero
                        ? -BattleScalar.One
                        : BattleScalar.One,
                    BattleScalar.Zero);
            }

            return new BattleVector2(
                BattleScalar.Zero,
                offset.YScalar < BattleScalar.Zero
                    ? -BattleScalar.One
                    : BattleScalar.One);
        }

        private static BattleVector2 ApplyBoundedRawCorrection(
            BattleVector2 anchorPosition,
            BattleVector2 separated,
            BattleVector2 direction,
            BattleScalar combinedRadius)
        {
            BattleVector2 relative = separated - anchorPosition;
            BattleScalar absoluteX = Absolute(direction.XScalar);
            BattleScalar absoluteY = Absolute(direction.YScalar);
            BattleScalar correctedExtent = combinedRadius + BattleScalar.Epsilon;
            if (absoluteX >= absoluteY)
            {
                BattleScalar correctedX = direction.XScalar < BattleScalar.Zero
                    ? -correctedExtent
                    : correctedExtent;
                return anchorPosition + new BattleVector2(
                    correctedX,
                    relative.YScalar);
            }

            BattleScalar correctedY = direction.YScalar < BattleScalar.Zero
                ? -correctedExtent
                : correctedExtent;
            return anchorPosition + new BattleVector2(
                relative.XScalar,
                correctedY);
        }

        private static BattleScalar Absolute(BattleScalar value)
        {
            return value < BattleScalar.Zero ? -value : value;
        }

        private static LocalAvoidanceRecoveryResult WriteResult(
            LocalAvoidanceWorkspace workspace,
            int agentCount,
            int passCount)
        {
            for (int i = 0; i < agentCount; i++)
            {
                workspace.RecoveryResults[i] = new LocalAvoidanceRecoveredAgent(
                    workspace.SortedAgents[i].AgentId,
                    workspace.RecoveredPositions[i],
                    workspace.RecoveredMoved[i]);
            }

            return new LocalAvoidanceRecoveryResult(
                workspace.RecoveryResults,
                agentCount,
                passCount);
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
