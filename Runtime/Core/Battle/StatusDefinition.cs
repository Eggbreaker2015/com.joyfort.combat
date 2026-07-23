using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    public enum StatusPolarity
    {
        Buff,
        Debuff,
        Neutral
    }

    public enum StatusStackPolicy
    {
        RefreshDurationAndAddStack
    }

    public sealed class StatusDefinition
    {
        private readonly BattleModifierDefinition[] _modifiers;
        private readonly ReadOnlyCollection<BattleModifierDefinition> _readOnlyModifiers;
        private readonly BattleTriggerDefinition[] _triggers;
        private readonly ReadOnlyCollection<BattleTriggerDefinition> _readOnlyTriggers;

        public StatusDefinition(string id, StatusPolarity polarity, int durationTicks, int tickIntervalTicks, int periodicDamage, IReadOnlyList<BattleModifierDefinition> modifiers, IReadOnlyList<BattleTriggerDefinition> triggers)
            : this(id, polarity, durationTicks, tickIntervalTicks, periodicDamage, modifiers, triggers, maxStacks: 1, StatusStackPolicy.RefreshDurationAndAddStack)
        {
        }

        public StatusDefinition(string id, StatusPolarity polarity, int durationTicks, int tickIntervalTicks, int periodicDamage, IReadOnlyList<BattleModifierDefinition> modifiers, IReadOnlyList<BattleTriggerDefinition> triggers, int maxStacks)
            : this(id, polarity, durationTicks, tickIntervalTicks, periodicDamage, modifiers, triggers, maxStacks, StatusStackPolicy.RefreshDurationAndAddStack)
        {
        }

        public StatusDefinition(
            string id,
            StatusPolarity polarity,
            int durationTicks,
            int tickIntervalTicks,
            int periodicDamage,
            IReadOnlyList<BattleModifierDefinition> modifiers,
            IReadOnlyList<BattleTriggerDefinition> triggers,
            int maxStacks,
            StatusStackPolicy stackPolicy)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Status definition id is required.", nameof(id)) : id;
            Polarity = polarity;
            DurationTicks = durationTicks > 0 ? durationTicks : throw new ArgumentOutOfRangeException(nameof(durationTicks));
            TickIntervalTicks = tickIntervalTicks > 0 ? tickIntervalTicks : throw new ArgumentOutOfRangeException(nameof(tickIntervalTicks));
            PeriodicDamage = periodicDamage >= 0 ? periodicDamage : throw new ArgumentOutOfRangeException(nameof(periodicDamage));
            MaxStacks = maxStacks > 0 ? maxStacks : throw new ArgumentOutOfRangeException(nameof(maxStacks));
            StackPolicy = ValidateStackPolicy(stackPolicy);
            _modifiers = CopyModifiers(modifiers);
            _readOnlyModifiers = new ReadOnlyCollection<BattleModifierDefinition>(_modifiers);
            _triggers = CopyTriggers(triggers);
            _readOnlyTriggers = new ReadOnlyCollection<BattleTriggerDefinition>(_triggers);
        }

        public string Id { get; }
        public StatusPolarity Polarity { get; }
        public int DurationTicks { get; }
        public int TickIntervalTicks { get; }
        public int PeriodicDamage { get; }
        public int MaxStacks { get; }
        public StatusStackPolicy StackPolicy { get; }
        public IReadOnlyList<BattleModifierDefinition> Modifiers => _readOnlyModifiers;
        public IReadOnlyList<BattleTriggerDefinition> Triggers => _readOnlyTriggers;

        private static StatusStackPolicy ValidateStackPolicy(StatusStackPolicy stackPolicy)
        {
            switch (stackPolicy)
            {
                case StatusStackPolicy.RefreshDurationAndAddStack:
                    return stackPolicy;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stackPolicy), stackPolicy, "Unsupported status stack policy.");
            }
        }

        private static BattleModifierDefinition[] CopyModifiers(IReadOnlyList<BattleModifierDefinition> modifiers)
        {
            if (modifiers == null)
            {
                throw new ArgumentNullException(nameof(modifiers));
            }

            var copy = new BattleModifierDefinition[modifiers.Count];
            for (var i = 0; i < modifiers.Count; i++)
            {
                BattleModifierDefinition modifier = modifiers[i] ?? throw new ArgumentNullException(nameof(modifiers));
                copy[i] = modifier.Target == BattleModifierTarget.Stat
                    ? BattleModifierDefinition.Stat(modifier.StatId, modifier.Operation, modifier.Value)
                    : BattleModifierDefinition.Damage(modifier.DamageStat, modifier.Operation, modifier.Value);
            }

            return copy;
        }

        private static BattleTriggerDefinition[] CopyTriggers(IReadOnlyList<BattleTriggerDefinition> triggers)
        {
            if (triggers == null)
            {
                throw new ArgumentNullException(nameof(triggers));
            }

            var copy = new BattleTriggerDefinition[triggers.Count];
            for (var i = 0; i < triggers.Count; i++)
            {
                copy[i] = triggers[i] ?? throw new ArgumentNullException(nameof(triggers));
            }

            return copy;
        }
    }
}
