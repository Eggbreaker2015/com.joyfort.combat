using System;
using Combat.Core.Battle;
using Combat.Core.LocalAvoidance;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class LocalAvoidanceSolverTests
    {
        private static readonly BattleScalar Half =
            BattleScalar.One / BattleScalar.FromInt(2);

        [Test]
        public void Solve_SingleMovingAgentSelectsPreferredStep()
        {
            LocalAvoidanceAgent agent = Agent(
                1,
                BattleVector2.Zero,
                BattleVector2.Right,
                BattleScalar.One);

            LocalAvoidanceSolveResult result = Solve(agent);

            Assert.AreEqual(1, result.DecisionCount);
            AssertDecision(result[0], 1, BattleVector2.Right, 0);
            Assert.AreEqual(1, result.Stats.GridCellCount);
            Assert.AreEqual(0, result.Stats.NeighborCheckCount);
            Assert.AreEqual(0, result.Stats.CandidateEvaluationCount);
            Assert.AreEqual(0, result.Stats.ConflictResolutionPassCount);
        }

        [Test]
        public void Solve_PreferredStepBelowBudgetPreservesLength()
        {
            BattleVector2 preferred = new BattleVector2(Half, BattleScalar.Zero);
            LocalAvoidanceAgent agent = Agent(
                1,
                BattleVector2.Zero,
                preferred,
                BattleScalar.One);

            LocalAvoidanceSolveResult result = Solve(agent);

            AssertDecision(result[0], 1, preferred, 0);
        }

        [Test]
        public void Solve_PreferredStepAboveBudgetClampsToBudget()
        {
            BattleVector2 preferred = new BattleVector2(
                BattleScalar.FromInt(3),
                BattleScalar.FromInt(4));
            BattleScalar budget = BattleScalar.FromInt(2);
            LocalAvoidanceAgent agent = Agent(
                1,
                BattleVector2.Zero,
                preferred,
                budget);

            LocalAvoidanceSolveResult result = Solve(agent);

            Assert.LessOrEqual(
                result[0].SelectedStep.MagnitudeScalar.RawValue,
                budget.RawValue);
            Assert.AreEqual(preferred.Normalized.XRaw, result[0].SelectedStep.Normalized.XRaw);
            Assert.AreEqual(preferred.Normalized.YRaw, result[0].SelectedStep.Normalized.YRaw);
            Assert.AreNotEqual(preferred.XRaw, result[0].SelectedStep.XRaw);
            Assert.AreEqual(0, result[0].SelectedCandidateIndex);
        }

        [Test]
        public void Solve_AnchoredAgentSkipsQueryAndCandidateEvaluation()
        {
            LocalAvoidanceAgent anchored = Agent(
                1,
                BattleVector2.Zero,
                BattleVector2.Right,
                BattleScalar.One,
                LocalAvoidanceMobility.Anchored);

            LocalAvoidanceSolveResult result = Solve(anchored);

            AssertDecision(
                result[0],
                anchored.AgentId,
                BattleVector2.Zero,
                LocalAvoidanceCandidateSet.ZeroIndex);
            Assert.AreEqual(0, result.Stats.NeighborCheckCount);
            Assert.AreEqual(0, result.Stats.CandidateEvaluationCount);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Solve_NonMovingStepSkipsQueryAndCandidateEvaluation(bool zeroBudget)
        {
            BattleScalar nearZero = BattleScalar.Epsilon;
            LocalAvoidanceAgent agent = zeroBudget
                ? Agent(
                    1,
                    BattleVector2.Zero,
                    BattleVector2.Right,
                    BattleScalar.Zero)
                : Agent(
                    1,
                    BattleVector2.Zero,
                    new BattleVector2(nearZero, BattleScalar.Zero),
                    BattleScalar.One);

            LocalAvoidanceSolveResult result = Solve(agent);

            AssertDecision(
                result[0],
                agent.AgentId,
                BattleVector2.Zero,
                LocalAvoidanceCandidateSet.ZeroIndex);
            Assert.AreEqual(0, result.Stats.NeighborCheckCount);
            Assert.AreEqual(0, result.Stats.CandidateEvaluationCount);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Solve_InactiveMovingAgentsDoNotEnterConflictCollection(bool zeroBudget)
        {
            BattleVector2 preferredStep = zeroBudget
                ? BattleVector2.Right
                : new BattleVector2(BattleScalar.Epsilon, BattleScalar.Zero);
            BattleScalar maxStepDistance = zeroBudget
                ? BattleScalar.Zero
                : BattleScalar.One;
            LocalAvoidanceAgent[] agents =
            {
                Agent(
                    1,
                    1,
                    BattleVector2.Zero,
                    preferredStep,
                    maxStepDistance),
                Agent(
                    2,
                    2,
                    BattleVector2.Zero,
                    preferredStep,
                    maxStepDistance)
            };

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(agents),
                new LocalAvoidanceWorkspace());

            Assert.AreEqual(0, result.Stats.ConflictResolutionPassCount);
            Assert.AreEqual(0, result.Stats.NeighborCheckCount);
            Assert.AreEqual(0, result.Stats.CandidateEvaluationCount);
            AssertDecision(
                result[0],
                1,
                BattleVector2.Zero,
                LocalAvoidanceCandidateSet.ZeroIndex);
            AssertDecision(
                result[1],
                2,
                BattleVector2.Zero,
                LocalAvoidanceCandidateSet.ZeroIndex);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Solve_ActiveMoverTreatsZeroStepMovingAgentAsPassiveObstacle(
            bool inactiveHasLowerId)
        {
            int activeId = inactiveHasLowerId ? 9 : 1;
            int inactiveId = inactiveHasLowerId ? 1 : 9;
            LocalAvoidanceAgent active = Agent(
                activeId,
                1,
                Position(-2, 0),
                BattleVector2.Right * BattleScalar.FromInt(2),
                BattleScalar.FromInt(2));
            LocalAvoidanceAgent inactive = Agent(
                inactiveId,
                2,
                BattleVector2.Zero,
                BattleVector2.Right,
                BattleScalar.Zero);

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(new[] { inactive, active }),
                new LocalAvoidanceWorkspace());

            int activeDecisionIndex = inactiveHasLowerId ? 1 : 0;
            int inactiveDecisionIndex = inactiveHasLowerId ? 0 : 1;
            AssertEnemyPairHardSafe(
                active,
                result[activeDecisionIndex],
                inactive,
                result[inactiveDecisionIndex]);
            AssertDecision(
                result[inactiveDecisionIndex],
                inactive.AgentId,
                BattleVector2.Zero,
                LocalAvoidanceCandidateSet.ZeroIndex);
            Assert.AreEqual(3, result.Stats.NeighborCheckCount);
            Assert.AreEqual(
                LocalAvoidanceCandidateSet.Count,
                result.Stats.CandidateEvaluationCount);
        }

        [Test]
        public void Solve_LowerIdInactiveMoverIsCollectedAsPassiveConflictObstacle()
        {
            LocalAvoidanceAgent inactive = Agent(
                1,
                2,
                BattleVector2.Zero,
                BattleVector2.Right,
                BattleScalar.Zero);
            LocalAvoidanceAgent active = Agent(
                9,
                1,
                BattleVector2.Zero,
                BattleVector2.Right,
                BattleScalar.One);
            var workspace = new LocalAvoidanceWorkspace();

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(new[] { active, inactive }),
                workspace);

            Assert.AreEqual(1, workspace.ConflictPairCount);
            Assert.AreEqual(inactive.AgentId, workspace.ConflictPairs[0].FirstAgentId);
            Assert.AreEqual(active.AgentId, workspace.ConflictPairs[0].SecondAgentId);
            Assert.AreEqual(
                LocalAvoidanceSettings.Default.MaxConflictResolutionPasses,
                result.Stats.ConflictResolutionPassCount);
            Assert.AreEqual(inactive.AgentId, result[0].AgentId);
            Assert.AreEqual(BattleVector2.Zero.XRaw, result[0].SelectedStep.XRaw);
            Assert.AreEqual(BattleVector2.Zero.YRaw, result[0].SelectedStep.YRaw);
            Assert.AreEqual(
                LocalAvoidanceCandidateSet.ZeroIndex,
                result[0].SelectedCandidateIndex);
            Assert.IsTrue(result[1].WasHardBlocked);
        }

        [Test]
        public void Solve_RejectsDuplicateAgentIds()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(7, Position(2, 0), BattleVector2.Right, BattleScalar.One),
                Agent(7, Position(-2, 0), BattleVector2.Right, BattleScalar.One)
            };

            Assert.Throws<ArgumentException>(() => LocalAvoidanceSolver.Solve(
                Frame(agents),
                new LocalAvoidanceWorkspace()));
        }

        [Test]
        public void Solve_PermutedInputProducesAgentIdOrderedRawIdenticalDecisions()
        {
            LocalAvoidanceAgent first = Agent(
                30,
                Position(20, 0),
                BattleVector2.Right,
                BattleScalar.One);
            LocalAvoidanceAgent second = Agent(
                10,
                Position(-20, 0),
                new BattleVector2(BattleScalar.Zero, BattleScalar.One),
                BattleScalar.One);
            LocalAvoidanceAgent third = Agent(
                20,
                Position(0, 20),
                new BattleVector2(-BattleScalar.One, BattleScalar.Zero),
                BattleScalar.One);

            LocalAvoidanceSolveResult original = LocalAvoidanceSolver.Solve(
                Frame(new[] { first, second, third }),
                new LocalAvoidanceWorkspace());
            LocalAvoidanceSolveResult permuted = LocalAvoidanceSolver.Solve(
                Frame(new[] { third, first, second }),
                new LocalAvoidanceWorkspace());

            Assert.AreEqual(3, original.DecisionCount);
            Assert.AreEqual(3, permuted.DecisionCount);
            for (int i = 0; i < original.DecisionCount; i++)
            {
                Assert.AreEqual(original[i].AgentId, permuted[i].AgentId);
                Assert.AreEqual(i == 0 ? 10 : i == 1 ? 20 : 30, original[i].AgentId);
                Assert.AreEqual(original[i].SelectedStep.XRaw, permuted[i].SelectedStep.XRaw);
                Assert.AreEqual(original[i].SelectedStep.YRaw, permuted[i].SelectedStep.YRaw);
                Assert.AreEqual(
                    original[i].SelectedCandidateIndex,
                    permuted[i].SelectedCandidateIndex);
                Assert.AreEqual(original[i].WasHardBlocked, permuted[i].WasHardBlocked);
            }
        }

        [Test]
        public void Solve_ResultIsSynchronousWorkspaceViewAndWorkspacesAreIsolated()
        {
            var firstWorkspace = new LocalAvoidanceWorkspace();
            var secondWorkspace = new LocalAvoidanceWorkspace();
            LocalAvoidanceSolveResult firstResult = LocalAvoidanceSolver.Solve(
                Frame(new[]
                {
                    Agent(1, BattleVector2.Zero, BattleVector2.Right, BattleScalar.One)
                }),
                firstWorkspace);
            LocalAvoidanceSolveResult isolatedResult = LocalAvoidanceSolver.Solve(
                Frame(new[]
                {
                    Agent(3, BattleVector2.Zero, BattleVector2.Right, BattleScalar.One)
                }),
                secondWorkspace);

            LocalAvoidanceSolveResult overwrite = LocalAvoidanceSolver.Solve(
                Frame(new[]
                {
                    Agent(
                        2,
                        BattleVector2.Zero,
                        new BattleVector2(BattleScalar.Zero, BattleScalar.One),
                        BattleScalar.One)
                }),
                firstWorkspace);

            Assert.AreEqual(2, firstResult[0].AgentId);
            Assert.AreEqual(BattleScalar.One.RawValue, firstResult[0].SelectedStep.YRaw);
            Assert.AreEqual(2, overwrite[0].AgentId);
            Assert.AreEqual(3, isolatedResult[0].AgentId);
            Assert.AreEqual(BattleScalar.One.RawValue, isolatedResult[0].SelectedStep.XRaw);
        }

        [Test]
        public void Solve_EmptyFrameReturnsZeroCountAndRealStats()
        {
            var workspace = new LocalAvoidanceWorkspace();

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(Array.Empty<LocalAvoidanceAgent>()),
                workspace);

            Assert.AreEqual(0, result.DecisionCount);
            Assert.AreEqual(0, result.Stats.GridCellCount);
            Assert.AreEqual(0, result.Stats.NeighborCheckCount);
            Assert.AreEqual(0, result.Stats.CandidateEvaluationCount);
            Assert.AreEqual(0, result.Stats.ConflictResolutionPassCount);
        }

        [Test]
        public void Solve_RejectsNullWorkspace()
        {
            Assert.Throws<ArgumentNullException>(() =>
                LocalAvoidanceSolver.Solve(
                    Frame(Array.Empty<LocalAvoidanceAgent>()),
                    null));
        }

        [Test]
        public void Solve_NearbyNeighborEvaluatesAllBaselineCandidates()
        {
            LocalAvoidanceAgent moving = Agent(
                1,
                BattleVector2.Zero,
                BattleVector2.Right,
                BattleScalar.One);
            LocalAvoidanceAgent anchored = Agent(
                2,
                Position(1, 0),
                BattleVector2.Zero,
                BattleScalar.Zero,
                LocalAvoidanceMobility.Anchored);

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(new[] { anchored, moving }),
                new LocalAvoidanceWorkspace());

            BattleVector2 expectedStep = LocalAvoidanceCandidateSet.Get(
                24,
                BattleVector2.Right,
                BattleScalar.One);
            AssertDecision(result[0], 1, expectedStep, 24);
            Assert.AreEqual(3, result.Stats.NeighborCheckCount);
            Assert.AreEqual(
                LocalAvoidanceCandidateSet.Count,
                result.Stats.CandidateEvaluationCount);
        }

        [Test]
        public void Solve_AfterWorkspaceWarmupDoesNotAllocateAcrossOneThousandCalls()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(1, 1, Position(-2, 0), BattleVector2.Right, BattleScalar.One),
                Agent(2, 2, Position(2, 0), Left(), BattleScalar.One),
                Agent(
                    3,
                    1,
                    Position(0, 2),
                    new BattleVector2(BattleScalar.Zero, -BattleScalar.One),
                    BattleScalar.One)
            };
            LocalAvoidanceFrame frame = Frame(agents);
            var workspace = new LocalAvoidanceWorkspace();
            LocalAvoidanceSolveResult result = default;
            for (var i = 0; i < 10; i++)
            {
                result = LocalAvoidanceSolver.Solve(frame, workspace);
            }

            Assert.AreEqual(1, result.Stats.ConflictResolutionPassCount);
            Assert.GreaterOrEqual(workspace.ConflictPairs.Length, 1);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            {
                result = LocalAvoidanceSolver.Solve(frame, workspace);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(3, result.DecisionCount);
            Assert.AreEqual(1, result.Stats.ConflictResolutionPassCount);
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void Solve_AnchoredWarmPathDoesNotAllocatePerCall()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(
                    1,
                    BattleVector2.Zero,
                    BattleVector2.Right,
                    BattleScalar.One),
                Agent(
                    2,
                    Position(1, 0),
                    BattleVector2.Zero,
                    BattleScalar.Zero,
                    LocalAvoidanceMobility.Anchored)
            };
            LocalAvoidanceFrame frame = Frame(agents);
            var workspace = new LocalAvoidanceWorkspace();
            LocalAvoidanceSolver.Solve(frame, workspace);

            long before = GC.GetAllocatedBytesForCurrentThread();
            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(frame, workspace);
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(2, result.DecisionCount);
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void Solve_LargeAgentCapacityKeepsCandidateScratchAtSixtyFour()
        {
            const int agentCount = 128;
            var agents = new LocalAvoidanceAgent[agentCount];
            for (int i = 0; i < agentCount; i++)
            {
                agents[i] = Agent(
                    agentCount - i,
                    Position(i, 0),
                    BattleVector2.Zero,
                    BattleScalar.Zero,
                    LocalAvoidanceMobility.Anchored);
            }

            var workspace = new LocalAvoidanceWorkspace();

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(agents),
                workspace);

            Assert.AreEqual(agentCount, result.DecisionCount);
            Assert.AreEqual(1, result[0].AgentId);
            Assert.AreEqual(agentCount, result[agentCount - 1].AgentId);
            Assert.AreEqual(128, workspace.AgentCapacity);
            Assert.AreEqual(64, workspace.CandidateSteps.Length);
            Assert.AreEqual(64, workspace.CandidateCosts.Length);
            Assert.AreEqual(0, result.Stats.NeighborCheckCount);
            Assert.AreEqual(0, result.Stats.CandidateEvaluationCount);
        }

        [Test]
        public void Solve_OneHundredTwentyEightDispersedAgentsUseBroadPhase()
        {
            const int width = 16;
            const int height = 8;
            const int spacing = 20;
            var agents = new LocalAvoidanceAgent[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    int index = (y * width) + x;
                    agents[index] = Agent(
                        index + 1,
                        Position(x * spacing, y * spacing),
                        BattleVector2.Right,
                        BattleScalar.One);
                }
            }

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(agents),
                new LocalAvoidanceWorkspace());

            Assert.AreEqual(agents.Length, result.DecisionCount);
            Assert.That(
                result.Stats.BroadphaseCandidateCount,
                Is.LessThan(agents.Length * agents.Length / 4));
            Assert.AreEqual(
                result.Stats.ActiveQueryCount,
                result.Stats.BroadphaseCandidateCount);
            Assert.That(result.Stats.GridCellCount, Is.GreaterThan(1));
        }

        [Test]
        public void Solve_SameGroupHeadOnAgentsChooseStableOppositeWorldSides()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(1, 1, Position(-1, 0), BattleVector2.Right, BattleScalar.One),
                Agent(2, 1, Position(1, 0), Left(), BattleScalar.One)
            };

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(agents),
                new LocalAvoidanceWorkspace());

            Assert.Greater(result[0].SelectedStep.XRaw, 0L);
            Assert.Less(result[1].SelectedStep.XRaw, 0L);
            Assert.AreNotEqual(0L, result[0].SelectedStep.YRaw);
            Assert.AreEqual(-result[0].SelectedStep.YRaw, result[1].SelectedStep.YRaw);
            Assert.AreEqual(result[0].SelectedCandidateIndex, result[1].SelectedCandidateIndex);
            Assert.IsFalse(result[0].WasHardBlocked);
            Assert.IsFalse(result[1].WasHardBlocked);
        }

        [Test]
        public void Solve_SameGroupCrossingAgentsPreserveProgressWithoutExactOverlap()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(11, 7, Position(-1, 0), BattleVector2.Right, BattleScalar.One),
                Agent(
                    12,
                    7,
                    Position(0, -1),
                    new BattleVector2(BattleScalar.Zero, BattleScalar.One),
                    BattleScalar.One)
            };

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(agents),
                new LocalAvoidanceWorkspace());
            BattleVector2 firstEnd = agents[0].Position + result[0].SelectedStep;
            BattleVector2 secondEnd = agents[1].Position + result[1].SelectedStep;

            Assert.Greater(result[0].SelectedStep.XRaw, 0L);
            Assert.Greater(result[1].SelectedStep.YRaw, 0L);
            Assert.IsTrue(LocalAvoidanceCandidateSet.IsFullSpeed(
                result[0].SelectedCandidateIndex));
            Assert.IsTrue(LocalAvoidanceCandidateSet.IsFullSpeed(
                result[1].SelectedCandidateIndex));
            Assert.IsFalse(
                firstEnd.XRaw == secondEnd.XRaw
                && firstEnd.YRaw == secondEnd.YRaw);
            Assert.IsFalse(result[0].WasHardBlocked);
            Assert.IsFalse(result[1].WasHardBlocked);
        }

        [Test]
        public void Solve_FriendlyPassingPrefersFullSpeedWhenSoftCompressionIncreaseIsSmall()
        {
            BattleScalar half = BattleScalar.One / BattleScalar.FromInt(2);
            LocalAvoidanceAgent[] agents =
            {
                Agent(
                    1,
                    7,
                    BattleVector2.Zero,
                    BattleVector2.Right,
                    BattleScalar.One),
                Agent(
                    2,
                    7,
                    new BattleVector2(half, -half),
                    BattleVector2.Right,
                    BattleScalar.One),
                Agent(
                    3,
                    7,
                    new BattleVector2(BattleScalar.One, -half),
                    new BattleVector2(BattleScalar.Zero, BattleScalar.One),
                    BattleScalar.One)
            };

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(agents),
                new LocalAvoidanceWorkspace());
            LocalAvoidanceSolveResult strictResult = LocalAvoidanceSolver.Solve(
                new LocalAvoidanceFrame(
                    agents,
                    agents.Length,
                    SettingsWithFullSpeedTolerance(BattleScalar.Zero)),
                new LocalAvoidanceWorkspace());

            Assert.IsTrue(LocalAvoidanceCandidateSet.IsFullSpeed(
                result[0].SelectedCandidateIndex));
            Assert.IsFalse(LocalAvoidanceCandidateSet.IsFullSpeed(
                strictResult[0].SelectedCandidateIndex));
            Assert.Greater(result[0].SelectedStep.XRaw, 0L);
            Assert.Less(result[0].SelectedStep.YRaw, 0L);
            Assert.IsFalse(result[0].WasHardBlocked);
        }

        [Test]
        public void Solve_SameGroupCongestionMayCompressWithoutStoppingEveryMover()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(1, 4, Position(-1, 0), BattleVector2.Right, BattleScalar.One),
                Agent(2, 4, Position(1, 0), Left(), BattleScalar.One),
                Agent(
                    3,
                    4,
                    Position(0, -1),
                    new BattleVector2(BattleScalar.Zero, BattleScalar.One),
                    BattleScalar.One),
                Agent(
                    4,
                    4,
                    Position(0, 1),
                    new BattleVector2(BattleScalar.Zero, -BattleScalar.One),
                    BattleScalar.One)
            };

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(agents),
                new LocalAvoidanceWorkspace());

            int movingCount = 0;
            for (int i = 0; i < result.DecisionCount; i++)
            {
                if (result[i].SelectedStep.SqrMagnitudeScalar > BattleScalar.Epsilon)
                {
                    movingCount++;
                }

                Assert.IsFalse(result[i].WasHardBlocked);
            }

            Assert.GreaterOrEqual(movingCount, 2);
        }

        [Test]
        public void Solve_DifferentGroupsCannotSweepThroughEachOther()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(1, 1, Position(-2, 0), BattleVector2.Right * BattleScalar.FromInt(3), BattleScalar.FromInt(3)),
                Agent(2, 2, Position(2, 0), Left() * BattleScalar.FromInt(3), BattleScalar.FromInt(3))
            };

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(agents),
                new LocalAvoidanceWorkspace());

            AssertEnemyPairHardSafe(agents[0], result[0], agents[1], result[1]);
            Assert.IsFalse(
                result[0].SelectedStep.XRaw == agents[0].PreferredStep.XRaw
                && result[0].SelectedStep.YRaw == agents[0].PreferredStep.YRaw
                && result[1].SelectedStep.XRaw == agents[1].PreferredStep.XRaw
                && result[1].SelectedStep.YRaw == agents[1].PreferredStep.YRaw);
        }

        [Test]
        public void Solve_PredictionQueryIncludesAgentsBeyondLegacyCellRadius()
        {
            BattleScalar two = BattleScalar.FromInt(2);
            LocalAvoidanceAgent[] agents =
            {
                Agent(1, 1, Position(-5, 0), BattleVector2.Right * two, two),
                Agent(2, 2, Position(5, 0), Left() * two, two)
            };

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(agents),
                new LocalAvoidanceWorkspace());

            Assert.AreEqual(6, result.Stats.NeighborCheckCount);
            Assert.AreEqual(
                LocalAvoidanceCandidateSet.Count * 2,
                result.Stats.CandidateEvaluationCount);
            Assert.AreNotEqual(agents[0].PreferredStep.YRaw, result[0].SelectedStep.YRaw);
            Assert.AreNotEqual(agents[1].PreferredStep.YRaw, result[1].SelectedStep.YRaw);
            Assert.IsFalse(result[0].WasHardBlocked);
            Assert.IsFalse(result[1].WasHardBlocked);
        }

        [Test]
        public void Solve_MovingAgentAvoidsAnchoredAgentWithoutQueryingAnchor()
        {
            LocalAvoidanceAgent moving = Agent(
                1,
                1,
                Position(-2, 0),
                BattleVector2.Right * BattleScalar.FromInt(2),
                BattleScalar.FromInt(2));
            LocalAvoidanceAgent anchored = Agent(
                2,
                2,
                BattleVector2.Zero,
                BattleVector2.Right,
                BattleScalar.One,
                Half,
                LocalAvoidanceMobility.Anchored);

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(new[] { anchored, moving }),
                new LocalAvoidanceWorkspace());

            AssertEnemyPairHardSafe(moving, result[0], anchored, result[1]);
            Assert.AreEqual(LocalAvoidanceCandidateSet.ZeroIndex, result[1].SelectedCandidateIndex);
            Assert.AreEqual(BattleVector2.Zero.XRaw, result[1].SelectedStep.XRaw);
            Assert.AreEqual(BattleVector2.Zero.YRaw, result[1].SelectedStep.YRaw);
            Assert.IsFalse(result[1].WasHardBlocked);
            Assert.AreEqual(LocalAvoidanceCandidateSet.Count, result.Stats.CandidateEvaluationCount);
        }

        [Test]
        public void Solve_HigherIdMovingAgentAvoidsLowerIdAnchor()
        {
            LocalAvoidanceAgent anchored = AnchoredAgent(
                1,
                2,
                BattleVector2.Zero);
            LocalAvoidanceAgent moving = Agent(
                9,
                1,
                Position(-2, 0),
                BattleVector2.Right * BattleScalar.FromInt(2),
                BattleScalar.FromInt(2));

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(new[] { moving, anchored }),
                new LocalAvoidanceWorkspace());

            Assert.AreEqual(anchored.AgentId, result[0].AgentId);
            Assert.AreEqual(BattleVector2.Zero.XRaw, result[0].SelectedStep.XRaw);
            Assert.AreEqual(BattleVector2.Zero.YRaw, result[0].SelectedStep.YRaw);
            Assert.AreEqual(LocalAvoidanceCandidateSet.ZeroIndex, result[0].SelectedCandidateIndex);
            Assert.IsFalse(result[0].WasHardBlocked);
            AssertEnemyPairHardSafe(anchored, result[0], moving, result[1]);
            Assert.IsFalse(result[1].WasHardBlocked);
        }

        [Test]
        public void Solve_LowerIdAnchorConflictPairIsStoredInAgentIdOrder()
        {
            LocalAvoidanceAgent anchored = AnchoredAgent(
                1,
                2,
                BattleVector2.Zero);
            LocalAvoidanceAgent moving = Agent(
                9,
                1,
                BattleVector2.Zero,
                BattleVector2.Right,
                BattleScalar.One);
            var workspace = new LocalAvoidanceWorkspace();

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(new[] { moving, anchored }),
                workspace);

            Assert.AreEqual(1, workspace.ConflictPairCount);
            Assert.AreEqual(0, workspace.ConflictPairs[0].FirstAgentIndex);
            Assert.AreEqual(1, workspace.ConflictPairs[0].SecondAgentIndex);
            Assert.AreEqual(anchored.AgentId, workspace.ConflictPairs[0].FirstAgentId);
            Assert.AreEqual(moving.AgentId, workspace.ConflictPairs[0].SecondAgentId);
            Assert.AreEqual(
                LocalAvoidanceSettings.Default.MaxConflictResolutionPasses,
                result.Stats.ConflictResolutionPassCount);
            AssertDecision(
                result[0],
                anchored.AgentId,
                BattleVector2.Zero,
                LocalAvoidanceCandidateSet.ZeroIndex);
            Assert.AreEqual(BattleVector2.Zero.XRaw, result[1].SelectedStep.XRaw);
            Assert.AreEqual(BattleVector2.Zero.YRaw, result[1].SelectedStep.YRaw);
            Assert.IsTrue(result[1].WasHardBlocked);
        }

        [Test]
        public void Solve_DifferentRadiiUseCombinedRadius()
        {
            BattleScalar quarter = BattleScalar.One / BattleScalar.FromInt(4);
            LocalAvoidanceAgent moving = Agent(
                1,
                1,
                Position(-2, 0),
                BattleVector2.Right,
                BattleScalar.One,
                quarter);
            LocalAvoidanceAgent anchored = Agent(
                2,
                2,
                BattleVector2.Zero,
                BattleVector2.Right,
                BattleScalar.One,
                BattleScalar.One,
                LocalAvoidanceMobility.Anchored);

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(new[] { moving, anchored }),
                new LocalAvoidanceWorkspace());

            AssertEnemyPairHardSafe(moving, result[0], anchored, result[1]);
            BattleScalar combinedRadius = moving.Radius + anchored.Radius;
            BattleScalar finalDistanceSquared = BattleVector2.SqrDistanceScalar(
                moving.Position + result[0].SelectedStep,
                anchored.Position);
            Assert.GreaterOrEqual(
                finalDistanceSquared.RawValue,
                (combinedRadius * combinedRadius).RawValue);
        }

        [Test]
        public void Solve_PermutedInputProducesIdenticalRawDecisions()
        {
            LocalAvoidanceAgent[] agents = CrossingEnemyAgents();
            LocalAvoidanceAgent[] permuted = { agents[2], agents[0], agents[3], agents[1] };

            LocalAvoidanceSolveResult first = LocalAvoidanceSolver.Solve(
                Frame(agents),
                new LocalAvoidanceWorkspace());
            LocalAvoidanceSolveResult second = LocalAvoidanceSolver.Solve(
                Frame(permuted),
                new LocalAvoidanceWorkspace());

            AssertRawResultsEqual(first, second);
        }

        [Test]
        public void Solve_NoHardLegalCandidateReturnsZeroAndHardBlocked()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(1, 1, BattleVector2.Zero, BattleVector2.Right, BattleScalar.One),
                AnchoredAgent(2, 2, Position(1, 0)),
                AnchoredAgent(3, 2, Position(-1, 0)),
                AnchoredAgent(4, 2, Position(0, 1)),
                AnchoredAgent(5, 2, Position(0, -1))
            };

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(agents),
                new LocalAvoidanceWorkspace());

            Assert.AreEqual(BattleVector2.Zero.XRaw, result[0].SelectedStep.XRaw);
            Assert.AreEqual(BattleVector2.Zero.YRaw, result[0].SelectedStep.YRaw);
            Assert.AreEqual(LocalAvoidanceCandidateSet.ZeroIndex, result[0].SelectedCandidateIndex);
            Assert.IsTrue(result[0].WasHardBlocked);
        }

        [Test]
        public void Solve_RepeatedFramesDoNotAlternatePassingSide()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(101, 1, Position(-1, 0), BattleVector2.Right, BattleScalar.One),
                Agent(202, 1, Position(1, 0), Left(), BattleScalar.One)
            };
            LocalAvoidanceFrame frame = Frame(agents);
            var workspace = new LocalAvoidanceWorkspace();

            LocalAvoidanceSolveResult first = LocalAvoidanceSolver.Solve(frame, workspace);
            long firstX = first[0].SelectedStep.XRaw;
            long firstY = first[0].SelectedStep.YRaw;
            int firstCandidate = first[0].SelectedCandidateIndex;
            LocalAvoidanceSolveStats firstStats = first.Stats;
            LocalAvoidanceSolveResult second = LocalAvoidanceSolver.Solve(frame, workspace);

            Assert.AreEqual(firstX, second[0].SelectedStep.XRaw);
            Assert.AreEqual(firstY, second[0].SelectedStep.YRaw);
            Assert.AreEqual(firstCandidate, second[0].SelectedCandidateIndex);
            Assert.AreNotEqual(0L, firstY);
            AssertStatsEqual(firstStats, second.Stats);
        }

        [Test]
        public void Solve_EnemyPairsRemainHardSafeFromLegalInitialState()
        {
            LocalAvoidanceAgent[] agents = CrossingEnemyAgents();

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(agents),
                new LocalAvoidanceWorkspace());

            for (int first = 0; first < agents.Length; first++)
            {
                for (int second = first + 1; second < agents.Length; second++)
                {
                    if (agents[first].GroupId != agents[second].GroupId)
                    {
                        AssertEnemyPairHardSafe(
                            agents[first],
                            result[first],
                            agents[second],
                            result[second]);
                    }
                }
            }
        }

        [Test]
        public void Solve_AdditionalAnchorsDoNotAddActiveQueryOrCandidateWork()
        {
            LocalAvoidanceAgent[] oneAnchor = { AnchoredAgent(1, 1, BattleVector2.Zero) };
            LocalAvoidanceAgent[] manyAnchors =
            {
                AnchoredAgent(1, 1, BattleVector2.Zero),
                AnchoredAgent(2, 2, Position(1, 0)),
                AnchoredAgent(3, 3, Position(-1, 0)),
                AnchoredAgent(4, 4, Position(0, 1))
            };

            LocalAvoidanceSolveResult first = LocalAvoidanceSolver.Solve(
                Frame(oneAnchor),
                new LocalAvoidanceWorkspace());
            LocalAvoidanceSolveResult second = LocalAvoidanceSolver.Solve(
                Frame(manyAnchors),
                new LocalAvoidanceWorkspace());

            Assert.AreEqual(0, first.Stats.NeighborCheckCount);
            Assert.AreEqual(0, first.Stats.CandidateEvaluationCount);
            Assert.AreEqual(0, second.Stats.NeighborCheckCount);
            Assert.AreEqual(0, second.Stats.CandidateEvaluationCount);
            for (int i = 0; i < second.DecisionCount; i++)
            {
                Assert.AreEqual(LocalAvoidanceCandidateSet.ZeroIndex, second[i].SelectedCandidateIndex);
            }
        }

        [Test]
        public void Solve_IdenticalFrameProducesIdenticalRawDecisionsAndStats()
        {
            LocalAvoidanceFrame frame = Frame(CrossingEnemyAgents());

            LocalAvoidanceSolveResult first = LocalAvoidanceSolver.Solve(
                frame,
                new LocalAvoidanceWorkspace());
            LocalAvoidanceSolveResult second = LocalAvoidanceSolver.Solve(
                frame,
                new LocalAvoidanceWorkspace());

            AssertRawResultsEqual(first, second);
            AssertStatsEqual(first.Stats, second.Stats);
        }

        [Test]
        public void Solve_FriendlySpacingIsSoftAndAllowsSlightCompression()
        {
            BattleScalar fifteenThirtySeconds = BattleScalar.FromInt(15)
                / BattleScalar.FromInt(32);
            LocalAvoidanceAgent[] agents =
            {
                Agent(
                    1,
                    9,
                    BattleVector2.Zero,
                    BattleVector2.Right * Half,
                    Half),
                Agent(
                    2,
                    9,
                    Position(1, 0),
                    BattleVector2.Right * fifteenThirtySeconds,
                    fifteenThirtySeconds)
            };

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(agents),
                new LocalAvoidanceWorkspace());

            AssertDecision(
                result[0],
                1,
                BattleVector2.Right * Half,
                0);
            AssertDecision(
                result[1],
                2,
                BattleVector2.Right * fifteenThirtySeconds,
                0);
            BattleScalar finalDistanceSquared = BattleVector2.SqrDistanceScalar(
                agents[0].Position + result[0].SelectedStep,
                agents[1].Position + result[1].SelectedStep);
            BattleScalar softRadius = (agents[0].Radius + agents[1].Radius)
                * BattleScalar.FromInt(LocalAvoidanceSettings.Default.SoftSpacingNumerator)
                / BattleScalar.FromInt(LocalAvoidanceSettings.Default.SoftSpacingDenominator);
            BattleScalar combinedRadius = agents[0].Radius + agents[1].Radius;
            Assert.Greater(finalDistanceSquared.RawValue, (softRadius * softRadius).RawValue);
            Assert.Less(finalDistanceSquared.RawValue, (combinedRadius * combinedRadius).RawValue);
            Assert.IsFalse(result[0].WasHardBlocked);
            Assert.IsFalse(result[1].WasHardBlocked);
        }

        [Test]
        public void Solve_ConflictResolutionUsesAtMostThreePassesAndReportsActualCount()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(1, 1, Position(-2, 0), BattleVector2.Right, BattleScalar.One),
                Agent(2, 2, Position(2, 0), Left(), BattleScalar.One),
                Agent(
                    3,
                    1,
                    Position(0, 2),
                    new BattleVector2(BattleScalar.Zero, -BattleScalar.One),
                    BattleScalar.One)
            };
            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(agents),
                new LocalAvoidanceWorkspace());

            AssertDecision(
                result[0],
                1,
                BattleVector2.FromRaw(3719549410L, -2147486034L),
                16);
            AssertDecision(
                result[1],
                2,
                BattleVector2.FromRaw(-3037000500L, 3037000500L),
                24);
            AssertDecision(
                result[2],
                3,
                BattleVector2.FromRaw(2277750375L, -2277750375L),
                21);
            Assert.AreEqual(1, result.Stats.ConflictResolutionPassCount);
            Assert.Greater(
                result.Stats.NeighborCheckCount,
                agents.Length * (agents.Length - 1));
            Assert.Greater(
                result.Stats.CandidateEvaluationCount,
                agents.Length * LocalAvoidanceCandidateSet.Count);
            Assert.LessOrEqual(
                result.Stats.ConflictResolutionPassCount,
                LocalAvoidanceSettings.Default.MaxConflictResolutionPasses);
            AssertEnemyPairHardSafe(agents[0], result[0], agents[1], result[1]);
            AssertEnemyPairHardSafe(agents[1], result[1], agents[2], result[2]);
        }

        [Test]
        public void Solve_AnchoredInitialOverlapDoesNotEnterConflictResolution()
        {
            LocalAvoidanceAgent[] agents =
            {
                AnchoredAgent(1, 1, BattleVector2.Zero),
                AnchoredAgent(2, 2, BattleVector2.Zero)
            };

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(agents),
                new LocalAvoidanceWorkspace());

            Assert.AreEqual(0, result.Stats.ConflictResolutionPassCount);
            Assert.AreEqual(0, result.Stats.NeighborCheckCount);
            Assert.AreEqual(0, result.Stats.CandidateEvaluationCount);
            AssertDecision(
                result[0],
                1,
                BattleVector2.Zero,
                LocalAvoidanceCandidateSet.ZeroIndex);
            AssertDecision(
                result[1],
                2,
                BattleVector2.Zero,
                LocalAvoidanceCandidateSet.ZeroIndex);
        }

        [Test]
        public void Workspace_StatsSaturateAndSolveResetsThem()
        {
            var workspace = new LocalAvoidanceWorkspace();
            workspace.AddNeighborChecks(int.MaxValue);
            workspace.AddNeighborChecks(1);
            workspace.AddCandidateEvaluations(int.MaxValue);
            workspace.AddCandidateEvaluations(1);

            Assert.AreEqual(int.MaxValue, workspace.NeighborCheckCount);
            Assert.AreEqual(int.MaxValue, workspace.CandidateEvaluationCount);

            LocalAvoidanceSolveResult result = LocalAvoidanceSolver.Solve(
                Frame(Array.Empty<LocalAvoidanceAgent>()),
                workspace);

            Assert.AreEqual(0, workspace.NeighborCheckCount);
            Assert.AreEqual(0, workspace.CandidateEvaluationCount);
            Assert.AreEqual(0, result.Stats.NeighborCheckCount);
            Assert.AreEqual(0, result.Stats.CandidateEvaluationCount);
        }

        private static LocalAvoidanceSolveResult Solve(LocalAvoidanceAgent agent)
        {
            return LocalAvoidanceSolver.Solve(
                Frame(new[] { agent }),
                new LocalAvoidanceWorkspace());
        }

        private static LocalAvoidanceFrame Frame(LocalAvoidanceAgent[] agents)
        {
            return new LocalAvoidanceFrame(
                agents,
                agents.Length,
                LocalAvoidanceSettings.Default);
        }

        private static LocalAvoidanceSettings SettingsWithFullSpeedTolerance(
            BattleScalar tolerance)
        {
            LocalAvoidanceSettings defaults = LocalAvoidanceSettings.Default;
            return new LocalAvoidanceSettings(
                defaults.PredictionTicks,
                defaults.MaxConflictResolutionPasses,
                defaults.CellSize,
                defaults.SoftSpacingNumerator,
                defaults.SoftSpacingDenominator,
                tolerance,
                defaults.FriendlyOverlapWeight,
                defaults.ProgressLossWeight,
                defaults.DirectionWeight,
                defaults.StepLossWeight,
                defaults.TurnWeight);
        }

        private static LocalAvoidanceAgent Agent(
            int agentId,
            BattleVector2 position,
            BattleVector2 preferredStep,
            BattleScalar maxStepDistance,
            LocalAvoidanceMobility mobility = LocalAvoidanceMobility.Moving)
        {
            return new LocalAvoidanceAgent(
                agentId,
                groupId: 1,
                position,
                BattleVector2.Right,
                preferredStep,
                Half,
                maxStepDistance,
                mobility);
        }

        private static LocalAvoidanceAgent Agent(
            int agentId,
            int groupId,
            BattleVector2 position,
            BattleVector2 preferredStep,
            BattleScalar maxStepDistance,
            BattleScalar radius = default(BattleScalar),
            LocalAvoidanceMobility mobility = LocalAvoidanceMobility.Moving)
        {
            BattleScalar resolvedRadius = radius > BattleScalar.Zero ? radius : Half;
            return new LocalAvoidanceAgent(
                agentId,
                groupId,
                position,
                preferredStep,
                preferredStep,
                resolvedRadius,
                maxStepDistance,
                mobility);
        }

        private static LocalAvoidanceAgent AnchoredAgent(
            int agentId,
            int groupId,
            BattleVector2 position)
        {
            return Agent(
                agentId,
                groupId,
                position,
                BattleVector2.Right,
                BattleScalar.Zero,
                Half,
                LocalAvoidanceMobility.Anchored);
        }

        private static LocalAvoidanceAgent[] CrossingEnemyAgents()
        {
            return new[]
            {
                Agent(10, 1, Position(-2, 0), BattleVector2.Right, BattleScalar.One),
                Agent(20, 2, Position(2, 0), Left(), BattleScalar.One),
                Agent(
                    30,
                    1,
                    Position(0, -2),
                    new BattleVector2(BattleScalar.Zero, BattleScalar.One),
                    BattleScalar.One),
                Agent(
                    40,
                    2,
                    Position(0, 2),
                    new BattleVector2(BattleScalar.Zero, -BattleScalar.One),
                    BattleScalar.One)
            };
        }

        private static BattleVector2 Position(int x, int y)
        {
            return new BattleVector2(BattleScalar.FromInt(x), BattleScalar.FromInt(y));
        }

        private static BattleVector2 Left()
        {
            return new BattleVector2(-BattleScalar.One, BattleScalar.Zero);
        }

        private static void AssertDecision(
            LocalAvoidanceDecision decision,
            int expectedAgentId,
            BattleVector2 expectedStep,
            int expectedCandidateIndex)
        {
            Assert.AreEqual(expectedAgentId, decision.AgentId);
            Assert.AreEqual(expectedStep.XRaw, decision.SelectedStep.XRaw);
            Assert.AreEqual(expectedStep.YRaw, decision.SelectedStep.YRaw);
            Assert.AreEqual(expectedCandidateIndex, decision.SelectedCandidateIndex);
            Assert.IsFalse(decision.WasHardBlocked);
        }

        private static void AssertEnemyPairHardSafe(
            LocalAvoidanceAgent firstAgent,
            LocalAvoidanceDecision firstDecision,
            LocalAvoidanceAgent secondAgent,
            LocalAvoidanceDecision secondDecision)
        {
            Assert.IsFalse(LocalAvoidanceGeometry.SweptCirclesOverlap(
                firstAgent.Position,
                firstDecision.SelectedStep,
                firstAgent.Radius,
                secondAgent.Position,
                secondDecision.SelectedStep,
                secondAgent.Radius,
                BattleScalar.One));

            BattleScalar combinedRadius = firstAgent.Radius + secondAgent.Radius;
            BattleScalar finalDistanceSquared = BattleVector2.SqrDistanceScalar(
                firstAgent.Position + firstDecision.SelectedStep,
                secondAgent.Position + secondDecision.SelectedStep);
            Assert.GreaterOrEqual(
                finalDistanceSquared.RawValue,
                (combinedRadius * combinedRadius).RawValue);
        }

        private static void AssertRawResultsEqual(
            LocalAvoidanceSolveResult first,
            LocalAvoidanceSolveResult second)
        {
            Assert.AreEqual(first.DecisionCount, second.DecisionCount);
            for (int i = 0; i < first.DecisionCount; i++)
            {
                Assert.AreEqual(first[i].AgentId, second[i].AgentId);
                Assert.AreEqual(first[i].SelectedStep.XRaw, second[i].SelectedStep.XRaw);
                Assert.AreEqual(first[i].SelectedStep.YRaw, second[i].SelectedStep.YRaw);
                Assert.AreEqual(
                    first[i].SelectedCandidateIndex,
                    second[i].SelectedCandidateIndex);
                Assert.AreEqual(first[i].WasHardBlocked, second[i].WasHardBlocked);
            }
        }

        private static void AssertStatsEqual(
            LocalAvoidanceSolveStats first,
            LocalAvoidanceSolveStats second)
        {
            Assert.AreEqual(first.GridCellCount, second.GridCellCount);
            Assert.AreEqual(first.NeighborCheckCount, second.NeighborCheckCount);
            Assert.AreEqual(first.CandidateEvaluationCount, second.CandidateEvaluationCount);
            Assert.AreEqual(
                first.ConflictResolutionPassCount,
                second.ConflictResolutionPassCount);
        }
    }
}
