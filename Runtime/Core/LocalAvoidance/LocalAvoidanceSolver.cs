using System;
using System.Collections.Generic;
using Combat.Core.Battle;

namespace Combat.Core.LocalAvoidance
{
    internal readonly struct LocalAvoidanceCandidateCost
    {
        internal LocalAvoidanceCandidateCost(
            int hardRisk,
            BattleScalar friendlyOverlapDepth,
            int passingSidePenalty,
            BattleScalar progressLoss,
            BattleScalar directionDeviation,
            BattleScalar stepLoss,
            BattleScalar turnDeviation,
            int candidateIndex)
        {
            HardRisk = hardRisk;
            FriendlyOverlapDepth = friendlyOverlapDepth;
            PassingSidePenalty = passingSidePenalty;
            ProgressLoss = progressLoss;
            DirectionDeviation = directionDeviation;
            StepLoss = stepLoss;
            TurnDeviation = turnDeviation;
            CandidateIndex = candidateIndex;
        }

        internal int HardRisk { get; }
        internal BattleScalar FriendlyOverlapDepth { get; }
        internal int PassingSidePenalty { get; }
        internal BattleScalar ProgressLoss { get; }
        internal BattleScalar DirectionDeviation { get; }
        internal BattleScalar StepLoss { get; }
        internal BattleScalar TurnDeviation { get; }
        internal int CandidateIndex { get; }
    }

