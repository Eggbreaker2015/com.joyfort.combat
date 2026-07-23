using System;
using System.Collections.Generic;

namespace Combat.Core.Battle
{
    public enum BattleStatId
    {
        MaxHealth = 0,
        MoveSpeed = 1
    }

    public readonly struct BattleStatEntry
    {
        public BattleStatEntry(BattleStatId stat, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Stat = stat;
            Value = value;
        }

        public BattleStatId Stat { get; }
        public float Value { get; }
    }

    public sealed class BattleStatBlock
    {
        private const double IntegerTolerance = 0.0001d;
        private readonly Dictionary<BattleStatId, float> _values;

        public BattleStatBlock(IReadOnlyList<BattleStatEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            _values = new Dictionary<BattleStatId, float>(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                BattleStatEntry entry = entries[i];
                if (_values.ContainsKey(entry.Stat))
                {
                    throw new ArgumentException($"Duplicate battle stat: {entry.Stat}.", nameof(entries));
                }

                _values.Add(entry.Stat, entry.Value);
            }
        }

        public bool TryGetFloat(BattleStatId stat, out float value)
        {
            return _values.TryGetValue(stat, out value);
        }

        public float RequireFloat(BattleStatId stat, string owner)
        {
            if (TryGetFloat(stat, out float value))
            {
                return value;
            }

            string ownerLabel = string.IsNullOrWhiteSpace(owner) ? "Battle stat block" : owner;
            throw new ArgumentException($"{ownerLabel} is missing required stat {stat}.");
        }

        public bool TryGetScalar(BattleStatId stat, out BattleScalar value)
        {
            if (TryGetFloat(stat, out float floatValue))
            {
                value = BattleScalar.FromFloat(floatValue);
                return true;
            }

            value = BattleScalar.Zero;
            return false;
        }

        public BattleScalar RequireScalar(BattleStatId stat, string owner)
        {
            if (TryGetScalar(stat, out BattleScalar value))
            {
                return value;
            }

            string ownerLabel = string.IsNullOrWhiteSpace(owner) ? "Battle stat block" : owner;
            throw new ArgumentException($"{ownerLabel} is missing required stat {stat}.");
        }

        public int RequireInt(BattleStatId stat, string owner)
        {
            float value = RequireFloat(stat, owner);
            double rounded = Math.Round(value);
            if (Math.Abs(value - rounded) > IntegerTolerance)
            {
                string ownerLabel = string.IsNullOrWhiteSpace(owner) ? "Battle stat block" : owner;
                throw new ArgumentException($"{ownerLabel} stat {stat} must be an integer value.");
            }

            if (rounded < int.MinValue || rounded > int.MaxValue)
            {
                string ownerLabel = string.IsNullOrWhiteSpace(owner) ? "Battle stat block" : owner;
                throw new ArgumentException($"{ownerLabel} stat {stat} must be within integer range.");
            }

            return checked((int)rounded);
        }
    }
}
