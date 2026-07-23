using System;

namespace Combat.Core.Battle
{
    public readonly struct ProjectileId : IEquatable<ProjectileId>
    {
        public ProjectileId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(ProjectileId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is ProjectileId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return $"ProjectileId({Value})";
        }

        public static bool operator ==(ProjectileId left, ProjectileId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ProjectileId left, ProjectileId right)
        {
            return !left.Equals(right);
        }
    }
}
