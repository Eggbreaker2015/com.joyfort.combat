using System;
using Combat.Core.Battle;
using Combat.Core.LocalAvoidance;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class LocalAvoidanceContractsTests
    {
        [Test]
        public void Agent_RejectsNegativeRadiusAndAllowsPointAgents()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LocalAvoidanceAgent(
                1,
                1,
                BattleVector2.Zero,
                BattleVector2.Right,
                BattleVector2.Zero,
                -BattleScalar.One,
                BattleScalar.One,
                LocalAvoidanceMobility.Moving));

            var pointAgent = new LocalAvoidanceAgent(
                1,
                1,
                BattleVector2.Zero,
                BattleVector2.Right,
                BattleVector2.Zero,
                BattleScalar.Zero,
                BattleScalar.Zero,
                LocalAvoidanceMobility.Anchored);

            Assert.AreEqual(BattleScalar.Zero, pointAgent.Radius);
        }

        [Test]
        public void Agent_RejectsNegativeMaxStepDistance()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LocalAvoidanceAgent(
                1,
                1,
                BattleVector2.Zero,
                BattleVector2.Right,
                BattleVector2.Zero,
                BattleScalar.One,
                -BattleScalar.One,
                LocalAvoidanceMobility.Moving));
        }

        [Test]
        public void Agent_NormalizesHeadingAndUsesRightForNearZeroHeading()
        {
            var heading = new BattleVector2(BattleScalar.FromInt(3), BattleScalar.FromInt(4));
            LocalAvoidanceAgent normalized = Agent(
                1,
                heading: heading);
            LocalAvoidanceAgent defaulted = Agent(
                2,
                heading: new BattleVector2(BattleScalar.Epsilon, BattleScalar.Zero));

            Assert.AreEqual(heading.Normalized, normalized.Heading);
            Assert.AreEqual(BattleVector2.Right, defaulted.Heading);
        }

        [Test]
        public void Agent_RecordsWhetherPreferredStepEndsAtMovementGoal()
        {
            LocalAvoidanceAgent continuing = Agent(1);
            var stopping = new LocalAvoidanceAgent(
                2,
                1,
                BattleVector2.Zero,
                BattleVector2.Right,
                BattleVector2.Right,
                BattleScalar.One,
                BattleScalar.One,
                LocalAvoidanceMobility.Moving,
                stopsAtPreferredStep: true);

            Assert.IsFalse(continuing.StopsAtPreferredStep);
            Assert.IsTrue(stopping.StopsAtPreferredStep);
        }

        [Test]
        public void Settings_RejectsInvalidLimitsAndWeights()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Settings(predictionTicks: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Settings(maxConflictResolutionPasses: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Settings(cellSize: BattleScalar.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => Settings(softSpacingNumerator: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Settings(softSpacingDenominator: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Settings(
                fullSpeedFriendlyOverlapTolerance: -BattleScalar.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => Settings(friendlyOverlapWeight: -BattleScalar.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => Settings(progressLossWeight: -BattleScalar.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => Settings(directionWeight: -BattleScalar.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => Settings(stepLossWeight: -BattleScalar.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => Settings(turnWeight: -BattleScalar.One));
        }

        [Test]
        public void Settings_DefaultUsesDeterministicContractValues()
        {
            LocalAvoidanceSettings settings = LocalAvoidanceSettings.Default;

            Assert.AreEqual(4, settings.PredictionTicks);
            Assert.AreEqual(3, settings.MaxConflictResolutionPasses);
            Assert.AreEqual(BattleScalar.FromInt(2), settings.CellSize);
            Assert.AreEqual(3, settings.SoftSpacingNumerator);
            Assert.AreEqual(4, settings.SoftSpacingDenominator);
            Assert.AreEqual(
                BattleScalar.One / BattleScalar.FromInt(8),
                settings.FullSpeedFriendlyOverlapTolerance);
            Assert.AreEqual(BattleScalar.FromInt(16), settings.FriendlyOverlapWeight);
            Assert.AreEqual(BattleScalar.FromInt(8), settings.ProgressLossWeight);
            Assert.AreEqual(BattleScalar.FromInt(4), settings.DirectionWeight);
            Assert.AreEqual(BattleScalar.FromInt(2), settings.StepLossWeight);
            Assert.AreEqual(BattleScalar.One, settings.TurnWeight);
        }

        [Test]
        public void Frame_RejectsCountOutsideBuffer()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LocalAvoidanceFrame(
                Array.Empty<LocalAvoidanceAgent>(),
                1,
                LocalAvoidanceSettings.Default));
        }

        [Test]
        public void Frame_IsNonOwningViewOfValidBufferRange()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(1),
                Agent(2)
            };
            var frame = new LocalAvoidanceFrame(agents, 1, LocalAvoidanceSettings.Default);

            agents[0] = Agent(3);

            Assert.AreEqual(1, frame.AgentCount);
            Assert.AreEqual(3, frame.GetAgent(0).AgentId);
            Assert.Throws<ArgumentOutOfRangeException>(() => frame.GetAgent(1));
        }

        [Test]
        public void SolveResult_ExposesAnchoredZeroStepDecision()
        {
            LocalAvoidanceAgent anchored = Agent(7, mobility: LocalAvoidanceMobility.Anchored);
            LocalAvoidanceDecision[] decisions =
            {
                new LocalAvoidanceDecision(
                    anchored.AgentId,
                    BattleVector2.Zero,
                    selectedCandidateIndex: 0,
                    wasHardBlocked: false)
            };
            var stats = new LocalAvoidanceSolveStats(1, 0, 0, 0);

            var result = new LocalAvoidanceSolveResult(decisions, 1, stats);

            Assert.AreEqual(1, result.DecisionCount);
            Assert.AreEqual(anchored.AgentId, result[0].AgentId);
            Assert.AreEqual(BattleVector2.Zero, result[0].SelectedStep);
            Assert.AreEqual(0, result[0].SelectedCandidateIndex);
            Assert.IsFalse(result[0].WasHardBlocked);
            Assert.AreEqual(1, result.Stats.GridCellCount);
        }

        [Test]
        public void Workspace_EnsuresAgentAndCandidateBuffersWithIndependentDoubledCapacity()
        {
            var workspace = new LocalAvoidanceWorkspace();

            workspace.EnsureAgentCapacity(17);
            workspace.EnsureCandidateCapacity(57);

            Assert.AreEqual(32, workspace.AgentCapacity);
            Assert.AreEqual(32, workspace.SortedAgents.Length);
            Assert.AreEqual(32, workspace.Decisions.Length);
            Assert.AreEqual(64, workspace.CandidateSteps.Length);
            Assert.AreEqual(64, workspace.CandidateCosts.Length);
            Assert.AreEqual(32, workspace.SelectedCandidateIndices.Length);
            Assert.AreEqual(32, workspace.NeighborIndices.Length);
            Assert.AreEqual(32, workspace.NeighborAgentIds.Length);
            Assert.AreEqual(32, workspace.GridEntries.Length);
            Assert.AreEqual(32, workspace.CellRanges.Length);
            Assert.AreEqual(32, workspace.RecoveredPositions.Length);
            Assert.Throws<ArgumentOutOfRangeException>(() => workspace.EnsureAgentCapacity(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => workspace.EnsureCandidateCapacity(-1));
        }

        private static LocalAvoidanceAgent Agent(
            int id,
            int group = 1,
            BattleVector2 position = default,
            BattleVector2 heading = default,
            BattleVector2 preferredStep = default,
            LocalAvoidanceMobility mobility = LocalAvoidanceMobility.Moving)
        {
            if (heading.Equals(default(BattleVector2)))
            {
                heading = BattleVector2.Right;
            }

            return new LocalAvoidanceAgent(
                id,
                group,
                position,
                heading,
                preferredStep,
                BattleScalar.FromFloat(0.25f),
                preferredStep.MagnitudeScalar,
                mobility);
        }

        private static LocalAvoidanceSettings Settings(
            int predictionTicks = 4,
            int maxConflictResolutionPasses = 3,
            BattleScalar? cellSize = null,
            int softSpacingNumerator = 3,
            int softSpacingDenominator = 4,
            BattleScalar fullSpeedFriendlyOverlapTolerance = default,
            BattleScalar friendlyOverlapWeight = default,
            BattleScalar progressLossWeight = default,
            BattleScalar directionWeight = default,
            BattleScalar stepLossWeight = default,
            BattleScalar turnWeight = default)
        {
            return new LocalAvoidanceSettings(
                predictionTicks,
                maxConflictResolutionPasses,
                cellSize ?? BattleScalar.One,
                softSpacingNumerator,
                softSpacingDenominator,
                fullSpeedFriendlyOverlapTolerance,
                friendlyOverlapWeight,
                progressLossWeight,
                directionWeight,
                stepLossWeight,
                turnWeight);
        }
    }
}
