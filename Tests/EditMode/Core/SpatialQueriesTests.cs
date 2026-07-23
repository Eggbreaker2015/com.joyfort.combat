using System;
using Combat.Core.Battle;
using Combat.Core.Spatial;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class SpatialQueriesTests
    {
        private static readonly SpatialCollisionFilter ProjectileFilter =
            new SpatialCollisionFilter(1u << 0, 1u << 1);
        private static readonly SpatialCollisionFilter TargetFilter =
            new SpatialCollisionFilter(1u << 1, 1u << 0);

        [Test]
        public void SweepCircle_OrdersByFractionThenStableProxyId()
        {
            SpatialProxy[] proxies =
            {
                CircleProxy(3, 1, new BattleVector2(2f, 0f)),
                CircleProxy(2, 2, new BattleVector2(2f, 0f)),
                CircleProxy(1, 0, BattleVector2.Zero)
            };
            var workspace = new SpatialQueryWorkspace();

            int count = SpatialQueries.SweepCircle(
                new BattleVector2(-2f, 0f),
                new BattleVector2(6f, 0f),
                BattleScalar.FromFloat(0.5f),
                ProjectileFilter,
                proxies,
                proxies.Length,
                workspace);

            Assert.That(count, Is.EqualTo(3));
            Assert.That(workspace.GetHit(0).ProxyId, Is.EqualTo(new SpatialProxyId(1)));
            Assert.That(workspace.GetHit(1).ProxyId, Is.EqualTo(new SpatialProxyId(2)));
            Assert.That(workspace.GetHit(2).ProxyId, Is.EqualTo(new SpatialProxyId(3)));
            Assert.That(workspace.GetHit(0).Fraction, Is.LessThan(workspace.GetHit(1).Fraction));
            Assert.That(workspace.GetHit(1).Fraction, Is.EqualTo(workspace.GetHit(2).Fraction));
        }

        [Test]
        public void SweepCircle_IsIndependentOfProxyInputOrder()
        {
            SpatialProxy first = CircleProxy(1, 0, BattleVector2.Zero);
            SpatialProxy second = CircleProxy(2, 1, new BattleVector2(2f, 0f));
            var forward = new SpatialQueryWorkspace();
            var reverse = new SpatialQueryWorkspace();

            SpatialQueries.SweepCircle(
                new BattleVector2(-2f, 0f),
                new BattleVector2(6f, 0f),
                BattleScalar.FromFloat(0.5f),
                ProjectileFilter,
                new[] { first, second },
                2,
                forward);
            SpatialQueries.SweepCircle(
                new BattleVector2(-2f, 0f),
                new BattleVector2(6f, 0f),
                BattleScalar.FromFloat(0.5f),
                ProjectileFilter,
                new[] { second, first },
                2,
                reverse);

            Assert.That(reverse.HitCount, Is.EqualTo(forward.HitCount));
            for (var i = 0; i < forward.HitCount; i++)
            {
                Assert.That(reverse.GetHit(i).ProxyId, Is.EqualTo(forward.GetHit(i).ProxyId));
                Assert.That(reverse.GetHit(i).Fraction.RawValue, Is.EqualTo(forward.GetHit(i).Fraction.RawValue));
                Assert.That(reverse.GetHit(i).Position, Is.EqualTo(forward.GetHit(i).Position));
            }
        }

        [Test]
        public void SweepCircle_UsesBidirectionalCollisionFilter()
        {
            var workspace = new SpatialQueryWorkspace();
            SpatialProxy[] proxies =
            {
                new SpatialProxy(
                    new SpatialProxyId(1),
                    BattleVector2.Zero,
                    SpatialShape2D.Circle(BattleScalar.One),
                    new SpatialCollisionFilter(1u << 1, 0u),
                    payloadIndex: 0)
            };

            int count = SpatialQueries.SweepCircle(
                new BattleVector2(-2f, 0f),
                new BattleVector2(4f, 0f),
                BattleScalar.Zero,
                ProjectileFilter,
                proxies,
                proxies.Length,
                workspace);

            Assert.That(count, Is.Zero);
        }

        [Test]
        public void Query_ReusesWorkspaceAndClearsPreviousResults()
        {
            var workspace = new SpatialQueryWorkspace();
            SpatialProxy[] proxies = { CircleProxy(1, 0, BattleVector2.Zero) };

            SpatialQueries.SweepCircle(
                new BattleVector2(-2f, 0f),
                new BattleVector2(4f, 0f),
                BattleScalar.Zero,
                ProjectileFilter,
                proxies,
                proxies.Length,
                workspace);
            int secondCount = SpatialQueries.SweepCircle(
                new BattleVector2(-2f, 3f),
                new BattleVector2(4f, 0f),
                BattleScalar.Zero,
                ProjectileFilter,
                proxies,
                proxies.Length,
                workspace);

            Assert.That(secondCount, Is.Zero);
            Assert.That(workspace.HitCount, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => workspace.GetHit(0));
        }

        [Test]
        public void Query_RejectsDuplicateProxyIds()
        {
            var workspace = new SpatialQueryWorkspace();
            SpatialProxy[] proxies =
            {
                CircleProxy(1, 0, BattleVector2.Zero),
                CircleProxy(1, 1, BattleVector2.Right)
            };

            Assert.Throws<ArgumentException>(() => SpatialQueries.SweepCircle(
                BattleVector2.Zero,
                BattleVector2.Right,
                BattleScalar.Zero,
                ProjectileFilter,
                proxies,
                proxies.Length,
                workspace));
        }

        [Test]
        public void SweepCircle_ValidatesDomainEvenWhenProxySetIsEmpty()
        {
            var workspace = new SpatialQueryWorkspace();
            BattleVector2 outsideDomain = new BattleVector2(
                SpatialDomain.MaxCoordinateMagnitude + BattleScalar.Epsilon,
                BattleScalar.Zero);

            Assert.Throws<ArgumentOutOfRangeException>(() => SpatialQueries.SweepCircle(
                outsideDomain,
                BattleVector2.Zero,
                BattleScalar.Zero,
                ProjectileFilter,
                Array.Empty<SpatialProxy>(),
                0,
                workspace));
        }

        [Test]
        public void SweepCircle_RejectsEndPositionOutsideSupportedDomain()
        {
            var workspace = new SpatialQueryWorkspace();
            BattleVector2 start = new BattleVector2(
                SpatialDomain.MaxCoordinateMagnitude,
                BattleScalar.Zero);

            Assert.Throws<ArgumentOutOfRangeException>(() => SpatialQueries.SweepCircle(
                start,
                BattleVector2.Right,
                BattleScalar.Zero,
                ProjectileFilter,
                Array.Empty<SpatialProxy>(),
                0,
                workspace));
        }

        [Test]
        public void OverlapCircle_IncludesCircleAndAabbBoundaryContacts()
        {
            var workspace = new SpatialQueryWorkspace();
            SpatialProxy[] proxies =
            {
                CircleProxy(2, 1, new BattleVector2(2f, 0f), radius: 0.5f),
                new SpatialProxy(
                    new SpatialProxyId(1),
                    BattleVector2.Zero,
                    SpatialShape2D.Aabb(new BattleVector2(0.5f, 0.5f)),
                    TargetFilter,
                    payloadIndex: 0)
            };

            int count = SpatialQueries.OverlapCircle(
                new BattleVector2(1f, 0f),
                BattleScalar.FromFloat(0.5f),
                ProjectileFilter,
                proxies,
                proxies.Length,
                workspace);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(workspace.GetHit(0).ProxyId, Is.EqualTo(new SpatialProxyId(1)));
            Assert.That(workspace.GetHit(1).ProxyId, Is.EqualTo(new SpatialProxyId(2)));
        }

        [Test]
        public void QueryAabb_ReturnsIntersectingProxyBoundsInStableOrder()
        {
            var workspace = new SpatialQueryWorkspace();
            SpatialProxy[] proxies =
            {
                CircleProxy(2, 1, new BattleVector2(1.5f, 0f), radius: 0.5f),
                CircleProxy(1, 0, new BattleVector2(-1.5f, 0f), radius: 0.5f),
                CircleProxy(3, 2, new BattleVector2(3f, 0f), radius: 0.5f)
            };

            int count = SpatialQueries.QueryAabb(
                new SpatialAabb(BattleVector2.Zero, new BattleVector2(1f, 1f)),
                ProjectileFilter,
                proxies,
                proxies.Length,
                workspace);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(workspace.GetHit(0).ProxyId, Is.EqualTo(new SpatialProxyId(1)));
            Assert.That(workspace.GetHit(1).ProxyId, Is.EqualTo(new SpatialProxyId(2)));
        }

        [Test]
        public void SweepCircle_AfterCapacityWarmupDoesNotAllocate()
        {
            SpatialProxy[] proxies =
            {
                CircleProxy(1, 0, BattleVector2.Zero),
                CircleProxy(2, 1, new BattleVector2(2f, 0f))
            };
            var workspace = new SpatialQueryWorkspace();
            SpatialQueries.SweepCircle(
                new BattleVector2(-2f, 0f),
                new BattleVector2(6f, 0f),
                BattleScalar.FromFloat(0.5f),
                ProjectileFilter,
                proxies,
                proxies.Length,
                workspace);

            long before = GC.GetAllocatedBytesForCurrentThread();
            SpatialQueries.SweepCircle(
                new BattleVector2(-2f, 0f),
                new BattleVector2(6f, 0f),
                BattleScalar.FromFloat(0.5f),
                ProjectileFilter,
                proxies,
                proxies.Length,
                workspace);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
        }

        private static SpatialProxy CircleProxy(
            int id,
            int payloadIndex,
            BattleVector2 position,
            float radius = 0.5f)
        {
            return new SpatialProxy(
                new SpatialProxyId(id),
                position,
                SpatialShape2D.Circle(BattleScalar.FromFloat(radius)),
                TargetFilter,
                payloadIndex);
        }
    }
}
