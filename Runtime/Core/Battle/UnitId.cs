using System;

namespace Combat.Core.Battle
{
    public readonly struct UnitId : IEquatable<UnitId>
    {
        public UnitId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(UnitId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is UnitId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
