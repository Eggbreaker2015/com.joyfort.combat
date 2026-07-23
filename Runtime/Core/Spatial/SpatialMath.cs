using System;
using Combat.Core.Battle;

namespace Combat.Core.Spatial
{
    internal static class SpatialMath
    {
        public static BattleScalar Dot(BattleVector2 left, BattleVector2 right)
        {
            return left.XScalar * right.XScalar + left.YScalar * right.YScalar;
        }

        public static BattleScalar Clamp(
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
    }
}
