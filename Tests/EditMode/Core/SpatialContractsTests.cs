using System;
using Combat.Core.Battle;
using Combat.Core.Spatial;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class SpatialContractsTests
    {
        [TestCase(0)]
        [TestCase(-1)]
        public void ProxyId_RejectsNonPositiveValue(int value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialProxyId(value));
        }

        [Test]
        public void Circle_RejectsNegativeRadius()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SpatialShape2D.Circle(-BattleScalar.Epsilon));
        }

        [Test]
        public void Aabb_RejectsNegativeHalfExtent()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SpatialShape2D.Aabb(new BattleVector2(-BattleScalar.Epsilon, BattleScalar.One)));
        }

        [Test]
        public void CollisionFilter_RequiresBothSidesToAllowCollision()
        {
            var units = new SpatialCollisionFilter(categoryBits: 1u << 0, maskBits: 1u << 1);
            var projectiles = new SpatialCollisionFilter(categoryBits: 1u << 1, maskBits: 1u << 0);
            var blockedProjectiles = new SpatialCollisionFilter(categoryBits: 1u << 1, maskBits: 0u);

            Assert.That(units.Allows(projectiles), Is.True);
            Assert.That(projectiles.Allows(units), Is.True);
            Assert.That(units.Allows(blockedProjectiles), Is.False);
            Assert.That(blockedProjectiles.Allows(units), Is.False);
        }

        [Test]
        public void Proxy_RejectsNegativePayloadIndex()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialProxy(
                new SpatialProxyId(1),
                BattleVector2.Zero,
                SpatialShape2D.Circle(BattleScalar.Zero),
                SpatialCollisionFilter.All,
                payloadIndex: -1));
        }

        [Test]
        public void Shape_RejectsExtentOutsideSupportedDomain()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SpatialShape2D.Circle(
                SpatialDomain.MaxShapeExtent + BattleScalar.Epsilon));
        }

        [Test]
        public void Proxy_RejectsPositionOutsideSupportedDomain()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialProxy(
                new SpatialProxyId(1),
                new BattleVector2(
                    SpatialDomain.MaxCoordinateMagnitude + BattleScalar.Epsilon,
                    BattleScalar.Zero),
                SpatialShape2D.Circle(BattleScalar.Zero),
                SpatialCollisionFilter.All,
                payloadIndex: 0));
        }

        [Test]
        public void Proxy_RejectsShapeBoundsOutsideSupportedDomain()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialProxy(
                new SpatialProxyId(1),
                new BattleVector2(
                    SpatialDomain.MaxCoordinateMagnitude,
                    BattleScalar.Zero),
                SpatialShape2D.Circle(BattleScalar.One),
                SpatialCollisionFilter.All,
                payloadIndex: 0));
        }
    }
}
