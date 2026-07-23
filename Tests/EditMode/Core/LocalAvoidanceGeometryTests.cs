using System;
using Combat.Core.Battle;
using Combat.Core.LocalAvoidance;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class LocalAvoidanceGeometryTests
    {
        private static readonly BattleScalar Quarter =
            BattleScalar.One / BattleScalar.FromInt(4);
        private static readonly BattleScalar Half =
            BattleScalar.One / BattleScalar.FromInt(2);

        [Test]
        public void CandidateSet_HasStableFiftySevenCandidateOrder()
        {
            Assert.AreEqual(57, LocalAvoidanceCandidateSet.Count);
            Assert.AreEqual(56, LocalAvoidanceCandidateSet.ZeroIndex);
            for (int index = 0; index < LocalAvoidanceCandidateSet.Count; index++)
            {
                Assert.AreEqual(
                    index < LocalAvoidanceCandidateSet.ZeroIndex && index % 4 == 0,
                    LocalAvoidanceCandidateSet.IsFullSpeed(index));
            }

            Assert.AreEqual(
                BattleVector2.Right,
                LocalAvoidanceCandidateSet.Get(
                    0,
                    BattleVector2.Right,
                    BattleScalar.One));
            Assert.AreEqual(
                BattleVector2.Zero,
                LocalAvoidanceCandidateSet.Get(
                    LocalAvoidanceCandidateSet.ZeroIndex,
                    BattleVector2.Zero,
                    BattleScalar.One));

            BattleVector2 plus15 = FullSpeedCandidate(1);
            BattleVector2 minus15 = FullSpeedCandidate(2);
            BattleVector2 plus30 = FullSpeedCandidate(3);
            BattleVector2 minus30 = FullSpeedCandidate(4);
            BattleVector2 plus45 = FullSpeedCandidate(5);
            BattleVector2 minus45 = FullSpeedCandidate(6);
            BattleVector2 plus60 = FullSpeedCandidate(7);
            BattleVector2 minus60 = FullSpeedCandidate(8);
            BattleVector2 plus90 = FullSpeedCandidate(9);
            BattleVector2 minus90 = FullSpeedCandidate(10);
            BattleVector2 plus120 = FullSpeedCandidate(11);
            BattleVector2 minus120 = FullSpeedCandidate(12);
            BattleVector2 reverse = FullSpeedCandidate(13);

            AssertMirroredPair(plus15, minus15);
            AssertMirroredPair(plus30, minus30);
            AssertMirroredPair(plus45, minus45);
            AssertMirroredPair(plus60, minus60);
            AssertMirroredPair(plus90, minus90);
            AssertMirroredPair(plus120, minus120);
            Assert.Greater(plus15.XRaw, plus30.XRaw);
            Assert.Greater(plus30.XRaw, plus45.XRaw);
            Assert.Greater(plus45.XRaw, plus60.XRaw);
            Assert.Greater(plus60.XRaw, plus90.XRaw);
            Assert.Greater(plus90.YRaw, plus60.YRaw);
            Assert.Greater(plus60.YRaw, plus45.YRaw);
            Assert.Greater(plus45.YRaw, plus30.YRaw);
            Assert.Greater(plus30.YRaw, plus15.YRaw);
            Assert.Less(plus120.XRaw, 0L);
            Assert.Greater(plus120.YRaw, 0L);
            Assert.Less(minus120.XRaw, 0L);
            Assert.Less(minus120.YRaw, 0L);
            Assert.Less(reverse.XRaw, 0L);
        }

        [Test]
        public void CandidateSet_UsesFourDescendingSpeedTiersPerDirection()
        {
            BattleScalar maxStepDistance = BattleScalar.FromInt(4);

            for (int directionIndex = 0; directionIndex < 14; directionIndex++)
            {
                int firstIndex = directionIndex * 4;
                BattleVector2 full = LocalAvoidanceCandidateSet.Get(
                    firstIndex,
                    BattleVector2.Right,
                    maxStepDistance);
                BattleVector2 threeQuarters = LocalAvoidanceCandidateSet.Get(
                    firstIndex + 1,
                    BattleVector2.Right,
                    maxStepDistance);
                BattleVector2 twoQuarters = LocalAvoidanceCandidateSet.Get(
                    firstIndex + 2,
                    BattleVector2.Right,
                    maxStepDistance);
                BattleVector2 oneQuarter = LocalAvoidanceCandidateSet.Get(
                    firstIndex + 3,
                    BattleVector2.Right,
                    maxStepDistance);

                Assert.Greater(full.MagnitudeScalar.RawValue, threeQuarters.MagnitudeScalar.RawValue);
                Assert.Greater(threeQuarters.MagnitudeScalar.RawValue, twoQuarters.MagnitudeScalar.RawValue);
                Assert.Greater(twoQuarters.MagnitudeScalar.RawValue, oneQuarter.MagnitudeScalar.RawValue);
                AssertMagnitudeMatchesTier(
                    threeQuarters,
                    BattleScalar.FromInt(3));
                AssertMagnitudeMatchesTier(
                    twoQuarters,
                    BattleScalar.FromInt(2));
                AssertMagnitudeMatchesTier(
                    oneQuarter,
                    BattleScalar.One);
            }
        }

        [Test]
        public void CandidateSet_IsRawDeterministicAndNeverExceedsStepBudget()
        {
            BattleVector2 direction = new BattleVector2(
                BattleScalar.FromInt(3),
                BattleScalar.FromInt(4));
            BattleScalar maxStepDistance = BattleScalar.FromInt(7) / BattleScalar.FromInt(3);

            for (int index = 0; index < LocalAvoidanceCandidateSet.Count; index++)
            {
                BattleVector2 first = LocalAvoidanceCandidateSet.Get(
                    index,
                    direction,
                    maxStepDistance);
                BattleVector2 second = LocalAvoidanceCandidateSet.Get(
                    index,
                    direction,
                    maxStepDistance);

                Assert.AreEqual(first.XRaw, second.XRaw);
                Assert.AreEqual(first.YRaw, second.YRaw);
                Assert.IsTrue(first.MagnitudeScalar <= maxStepDistance);
            }
        }

        [Test]
        public void CandidateSet_ZeroBudgetReturnsZeroForEveryCandidate()
        {
            for (int index = 0; index < LocalAvoidanceCandidateSet.Count; index++)
            {
                Assert.AreEqual(
                    BattleVector2.Zero,
                    LocalAvoidanceCandidateSet.Get(
                        index,
                        BattleVector2.Zero,
                        BattleScalar.Zero));
            }
        }

        [Test]
        public void CandidateSet_RejectsInvalidIndexBudgetAndNearZeroDirection()
        {
            Assert.Throws<ArgumentOutOfRangeException>(GetCandidateAtNegativeIndex);
            Assert.Throws<ArgumentOutOfRangeException>(GetCandidatePastEnd);
            Assert.Throws<ArgumentOutOfRangeException>(GetCandidateWithNegativeBudget);
            Assert.Throws<ArgumentOutOfRangeException>(GetCandidateWithZeroDirection);
            Assert.Throws<ArgumentOutOfRangeException>(GetCandidateWithNearZeroDirection);
        }

        [Test]
        public void DotClampAndClosestApproach_UseFixedPointBoundaries()
        {
            Assert.AreEqual(
                BattleScalar.FromInt(11),
                LocalAvoidanceGeometry.Dot(Vector(1, 2), Vector(3, 4)));
            Assert.AreEqual(
                BattleScalar.Zero,
                LocalAvoidanceGeometry.Clamp(-BattleScalar.One, BattleScalar.Zero, BattleScalar.One));
            Assert.AreEqual(
                Half,
                LocalAvoidanceGeometry.Clamp(Half, BattleScalar.Zero, BattleScalar.One));
            Assert.AreEqual(
                BattleScalar.One,
                LocalAvoidanceGeometry.Clamp(BattleScalar.FromInt(2), BattleScalar.Zero, BattleScalar.One));
            Assert.Throws<ArgumentOutOfRangeException>(ClampWithReversedBounds);

            BattleVector2 relativeStart = new BattleVector2(
                -BattleScalar.FromInt(2),
                BattleScalar.One);
            BattleVector2 relativeStep = Vector(4, 0);

            Assert.AreEqual(
                Half,
                LocalAvoidanceGeometry.ClosestApproachParameter(
                    relativeStart,
                    relativeStep));
            Assert.AreEqual(
                BattleScalar.One,
                LocalAvoidanceGeometry.MinimumDistanceSquared(
                    relativeStart,
                    relativeStep));
            Assert.AreEqual(
                BattleScalar.Zero,
                LocalAvoidanceGeometry.ClosestApproachParameter(
                    relativeStart,
                    BattleVector2.Zero));
        }

        [Test]
        public void SweptCirclesOverlap_DetectsHeadOnCrossingWithinTick()
        {
            bool overlap = LocalAvoidanceGeometry.SweptCirclesOverlap(
                Vector(-1, 0),
                Vector(2, 0),
                Quarter,
                Vector(1, 0),
                Vector(-2, 0),
                Quarter,
                BattleScalar.One);

            Assert.IsTrue(overlap);
        }

        [Test]
        public void SweptCirclesOverlap_ParallelEqualStepsRemainSeparated()
        {
            bool overlap = LocalAvoidanceGeometry.SweptCirclesOverlap(
                Vector(0, 0),
                Vector(2, 0),
                Half,
                Vector(0, 2),
                Vector(2, 0),
                Half,
                BattleScalar.FromInt(4));

            Assert.IsFalse(overlap);
        }

        [Test]
        public void SweptCirclesOverlap_TangentClosestApproachIsLegal()
        {
            bool overlap = LocalAvoidanceGeometry.SweptCirclesOverlap(
                Vector(-1, 0),
                Vector(2, 0),
                Half,
                Vector(0, 1),
                BattleVector2.Zero,
                Half,
                BattleScalar.One);

            Assert.IsFalse(overlap);
        }

        [Test]
        public void SweptCirclesOverlap_ZeroRelativeStepUsesInitialSeparation()
        {
            Assert.IsTrue(LocalAvoidanceGeometry.SweptCirclesOverlap(
                Vector(0, 0),
                Vector(1, 0),
                Half,
                new BattleVector2(Half, BattleScalar.Zero),
                Vector(1, 0),
                Half,
                BattleScalar.FromInt(4)));
            Assert.IsFalse(LocalAvoidanceGeometry.SweptCirclesOverlap(
                Vector(0, 0),
                Vector(1, 0),
                Half,
                Vector(2, 0),
                Vector(1, 0),
                Half,
                BattleScalar.FromInt(4)));
            Assert.IsFalse(LocalAvoidanceGeometry.SweptCirclesOverlap(
                Vector(0, 0),
                Vector(1, 0),
                Half,
                Vector(1, 0),
                Vector(1, 0),
                Half,
                BattleScalar.FromInt(4)));
        }

        [Test]
        public void SweptCirclesOverlap_FourTickHorizonFindsFutureConflict()
        {
            bool oneTick = LocalAvoidanceGeometry.SweptCirclesOverlap(
                Vector(0, 0),
                Vector(1, 0),
                Half,
                Vector(4, 0),
                BattleVector2.Zero,
                Half,
                BattleScalar.One);
            bool fourTicks = LocalAvoidanceGeometry.SweptCirclesOverlap(
                Vector(0, 0),
                Vector(1, 0),
                Half,
                Vector(4, 0),
                BattleVector2.Zero,
                Half,
                BattleScalar.FromInt(4));

            Assert.IsFalse(oneTick);
            Assert.IsTrue(fourTicks);
        }

        [Test]
        public void PredictPenetrationDepth_IsZeroOrPositiveAndSymmetric()
        {
            BattleScalar separated = LocalAvoidanceGeometry.PredictPenetrationDepth(
                Vector(0, 0),
                Vector(1, 0),
                Half,
                Vector(4, 0),
                BattleVector2.Zero,
                Half,
                BattleScalar.One);
            BattleScalar firstOrder = LocalAvoidanceGeometry.PredictPenetrationDepth(
                Vector(-1, 0),
                Vector(2, 0),
                Half,
                Vector(1, 0),
                Vector(-2, 0),
                Half,
                BattleScalar.One);
            BattleScalar secondOrder = LocalAvoidanceGeometry.PredictPenetrationDepth(
                Vector(1, 0),
                Vector(-2, 0),
                Half,
                Vector(-1, 0),
                Vector(2, 0),
                Half,
                BattleScalar.One);

            Assert.AreEqual(BattleScalar.Zero, separated);
            Assert.Greater(firstOrder.RawValue, 0L);
            Assert.AreEqual(firstOrder.RawValue, secondOrder.RawValue);
        }

        [Test]
        public void Geometry_RejectsNegativeRadiiAndHorizon()
        {
            Assert.Throws<ArgumentOutOfRangeException>(SweepWithNegativeFirstRadius);
            Assert.Throws<ArgumentOutOfRangeException>(SweepWithNegativeSecondRadius);
            Assert.Throws<ArgumentOutOfRangeException>(SweepWithNegativeHorizon);
            Assert.Throws<ArgumentOutOfRangeException>(PenetrationWithNegativeRadius);
            Assert.Throws<ArgumentOutOfRangeException>(PenetrationWithNegativeHorizon);
        }

        private static BattleVector2 FullSpeedCandidate(int directionIndex)
        {
            return LocalAvoidanceCandidateSet.Get(
                directionIndex * 4,
                BattleVector2.Right,
                BattleScalar.One);
        }

        private static void AssertMirroredPair(
            BattleVector2 positive,
            BattleVector2 negative)
        {
            Assert.AreEqual(positive.XRaw, negative.XRaw);
            Assert.AreEqual(positive.YRaw, -negative.YRaw);
        }

        private static void AssertMagnitudeMatchesTier(
            BattleVector2 candidate,
            BattleScalar expectedMagnitude)
        {
            long actualRaw = candidate.MagnitudeScalar.RawValue;
            long rawDifference = expectedMagnitude.RawValue - actualRaw;
            Assert.GreaterOrEqual(rawDifference, 0L);
            Assert.LessOrEqual(rawDifference, 8L);
        }

        private static BattleVector2 Vector(int x, int y)
        {
            return new BattleVector2(BattleScalar.FromInt(x), BattleScalar.FromInt(y));
        }

        private static void GetCandidateAtNegativeIndex()
        {
            LocalAvoidanceCandidateSet.Get(-1, BattleVector2.Right, BattleScalar.One);
        }

        private static void GetCandidatePastEnd()
        {
            LocalAvoidanceCandidateSet.Get(
                LocalAvoidanceCandidateSet.Count,
                BattleVector2.Right,
                BattleScalar.One);
        }

        private static void GetCandidateWithNegativeBudget()
        {
            LocalAvoidanceCandidateSet.Get(
                0,
                BattleVector2.Right,
                -BattleScalar.One);
        }

        private static void GetCandidateWithZeroDirection()
        {
            LocalAvoidanceCandidateSet.Get(
                0,
                BattleVector2.Zero,
                BattleScalar.One);
        }

        private static void GetCandidateWithNearZeroDirection()
        {
            LocalAvoidanceCandidateSet.Get(
                0,
                new BattleVector2(BattleScalar.Epsilon, BattleScalar.Zero),
                BattleScalar.One);
        }

        private static void ClampWithReversedBounds()
        {
            LocalAvoidanceGeometry.Clamp(
                BattleScalar.Zero,
                BattleScalar.One,
                BattleScalar.Zero);
        }

        private static void SweepWithNegativeFirstRadius()
        {
            LocalAvoidanceGeometry.SweptCirclesOverlap(
                BattleVector2.Zero,
                BattleVector2.Zero,
                -BattleScalar.One,
                BattleVector2.Zero,
                BattleVector2.Zero,
                BattleScalar.Zero,
                BattleScalar.Zero);
        }

        private static void SweepWithNegativeSecondRadius()
        {
            LocalAvoidanceGeometry.SweptCirclesOverlap(
                BattleVector2.Zero,
                BattleVector2.Zero,
                BattleScalar.Zero,
                BattleVector2.Zero,
                BattleVector2.Zero,
                -BattleScalar.One,
                BattleScalar.Zero);
        }

        private static void SweepWithNegativeHorizon()
        {
            LocalAvoidanceGeometry.SweptCirclesOverlap(
                BattleVector2.Zero,
                BattleVector2.Zero,
                BattleScalar.Zero,
                BattleVector2.Zero,
                BattleVector2.Zero,
                BattleScalar.Zero,
                -BattleScalar.One);
        }

        private static void PenetrationWithNegativeRadius()
        {
            LocalAvoidanceGeometry.PredictPenetrationDepth(
                BattleVector2.Zero,
                BattleVector2.Zero,
                -BattleScalar.One,
                BattleVector2.Zero,
                BattleVector2.Zero,
                BattleScalar.Zero,
                BattleScalar.Zero);
        }

        private static void PenetrationWithNegativeHorizon()
        {
            LocalAvoidanceGeometry.PredictPenetrationDepth(
                BattleVector2.Zero,
                BattleVector2.Zero,
                BattleScalar.Zero,
                BattleVector2.Zero,
                BattleVector2.Zero,
                BattleScalar.Zero,
                -BattleScalar.One);
        }
    }
}
