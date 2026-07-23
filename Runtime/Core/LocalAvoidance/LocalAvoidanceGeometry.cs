using System;
using Combat.Core.Battle;

namespace Combat.Core.LocalAvoidance
{
    internal static class LocalAvoidanceGeometry
    {
        internal static BattleScalar Dot(BattleVector2 left, BattleVector2 right)
        {
            return left.XScalar * right.XScalar + left.YScalar * right.YScalar;
        }

        internal static BattleScalar Clamp(
            BattleScalar value,
            BattleScalar minimum,
            BattleScalar maximum)
        {
            if (minimum > maximum)
            {
                throw new ArgumentOutOfRangeException(nameof(minimum));
            }

            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }

        internal static BattleScalar ClosestApproachParameter(
            BattleVector2 relativeStart,
            BattleVector2 relativeStep)
        {
            BattleScalar denominator = Dot(relativeStep, relativeStep);
            if (denominator <= BattleScalar.Epsilon)
            {
                return BattleScalar.Zero;
            }

            return Clamp(
                -Dot(relativeStart, relativeStep) / denominator,
                BattleScalar.Zero,
                BattleScalar.One);
        }

        internal static BattleScalar MinimumDistanceSquared(
            BattleVector2 relativeStart,
            BattleVector2 relativeStep)
        {
            BattleScalar time = ClosestApproachParameter(relativeStart, relativeStep);
            BattleVector2 closest = relativeStart + relativeStep * time;
            return closest.SqrMagnitudeScalar;
        }

        internal static bool SweptCirclesOverlap(
            BattleVector2 firstPosition,
            BattleVector2 firstStep,
            BattleScalar firstRadius,
            BattleVector2 secondPosition,
            BattleVector2 secondStep,
            BattleScalar secondRadius,
            BattleScalar horizon)
        {
            ValidatePredictionInputs(firstRadius, secondRadius, horizon);

            BattleScalar combinedRadius = firstRadius + secondRadius;
            BattleScalar minimumDistanceSquared = PredictionMinimumDistanceSquared(
                firstPosition,
                firstStep,
                secondPosition,
                secondStep,
                horizon);
            return minimumDistanceSquared < combinedRadius * combinedRadius;
        }

        internal static BattleScalar PredictPenetrationDepth(
            BattleVector2 firstPosition,
            BattleVector2 firstStep,
            BattleScalar firstRadius,
            BattleVector2 secondPosition,
            BattleVector2 secondStep,
            BattleScalar secondRadius,
            BattleScalar horizon)
        {
            ValidatePredictionInputs(firstRadius, secondRadius, horizon);

            BattleScalar combinedRadius = firstRadius + secondRadius;
            BattleScalar minimumDistanceSquared = PredictionMinimumDistanceSquared(
                firstPosition,
                firstStep,
                secondPosition,
                secondStep,
                horizon);
            BattleScalar penetration = combinedRadius
                - BattleScalar.Sqrt(minimumDistanceSquared);
            return penetration > BattleScalar.Zero
                ? penetration
                : BattleScalar.Zero;
        }

        private static BattleScalar PredictionMinimumDistanceSquared(
            BattleVector2 firstPosition,
            BattleVector2 firstStep,
            BattleVector2 secondPosition,
            BattleVector2 secondStep,
            BattleScalar horizon)
        {
            BattleVector2 relativeStart = firstPosition - secondPosition;
            BattleVector2 relativeStep = (firstStep - secondStep) * horizon;
            return MinimumDistanceSquared(relativeStart, relativeStep);
        }

        private static void ValidatePredictionInputs(
            BattleScalar firstRadius,
            BattleScalar secondRadius,
            BattleScalar horizon)
        {
            if (firstRadius < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(firstRadius));
            }

            if (secondRadius < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(secondRadius));
            }

            if (horizon < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(horizon));
            }
        }
    }
}