    internal static class LocalAvoidanceSolver
    {
        internal static LocalAvoidanceSolveResult Solve(
            LocalAvoidanceFrame frame,
            LocalAvoidanceWorkspace workspace)
        {
            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            workspace.ResetSolveStats();
            workspace.EnsureAgentCapacity(frame.AgentCount);
            workspace.EnsureCandidateCapacity(LocalAvoidanceCandidateSet.Count);

            for (int i = 0; i < frame.AgentCount; i++)
            {
                workspace.SortedAgents[i] = frame.GetAgent(i);
            }

            Array.Sort(
                workspace.SortedAgents,
                0,
                frame.AgentCount,
                AgentIdComparer.Instance);
            ValidateUniqueAgentIds(workspace.SortedAgents, frame.AgentCount);
            CalculateFrameExtents(workspace, frame.AgentCount);

            LocalAvoidanceSettings settings = frame.Settings;
            var sortedFrame = new LocalAvoidanceFrame(
                workspace.SortedAgents,
                frame.AgentCount,
                settings);
            var grid = new LocalAvoidanceUniformGrid(settings.CellSize);
            grid.Build(sortedFrame, workspace);
            PreparePredictedSteps(workspace, frame.AgentCount);

            for (int agentIndex = 0; agentIndex < frame.AgentCount; agentIndex++)
            {
                LocalAvoidanceAgent agent = workspace.SortedAgents[agentIndex];
                if (ShouldSelectZero(agent))
                {
                    SetSelection(
                        workspace,
                        agentIndex,
                        BattleVector2.Zero,
                        LocalAvoidanceCandidateSet.ZeroIndex,
                        hardBlocked: false);
                    continue;
                }

                BattleScalar queryRadius = CalculateQueryRadius(agent, settings, workspace);
                int queryCount = QueryAndCount(
                    agentIndex,
                    agent,
                    queryRadius,
                    grid,
                    workspace);
                bool hasNeighbor = false;
                for (int neighborIndex = 0; neighborIndex < queryCount; neighborIndex++)
                {
                    if (workspace.GetNeighborAgentId(neighborIndex) == agent.AgentId)
                    {
                        continue;
                    }

                    hasNeighbor = true;
                }

                if (!hasNeighbor)
                {
                    SetSelection(
                        workspace,
                        agentIndex,
                        ClampPreferredStep(agent),
                        selectedCandidateIndex: 0,
                        hardBlocked: false);
                    continue;
                }

                bool hardBlocked;
                int selectedCandidateIndex = EvaluateCandidates(
                    agentIndex,
                    agent,
                    settings,
                    workspace,
                    queryCount,
                    out hardBlocked);
                workspace.AddCandidateEvaluations(LocalAvoidanceCandidateSet.Count);
                BattleVector2 selectedStep = hardBlocked
                    ? BattleVector2.Zero
                    : workspace.CandidateSteps[selectedCandidateIndex];
                SetSelection(
                    workspace,
                    agentIndex,
                    selectedStep,
                    selectedCandidateIndex,
                    hardBlocked);
            }

            int conflictResolutionPassCount = ResolveConflicts(
                frame.AgentCount,
                settings,
                grid,
                workspace);
            ValidateFinalHardConstraints(
                frame.AgentCount,
                settings,
                grid,
                workspace);
            WriteDecisions(workspace, frame.AgentCount);

            var stats = new LocalAvoidanceSolveStats(
                workspace.CellRangeCount,
                workspace.NeighborCheckCount,
                workspace.CandidateEvaluationCount,
                conflictResolutionPassCount,
                workspace.BroadphaseCandidateCount,
                workspace.ActiveQueryCount);
            return new LocalAvoidanceSolveResult(
                workspace.Decisions,
                frame.AgentCount,
                stats);
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

        private static void CalculateFrameExtents(
            LocalAvoidanceWorkspace workspace,
            int agentCount)
        {
            BattleScalar maxRadius = BattleScalar.Zero;
            BattleScalar maxStepDistance = BattleScalar.Zero;
            for (int i = 0; i < agentCount; i++)
            {
                LocalAvoidanceAgent agent = workspace.SortedAgents[i];
                if (agent.Radius > maxRadius)
                {
                    maxRadius = agent.Radius;
                }

                if (agent.MaxStepDistance > maxStepDistance)
                {
                    maxStepDistance = agent.MaxStepDistance;
                }
            }

            workspace.MaxRadius = maxRadius;
            workspace.MaxStepDistance = maxStepDistance;
        }

        private static void PreparePredictedSteps(
            LocalAvoidanceWorkspace workspace,
            int agentCount)
        {
            for (int i = 0; i < agentCount; i++)
            {
                LocalAvoidanceAgent agent = workspace.SortedAgents[i];
                workspace.PredictedSteps[i] = ShouldSelectZero(agent)
                    ? BattleVector2.Zero
                    : ClampPreferredStep(agent);
            }
        }

        private static bool ShouldSelectZero(LocalAvoidanceAgent agent)
        {
            return agent.Mobility == LocalAvoidanceMobility.Anchored
                || agent.MaxStepDistance <= BattleScalar.Zero
                || agent.PreferredStep.SqrMagnitudeScalar <= BattleScalar.Epsilon;
        }

        private static BattleVector2 ClampPreferredStep(LocalAvoidanceAgent agent)
        {
            if (agent.PreferredStep.MagnitudeScalar <= agent.MaxStepDistance)
            {
                return agent.PreferredStep;
            }

            return LocalAvoidanceCandidateSet.Get(
                0,
                agent.PreferredStep,
                agent.MaxStepDistance);
        }

        private static BattleScalar CalculateQueryRadius(
            LocalAvoidanceAgent agent,
            LocalAvoidanceSettings settings,
            LocalAvoidanceWorkspace workspace)
        {
            return agent.Radius
                + workspace.MaxRadius
                + (agent.MaxStepDistance + workspace.MaxStepDistance)
                * BattleScalar.FromInt(settings.PredictionTicks);
        }

        private static int EvaluateCandidates(
            int agentIndex,
            LocalAvoidanceAgent agent,
            LocalAvoidanceSettings settings,
            LocalAvoidanceWorkspace workspace,
            int queryCount,
            out bool hardBlocked)
        {
            BattleVector2 preferredDirection = agent.PreferredStep.Normalized;
            BattleScalar preferredMagnitude = agent.PreferredStep.MagnitudeScalar;
            BattleScalar candidateBudget = CalculateCandidateBudget(agent);
            int selectedCandidateIndex = LocalAvoidanceCandidateSet.ZeroIndex;
            bool hasHardLegalCandidate = false;
            bool hasHardLegalMovement = false;
            for (int candidateIndex = 0;
                candidateIndex < LocalAvoidanceCandidateSet.Count;
                candidateIndex++)
            {
                BattleVector2 candidateStep = LocalAvoidanceCandidateSet.Get(
                    candidateIndex,
                    preferredDirection,
                    candidateBudget);
                LocalAvoidanceCandidateCost candidateCost = CalculateCandidateCost(
                    agentIndex,
                    agent,
                    preferredDirection,
                    preferredMagnitude,
                    candidateStep,
                    candidateIndex,
                    settings,
                    workspace,
                    queryCount);
                workspace.CandidateSteps[candidateIndex] = candidateStep;
                workspace.CandidateCosts[candidateIndex] = candidateCost;

                if (candidateCost.HardRisk >= 2)
                {
                    continue;
                }

                if (candidateIndex != LocalAvoidanceCandidateSet.ZeroIndex)
                {
                    hasHardLegalMovement = true;
                }

                if (!hasHardLegalCandidate
                    || CompareCandidateCosts(
                        candidateCost,
                        workspace.CandidateCosts[selectedCandidateIndex],
                        settings) < 0)
                {
                    selectedCandidateIndex = candidateIndex;
                }

                hasHardLegalCandidate = true;
            }

            hardBlocked = !hasHardLegalMovement;
            if (!hasHardLegalCandidate)
            {
                return LocalAvoidanceCandidateSet.ZeroIndex;
            }

            return PreferFullSpeedCandidate(
                selectedCandidateIndex,
                settings,
                workspace);
        }

        private static int PreferFullSpeedCandidate(
            int selectedCandidateIndex,
            LocalAvoidanceSettings settings,
            LocalAvoidanceWorkspace workspace)
        {
            LocalAvoidanceCandidateCost selectedCost =
                workspace.CandidateCosts[selectedCandidateIndex];
            BattleScalar maximumFriendlyOverlap =
                selectedCost.FriendlyOverlapDepth
                + settings.FullSpeedFriendlyOverlapTolerance;
            int fullSpeedCandidateIndex = -1;
            for (int candidateIndex = 0;
                candidateIndex < LocalAvoidanceCandidateSet.Count;
                candidateIndex++)
            {
                if (!LocalAvoidanceCandidateSet.IsFullSpeed(candidateIndex))
                {
                    continue;
                }

                LocalAvoidanceCandidateCost candidateCost =
                    workspace.CandidateCosts[candidateIndex];
                if (candidateCost.HardRisk != 0
                    || candidateCost.FriendlyOverlapDepth > maximumFriendlyOverlap
                    || candidateCost.ProgressLoss
                        > selectedCost.ProgressLoss + BattleScalar.Epsilon
                    || candidateCost.PassingSidePenalty
                        > selectedCost.PassingSidePenalty)
                {
                    continue;
                }

                if (fullSpeedCandidateIndex < 0
                    || CompareCandidateCosts(
                        candidateCost,
                        workspace.CandidateCosts[fullSpeedCandidateIndex],
                        settings) < 0)
                {
                    fullSpeedCandidateIndex = candidateIndex;
                }
            }

            return fullSpeedCandidateIndex >= 0
                ? fullSpeedCandidateIndex
                : selectedCandidateIndex;
        }

        private static BattleScalar CalculateCandidateBudget(
            LocalAvoidanceAgent agent)
        {
            BattleScalar preferredMagnitude = agent.PreferredStep.MagnitudeScalar;
            return preferredMagnitude <= agent.MaxStepDistance
                ? preferredMagnitude
                : agent.MaxStepDistance;
        }

        private static LocalAvoidanceCandidateCost CalculateCandidateCost(
            int agentIndex,
            LocalAvoidanceAgent agent,
            BattleVector2 preferredDirection,
            BattleScalar preferredMagnitude,
            BattleVector2 candidateStep,
            int candidateIndex,
            LocalAvoidanceSettings settings,
            LocalAvoidanceWorkspace workspace,
            int queryCount)
        {
            int hardRisk = 0;
            int passingSidePenalty = 0;
            BattleScalar friendlyOverlapDepth = BattleScalar.Zero;
            BattleScalar predictionHorizon = BattleScalar.FromInt(settings.PredictionTicks);
            BattleScalar candidateMagnitude = candidateStep.MagnitudeScalar;
            BattleScalar progress = LocalAvoidanceGeometry.Dot(
                candidateStep,
                preferredDirection);
            bool reachesGoalThisTick = agent.StopsAtPreferredStep
                && progress >= preferredMagnitude - BattleScalar.Epsilon;
            BattleScalar candidatePredictionHorizon = reachesGoalThisTick
                ? BattleScalar.One
                : predictionHorizon;
            for (int neighborSlot = 0; neighborSlot < queryCount; neighborSlot++)
            {
                int neighborIndex = workspace.GetNeighborAgentIndex(neighborSlot);
                if (neighborIndex == agentIndex)
                {
                    continue;
                }

                LocalAvoidanceAgent neighbor = workspace.SortedAgents[neighborIndex];
                BattleVector2 neighborStep = workspace.PredictedSteps[neighborIndex];
                bool passingRelevant;
                if (neighbor.GroupId == agent.GroupId)
                {
                    BattleScalar softRadius = (agent.Radius + neighbor.Radius)
                        * BattleScalar.FromInt(settings.SoftSpacingNumerator)
                        / BattleScalar.FromInt(settings.SoftSpacingDenominator);
                    BattleScalar penetration = LocalAvoidanceGeometry.PredictPenetrationDepth(
                        agent.Position,
                        candidateStep,
                        softRadius,
                        neighbor.Position,
                        neighborStep,
                        BattleScalar.Zero,
                        candidatePredictionHorizon);
                    friendlyOverlapDepth += penetration;
                    passingRelevant = LocalAvoidanceGeometry.SweptCirclesOverlap(
                        agent.Position,
                        workspace.PredictedSteps[agentIndex],
                        softRadius,
                        neighbor.Position,
                        neighborStep,
                        BattleScalar.Zero,
                        candidatePredictionHorizon);
                }
                else
                {
                    bool immediateConflict = CreatesHardConflict(
                        agent,
                        candidateStep,
                        neighbor,
                        neighborStep);
                    bool predictedConflict = immediateConflict
                        || LocalAvoidanceGeometry.SweptCirclesOverlap(
                            agent.Position,
                            candidateStep,
                            agent.Radius,
                            neighbor.Position,
                            neighborStep,
                            neighbor.Radius,
                            candidatePredictionHorizon);
                    if (immediateConflict)
                    {
                        hardRisk = 2;
                    }
                    else if (predictedConflict && hardRisk < 1)
                    {
                        hardRisk = 1;
                    }

                    passingRelevant = LocalAvoidanceGeometry.SweptCirclesOverlap(
                        agent.Position,
                        workspace.PredictedSteps[agentIndex],
                        agent.Radius,
                        neighbor.Position,
                        neighborStep,
                        neighbor.Radius,
                        candidatePredictionHorizon);
                }

                if (passingRelevant)
                {
                    passingSidePenalty = AddPassingSidePenalty(
                        passingSidePenalty,
                        agent.AgentId,
                        neighbor.AgentId,
                        preferredDirection,
                        candidateStep);
                }
            }

            BattleScalar progressLoss = preferredMagnitude > progress
                ? preferredMagnitude - progress
                : BattleScalar.Zero;
            BattleScalar stepLoss = Absolute(preferredMagnitude - candidateMagnitude);

            BattleScalar directionDeviation;
            BattleScalar turnDeviation;
            if (candidateStep.SqrMagnitudeScalar <= BattleScalar.Epsilon)
            {
                directionDeviation = BattleScalar.One;
                turnDeviation = BattleScalar.One;
            }
            else
            {
                BattleVector2 candidateDirection = candidateStep.Normalized;
                directionDeviation = BattleScalar.One - ClampUnitDot(
                    LocalAvoidanceGeometry.Dot(candidateDirection, preferredDirection));
                turnDeviation = BattleScalar.One - ClampUnitDot(
                    LocalAvoidanceGeometry.Dot(candidateDirection, agent.Heading));
            }

            return new LocalAvoidanceCandidateCost(
                hardRisk,
                friendlyOverlapDepth,
                passingSidePenalty,
                progressLoss,
                directionDeviation,
                stepLoss,
                turnDeviation,
                candidateIndex);
        }

        private static int AddPassingSidePenalty(
            int currentPenalty,
            int agentId,
            int neighborId,
            BattleVector2 preferredDirection,
            BattleVector2 candidateStep)
        {
            BattleScalar cross = preferredDirection.XScalar * candidateStep.YScalar
                - preferredDirection.YScalar * candidateStep.XScalar;
            int candidateSign = cross > BattleScalar.Epsilon
                ? 1
                : cross < -BattleScalar.Epsilon ? -1 : 0;
            int desiredSign = GetPairPassingSign(agentId, neighborId);
            int increment = candidateSign == desiredSign
                ? 0
                : candidateSign == 0 ? 1 : 2;
            return currentPenalty > int.MaxValue - increment
                ? int.MaxValue
                : currentPenalty + increment;
        }

        private static int GetPairPassingSign(int firstAgentId, int secondAgentId)
        {
            long minimum = firstAgentId < secondAgentId ? firstAgentId : secondAgentId;
            long maximum = firstAgentId < secondAgentId ? secondAgentId : firstAgentId;
            long pairHash = (minimum * 397L) ^ maximum;
            return (pairHash & 1L) == 0L ? 1 : -1;
        }

        private static int CompareCandidateCosts(
            LocalAvoidanceCandidateCost left,
            LocalAvoidanceCandidateCost right,
            LocalAvoidanceSettings settings)
        {
            int hardRiskComparison = left.HardRisk.CompareTo(right.HardRisk);
            if (hardRiskComparison != 0)
            {
                return hardRiskComparison;
            }

            BattleScalar leftScore = CalculateWeightedScore(left, settings);
            BattleScalar rightScore = CalculateWeightedScore(right, settings);
            int scoreComparison = leftScore.CompareTo(rightScore);
            return scoreComparison != 0
                ? scoreComparison
                : left.CandidateIndex.CompareTo(right.CandidateIndex);
        }

        private static BattleScalar CalculateWeightedScore(
            LocalAvoidanceCandidateCost cost,
            LocalAvoidanceSettings settings)
        {
            return cost.FriendlyOverlapDepth * settings.FriendlyOverlapWeight
                + BattleScalar.FromInt(cost.PassingSidePenalty)
                + cost.ProgressLoss * settings.ProgressLossWeight
                + cost.DirectionDeviation * settings.DirectionWeight
                + cost.StepLoss * settings.StepLossWeight
                + cost.TurnDeviation * settings.TurnWeight;
        }

        private static int ResolveConflicts(
            int agentCount,
            LocalAvoidanceSettings settings,
            LocalAvoidanceUniformGrid grid,
            LocalAvoidanceWorkspace workspace)
        {
            int executedPassCount = 0;
            for (int pass = 0; pass < settings.MaxConflictResolutionPasses; pass++)
            {
                Array.Copy(workspace.SelectedSteps, workspace.SnapshotSteps, agentCount);
                int pairCount = CollectConflictPairs(
                    agentCount,
                    settings,
                    grid,
                    workspace.SnapshotSteps,
                    workspace);
                if (pairCount == 0)
                {
                    break;
                }

                executedPassCount++;
                ResolveConflictPairs(
                    pairCount,
                    settings,
                    grid,
                    workspace.SnapshotSteps,
                    workspace);
            }

            return executedPassCount;
        }

        private static void ResolveConflictPairs(
            int pairCount,
            LocalAvoidanceSettings settings,
            LocalAvoidanceUniformGrid grid,
            BattleVector2[] referenceSteps,
            LocalAvoidanceWorkspace workspace)
        {
            for (int pairIndex = 0; pairIndex < pairCount; pairIndex++)
            {
                LocalAvoidanceWorkspace.ConflictPair pair = workspace.ConflictPairs[pairIndex];
                LocalAvoidanceAgent first = workspace.SortedAgents[pair.FirstAgentIndex];
                LocalAvoidanceAgent second = workspace.SortedAgents[pair.SecondAgentIndex];
                bool secondCanYield = second.Mobility == LocalAvoidanceMobility.Moving;
                bool firstCanYield = first.Mobility == LocalAvoidanceMobility.Moving;

                if (secondCanYield && TrySelectNextHardLegalCandidate(
                    pair.SecondAgentIndex,
                    settings,
                    grid,
                    referenceSteps,
                    workspace))
                {
                    continue;
                }

                if (firstCanYield && TrySelectNextHardLegalCandidate(
                    pair.FirstAgentIndex,
                    settings,
                    grid,
                    referenceSteps,
                    workspace))
                {
                    continue;
                }

                if (secondCanYield)
                {
                    SetSelection(
                        workspace,
                        pair.SecondAgentIndex,
                        BattleVector2.Zero,
                        LocalAvoidanceCandidateSet.ZeroIndex,
                        hardBlocked: true);
                }

                if (firstCanYield)
                {
                    SetSelection(
                        workspace,
                        pair.FirstAgentIndex,
                        BattleVector2.Zero,
                        LocalAvoidanceCandidateSet.ZeroIndex,
                        hardBlocked: true);
                }
            }
        }

        private static bool TrySelectNextHardLegalCandidate(
            int agentIndex,
            LocalAvoidanceSettings settings,
            LocalAvoidanceUniformGrid grid,
            BattleVector2[] referenceSteps,
            LocalAvoidanceWorkspace workspace)
        {
            LocalAvoidanceAgent agent = workspace.SortedAgents[agentIndex];
            if (ShouldSelectZero(agent))
            {
                return false;
            }

            int firstCandidateIndex = workspace.SelectedCandidateIndices[agentIndex] + 1;
            if (firstCandidateIndex < 0)
            {
                firstCandidateIndex = 0;
            }

            BattleVector2 preferredDirection = agent.PreferredStep.Normalized;
            BattleScalar candidateBudget = CalculateCandidateBudget(agent);
            for (int candidateIndex = firstCandidateIndex;
                candidateIndex < LocalAvoidanceCandidateSet.ZeroIndex;
                candidateIndex++)
            {
                BattleVector2 candidateStep = LocalAvoidanceCandidateSet.Get(
                    candidateIndex,
                    preferredDirection,
                    candidateBudget);
                workspace.AddCandidateEvaluations(1);
                if (!IsHardLegalAgainstSnapshot(
                    agentIndex,
                    agent,
                    candidateStep,
                    settings,
                    grid,
                    referenceSteps,
                    workspace))
                {
                    continue;
                }

                SetSelection(
                    workspace,
                    agentIndex,
                    candidateStep,
                    candidateIndex,
                    hardBlocked: false);
                return true;
            }

            return false;
        }

        private static bool IsHardLegalAgainstSnapshot(
            int agentIndex,
            LocalAvoidanceAgent agent,
            BattleVector2 candidateStep,
            LocalAvoidanceSettings settings,
            LocalAvoidanceUniformGrid grid,
            BattleVector2[] referenceSteps,
            LocalAvoidanceWorkspace workspace)
        {
            BattleScalar queryRadius = CalculateQueryRadius(agent, settings, workspace);
            int queryCount = QueryAndCount(
                agentIndex,
                agent,
                queryRadius,
                grid,
                workspace);
            for (int neighborSlot = 0; neighborSlot < queryCount; neighborSlot++)
            {
                int neighborIndex = workspace.GetNeighborAgentIndex(neighborSlot);
                if (neighborIndex == agentIndex)
                {
                    continue;
                }

                LocalAvoidanceAgent neighbor = workspace.SortedAgents[neighborIndex];
                if (neighbor.GroupId == agent.GroupId)
                {
                    continue;
                }

                if (CreatesHardConflict(
                    agent,
                    candidateStep,
                    neighbor,
                    referenceSteps[neighborIndex]))
                {
                    return false;
                }
            }

            return true;
        }

        private static int QueryAndCount(
            int agentIndex,
            LocalAvoidanceAgent agent,
            BattleScalar queryRadius,
            LocalAvoidanceUniformGrid grid,
            LocalAvoidanceWorkspace workspace)
        {
            if (ShouldSelectZero(agent))
            {
                throw new InvalidOperationException(
                    "Local avoidance agents inactive for this tick may not initiate grid queries.");
            }

            workspace.AddActiveQueries(1);
            int queryCount = grid.Query(agent.Position, queryRadius, workspace);
            int nonSelfNeighborCount = 0;
            for (int neighborSlot = 0; neighborSlot < queryCount; neighborSlot++)
            {
                if (workspace.GetNeighborAgentIndex(neighborSlot) != agentIndex)
                {
                    nonSelfNeighborCount++;
                }
            }

            workspace.AddNeighborChecks(nonSelfNeighborCount);
            return queryCount;
        }

        private static int CollectConflictPairs(
            int agentCount,
            LocalAvoidanceSettings settings,
            LocalAvoidanceUniformGrid grid,
            BattleVector2[] steps,
            LocalAvoidanceWorkspace workspace)
        {
            int pairCount = 0;
            for (int agentIndex = 0; agentIndex < agentCount; agentIndex++)
            {
                LocalAvoidanceAgent agent = workspace.SortedAgents[agentIndex];
                if (ShouldSelectZero(agent))
                {
                    continue;
                }

                BattleScalar queryRadius = CalculateQueryRadius(agent, settings, workspace);
                int queryCount = QueryAndCount(
                    agentIndex,
                    agent,
                    queryRadius,
                    grid,
                    workspace);
                for (int neighborSlot = 0; neighborSlot < queryCount; neighborSlot++)
                {
                    int neighborIndex = workspace.GetNeighborAgentIndex(neighborSlot);
                    LocalAvoidanceAgent neighbor = workspace.SortedAgents[neighborIndex];
                    if (neighborIndex == agentIndex
                        || neighbor.GroupId == agent.GroupId
                        || (!ShouldSelectZero(neighbor)
                            && neighbor.AgentId <= agent.AgentId)
                        || !CreatesHardConflict(
                            agent,
                            steps[agentIndex],
                            neighbor,
                            steps[neighborIndex]))
                    {
                        continue;
                    }

                    if (pairCount == int.MaxValue)
                    {
                        throw new InvalidOperationException(
                            "Local avoidance conflict pair capacity exceeded.");
                    }

                    workspace.EnsureConflictPairCapacity(pairCount + 1);
                    bool agentHasLowerId = agent.AgentId < neighbor.AgentId;
                    workspace.ConflictPairs[pairCount] = new LocalAvoidanceWorkspace.ConflictPair
                    {
                        FirstAgentIndex = agentHasLowerId ? agentIndex : neighborIndex,
                        SecondAgentIndex = agentHasLowerId ? neighborIndex : agentIndex,
                        FirstAgentId = agentHasLowerId ? agent.AgentId : neighbor.AgentId,
                        SecondAgentId = agentHasLowerId ? neighbor.AgentId : agent.AgentId
                    };
                    pairCount++;
                }
            }

            Array.Sort(
                workspace.ConflictPairs,
                0,
                pairCount,
                ConflictPairComparer.Instance);
            workspace.ConflictPairCount = pairCount;
            return pairCount;
        }

        private static bool CreatesHardConflict(
            LocalAvoidanceAgent first,
            BattleVector2 firstStep,
            LocalAvoidanceAgent second,
            BattleVector2 secondStep)
        {
            if (LocalAvoidanceGeometry.SweptCirclesOverlap(
                first.Position,
                firstStep,
                first.Radius,
                second.Position,
                secondStep,
                second.Radius,
                BattleScalar.One))
            {
                return true;
            }

            BattleScalar combinedRadius = first.Radius + second.Radius;
            BattleVector2 firstEnd = first.Position + firstStep;
            BattleVector2 secondEnd = second.Position + secondStep;
            return BattleVector2.SqrDistanceScalar(firstEnd, secondEnd)
                < combinedRadius * combinedRadius;
        }

        private static void ValidateFinalHardConstraints(
            int agentCount,
            LocalAvoidanceSettings settings,
            LocalAvoidanceUniformGrid grid,
            LocalAvoidanceWorkspace workspace)
        {
            long maximumRounds = (long)agentCount * LocalAvoidanceCandidateSet.Count + 1L;
            for (long round = 0L; round < maximumRounds; round++)
            {
                Array.Copy(workspace.SelectedSteps, workspace.SnapshotSteps, agentCount);
                int pairCount = CollectConflictPairs(
                    agentCount,
                    settings,
                    grid,
                    workspace.SnapshotSteps,
                    workspace);
                if (pairCount == 0)
                {
                    return;
                }

                bool changed = ResolveFinalConflictPairs(
                    pairCount,
                    settings,
                    grid,
                    workspace.SnapshotSteps,
                    workspace);
                if (!changed)
                {
                    return;
                }
            }
        }

        private static bool ResolveFinalConflictPairs(
            int pairCount,
            LocalAvoidanceSettings settings,
            LocalAvoidanceUniformGrid grid,
            BattleVector2[] referenceSteps,
            LocalAvoidanceWorkspace workspace)
        {
            bool changed = false;
            for (int pairIndex = 0; pairIndex < pairCount; pairIndex++)
            {
                LocalAvoidanceWorkspace.ConflictPair pair = workspace.ConflictPairs[pairIndex];
                LocalAvoidanceAgent first = workspace.SortedAgents[pair.FirstAgentIndex];
                LocalAvoidanceAgent second = workspace.SortedAgents[pair.SecondAgentIndex];
                if (second.Mobility == LocalAvoidanceMobility.Moving
                    && TrySelectNextHardLegalCandidate(
                        pair.SecondAgentIndex,
                        settings,
                        grid,
                        referenceSteps,
                        workspace))
                {
                    changed = true;
                    continue;
                }

                if (first.Mobility == LocalAvoidanceMobility.Moving
                    && TrySelectNextHardLegalCandidate(
                        pair.FirstAgentIndex,
                        settings,
                        grid,
                        referenceSteps,
                        workspace))
                {
                    changed = true;
                    continue;
                }

                if (second.Mobility == LocalAvoidanceMobility.Moving)
                {
                    changed |= SetHardBlockedZero(workspace, pair.SecondAgentIndex);
                }

                if (first.Mobility == LocalAvoidanceMobility.Moving)
                {
                    changed |= SetHardBlockedZero(workspace, pair.FirstAgentIndex);
                }
            }

            return changed;
        }

        private static bool SetHardBlockedZero(
            LocalAvoidanceWorkspace workspace,
            int agentIndex)
        {
            bool changed = workspace.SelectedCandidateIndices[agentIndex]
                    != LocalAvoidanceCandidateSet.ZeroIndex
                || workspace.SelectedSteps[agentIndex].SqrMagnitudeScalar
                    > BattleScalar.Epsilon
                || !workspace.HardBlocked[agentIndex];
            SetSelection(
                workspace,
                agentIndex,
                BattleVector2.Zero,
                LocalAvoidanceCandidateSet.ZeroIndex,
                hardBlocked: true);
            return changed;
        }

        private static BattleScalar ClampUnitDot(BattleScalar value)
        {
            if (value < -BattleScalar.One)
            {
                return -BattleScalar.One;
            }

            return value > BattleScalar.One ? BattleScalar.One : value;
        }

        private static BattleScalar Absolute(BattleScalar value)
        {
            return value < BattleScalar.Zero ? -value : value;
        }

        private static void SetSelection(
            LocalAvoidanceWorkspace workspace,
            int agentIndex,
            BattleVector2 selectedStep,
            int selectedCandidateIndex,
            bool hardBlocked)
        {
            workspace.SelectedSteps[agentIndex] = selectedStep;
            workspace.SelectedCandidateIndices[agentIndex] = selectedCandidateIndex;
            workspace.HardBlocked[agentIndex] = hardBlocked;
        }

        private static void WriteDecisions(
            LocalAvoidanceWorkspace workspace,
            int agentCount)
        {
            for (int i = 0; i < agentCount; i++)
            {
                workspace.Decisions[i] = new LocalAvoidanceDecision(
                    workspace.SortedAgents[i].AgentId,
                    workspace.SelectedSteps[i],
                    workspace.SelectedCandidateIndices[i],
                    workspace.HardBlocked[i]);
            }
        }

        private sealed class AgentIdComparer : IComparer<LocalAvoidanceAgent>
        {
            internal static readonly AgentIdComparer Instance = new AgentIdComparer();

            public int Compare(LocalAvoidanceAgent left, LocalAvoidanceAgent right)
            {
                return left.AgentId.CompareTo(right.AgentId);
            }
        }

        private sealed class ConflictPairComparer :
            IComparer<LocalAvoidanceWorkspace.ConflictPair>
        {
            internal static readonly ConflictPairComparer Instance =
                new ConflictPairComparer();

            public int Compare(
                LocalAvoidanceWorkspace.ConflictPair left,
                LocalAvoidanceWorkspace.ConflictPair right)
            {
                int firstComparison = left.FirstAgentId.CompareTo(right.FirstAgentId);
                return firstComparison != 0
                    ? firstComparison
                    : left.SecondAgentId.CompareTo(right.SecondAgentId);
            }
        }
    }
}
