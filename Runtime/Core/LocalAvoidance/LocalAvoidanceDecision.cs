using System;
using Combat.Core.Battle;

namespace Combat.Core.LocalAvoidance
{
    internal readonly struct LocalAvoidanceDecision
    {
        public LocalAvoidanceDecision(
            int agentId,
            BattleVector2 selectedStep,
            int selectedCandidateIndex,
            bool wasHardBlocked)
        {
            if (agentId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(agentId));
            }

            AgentId = agentId;
            SelectedStep = selectedStep;
            SelectedCandidateIndex = selectedCandidateIndex;
            WasHardBlocked = wasHardBlocked;
        }

        public int AgentId { get; }
        public BattleVector2 SelectedStep { get; }
        public int SelectedCandidateIndex { get; }
        public bool WasHardBlocked { get; }
    }

    internal readonly struct LocalAvoidanceSolveStats
    {
        public LocalAvoidanceSolveStats(
            int gridCellCount,
            int neighborCheckCount,
            int candidateEvaluationCount,
            int conflictResolutionPassCount,
            int broadphaseCandidateCount = 0,
            int activeQueryCount = 0)
        {
            GridCellCount = gridCellCount;
            NeighborCheckCount = neighborCheckCount;
            CandidateEvaluationCount = candidateEvaluationCount;
            ConflictResolutionPassCount = conflictResolutionPassCount;
            BroadphaseCandidateCount = broadphaseCandidateCount;
            ActiveQueryCount = activeQueryCount;
        }

        public int GridCellCount { get; }
        public int NeighborCheckCount { get; }
        public int CandidateEvaluationCount { get; }
        public int ConflictResolutionPassCount { get; }
        public int BroadphaseCandidateCount { get; }
        public int ActiveQueryCount { get; }
    }

    // This view is valid only until the next Solve that uses the same workspace.
    internal readonly struct LocalAvoidanceSolveResult
    {
        private readonly LocalAvoidanceDecision[] _decisions;

        public LocalAvoidanceSolveResult(
            LocalAvoidanceDecision[] decisions,
            int decisionCount,
            LocalAvoidanceSolveStats stats)
        {
            if (decisions == null)
            {
                throw new ArgumentNullException(nameof(decisions));
            }

            if (decisionCount < 0 || decisionCount > decisions.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(decisionCount));
            }

            _decisions = decisions;
            DecisionCount = decisionCount;
            Stats = stats;
        }

        public int DecisionCount { get; }
        public LocalAvoidanceSolveStats Stats { get; }

        public LocalAvoidanceDecision this[int index]
        {
            get
            {
                if (index < 0 || index >= DecisionCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _decisions[index];
            }
        }
    }
}
