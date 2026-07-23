using Combat.Core.Battle;
using Combat.Core.Spatial;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class SpatialGeometryTests
    {
        [Test]
        public void CirclesOverlap_IncludesExternalTangent()
        {
            bool overlaps = SpatialGeometry.CirclesOverlap(
                BattleVector2.Zero,
                BattleScalar.FromFloat(0.5f),
                new BattleVector2(1f, 0f),
                BattleScalar.FromFloat(0.5f));

            Assert.That(overlaps, Is.True);
        }

        [Test]
        public void SweepCircleAgainstCircle_FindsHighSpeedFirstContact()
        {
            bool hit = SpatialGeometry.TrySweepCircleAgainstCircle(
                new BattleVector2(-2f, 0f),
                new BattleVector2(4f, 0f),
                BattleScalar.FromFloat(0.5f),
                BattleVector2.Zero,
                BattleScalar.FromFloat(0.5f),
                out SpatialSweepHit result);

            Assert.That(hit, Is.True);
            Assert.That(result.Fraction.RawValue, Is.EqualTo(BattleScalar.FromFloat(0.25f).RawValue));
            Assert.That(result.Position, Is.EqualTo(new BattleVector2(-1f, 0f)));
            Assert.That(result.Point, Is.EqualTo(new BattleVector2(-0.5f, 0f)));
            Assert.That(result.Normal, Is.EqualTo(new BattleVector2(-1f, 0f)));
            Assert.That(result.StartedOverlapping, Is.False);
        }

        [Test]
        public void SweepCircleAgainstCircle_ReportsInitialOverlapAtZero()
        {
            bool hit = SpatialGeometry.TrySweepCircleAgainstCircle(
                BattleVector2.Zero,
                BattleVector2.Right,
                BattleScalar.FromFloat(0.5f),
                BattleVector2.Zero,
                BattleScalar.FromFloat(0.5f),
                out SpatialSweepHit result);

            Assert.That(hit, Is.True);
            Assert.That(result.Fraction, Is.EqualTo(BattleScalar.Zero));
            Assert.That(result.Position, Is.EqualTo(BattleVector2.Zero));
            Assert.That(result.Normal, Is.EqualTo(new BattleVector2(-1f, 0f)));
            Assert.That(result.StartedOverlapping, Is.True);
        }

        [Test]
        public void SweepCircleAgainstCircle_ZeroDeltaOnlyHitsWhenAlreadyOverlapping()
        {
            bool separated = SpatialGeometry.TrySweepCircleAgainstCircle(
                BattleVector2.Zero,
                BattleVector2.Zero,
                BattleScalar.FromFloat(0.5f),
                new BattleVector2(2f, 0f),
                BattleScalar.FromFloat(0.5f),
                out _);
            bool overlapping = SpatialGeometry.TrySweepCircleAgainstCircle(
                BattleVector2.Zero,
                BattleVector2.Zero,
                BattleScalar.FromFloat(0.5f),
                new BattleVector2(1f, 0f),
                BattleScalar.FromFloat(0.5f),
                out SpatialSweepHit result);

            Assert.That(separated, Is.False);
            Assert.That(overlapping, Is.True);
            Assert.That(result.Fraction, Is.EqualTo(BattleScalar.Zero));
        }

        [Test]
        public void SweepCircleAgainstCircle_IncludesPathTangent()
        {
            bool hit = SpatialGeometry.TrySweepCircleAgainstCircle(
                new BattleVector2(-2f, 1f),
                new BattleVector2(4f, 0f),
                BattleScalar.FromFloat(0.5f),
                BattleVector2.Zero,
                BattleScalar.FromFloat(0.5f),
                out SpatialSweepHit result);

            Assert.That(hit, Is.True);
            Assert.That(result.Fraction, Is.EqualTo(BattleScalar.FromFloat(0.5f)));
            Assert.That(result.Position, Is.EqualTo(new BattleVector2(0f, 1f)));
            Assert.That(result.Normal, Is.EqualTo(new BattleVector2(0f, 1f)));
        }

        [Test]
        public void SweepCircleAgainstCircle_RejectsMotionAwayFromTarget()
        {
            bool hit = SpatialGeometry.TrySweepCircleAgainstCircle(
                new BattleVector2(2f, 0f),
                BattleVector2.Right,
                BattleScalar.FromFloat(0.5f),
                BattleVector2.Zero,
                BattleScalar.FromFloat(0.5f),
                out _);

            Assert.That(hit, Is.False);
        }

        [Test]
        public void SweepCircleAgainstCircle_IncludesContactAtEndOfStep()
        {
            bool hit = SpatialGeometry.TrySweepCircleAgainstCircle(
                new BattleVector2(-2f, 0f),
                BattleVector2.Right,
                BattleScalar.FromFloat(0.5f),
                BattleVector2.Zero,
                BattleScalar.FromFloat(0.5f),
                out SpatialSweepHit result);

            Assert.That(hit, Is.True);
            Assert.That(result.Fraction, Is.EqualTo(BattleScalar.One));
            Assert.That(result.Position, Is.EqualTo(new BattleVector2(-1f, 0f)));
        }

        [Test]
        public void SweepCircleAgainstCircle_RejectsStepOutsideSupportedDomain()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                SpatialGeometry.TrySweepCircleAgainstCircle(
                    BattleVector2.Zero,
                    new BattleVector2(
                        SpatialDomain.MaxStepComponentMagnitude + BattleScalar.Epsilon,
                        BattleScalar.Zero),
                    BattleScalar.Zero,
                    BattleVector2.Right,
                    BattleScalar.Zero,
                    out _));
        }
    }
}
