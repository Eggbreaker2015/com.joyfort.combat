using System;
using Combat.Core.Battle;

namespace Combat.Core.LocalAvoidance
{
    internal sealed class LocalAvoidanceWorkspace
    {
        private const int InitialAgentCapacity = 16;

        private LocalAvoidanceAgent[] _sortedAgents = Array.Empty<LocalAvoidanceAgent>();
        private LocalAvoidanceDecision[] _decisions = Array.Empty<LocalAvoidanceDecision>();
        private BattleVector2[] _candidateSteps = Array.Empty<BattleVector2>();
        private LocalAvoidanceCandidateCost[] _candidateCosts =
            Array.Empty<LocalAvoidanceCandidateCost>();
        private int[] _selectedCandidateIndices = Array.Empty<int>();
        private BattleVector2[] _predictedSteps = Array.Empty<BattleVector2>();
        private BattleVector2[] _selectedSteps = Array.Empty<BattleVector2>();
        private BattleVector2[] _snapshotSteps = Array.Empty<BattleVector2>();
        private bool[] _hardBlocked = Array.Empty<bool>();
        private ConflictPair[] _conflictPairs = Array.Empty<ConflictPair>();
        private int[] _neighborIndices = Array.Empty<int>();
        private int[] _neighborAgentIds = Array.Empty<int>();
        private GridEntry[] _gridEntries = Array.Empty<GridEntry>();
        private CellRange[] _cellRanges = Array.Empty<CellRange>();
        private BattleVector2[] _recoveredPositions = Array.Empty<BattleVector2>();
        private bool[] _recoveredMoved = Array.Empty<bool>();
        private LocalAvoidanceAgent[] _recoveryAgents = Array.Empty<LocalAvoidanceAgent>();
        private LocalAvoidanceRecoveredAgent[] _recoveryResults =
            Array.Empty<LocalAvoidanceRecoveredAgent>();

        internal LocalAvoidanceAgent[] SortedAgents => _sortedAgents;
        internal LocalAvoidanceDecision[] Decisions => _decisions;
        internal BattleVector2[] CandidateSteps => _candidateSteps;
        internal LocalAvoidanceCandidateCost[] CandidateCosts => _candidateCosts;
        internal int[] SelectedCandidateIndices => _selectedCandidateIndices;
        internal BattleVector2[] PredictedSteps => _predictedSteps;
        internal BattleVector2[] SelectedSteps => _selectedSteps;
        internal BattleVector2[] SnapshotSteps => _snapshotSteps;
        internal bool[] HardBlocked => _hardBlocked;
        internal ConflictPair[] ConflictPairs => _conflictPairs;
        internal int[] NeighborIndices => _neighborIndices;
        internal int[] NeighborAgentIds => _neighborAgentIds;
        internal GridEntry[] GridEntries => _gridEntries;
        internal CellRange[] CellRanges => _cellRanges;
        internal BattleVector2[] RecoveredPositions => _recoveredPositions;
        internal bool[] RecoveredMoved => _recoveredMoved;
        internal LocalAvoidanceAgent[] RecoveryAgents => _recoveryAgents;
        internal LocalAvoidanceRecoveredAgent[] RecoveryResults => _recoveryResults;
        internal int AgentCapacity => _sortedAgents.Length;
        internal int AgentCount { get; set; }
        internal int GridEntryCount { get; set; }
        internal int CellRangeCount { get; set; }
        internal int NeighborCount { get; set; }
        internal int ConflictPairCount { get; set; }
        internal int NeighborCheckCount { get; private set; }
        internal int BroadphaseCandidateCount { get; private set; }
        internal int ActiveQueryCount { get; private set; }
        internal int CandidateEvaluationCount { get; private set; }
        internal BattleScalar MaxRadius { get; set; }
        internal BattleScalar MaxStepDistance { get; set; }

        internal void ResetSolveStats()
        {
            NeighborCheckCount = 0;
            BroadphaseCandidateCount = 0;
            ActiveQueryCount = 0;
            CandidateEvaluationCount = 0;
        }

        internal void AddNeighborChecks(int count)
        {
            NeighborCheckCount = SaturatingAdd(NeighborCheckCount, count);
        }

