using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Combat.Core.Spatial;

namespace Combat.Core.Battle
{
    public enum BattleSpatialShapeType
    {
        Circle,
        Aabb
    }

    public readonly struct BattleSpatialEntryDefinition
    {
        public BattleSpatialEntryDefinition(
            int stableId,
            BattleSpatialShapeType shapeType,
            BattleVector2 center,
            BattleScalar radius,
            BattleVector2 size,
            uint categoryBits,
            uint maskBits)
        {
            if (stableId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stableId));
            }

            if (!Enum.IsDefined(typeof(BattleSpatialShapeType), shapeType))
            {
                throw new ArgumentOutOfRangeException(nameof(shapeType));
            }

            SpatialDomain.ValidatePosition(center, nameof(center));
            switch (shapeType)
            {
                case BattleSpatialShapeType.Circle:
                    if (radius <= BattleScalar.Zero)
                    {
                        throw new ArgumentOutOfRangeException(nameof(radius));
                    }

                    SpatialDomain.ValidateShapeExtent(radius, nameof(radius));
                    SpatialDomain.ValidatePosition(
                        center - new BattleVector2(radius, radius),
                        nameof(radius));
                    SpatialDomain.ValidatePosition(
                        center + new BattleVector2(radius, radius),
                        nameof(radius));
                    size = BattleVector2.Zero;
                    break;
                case BattleSpatialShapeType.Aabb:
                    if (size.XScalar <= BattleScalar.Zero
                        || size.YScalar <= BattleScalar.Zero)
                    {
                        throw new ArgumentOutOfRangeException(nameof(size));
                    }

                    BattleScalar half = BattleScalar.FromDouble(0.5d);
                    BattleVector2 halfExtents = size * half;
                    SpatialDomain.ValidateShapeExtent(halfExtents.XScalar, nameof(size));
                    SpatialDomain.ValidateShapeExtent(halfExtents.YScalar, nameof(size));
                    SpatialDomain.ValidatePosition(center - halfExtents, nameof(size));
                    SpatialDomain.ValidatePosition(center + halfExtents, nameof(size));
                    radius = BattleScalar.Zero;
                    break;
            }

            StableId = stableId;
            ShapeType = shapeType;
            Center = center;
            Radius = radius;
            Size = size;
            CategoryBits = categoryBits;
            MaskBits = maskBits;
        }

        public int StableId { get; }
        public BattleSpatialShapeType ShapeType { get; }
        public BattleVector2 Center { get; }
        public BattleScalar Radius { get; }
        public BattleVector2 Size { get; }
        public uint CategoryBits { get; }
        public uint MaskBits { get; }
    }

    public sealed class BattleSpatialMapDefinition
    {
        private static readonly BattleSpatialMapDefinition EmptyInstance =
            new BattleSpatialMapDefinition(Array.Empty<BattleSpatialEntryDefinition>());

        public BattleSpatialMapDefinition(IReadOnlyList<BattleSpatialEntryDefinition> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var copied = new List<BattleSpatialEntryDefinition>(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                copied.Add(entries[i]);
            }

            copied.Sort(StableIdComparer.Instance);
            for (var i = 1; i < copied.Count; i++)
            {
                if (copied[i - 1].StableId == copied[i].StableId)
                {
                    throw new ArgumentException(
                        "Battle spatial map entries require unique stable IDs.",
                        nameof(entries));
                }
            }

            Entries = new ReadOnlyCollection<BattleSpatialEntryDefinition>(copied);
        }

        public static BattleSpatialMapDefinition Empty => EmptyInstance;
        public static BattleScalar MaxCoordinateMagnitude => SpatialDomain.MaxCoordinateMagnitude;
        public static BattleScalar MaxShapeExtent => SpatialDomain.MaxShapeExtent;
        public ReadOnlyCollection<BattleSpatialEntryDefinition> Entries { get; }

        private sealed class StableIdComparer : IComparer<BattleSpatialEntryDefinition>
        {
            public static readonly StableIdComparer Instance = new StableIdComparer();

            public int Compare(
                BattleSpatialEntryDefinition left,
                BattleSpatialEntryDefinition right)
            {
                return left.StableId.CompareTo(right.StableId);
            }
        }
    }
}
