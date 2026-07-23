using Combat.Core.Battle;

namespace Combat.Core.Spatial
{
    internal enum SpatialShapeType
    {
        Circle,
        Aabb
    }

    internal readonly struct SpatialShape2D
    {
        private SpatialShape2D(
            SpatialShapeType type,
            BattleScalar radius,
            BattleVector2 halfExtents)
        {
            Type = type;
            Radius = radius;
            HalfExtents = halfExtents;
        }

        public SpatialShapeType Type { get; }
        public BattleScalar Radius { get; }
        public BattleVector2 HalfExtents { get; }

        public static SpatialShape2D Circle(BattleScalar radius)
        {
            SpatialDomain.ValidateShapeExtent(radius, nameof(radius));

            return new SpatialShape2D(SpatialShapeType.Circle, radius, BattleVector2.Zero);
        }

        public static SpatialShape2D Aabb(BattleVector2 halfExtents)
        {
            SpatialDomain.ValidateShapeExtent(halfExtents.XScalar, nameof(halfExtents));
            SpatialDomain.ValidateShapeExtent(halfExtents.YScalar, nameof(halfExtents));

            return new SpatialShape2D(SpatialShapeType.Aabb, BattleScalar.Zero, halfExtents);
        }
    }
}
