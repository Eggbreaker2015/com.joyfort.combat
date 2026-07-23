using System;

namespace Combat.Core.Battle
{
    internal readonly struct EntityId : IEquatable<EntityId>
    {
        public EntityId(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public int Index { get; }
        public int Generation { get; }
        public bool IsValid => Generation > 0;

        public bool Equals(EntityId other)
        {
            return Index == other.Index && Generation == other.Generation;
        }

        public override bool Equals(object obj)
        {
            return obj is EntityId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Index, Generation).GetHashCode();
        }

        public override string ToString()
        {
            return IsValid ? $"Entity({Index}, {Generation})" : "Entity(invalid)";
        }
    }
}
