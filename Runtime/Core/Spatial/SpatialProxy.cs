using System;
using Combat.Core.Battle;

namespace Combat.Core.Spatial
{
    internal readonly struct SpatialProxy
    {
        public SpatialProxy(
            SpatialProxyId id,
            BattleVector2 position,
            SpatialShape2D shape,
            SpatialCollisionFilter filter,
            int payloadIndex)
        {
            if (payloadIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(payloadIndex));
            }

            SpatialDomain.ValidatePosition(position, nameof(position));
            BattleVector2 halfExtents;
            switch (shape.Type)
            {
                case SpatialShapeType.Circle:
                    halfExtents = new BattleVector2(shape.Radius, shape.Radius);
                    break;
                case SpatialShapeType.Aabb:
                    halfExtents = shape.HalfExtents;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(shape));
            }

            SpatialDomain.ValidatePosition(position - halfExtents, nameof(shape));
            SpatialDomain.ValidatePosition(position + halfExtents, nameof(shape));
            Id = id;
            Position = position;
            Shape = shape;
            Filter = filter;
            PayloadIndex = payloadIndex;
        }

        public SpatialProxyId Id { get; }
        public BattleVector2 Position { get; }
        public SpatialShape2D Shape { get; }
        public SpatialCollisionFilter Filter { get; }
        public int PayloadIndex { get; }
    }
}