        internal void AddBroadphaseCandidates(int count)
        {
            BroadphaseCandidateCount = SaturatingAdd(
                BroadphaseCandidateCount,
                count);
        }

        internal void AddActiveQueries(int count)
        {
            ActiveQueryCount = SaturatingAdd(ActiveQueryCount, count);
        }

        internal void AddCandidateEvaluations(int count)
        {
            CandidateEvaluationCount = SaturatingAdd(CandidateEvaluationCount, count);
        }

        public void EnsureAgentCapacity(int requiredCapacity)
        {
            if (requiredCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredCapacity));
            }

            if (_sortedAgents.Length >= requiredCapacity)
            {
                return;
            }

            int capacity = _sortedAgents.Length == 0
                ? InitialAgentCapacity
                : _sortedAgents.Length;
            while (capacity < requiredCapacity)
            {
                if (capacity > int.MaxValue / 2)
                {
                    capacity = requiredCapacity;
                    break;
                }

                capacity *= 2;
            }

            Array.Resize(ref _sortedAgents, capacity);
            Array.Resize(ref _decisions, capacity);
            Array.Resize(ref _selectedCandidateIndices, capacity);
            Array.Resize(ref _predictedSteps, capacity);
            Array.Resize(ref _selectedSteps, capacity);
            Array.Resize(ref _snapshotSteps, capacity);
            Array.Resize(ref _hardBlocked, capacity);
            Array.Resize(ref _neighborIndices, capacity);
            Array.Resize(ref _neighborAgentIds, capacity);
            Array.Resize(ref _gridEntries, capacity);
            Array.Resize(ref _cellRanges, capacity);
            Array.Resize(ref _recoveredPositions, capacity);
            Array.Resize(ref _recoveredMoved, capacity);
            Array.Resize(ref _recoveryAgents, capacity);
            Array.Resize(ref _recoveryResults, capacity);
        }

        internal void EnsureCandidateCapacity(int requiredCapacity)
        {
            if (requiredCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredCapacity));
            }

            if (_candidateSteps.Length >= requiredCapacity)
            {
                return;
            }

            int capacity = _candidateSteps.Length == 0
                ? InitialAgentCapacity
                : _candidateSteps.Length;
            while (capacity < requiredCapacity)
            {
                if (capacity > int.MaxValue / 2)
                {
                    capacity = requiredCapacity;
                    break;
                }

                capacity *= 2;
            }

            Array.Resize(ref _candidateSteps, capacity);
            Array.Resize(ref _candidateCosts, capacity);
        }

        internal void EnsureConflictPairCapacity(int requiredCapacity)
        {
            if (requiredCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredCapacity));
            }

            if (_conflictPairs.Length >= requiredCapacity)
            {
                return;
            }

            int capacity = _conflictPairs.Length == 0
                ? InitialAgentCapacity
                : _conflictPairs.Length;
            while (capacity < requiredCapacity)
            {
                if (capacity > int.MaxValue / 2)
                {
                    capacity = requiredCapacity;
                    break;
                }

                capacity *= 2;
            }

            Array.Resize(ref _conflictPairs, capacity);
        }

        internal int GetNeighborAgentId(int index)
        {
            ValidateNeighborIndex(index);
            return _neighborAgentIds[index];
        }

        internal int GetNeighborAgentIndex(int index)
        {
            ValidateNeighborIndex(index);
            return _neighborIndices[index];
        }

        private void ValidateNeighborIndex(int index)
        {
            if (index < 0 || index >= NeighborCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static int SaturatingAdd(int value, int increment)
        {
            if (increment < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(increment));
            }

            return value > int.MaxValue - increment
                ? int.MaxValue
                : value + increment;
        }

        internal struct GridEntry
        {
            public long CellX;
            public long CellY;
            public int AgentIndex;
            public int AgentId;
        }

        internal struct CellRange
        {
            public long CellX;
            public long CellY;
            public int StartIndex;
            public int Count;
        }

        internal struct ConflictPair
        {
            public int FirstAgentIndex;
            public int SecondAgentIndex;
            public int FirstAgentId;
            public int SecondAgentId;
        }
    }
}
