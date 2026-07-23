using System;

namespace Combat.Core.Spatial
{
    internal readonly struct SpatialProxyId : IEquatable<SpatialProxyId>, IComparable<SpatialProxyId>
    {
        public SpatialProxyId(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Value = value;
        }

        public int Value { get; }

        public int CompareTo(SpatialProxyId other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(SpatialProxyId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is SpatialProxyId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public static bool operator ==(SpatialProxyId left, SpatialProxyId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SpatialProxyId left, SpatialProxyId right)
        {
            return !left.Equals(right);
        }
    }
}
