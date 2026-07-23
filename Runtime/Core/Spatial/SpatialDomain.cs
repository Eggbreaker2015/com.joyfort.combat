using System;
using Combat.Core.Battle;

namespace Combat.Core.Spatial
{
    internal static class SpatialDomain
    {
        public static BattleScalar MaxCoordinateMagnitude => BattleScalar.FromInt(10000);
        public static BattleScalar MaxShapeExtent => BattleScalar.FromInt(1000);
        public static BattleScalar MaxStepComponentMagnitude => BattleScalar.FromInt(10000);

        public static void ValidatePosition(BattleVector2 position, string parameterName)
        {
            ValidateMagnitude(position.XScalar, MaxCoordinateMagnitude, parameterName);
            ValidateMagnitude(position.YScalar, MaxCoordinateMagnitude, parameterName);
        }

        public static void ValidateStep(BattleVector2 step, string parameterName)
        {
            ValidateMagnitude(step.XScalar, MaxStepComponentMagnitude, parameterName);
            ValidateMagnitude(step.YScalar, MaxStepComponentMagnitude, parameterName);
        }

        public static void ValidateShapeExtent(BattleScalar extent, string parameterName)
        {
            if (extent < BattleScalar.Zero || extent > MaxShapeExtent)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void ValidateMagnitude(
            BattleScalar value,
            BattleScalar maximumMagnitude,
            string parameterName)
        {
            if (value < -maximumMagnitude || value > maximumMagnitude)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
