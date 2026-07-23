using System.Collections.Generic;
using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class ProjectileCollisionTests
    {
        [Test]
        public void CollectHits_ReturnsCircleOverlap()
        {
            var detector = new CircleProjectileCollisionDetector();
            var hits = new List<ProjectileHit>();
            var frame = new ProjectileCollisionFrame(
                new[]
                {
                    new ProjectileCollisionSnapshot(new ProjectileId(1), new EntityId(10, 1), new EntityId(1, 1), new TeamId(1), new BattleVector2(0f, 0f), new BattleVector2(0f, 0f), 0.5f)
                },
                new[]
                {
                    new ProjectileTargetSnapshot(new UnitId(2), new EntityId(2, 1), new TeamId(2), new BattleVector2(0.75f, 0f), 0.25f)
                });

            detector.CollectHits(frame, hits);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(new ProjectileId(1), hits[0].ProjectileId);
            Assert.AreEqual(new UnitId(2), hits[0].TargetUnitId);
        }

        [Test]
        public void CollectHits_StoresProjectilePositionAsHitPosition()
        {
            var detector = new CircleProjectileCollisionDetector();
            var hits = new List<ProjectileHit>();
            var frame = new ProjectileCollisionFrame(
                new[]
                {
                    new ProjectileCollisionSnapshot(new ProjectileId(1), new EntityId(10, 1), new EntityId(1, 1), new TeamId(1), new BattleVector2(2f, 3f), new BattleVector2(2f, 3f), 0.5f)
                },
                new[]
                {
                    new ProjectileTargetSnapshot(new UnitId(2), new EntityId(2, 1), new TeamId(2), new BattleVector2(2.5f, 3f), 0.25f)
                });

            detector.CollectHits(frame, hits);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(new BattleVector2(2f, 3f), hits[0].Position);
        }

        [Test]
        public void CollectHits_IgnoresFriendlyTargets()
        {
            var detector = new CircleProjectileCollisionDetector();
            var hits = new List<ProjectileHit>();
            var frame = new ProjectileCollisionFrame(
                new[]
                {
                    new ProjectileCollisionSnapshot(new ProjectileId(1), new EntityId(10, 1), new EntityId(1, 1), new TeamId(1), new BattleVector2(0f, 0f), new BattleVector2(0f, 0f), 1f)
                },
                new[]
                {
                    new ProjectileTargetSnapshot(new UnitId(2), new EntityId(2, 1), new TeamId(1), new BattleVector2(0f, 0f), 1f)
                });

            detector.CollectHits(frame, hits);

            Assert.AreEqual(0, hits.Count);
        }

        [Test]
        public void CollectHits_SweepsAcrossHighSpeedPath()
        {
            var detector = new CircleProjectileCollisionDetector();
            var hits = new List<ProjectileHit>();
            var frame = new ProjectileCollisionFrame(
                new[]
                {
                    new ProjectileCollisionSnapshot(
                        new ProjectileId(1),
                        new EntityId(10, 1),
                        new EntityId(1, 1),
                        new TeamId(1),
                        new BattleVector2(-2f, 0f),
                        new BattleVector2(2f, 0f),
                        0.5f)
                },
                new[]
                {
                    new ProjectileTargetSnapshot(
                        new UnitId(2),
                        new EntityId(2, 1),
                        new TeamId(2),
                        BattleVector2.Zero,
                        0.5f)
                });

            detector.CollectHits(frame, hits);

            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].Fraction, Is.EqualTo(BattleScalar.FromFloat(0.25f)));
            Assert.That(hits[0].Position, Is.EqualTo(new BattleVector2(-1f, 0f)));
        }

        [Test]
        public void CollectHits_OrdersEachProjectileByFractionThenTargetUnitId()
        {
            var detector = new CircleProjectileCollisionDetector();
            var hits = new List<ProjectileHit>();
            var frame = new ProjectileCollisionFrame(
                new[]
                {
                    new ProjectileCollisionSnapshot(
                        new ProjectileId(1),
                        new EntityId(10, 1),
                        new EntityId(1, 1),
                        new TeamId(1),
                        new BattleVector2(-2f, 0f),
                        new BattleVector2(4f, 0f),
                        0.5f)
                },
                new[]
                {
                    new ProjectileTargetSnapshot(new UnitId(4), new EntityId(4, 1), new TeamId(2), new BattleVector2(2f, 0f), 0.5f),
                    new ProjectileTargetSnapshot(new UnitId(3), new EntityId(3, 1), new TeamId(2), BattleVector2.Zero, 0.5f),
                    new ProjectileTargetSnapshot(new UnitId(2), new EntityId(2, 1), new TeamId(2), BattleVector2.Zero, 0.5f)
                });

            detector.CollectHits(frame, hits);

            Assert.That(hits.Count, Is.EqualTo(3));
            Assert.That(hits[0].TargetUnitId, Is.EqualTo(new UnitId(2)));
            Assert.That(hits[1].TargetUnitId, Is.EqualTo(new UnitId(3)));
            Assert.That(hits[0].Fraction, Is.EqualTo(hits[1].Fraction));
            Assert.That(hits[2].TargetUnitId, Is.EqualTo(new UnitId(4)));
            Assert.That(hits[1].Fraction, Is.LessThan(hits[2].Fraction));
        }
    }
}
