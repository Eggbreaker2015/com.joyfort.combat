using System;

namespace Combat.Core.Battle
{
    internal static class StatusApplicationDataFactory
    {
        public static StatusApplicationData Create(StatusDefinition status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            var modifiers = new BattleModifierData[status.Modifiers.Count];
            for (var modifierIndex = 0; modifierIndex < status.Modifiers.Count; modifierIndex++)
            {
                BattleModifierDefinition modifier = status.Modifiers[modifierIndex];
                modifiers[modifierIndex] = modifier.Target == BattleModifierTarget.Stat
                    ? BattleModifierData.Stat(modifier.StatId, modifier.Operation, modifier.Value)
                    : BattleModifierData.Damage(modifier.DamageStat, modifier.Operation, modifier.Value);
            }

            var triggers = new BattleTriggerData[status.Triggers.Count];
            for (var triggerIndex = 0; triggerIndex < status.Triggers.Count; triggerIndex++)
            {
                BattleTriggerDefinition trigger = status.Triggers[triggerIndex];
                var effects = new BattleReactionEffectData[trigger.Effects.Count];
                for (var effectIndex = 0; effectIndex < trigger.Effects.Count; effectIndex++)
                {
                    effects[effectIndex] = BattleEffectRuntimeDataFactory.CreateReactionEffectData(trigger.Effects[effectIndex]);
                }

                triggers[triggerIndex] = new BattleTriggerData(trigger.Timing, trigger.ConditionProgram, effects);
            }

            return new StatusApplicationData(
                status.Id,
                status.Polarity,
                status.DurationTicks,
                status.TickIntervalTicks,
                status.PeriodicDamage,
                modifiers,
                triggers,
                status.MaxStacks,
                status.StackPolicy);
        }
    }
}
