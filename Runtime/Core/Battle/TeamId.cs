using System;

namespace Combat.Core.Battle
{
    public readonly struct TeamId : IEquatable<TeamId>
    {
        public TeamId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(TeamId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is TeamId other && Equals(other);
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
