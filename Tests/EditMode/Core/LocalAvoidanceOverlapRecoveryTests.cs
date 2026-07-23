using System;
using Combat.Core.Battle;
using Combat.Core.LocalAvoidance;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class LocalAvoidanceOverlapRecoveryTests
    {
        private static readonly BattleScalar Radius =
            BattleScalar.One / BattleScalar.FromInt(4);

        [Test]
        public void Resolve_DifferentGroupAnchoredPairRecoversIllegalOverlap()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(1, 1, BattleVector2.Zero),
                Agent(2, 2, BattleVector2.Zero)
            };

            LocalAvoidanceRecoveryResult result = Resolve(agents);

            Assert.GreaterOrEqual(
                BattleVector2.DistanceScalar(
                    result[0].Position,
                    result[1].Position).RawValue,
                (agents[0].Radius + agents[1].Radius).RawValue);
            Assert.AreEqual(BattleVector2.Zero, result[0].Position);
            Assert.IsFalse(result[0].WasMoved);
            Assert.IsTrue(result[1].WasMoved);
        }

        [Test]
        public void Resolve_ExactOverlapSeparatesHigherIdToStableRightRawPosition()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(20, 2, BattleVector2.Zero),
                Agent(10, 1, BattleVector2.Zero)
            };

            LocalAvoidanceRecoveryResult first = Resolve(
                agents,
                new LocalAvoidanceWorkspace());
            LocalAvoidanceRecoveryResult second = Resolve(
                agents,
                new LocalAvoidanceWorkspace());

            Assert.Greater(first[1].Position.XRaw, 0L);
            Assert.AreEqual(0L, first[1].Position.YRaw);
            Assert.AreEqual(first[0].Position.XRaw, second[0].Position.XRaw);
            Assert.AreEqual(first[0].Position.YRaw, second[0].Position.YRaw);
            Assert.AreEqual(first[1].Position.XRaw, second[1].Position.XRaw);
            Assert.AreEqual(first[1].Position.YRaw, second[1].Position.YRaw);
            Assert.AreEqual(first[0].WasMoved, second[0].WasMoved);
            Assert.AreEqual(first[1].WasMoved, second[1].WasMoved);
        }

        [Test]
        public void Resolve_InterleavedEnemyOverlapsAreLegalWithinFourPasses()
        {
            BattleScalar eighth = BattleScalar.One / BattleScalar.FromInt(8);
            LocalAvoidanceAgent[] agents =
            {
                Agent(4, 2, new BattleVector2(eighth, BattleScalar.Zero)),
                Agent(1, 1, BattleVector2.Zero),
                Agent(3, 1, new BattleVector2(eighth, BattleScalar.Zero)),
                Agent(2, 2, BattleVector2.Zero)
            };

            LocalAvoidanceRecoveryResult result = Resolve(agents);

            Assert.LessOrEqual(result.PassCount, 4);
            AssertNoEnemyOverlap(result, agents);
        }

        [Test]
        public void Resolve_SameGroupOverlapDoesNotForceSeparation()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(1, 7, BattleVector2.Zero),
                Agent(2, 7, BattleVector2.Zero)
            };

            LocalAvoidanceRecoveryResult result = Resolve(agents);

            Assert.AreEqual(BattleVector2.Zero, result[0].Position);
            Assert.AreEqual(BattleVector2.Zero, result[1].Position);
            Assert.IsFalse(result[0].WasMoved);
            Assert.IsFalse(result[1].WasMoved);
        }

        [Test]
        public void Resolve_LowerMovingHigherAnchoredStillUsesLowerIdAsStableAnchor()
        {
            AssertMixedMobilityUsesLowerIdAnchor(
                LocalAvoidanceMobility.Moving,
                LocalAvoidanceMobility.Anchored);
        }

        [Test]
        public void Resolve_LowerAnchoredHigherMovingStillUsesLowerIdAsStableAnchor()
        {
            AssertMixedMobilityUsesLowerIdAnchor(
                LocalAvoidanceMobility.Anchored,
                LocalAvoidanceMobility.Moving);
        }

        private static void AssertMixedMobilityUsesLowerIdAnchor(
            LocalAvoidanceMobility lowerMobility,
            LocalAvoidanceMobility higherMobility)
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(9, 2, BattleVector2.Zero, higherMobility),
                Agent(3, 1, BattleVector2.Zero, lowerMobility)
            };

            LocalAvoidanceRecoveryResult result = Resolve(agents);

            Assert.AreEqual(3, result[0].AgentId);
            Assert.AreEqual(BattleVector2.Zero, result[0].Position);
            Assert.IsFalse(result[0].WasMoved);
            Assert.AreEqual(9, result[1].AgentId);
            Assert.IsTrue(result[1].WasMoved);
        }

        [Test]
        public void Resolve_PermutedInputProducesIdenticalRawResultsByAgentId()
        {
            LocalAvoidanceAgent[] agents = InterleavedAgents();
            LocalAvoidanceAgent[] permuted =
            {
                agents[2], agents[0], agents[3], agents[1]
            };

            LocalAvoidanceRecoveryResult first = Resolve(
                agents,
                new LocalAvoidanceWorkspace());
            LocalAvoidanceRecoveryResult second = Resolve(
                permuted,
                new LocalAvoidanceWorkspace());

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].AgentId, second[i].AgentId);
                Assert.AreEqual(first[i].Position.XRaw, second[i].Position.XRaw);
                Assert.AreEqual(first[i].Position.YRaw, second[i].Position.YRaw);
                Assert.AreEqual(first[i].WasMoved, second[i].WasMoved);
            }
        }

        [Test]
        public void Resolve_ReturnsAgentIdOrderAndRejectsDuplicateIds()
        {
            LocalAvoidanceRecoveryResult result = Resolve(new[]
            {
                Agent(30, 1, Position(4, 0)),
                Agent(10, 1, Position(0, 0)),
                Agent(20, 1, Position(2, 0))
            });

            Assert.AreEqual(10, result[0].AgentId);
            Assert.AreEqual(20, result[1].AgentId);
            Assert.AreEqual(30, result[2].AgentId);
            Assert.Throws<ArgumentException>(() => Resolve(new[]
            {
                Agent(1, 1, BattleVector2.Zero),
                Agent(1, 2, BattleVector2.Zero)
            }));
        }

        [Test]
        public void Contracts_RejectInvalidAgentResultAndResolveArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LocalAvoidanceRecoveredAgent(
                    0,
                    BattleVector2.Zero,
                    wasMoved: false));
            Assert.Throws<ArgumentNullException>(() =>
                new LocalAvoidanceRecoveryResult(null, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LocalAvoidanceRecoveryResult(
                    Array.Empty<LocalAvoidanceRecoveredAgent>(),
                    -1,
                    0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LocalAvoidanceRecoveryResult(
                    Array.Empty<LocalAvoidanceRecoveredAgent>(),
                    1,
                    0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LocalAvoidanceRecoveryResult(
                    Array.Empty<LocalAvoidanceRecoveredAgent>(),
                    0,
                    -1));

            var validResult = new LocalAvoidanceRecoveryResult(
                new[]
                {
                    new LocalAvoidanceRecoveredAgent(
                        1,
                        BattleVector2.Zero,
                        wasMoved: false)
                },
                1,
                0);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                LocalAvoidanceRecoveredAgent ignored = validResult[-1];
            });
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                LocalAvoidanceRecoveredAgent ignored = validResult[1];
            });

            LocalAvoidanceFrame empty = Frame(Array.Empty<LocalAvoidanceAgent>());
            Assert.Throws<ArgumentNullException>(() =>
                LocalAvoidanceOverlapRecovery.Resolve(empty, 4, null));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                LocalAvoidanceOverlapRecovery.Resolve(
                    empty,
                    -1,
                    new LocalAvoidanceWorkspace()));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LocalAvoidanceFrame(
                    Array.Empty<LocalAvoidanceAgent>(),
                    1,
                    LocalAvoidanceSettings.Default));
        }

        [Test]
        public void Resolve_ZeroPassesSortsWithoutMoving()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(2, 2, BattleVector2.Zero),
                Agent(1, 1, BattleVector2.Zero)
            };

            LocalAvoidanceRecoveryResult result =
                LocalAvoidanceOverlapRecovery.Resolve(
                    Frame(agents),
                    0,
                    new LocalAvoidanceWorkspace());

            Assert.AreEqual(0, result.PassCount);
            Assert.AreEqual(1, result[0].AgentId);
            Assert.AreEqual(BattleVector2.Zero, result[0].Position);
            Assert.IsFalse(result[0].WasMoved);
            Assert.AreEqual(2, result[1].AgentId);
            Assert.AreEqual(BattleVector2.Zero, result[1].Position);
            Assert.IsFalse(result[1].WasMoved);
        }

        [Test]
        public void Resolve_HardCapsRecoveryAtFourPasses()
        {
            LocalAvoidanceAgent[] agents = new LocalAvoidanceAgent[8];
            for (int i = 0; i < agents.Length; i++)
            {
                agents[i] = Agent(
                    i + 1,
                    (i & 1) + 1,
                    BattleVector2.Zero);
            }

            LocalAvoidanceRecoveryResult result =
                LocalAvoidanceOverlapRecovery.Resolve(
                    Frame(agents),
                    int.MaxValue,
                    new LocalAvoidanceWorkspace());

            Assert.LessOrEqual(result.PassCount, 4);
        }

        [Test]
        public void Resolve_ExactSharedTangentDoesNotOscillateAcrossAnchors()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(1, 1, BattleVector2.Zero),
                Agent(2, 1, Position(-1, 0)),
                Agent(3, 2, Position(-1, 0))
            };

            LocalAvoidanceRecoveryResult result = Resolve(agents);

            Assert.AreEqual(
                -(Radius + Radius).RawValue,
                result[2].Position.XRaw);
            Assert.AreEqual(0L, result[2].Position.YRaw);
            AssertNoEnemyOverlap(result, agents);
        }

        [Test]
        public void Resolve_TracksAgentsMovedAcrossTheirPassStartGridCells()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(1, 1, new BattleVector2(-Radius - Radius, BattleScalar.Zero)),
                Agent(2, 1, Position(-2, 0)),
                Agent(3, 1, new BattleVector2(-BattleScalar.One - Radius, BattleScalar.Zero)),
                Agent(4, 1, new BattleVector2(-BattleScalar.One - Radius - Radius, BattleScalar.Zero)),
                Agent(5, 1, new BattleVector2(-BattleScalar.One - Radius - Radius - Radius, BattleScalar.Zero)),
                Agent(6, 2, Position(-2, 0))
            };

            LocalAvoidanceRecoveryResult result = Resolve(agents);

            AssertNoEnemyOverlap(result, agents);
        }

        [Test]
        public void Resolve_DoesNotOscillateBetweenTwoLowerIdAnchors()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(1, 1, new BattleVector2(-Radius - Radius - Radius, BattleScalar.Zero)),
                Agent(2, 1, new BattleVector2(-BattleScalar.One - Radius - Radius, BattleScalar.Zero)),
                Agent(3, 2, new BattleVector2(-BattleScalar.One - Radius - Radius, BattleScalar.Zero))
            };

            LocalAvoidanceRecoveryResult result = Resolve(agents);

            AssertNoEnemyOverlap(result, agents);
        }

        [Test]
        public void Resolve_EscapesDenseLowerIdAnchorChain()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(1, 1, new BattleVector2(-BattleScalar.One - Radius, BattleScalar.Zero)),
                Agent(2, 1, new BattleVector2(-BattleScalar.One - Radius - Radius - Radius, BattleScalar.Zero)),
                Agent(3, 1, new BattleVector2(-BattleScalar.FromInt(2) - Radius - Radius, BattleScalar.Zero)),
                Agent(4, 1, BattleVector2.FromRaw(-BattleScalar.FromInt(3).RawValue, 0L)),
                Agent(5, 2, new BattleVector2(-BattleScalar.FromInt(2) - Radius - Radius - Radius, BattleScalar.Zero))
            };

            LocalAvoidanceRecoveryResult result = Resolve(agents);

            AssertNoEnemyOverlap(result, agents);
        }

        [Test]
        public void Resolve_TangentEnemyPairRemainsUnchanged()
        {
            BattleScalar combinedRadius = Radius + Radius;
            LocalAvoidanceAgent[] agents =
            {
                Agent(1, 1, BattleVector2.Zero),
                Agent(
                    2,
                    2,
                    new BattleVector2(combinedRadius, BattleScalar.Zero))
            };

            LocalAvoidanceRecoveryResult result = Resolve(agents);

            Assert.AreEqual(agents[0].Position, result[0].Position);
            Assert.AreEqual(agents[1].Position, result[1].Position);
            Assert.IsFalse(result[0].WasMoved);
            Assert.IsFalse(result[1].WasMoved);
        }

        [Test]
        public void Resolve_DiagonalDifferentRadiusPairRoundsToLegalDistance()
        {
            BattleScalar largerRadius =
                BattleScalar.FromInt(3) / BattleScalar.FromInt(4);
            LocalAvoidanceAgent[] agents =
            {
                Agent(1, 1, BattleVector2.Zero),
                new LocalAvoidanceAgent(
                    2,
                    2,
                    new BattleVector2(Radius, Radius),
                    BattleVector2.Right,
                    BattleVector2.Zero,
                    largerRadius,
                    BattleScalar.Zero,
                    LocalAvoidanceMobility.Anchored)
            };

            LocalAvoidanceRecoveryResult result = Resolve(agents);
            BattleScalar combinedRadius = Radius + largerRadius;

            Assert.GreaterOrEqual(
                BattleVector2.SqrDistanceScalar(
                    result[0].Position,
                    result[1].Position).RawValue,
                (combinedRadius * combinedRadius).RawValue);
            Assert.IsFalse(result[0].WasMoved);
            Assert.IsTrue(result[1].WasMoved);
        }

        [Test]
        public void Resolve_DoesNotChangeSolverStatsOrCreateCandidateScratch()
        {
            var workspace = new LocalAvoidanceWorkspace();
            workspace.AddNeighborChecks(7);
            workspace.AddCandidateEvaluations(11);

            Resolve(new[]
            {
                Agent(1, 1, BattleVector2.Zero),
                Agent(2, 2, BattleVector2.Zero)
            }, workspace);

            Assert.AreEqual(7, workspace.NeighborCheckCount);
            Assert.AreEqual(11, workspace.CandidateEvaluationCount);
            Assert.AreEqual(0, workspace.CandidateSteps.Length);
            Assert.AreEqual(0, workspace.CandidateCosts.Length);
            Assert.AreEqual(0, workspace.ConflictPairs.Length);
        }

        [Test]
        public void Resolve_EmptyFrameReturnsEmptyResult()
        {
            LocalAvoidanceRecoveryResult result = Resolve(
                Array.Empty<LocalAvoidanceAgent>());

            Assert.AreEqual(0, result.Count);
            Assert.AreEqual(0, result.PassCount);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                LocalAvoidanceRecoveredAgent ignored = result[0];
            });
        }

        [Test]
        public void Resolve_SameWorkspaceOverwritesPriorResultView()
        {
            var workspace = new LocalAvoidanceWorkspace();
            LocalAvoidanceRecoveryResult first = Resolve(
                new[]
                {
                    Agent(1, 1, BattleVector2.Zero),
                    Agent(2, 2, BattleVector2.Zero)
                },
                workspace);
            long recoveredXRaw = first[1].Position.XRaw;

            LocalAvoidanceRecoveryResult second = Resolve(
                new[]
                {
                    Agent(1, 1, Position(10, 0)),
                    Agent(2, 2, Position(12, 0))
                },
                workspace);

            Assert.AreNotEqual(recoveredXRaw, first[1].Position.XRaw);
            Assert.AreEqual(second[0].Position.XRaw, first[0].Position.XRaw);
            Assert.AreEqual(second[1].Position.XRaw, first[1].Position.XRaw);
            Assert.AreEqual(second[0].WasMoved, first[0].WasMoved);
            Assert.AreEqual(second[1].WasMoved, first[1].WasMoved);
        }

        [Test]
        public void Resolve_DifferentWorkspacesKeepResultViewsIsolated()
        {
            var firstWorkspace = new LocalAvoidanceWorkspace();
            LocalAvoidanceRecoveryResult first = Resolve(
                new[]
                {
                    Agent(1, 1, BattleVector2.Zero),
                    Agent(2, 2, BattleVector2.Zero)
                },
                firstWorkspace);
            long firstHigherXRaw = first[1].Position.XRaw;

            Resolve(
                new[]
                {
                    Agent(1, 1, Position(20, 0)),
                    Agent(2, 2, Position(22, 0))
                },
                new LocalAvoidanceWorkspace());

            Assert.AreEqual(firstHigherXRaw, first[1].Position.XRaw);
            Assert.IsTrue(first[1].WasMoved);
        }

        [Test]
        public void Resolve_AfterMatchingWarmupDoesNotAllocatePerCall()
        {
            LocalAvoidanceAgent[] agents = InterleavedAgents();
            LocalAvoidanceFrame frame = Frame(agents);
            var workspace = new LocalAvoidanceWorkspace();
            LocalAvoidanceOverlapRecovery.Resolve(frame, 4, workspace);

            long before = GC.GetAllocatedBytesForCurrentThread();
            LocalAvoidanceRecoveryResult result =
                LocalAvoidanceOverlapRecovery.Resolve(frame, 4, workspace);
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(agents.Length, result.Count);
            Assert.AreEqual(0L, allocatedBytes);
        }

        private static LocalAvoidanceRecoveryResult Resolve(
            LocalAvoidanceAgent[] agents,
            LocalAvoidanceWorkspace workspace = null)
        {
            return LocalAvoidanceOverlapRecovery.Resolve(
                Frame(agents),
                4,
                workspace ?? new LocalAvoidanceWorkspace());
        }

        private static LocalAvoidanceFrame Frame(LocalAvoidanceAgent[] agents)
        {
            return new LocalAvoidanceFrame(
                agents,
                agents.Length,
                LocalAvoidanceSettings.Default);
        }

        private static LocalAvoidanceAgent Agent(
            int id,
            int group,
            BattleVector2 position,
            LocalAvoidanceMobility mobility = LocalAvoidanceMobility.Anchored)
        {
            return new LocalAvoidanceAgent(
                id,
                group,
                position,
                BattleVector2.Right,
                BattleVector2.Zero,
                Radius,
                BattleScalar.Zero,
                mobility);
        }

        private static BattleVector2 Position(int x, int y)
        {
            return new BattleVector2(
                BattleScalar.FromInt(x),
                BattleScalar.FromInt(y));
        }

        private static LocalAvoidanceAgent[] InterleavedAgents()
        {
            BattleScalar eighth = BattleScalar.One / BattleScalar.FromInt(8);
            return new[]
            {
                Agent(4, 2, new BattleVector2(eighth, BattleScalar.Zero)),
                Agent(1, 1, BattleVector2.Zero),
                Agent(3, 1, new BattleVector2(eighth, BattleScalar.Zero)),
                Agent(2, 2, BattleVector2.Zero)
            };
        }

        private static void AssertNoEnemyOverlap(
            LocalAvoidanceRecoveryResult result,
            LocalAvoidanceAgent[] input)
        {
            for (int first = 0; first < result.Count; first++)
            {
                LocalAvoidanceAgent firstAgent = FindAgent(input, result[first].AgentId);
                for (int second = first + 1; second < result.Count; second++)
                {
                    LocalAvoidanceAgent secondAgent = FindAgent(
                        input,
                        result[second].AgentId);
                    if (firstAgent.GroupId == secondAgent.GroupId)
                    {
                        continue;
                    }

                    BattleScalar combinedRadius = firstAgent.Radius + secondAgent.Radius;
                    Assert.GreaterOrEqual(
                        BattleVector2.SqrDistanceScalar(
                            result[first].Position,
                            result[second].Position).RawValue,
                        (combinedRadius * combinedRadius).RawValue,
                        $"Enemy agents {firstAgent.AgentId} and {secondAgent.AgentId} overlap.");
                }
            }
        }

        private static LocalAvoidanceAgent FindAgent(
            LocalAvoidanceAgent[] agents,
            int agentId)
        {
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].AgentId == agentId)
                {
                    return agents[i];
                }
            }

            Assert.Fail($"Missing agent {agentId}.");
            return default;
        }
    }
}
