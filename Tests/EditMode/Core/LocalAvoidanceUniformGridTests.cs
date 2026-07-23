using System;
using Combat.Core.Battle;
using Combat.Core.LocalAvoidance;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class LocalAvoidanceUniformGridTests
    {
        [TestCase(0L, 65536L, 0L)]
        [TestCase(1L, 65536L, 0L)]
        [TestCase(-1L, 65536L, -1L)]
        [TestCase(-65537L, 65536L, -2L)]
        public void FloorToCell_UsesMathematicalFloor(
            long coordinateRaw,
            long cellSizeRaw,
            long expected)
        {
            Assert.AreEqual(
                expected,
                LocalAvoidanceUniformGrid.FloorToCell(coordinateRaw, cellSizeRaw));
        }

        [Test]
        public void FloorToCell_RejectsNonPositiveCellSize()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                LocalAvoidanceUniformGrid.FloorToCell(0L, 0L));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                LocalAvoidanceUniformGrid.FloorToCell(0L, -1L));
        }

        [Test]
        public void Constructor_RejectsNonPositiveCellSize()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LocalAvoidanceUniformGrid(BattleScalar.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LocalAvoidanceUniformGrid(-BattleScalar.One));
        }

        [Test]
        public void DefaultGrid_RejectsEmptyBuildAndQueryAsUnconfigured()
        {
            LocalAvoidanceUniformGrid grid = default;
            var workspace = new LocalAvoidanceWorkspace();

            Assert.Throws<InvalidOperationException>(() =>
                grid.Build(Frame(Array.Empty<LocalAvoidanceAgent>()), workspace));
            Assert.Throws<InvalidOperationException>(() =>
                grid.Query(BattleVector2.Zero, BattleScalar.Zero, workspace));
        }

        [Test]
        public void Build_SortsAgentsWithinCellByAgentIdWithoutMutatingFrameBuffer()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(30, Position(0, 0)),
                Agent(10, Position(0, 0)),
                Agent(20, Position(0, 0))
            };
            LocalAvoidanceFrame frame = Frame(agents);
            var workspace = new LocalAvoidanceWorkspace();
            var grid = new LocalAvoidanceUniformGrid(BattleScalar.One);

            grid.Build(frame, workspace);

            Assert.AreEqual(3, workspace.GridEntryCount);
            Assert.AreEqual(1, workspace.CellRangeCount);
            Assert.AreEqual(10, workspace.GridEntries[0].AgentId);
            Assert.AreEqual(20, workspace.GridEntries[1].AgentId);
            Assert.AreEqual(30, workspace.GridEntries[2].AgentId);
            Assert.AreEqual(30, agents[0].AgentId);
            Assert.AreEqual(10, agents[1].AgentId);
            Assert.AreEqual(20, agents[2].AgentId);
        }

        [Test]
        public void Query_AcrossNegativeAndPositiveCellsReturnsAgentIdsInStableOrder()
        {
            LocalAvoidanceAgent[] agents =
            {
                Agent(30, Position(-1, 0)),
                Agent(10, Position(1, 0)),
                Agent(20, Position(0, 0))
            };
            var workspace = new LocalAvoidanceWorkspace();
            var grid = new LocalAvoidanceUniformGrid(BattleScalar.One);
            grid.Build(Frame(agents), workspace);

            int count = grid.Query(
                BattleVector2.Zero,
                BattleScalar.FromInt(2),
                workspace);

            Assert.AreEqual(3, count);
            AssertNeighbor(workspace, 0, 10);
            AssertNeighbor(workspace, 1, 20);
            AssertNeighbor(workspace, 2, 30);
        }

        [Test]
        public void Query_SortsLargeNeighborSetAndPreservesAgentIndexMapping()
        {
            const int agentCount = 128;
            var agents = new LocalAvoidanceAgent[agentCount];
            for (int i = 0; i < agentCount; i++)
            {
                int x = (i / 8) - 8;
                int y = (i % 8) - 4;
                agents[i] = Agent(agentCount - i, Position(x, y));
            }

            var workspace = new LocalAvoidanceWorkspace();
            var grid = new LocalAvoidanceUniformGrid(BattleScalar.One);
            grid.Build(Frame(agents), workspace);

            int count = grid.Query(
                BattleVector2.Zero,
                BattleScalar.FromInt(10),
                workspace);

            Assert.AreEqual(agentCount, count);
            for (int i = 0; i < agentCount; i++)
            {
                AssertNeighbor(workspace, i, i + 1);
            }
        }

        [Test]
        public void Query_AppliesExactCircularFilterAndIncludesBoundary()
        {
            BattleScalar threeQuarters = BattleScalar.FromInt(3) / BattleScalar.FromInt(4);
            LocalAvoidanceAgent[] agents =
            {
                Agent(3, new BattleVector2(threeQuarters, threeQuarters)),
                Agent(2, Position(1, 0)),
                Agent(1, new BattleVector2(
                    BattleScalar.One / BattleScalar.FromInt(2),
                    BattleScalar.One / BattleScalar.FromInt(2)))
            };
            var workspace = new LocalAvoidanceWorkspace();
            var grid = new LocalAvoidanceUniformGrid(BattleScalar.One);
            grid.Build(Frame(agents), workspace);

            int count = grid.Query(BattleVector2.Zero, BattleScalar.One, workspace);

            Assert.AreEqual(2, count);
            AssertNeighbor(workspace, 0, 1);
            AssertNeighbor(workspace, 1, 2);
        }

        [Test]
        public void Build_IndexesAnchoredAgentForMovingQuery()
        {
            LocalAvoidanceAgent moving = Agent(
                1,
                BattleVector2.Zero,
                LocalAvoidanceMobility.Moving);
            LocalAvoidanceAgent anchored = Agent(
                2,
                Position(1, 0),
                LocalAvoidanceMobility.Anchored);
            var workspace = new LocalAvoidanceWorkspace();
            var grid = new LocalAvoidanceUniformGrid(BattleScalar.One);
            grid.Build(Frame(new[] { anchored, moving }), workspace);

            int count = grid.Query(moving.Position, BattleScalar.One, workspace);

            Assert.AreEqual(2, count);
            AssertNeighbor(workspace, 0, moving.AgentId);
            AssertNeighbor(workspace, 1, anchored.AgentId);
        }

        [Test]
        public void BuildAndQuery_ValidateInputsAndSupportEmptyFrame()
        {
            var workspace = new LocalAvoidanceWorkspace();
            var grid = new LocalAvoidanceUniformGrid(BattleScalar.One);

            Assert.Throws<ArgumentNullException>(() =>
                grid.Build(Frame(Array.Empty<LocalAvoidanceAgent>()), null));
            Assert.Throws<ArgumentNullException>(() =>
                grid.Query(BattleVector2.Zero, BattleScalar.Zero, null));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                grid.Query(BattleVector2.Zero, -BattleScalar.One, workspace));

            grid.Build(Frame(Array.Empty<LocalAvoidanceAgent>()), workspace);

            Assert.AreEqual(0, workspace.AgentCount);
            Assert.AreEqual(0, workspace.GridEntryCount);
            Assert.AreEqual(0, workspace.CellRangeCount);
            Assert.AreEqual(0, grid.Query(BattleVector2.Zero, BattleScalar.Zero, workspace));
            Assert.AreEqual(0, workspace.NeighborCount);
            Assert.Throws<ArgumentOutOfRangeException>(() => workspace.GetNeighborAgentId(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => workspace.GetNeighborAgentIndex(0));
        }

        [Test]
        public void RebuildAndQuery_ClearCountsFromPreviousUse()
        {
            var workspace = new LocalAvoidanceWorkspace();
            var grid = new LocalAvoidanceUniformGrid(BattleScalar.One);
            grid.Build(Frame(new[]
            {
                Agent(3, Position(-1, 0)),
                Agent(1, Position(0, 0)),
                Agent(2, Position(1, 0))
            }), workspace);
            Assert.AreEqual(3, grid.Query(BattleVector2.Zero, BattleScalar.FromInt(2), workspace));

            grid.Build(Frame(new[] { Agent(7, Position(10, 10)) }), workspace);

            Assert.AreEqual(1, workspace.AgentCount);
            Assert.AreEqual(1, workspace.GridEntryCount);
            Assert.AreEqual(1, workspace.CellRangeCount);
            Assert.AreEqual(0, workspace.NeighborCount);
            Assert.AreEqual(0, grid.Query(BattleVector2.Zero, BattleScalar.One, workspace));
            Assert.AreEqual(0, workspace.NeighborCount);

            grid.Build(Frame(Array.Empty<LocalAvoidanceAgent>()), workspace);

            Assert.AreEqual(0, workspace.AgentCount);
            Assert.AreEqual(0, workspace.GridEntryCount);
            Assert.AreEqual(0, workspace.CellRangeCount);
            Assert.AreEqual(0, workspace.NeighborCount);
        }

        private static void AssertNeighbor(
            LocalAvoidanceWorkspace workspace,
            int neighborIndex,
            int expectedAgentId)
        {
            Assert.AreEqual(expectedAgentId, workspace.GetNeighborAgentId(neighborIndex));
            int agentIndex = workspace.GetNeighborAgentIndex(neighborIndex);
            Assert.AreEqual(expectedAgentId, workspace.SortedAgents[agentIndex].AgentId);
        }

        private static LocalAvoidanceFrame Frame(LocalAvoidanceAgent[] agents)
        {
            return new LocalAvoidanceFrame(
                agents,
                agents.Length,
                LocalAvoidanceSettings.Default);
        }

        private static LocalAvoidanceAgent Agent(
            int agentId,
            BattleVector2 position,
            LocalAvoidanceMobility mobility = LocalAvoidanceMobility.Anchored)
        {
            return new LocalAvoidanceAgent(
                agentId,
                groupId: 1,
                position,
                BattleVector2.Right,
                BattleVector2.Zero,
                BattleScalar.One,
                BattleScalar.Zero,
                mobility);
        }

        private static BattleVector2 Position(int x, int y)
        {
            return new BattleVector2(BattleScalar.FromInt(x), BattleScalar.FromInt(y));
        }
    }
}
