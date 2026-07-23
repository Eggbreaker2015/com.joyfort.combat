using Combat.Core.Battle;

namespace Combat.Core.Spatial
{
    internal readonly struct SpatialAabb
    {
        public SpatialAabb(BattleVector2 center, BattleVector2 halfExtents)
        {
            SpatialDomain.ValidatePosition(center, nameof(center));
            SpatialDomain.ValidateShapeExtent(halfExtents.XScalar, nameof(halfExtents));
            SpatialDomain.ValidateShapeExtent(halfExtents.YScalar, nameof(halfExtents));

            BattleVector2 minimum = center - halfExtents;
            BattleVector2 maximum = center + halfExtents;
            SpatialDomain.ValidatePosition(minimum, nameof(halfExtents));
            SpatialDomain.ValidatePosition(maximum, nameof(halfExtents));

            Center = center;
            HalfExtents = halfExtents;
            Minimum = minimum;
            Maximum = maximum;
        }

        public BattleVector2 Center { get; }
        public BattleVector2 HalfExtents { get; }
        public BattleVector2 Minimum { get; }
        public BattleVector2 Maximum { get; }

        public bool Overlaps(SpatialAabb other)
        {
            return Minimum.XScalar <= other.Maximum.XScalar
                && Maximum.XScalar >= other.Minimum.XScalar
                && Minimum.YScalar <= other.Maximum.YScalar
                && Maximum.YScalar >= other.Minimum.YScalar;
        }

        public static SpatialAabb FromProxy(SpatialProxy proxy)
        {
            switch (proxy.Shape.Type)
            {
                case SpatialShapeType.Circle:
                    BattleScalar radius = proxy.Shape.Radius;
                    return new SpatialAabb(
                        proxy.Position,
                        new BattleVector2(radius, radius));
                case SpatialShapeType.Aabb:
                    return new SpatialAabb(proxy.Position, proxy.Shape.HalfExtents);
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(proxy),
                        proxy.Shape.Type,
                        "Unsupported spatial shape type.");
            }
        }
    }
}
