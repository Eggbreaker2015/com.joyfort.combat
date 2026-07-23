using System;
using Combat.Core.Battle;

namespace Combat.Core.LocalAvoidance
{
    internal static class LocalAvoidanceCandidateSet
    {
        internal const int Count = 57;
        internal const int ZeroIndex = 56;

        private static readonly BattleScalar QuarterDivisor = BattleScalar.FromInt(4);
        private static readonly BattleScalar DegreesPerCircle = BattleScalar.FromInt(360);

        internal static bool IsFullSpeed(int index)
        {
            return index >= 0
                && index < ZeroIndex
                && index % 4 == 0;
        }

        internal static BattleVector2 Get(
            int index,
            BattleVector2 preferredDirection,
            BattleScalar maxStepDistance)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (maxStepDistance < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maxStepDistance));
            }

            if (index == ZeroIndex || maxStepDistance == BattleScalar.Zero)
            {
                return BattleVector2.Zero;
            }

            if (preferredDirection.SqrMagnitudeScalar <= BattleScalar.Epsilon)
            {
                throw new ArgumentOutOfRangeException(nameof(preferredDirection));
            }

            int directionIndex = index / 4;
            int speedNumerator = 4 - (index % 4);
            int degrees = GetDegrees(directionIndex);
            BattleVector2 direction = preferredDirection.Normalized;
            BattleScalar radians = BattleScalar.FromInt(degrees)
                * BattleScalar.TwoPi
                / DegreesPerCircle;
            BattleScalar cos = BattleScalar.Cos(radians);
            BattleScalar sin = BattleScalar.Sin(radians);
            BattleVector2 rotated = new BattleVector2(
                direction.XScalar * cos - direction.YScalar * sin,
                direction.XScalar * sin + direction.YScalar * cos);
            BattleVector2 candidate = rotated.Normalized
                * maxStepDistance
                * BattleScalar.FromInt(speedNumerator)
                / QuarterDivisor;
            BattleScalar tierBudget = maxStepDistance
                * BattleScalar.FromInt(speedNumerator)
                / QuarterDivisor;
            return ClampMagnitude(candidate, tierBudget);
        }

        private static int GetDegrees(int directionIndex)
        {
            switch (directionIndex)
            {
                case 0:
                    return 0;
                case 1:
                    return 15;
                case 2:
                    return -15;
                case 3:
                    return 30;
                case 4:
                    return -30;
                case 5:
                    return 45;
                case 6:
                    return -45;
                case 7:
                    return 60;
                case 8:
                    return -60;
                case 9:
                    return 90;
                case 10:
                    return -90;
                case 11:
                    return 120;
                case 12:
                    return -120;
                case 13:
                    return 180;
                default:
                    throw new ArgumentOutOfRangeException(nameof(directionIndex));
            }
        }

        private static BattleVector2 ClampMagnitude(
            BattleVector2 value,
            BattleScalar maxMagnitude)
        {
            BattleScalar magnitude = value.MagnitudeScalar;
            if (magnitude <= maxMagnitude)
            {
                return value;
            }

            BattleVector2 clamped = value * (maxMagnitude / magnitude);
            while (clamped.MagnitudeScalar > maxMagnitude)
            {
                long xRaw = clamped.XRaw;
                long yRaw = clamped.YRaw;
                ulong absoluteXRaw = AbsoluteRaw(xRaw);
                ulong absoluteYRaw = AbsoluteRaw(yRaw);
                if (absoluteXRaw >= absoluteYRaw)
                {
                    xRaw += xRaw < 0L ? 1L : -1L;
                }
                else
                {
                    yRaw += yRaw < 0L ? 1L : -1L;
                }

                clamped = BattleVector2.FromRaw(xRaw, yRaw);
            }

            return clamped;
        }

        private static ulong AbsoluteRaw(long value)
        {
            return value < 0L
                ? (ulong)(-(value + 1L)) + 1UL
                : (ulong)value;
        }
    }
}
