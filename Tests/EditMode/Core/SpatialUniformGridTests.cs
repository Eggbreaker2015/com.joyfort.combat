using System;
using Combat.Core.Battle;
using Combat.Core.Spatial;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class SpatialUniformGridTests
    {
        private static readonly SpatialCollisionFilter QueryFilter =
            new SpatialCollisionFilter(1u, 2u);
        private static readonly SpatialCollisionFilter ProxyFilter =
            new SpatialCollisionFilter(2u, 1u);

        [TestCase(0L, 10L, 0L)]
        [TestCase(9L, 10L, 0L)]
        [TestCase(10L, 10L, 1L)]
        [TestCase(-1L, 10L, -1L)]
        [TestCase(-10L, 10L, -1L)]
        [TestCase(-11L, 10L, -2L)]
        public void FloorToCell_UsesMathematicalFloor(
            long coordinateRaw,
            long cellSizeRaw,
            long expected)
        {
            Assert.That(
                DeterministicUniformGrid.FloorToCell(coordinateRaw, cellSizeRaw),
                Is.EqualTo(expected));
        }

        [Test]
        public void Build_RejectsDuplicateStableIds()
        {
            var grid = new DeterministicUniformGrid(BattleScalar.One);
            SpatialProxy[] proxies =
            {
                CircleProxy(1, 0, BattleVector2.Zero),
                CircleProxy(1, 1, BattleVector2.Right)
            };

            Assert.Throws<ArgumentException>(() => grid.Build(proxies, proxies.Length));
        }

        [Test]
        public void FailedRebuild_InvalidatesPreviousFrame()
        {
            var grid = new DeterministicUniformGrid(BattleScalar.One);
            var workspace = new SpatialQueryWorkspace();
            grid.Build(new[] { CircleProxy(1, 0, BattleVector2.Zero) }, 1);
            SpatialProxy[] duplicates =
            {
                CircleProxy(2, 0, BattleVector2.Zero),
                CircleProxy(2, 1, BattleVector2.Right)
            };

            Assert.Throws<ArgumentException>(() => grid.Build(duplicates, duplicates.Length));
            Assert.Throws<InvalidOperationException>(() => grid.OverlapCircle(
                BattleVector2.Zero,
                BattleScalar.One,
                QueryFilter,
                workspace));
        }

        [Test]
        public void SweepCircle_MatchesLinearOracleAndStableOrder()
        {
            SpatialProxy[] shuffled =
            {
                CircleProxy(4, 3, new BattleVector2(20f, 20f)),
                CircleProxy(3, 2, new BattleVector2(2f, 0f)),
                CircleProxy(1, 0, BattleVector2.Zero),
                CircleProxy(2, 1, new BattleVector2(2f, 0f))
            };
            var linear = new SpatialQueryWorkspace();
            var indexed = new SpatialQueryWorkspace();
            var grid = new DeterministicUniformGrid(BattleScalar.One);
            grid.Build(shuffled, shuffled.Length);

            int linearCount = SpatialQueries.SweepCircle(
                new BattleVector2(-2f, 0f),
                new BattleVector2(6f, 0f),
                BattleScalar.FromFloat(0.5f),
                QueryFilter,
                shuffled,
                shuffled.Length,
                linear);
            int indexedCount = grid.SweepCircle(
                new BattleVector2(-2f, 0f),
                new BattleVector2(6f, 0f),
                BattleScalar.FromFloat(0.5f),
                QueryFilter,
                indexed);

            AssertSameHits(linear, linearCount, indexed, indexedCount);
            Assert.That(indexed.CandidateCount, Is.LessThan(shuffled.Length));
        }

        [Test]
        public void OverlapAndAabbQueries_MatchLinearOracle()
        {
            SpatialProxy[] proxies =
            {
                CircleProxy(3, 2, new BattleVector2(6f, 0f)),
                new SpatialProxy(
                    new SpatialProxyId(2),
                    new BattleVector2(1.5f, 0f),
                    SpatialShape2D.Aabb(new BattleVector2(0.5f, 0.5f)),
                    ProxyFilter,
                    1),
                CircleProxy(1, 0, BattleVector2.Zero)
            };
            var grid = new DeterministicUniformGrid(BattleScalar.One);
            grid.Build(proxies, proxies.Length);
            var linear = new SpatialQueryWorkspace();
            var indexed = new SpatialQueryWorkspace();

            int linearOverlap = SpatialQueries.OverlapCircle(
                BattleVector2.Right,
                BattleScalar.One,
                QueryFilter,
                proxies,
                proxies.Length,
                linear);
            int indexedOverlap = grid.OverlapCircle(
                BattleVector2.Right,
                BattleScalar.One,
                QueryFilter,
                indexed);
            AssertSameHits(linear, linearOverlap, indexed, indexedOverlap);

            SpatialAabb bounds = new SpatialAabb(
                BattleVector2.Zero,
                new BattleVector2(BattleScalar.One, BattleScalar.One));
            int linearAabb = SpatialQueries.QueryAabb(
                bounds,
                QueryFilter,
                proxies,
                proxies.Length,
                linear);
            int indexedAabb = grid.QueryAabb(bounds, QueryFilter, indexed);
            AssertSameHits(linear, linearAabb, indexed, indexedAabb);
        }

        [Test]
        public void RebuildAndQuery_AreIndependentOfProxyInputOrder()
        {
            SpatialProxy first = CircleProxy(1, 0, BattleVector2.Zero);
            SpatialProxy second = CircleProxy(2, 1, new BattleVector2(2f, 0f));
            var grid = new DeterministicUniformGrid(BattleScalar.One);
            var forward = new SpatialQueryWorkspace();
            var reverse = new SpatialQueryWorkspace();

            grid.Build(new[] { first, second }, 2);
            int forwardCount = grid.SweepCircle(
                new BattleVector2(-2f, 0f),
                new BattleVector2(6f, 0f),
                BattleScalar.Zero,
                QueryFilter,
                forward);
            grid.Build(new[] { second, first }, 2);
            int reverseCount = grid.SweepCircle(
                new BattleVector2(-2f, 0f),
                new BattleVector2(6f, 0f),
                BattleScalar.Zero,
                QueryFilter,
                reverse);

            AssertSameHits(forward, forwardCount, reverse, reverseCount);
        }

        [Test]
        public void Query_AfterWarmupDoesNotAllocate()
        {
            SpatialProxy[] proxies =
            {
                CircleProxy(1, 0, BattleVector2.Zero),
                CircleProxy(2, 1, new BattleVector2(2f, 0f))
            };
            var grid = new DeterministicUniformGrid(BattleScalar.One);
            var workspace = new SpatialQueryWorkspace();
            grid.Build(proxies, proxies.Length);
            grid.SweepCircle(
                new BattleVector2(-2f, 0f),
                new BattleVector2(6f, 0f),
                BattleScalar.Zero,
                QueryFilter,
                workspace);

            long before = GC.GetAllocatedBytesForCurrentThread();
            grid.SweepCircle(
                new BattleVector2(-2f, 0f),
                new BattleVector2(6f, 0f),
                BattleScalar.Zero,
                QueryFilter,
                workspace);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
        }

        private static void AssertSameHits(
            SpatialQueryWorkspace expected,
            int expectedCount,
            SpatialQueryWorkspace actual,
            int actualCount)
        {
            Assert.That(actualCount, Is.EqualTo(expectedCount));
            for (var i = 0; i < expectedCount; i++)
            {
                SpatialHit expectedHit = expected.GetHit(i);
                SpatialHit actualHit = actual.GetHit(i);
                Assert.That(actualHit.ProxyId, Is.EqualTo(expectedHit.ProxyId));
                Assert.That(actualHit.Fraction.RawValue, Is.EqualTo(expectedHit.Fraction.RawValue));
                Assert.That(actualHit.Position, Is.EqualTo(expectedHit.Position));
            }
        }

        private static SpatialProxy CircleProxy(
            int id,
            int payloadIndex,
            BattleVector2 position)
        {
            return new SpatialProxy(
                new SpatialProxyId(id),
                position,
                SpatialShape2D.Circle(BattleScalar.FromFloat(0.5f)),
                ProxyFilter,
                payloadIndex);
        }
    }
}
