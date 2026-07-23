using System;
using System.Collections.Generic;
using Combat.Core.Battle;

namespace Combat.Core.LocalAvoidance
{
    internal readonly struct LocalAvoidanceUniformGrid
    {
        private readonly BattleScalar _cellSize;

        public LocalAvoidanceUniformGrid(BattleScalar cellSize)
        {
            if (cellSize <= BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            }

            _cellSize = cellSize;
        }

        internal static long FloorToCell(long coordinateRaw, long cellSizeRaw)
        {
            if (cellSizeRaw <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSizeRaw));
            }

            long quotient = coordinateRaw / cellSizeRaw;
            long remainder = coordinateRaw % cellSizeRaw;
            return remainder != 0L && coordinateRaw < 0L ? quotient - 1L : quotient;
        }

        public void Build(LocalAvoidanceFrame frame, LocalAvoidanceWorkspace workspace)
        {
            EnsureConfigured();

            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            workspace.AgentCount = 0;
            workspace.GridEntryCount = 0;
            workspace.CellRangeCount = 0;
            workspace.NeighborCount = 0;
            workspace.EnsureAgentCapacity(frame.AgentCount);

            long cellSizeRaw = _cellSize.RawValue;
            for (int i = 0; i < frame.AgentCount; i++)
            {
                LocalAvoidanceAgent agent = frame.GetAgent(i);
                workspace.SortedAgents[i] = agent;
                workspace.GridEntries[i] = new LocalAvoidanceWorkspace.GridEntry
                {
                    CellX = FloorToCell(agent.Position.XRaw, cellSizeRaw),
                    CellY = FloorToCell(agent.Position.YRaw, cellSizeRaw),
                    AgentIndex = i,
                    AgentId = agent.AgentId
                };
            }

            workspace.AgentCount = frame.AgentCount;
            workspace.GridEntryCount = frame.AgentCount;
            if (frame.AgentCount == 0)
            {
                return;
            }

            Array.Sort(
                workspace.GridEntries,
                0,
                frame.AgentCount,
                GridEntryComparer.Instance);

            int rangeStart = 0;
            int rangeCount = 0;
            for (int i = 1; i <= frame.AgentCount; i++)
            {
                if (i < frame.AgentCount
                    && IsSameCell(workspace.GridEntries[rangeStart], workspace.GridEntries[i]))
                {
                    continue;
                }

                LocalAvoidanceWorkspace.GridEntry first = workspace.GridEntries[rangeStart];
                workspace.CellRanges[rangeCount] = new LocalAvoidanceWorkspace.CellRange
                {
                    CellX = first.CellX,
                    CellY = first.CellY,
                    StartIndex = rangeStart,
                    Count = i - rangeStart
                };
                rangeCount++;
                rangeStart = i;
            }

            workspace.CellRangeCount = rangeCount;
        }

        public int Query(
            BattleVector2 center,
            BattleScalar radius,
            LocalAvoidanceWorkspace workspace)
        {
            return Query(
                center,
                radius,
                radius,
                useRecoveredPositions: false,
                workspace: workspace);
        }

        internal int QueryRecovered(
            BattleVector2 center,
            BattleScalar radius,
            BattleScalar maximumDisplacement,
            LocalAvoidanceWorkspace workspace)
        {
            if (maximumDisplacement < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumDisplacement));
            }

            return Query(
                center,
                radius,
                radius + maximumDisplacement,
                useRecoveredPositions: true,
                workspace: workspace);
        }

        private int Query(
            BattleVector2 center,
            BattleScalar exactRadius,
            BattleScalar broadphaseRadius,
            bool useRecoveredPositions,
            LocalAvoidanceWorkspace workspace)
        {
            EnsureConfigured();

            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (exactRadius < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(exactRadius));
            }

            workspace.NeighborCount = 0;
            if (workspace.CellRangeCount == 0)
            {
                return 0;
            }

            long cellSizeRaw = _cellSize.RawValue;
            long minCellX = FloorToCell(
                (center.XScalar - broadphaseRadius).RawValue,
                cellSizeRaw);
            long maxCellX = FloorToCell(
                (center.XScalar + broadphaseRadius).RawValue,
                cellSizeRaw);
            long minCellY = FloorToCell(
                (center.YScalar - broadphaseRadius).RawValue,
                cellSizeRaw);
            long maxCellY = FloorToCell(
                (center.YScalar + broadphaseRadius).RawValue,
                cellSizeRaw);

            int broadphaseCount = 0;
            long cellX = minCellX;
            while (cellX <= maxCellX)
            {
                long cellY = minCellY;
                while (cellY <= maxCellY)
                {
                    int rangeIndex = FindCellRange(cellX, cellY, workspace);
                    if (rangeIndex >= 0)
                    {
                        LocalAvoidanceWorkspace.CellRange range =
                            workspace.CellRanges[rangeIndex];
                        int rangeEnd = range.StartIndex + range.Count;
                        for (int i = range.StartIndex; i < rangeEnd; i++)
                        {
                            LocalAvoidanceWorkspace.GridEntry entry = workspace.GridEntries[i];
                            workspace.NeighborIndices[broadphaseCount] = entry.AgentIndex;
                            workspace.NeighborAgentIds[broadphaseCount] = entry.AgentId;
                            broadphaseCount++;
                        }
                    }

                    if (cellY == maxCellY)
                    {
                        break;
                    }

                    cellY++;
                }

                if (cellX == maxCellX)
                {
                    break;
                }

                cellX++;
            }

            BattleScalar radiusSquared = exactRadius * exactRadius;
            workspace.AddBroadphaseCandidates(broadphaseCount);
            int exactCount = 0;
            for (int i = 0; i < broadphaseCount; i++)
            {
                int agentIndex = workspace.NeighborIndices[i];
                BattleVector2 agentPosition = useRecoveredPositions
                    ? workspace.RecoveredPositions[agentIndex]
                    : workspace.SortedAgents[agentIndex].Position;
                if (BattleVector2.SqrDistanceScalar(agentPosition, center) > radiusSquared)
                {
                    continue;
                }

                workspace.NeighborIndices[exactCount] = agentIndex;
                workspace.NeighborAgentIds[exactCount] = workspace.NeighborAgentIds[i];
                exactCount++;
            }

            Array.Sort(
                workspace.NeighborAgentIds,
                workspace.NeighborIndices,
                0,
                exactCount);
            workspace.NeighborCount = exactCount;
            return exactCount;
        }

        private void EnsureConfigured()
        {
            if (_cellSize <= BattleScalar.Zero)
            {
                throw new InvalidOperationException(
                    "LocalAvoidanceUniformGrid requires a positive configured cell size.");
            }
        }

        private static bool IsSameCell(
            LocalAvoidanceWorkspace.GridEntry left,
            LocalAvoidanceWorkspace.GridEntry right)
        {
            return left.CellX == right.CellX && left.CellY == right.CellY;
        }

        private static int FindCellRange(
            long cellX,
            long cellY,
            LocalAvoidanceWorkspace workspace)
        {
            int low = 0;
            int high = workspace.CellRangeCount - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                LocalAvoidanceWorkspace.CellRange range = workspace.CellRanges[middle];
                int comparison = CompareCell(range.CellX, range.CellY, cellX, cellY);
                if (comparison == 0)
                {
                    return middle;
                }

                if (comparison < 0)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return -1;
        }

        private static int CompareCell(long leftX, long leftY, long rightX, long rightY)
        {
            int xComparison = leftX.CompareTo(rightX);
            return xComparison != 0 ? xComparison : leftY.CompareTo(rightY);
        }

        private sealed class GridEntryComparer : IComparer<LocalAvoidanceWorkspace.GridEntry>
        {
            internal static readonly GridEntryComparer Instance = new GridEntryComparer();

            public int Compare(
                LocalAvoidanceWorkspace.GridEntry left,
                LocalAvoidanceWorkspace.GridEntry right)
            {
                int cellComparison = CompareCell(
                    left.CellX,
                    left.CellY,
                    right.CellX,
                    right.CellY);
                return cellComparison != 0
                    ? cellComparison
                    : left.AgentId.CompareTo(right.AgentId);
            }
        }
    }
}
