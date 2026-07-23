namespace Combat.Core.Spatial
{
    internal readonly struct SpatialCollisionFilter
    {
        public SpatialCollisionFilter(uint categoryBits, uint maskBits)
        {
            CategoryBits = categoryBits;
            MaskBits = maskBits;
        }

        public static SpatialCollisionFilter All => new SpatialCollisionFilter(uint.MaxValue, uint.MaxValue);

        public uint CategoryBits { get; }
        public uint MaskBits { get; }

        public bool Allows(SpatialCollisionFilter other)
        {
            return (MaskBits & other.CategoryBits) != 0u
                && (other.MaskBits & CategoryBits) != 0u;
        }
    }
}
