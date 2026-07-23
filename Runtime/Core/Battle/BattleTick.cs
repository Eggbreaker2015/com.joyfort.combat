using System;

namespace Combat.Core.Battle
{
    public readonly struct BattleTick : IEquatable<BattleTick>
    {
        public BattleTick(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public BattleTick Next()
        {
            return new BattleTick(Value + 1);
        }

        public bool Equals(BattleTick other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is BattleTick other && Equals(other);
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
