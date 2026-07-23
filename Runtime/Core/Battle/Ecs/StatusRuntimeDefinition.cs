using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    internal readonly struct StatusRuntimeDefinition
    {
        private readonly BattleModifierInstance[] _modifiers;
        private readonly ReadOnlyCollection<BattleModifierInstance> _readOnlyModifiers;
        private readonly BattleTriggerInstance[] _triggers;
        private readonly ReadOnlyCollection<BattleTriggerInstance> _readOnlyTriggers;

        public StatusRuntimeDefinition(
            string id,
            StatusPolarity polarity,
            int tickIntervalTicks,
            int periodicDamage,
            int maxStacks,
            StatusStackPolicy stackPolicy,
            IReadOnlyList<BattleModifierInstance> modifiers,
            IReadOnlyList<BattleTriggerInstance> triggers)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Status id is required.", nameof(id)) : id;
            Polarity = polarity;
            TickIntervalTicks = tickIntervalTicks > 0 ? tickIntervalTicks : throw new ArgumentOutOfRangeException(nameof(tickIntervalTicks));
            PeriodicDamage = periodicDamage >= 0 ? periodicDamage : throw new ArgumentOutOfRangeException(nameof(periodicDamage));
            MaxStacks = maxStacks > 0 ? maxStacks : throw new ArgumentOutOfRangeException(nameof(maxStacks));
            StackPolicy = ValidateStackPolicy(stackPolicy);
            _modifiers = CopyModifiers(modifiers);
            _readOnlyModifiers = new ReadOnlyCollection<BattleModifierInstance>(_modifiers);
            _triggers = CopyTriggers(triggers);
            _readOnlyTriggers = new ReadOnlyCollection<BattleTriggerInstance>(_triggers);
        }

        public string Id { get; }
        public StatusPolarity Polarity { get; }
        public int TickIntervalTicks { get; }
        public int PeriodicDamage { get; }
        public int MaxStacks { get; }
        public StatusStackPolicy StackPolicy { get; }
        public IReadOnlyList<BattleModifierInstance> Modifiers => _readOnlyModifiers ?? EmptyModifiers;
        public IReadOnlyList<BattleTriggerInstance> Triggers => _readOnlyTriggers ?? EmptyTriggers;

        private static readonly ReadOnlyCollection<BattleModifierInstance> EmptyModifiers = new ReadOnlyCollection<BattleModifierInstance>(Array.Empty<BattleModifierInstance>());
        private static readonly ReadOnlyCollection<BattleTriggerInstance> EmptyTriggers = new ReadOnlyCollection<BattleTriggerInstance>(Array.Empty<BattleTriggerInstance>());

        public static StatusRuntimeDefinition FromApplicationData(StatusApplicationData status)
        {
            var modifiers = new BattleModifierInstance[status.Modifiers.Count];
            for (var i = 0; i < status.Modifiers.Count; i++)
            {
                BattleModifierData modifier = status.Modifiers[i];
                modifiers[i] = modifier.Target == BattleModifierTarget.Stat
                    ? BattleModifierInstance.Stat(modifier.StatId, modifier.Operation, modifier.Value)
                    : BattleModifierInstance.Damage(modifier.DamageStat, modifier.Operation, modifier.Value);
            }

            BattleTriggerInstance[] triggers = CreateTriggerInstances(status.Triggers);
            return new StatusRuntimeDefinition(
                status.Id,
                status.Polarity,
                status.TickIntervalTicks,
                status.PeriodicDamage,
                status.MaxStacks,
                status.StackPolicy,
                modifiers,
                triggers);
        }

        private static BattleModifierInstance[] CopyModifiers(IReadOnlyList<BattleModifierInstance> modifiers)
        {
            if (modifiers == null)
            {
                throw new ArgumentNullException(nameof(modifiers));
            }

            var copy = new BattleModifierInstance[modifiers.Count];
            for (var i = 0; i < modifiers.Count; i++)
            {
                BattleModifierInstance modifier = modifiers[i];
                copy[i] = modifier.Target == BattleModifierTarget.Stat
                    ? BattleModifierInstance.Stat(modifier.StatId, modifier.Operation, modifier.Value)
                    : BattleModifierInstance.Damage(modifier.DamageStat, modifier.Operation, modifier.Value);
            }

            return copy;
        }

        private static BattleTriggerInstance[] CopyTriggers(IReadOnlyList<BattleTriggerInstance> triggers)
        {
            if (triggers == null)
            {
                throw new ArgumentNullException(nameof(triggers));
            }

            var copy = new BattleTriggerInstance[triggers.Count];
            for (var i = 0; i < triggers.Count; i++)
            {
                BattleTriggerInstance trigger = triggers[i];
                copy[i] = new BattleTriggerInstance(trigger.Timing, trigger.ConditionProgram, trigger.Effects);
            }

            return copy;
        }

        private static BattleTriggerInstance[] CreateTriggerInstances(IReadOnlyList<BattleTriggerData> triggers)
        {
            var instances = new BattleTriggerInstance[triggers.Count];
            for (var triggerIndex = 0; triggerIndex < triggers.Count; triggerIndex++)
            {
                BattleTriggerData trigger = triggers[triggerIndex];
                var effects = new BattleReactionEffectInstance[trigger.Effects.Count];
                for (var effectIndex = 0; effectIndex < trigger.Effects.Count; effectIndex++)
                {
                    BattleReactionEffectData effect = trigger.Effects[effectIndex];
                    effects[effectIndex] = BattleReactionEffectInstance.Create(effect.Target, effect.Effect);
                }

                instances[triggerIndex] = new BattleTriggerInstance(trigger.Timing, trigger.ConditionProgram, effects);
            }

            return instances;
        }

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
    }
}
