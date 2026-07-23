using System;
using System.Collections.Generic;

namespace Combat.Core.Battle
{
    internal static class StatusTriggerResolver
    {
        public static void QueueTriggers(BattleWorld world, BattleTick tick, BattleTriggerContext context)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (context.TriggerPolicy != BattleEffectTriggerPolicy.CanTriggerReactions
                || !world.StatusComponents.TryGet(context.Owner, out StatusComponent component)
                || !world.CanResolveReactionUnit(context.Owner))
            {
                return;
            }

            BattleConditionEvaluationContext conditionContext = BattleConditionEvaluationContext.FromTrigger(world, tick, context);
            IReadOnlyList<StatusInstance> statuses = component.Statuses;
            for (var statusIndex = 0; statusIndex < statuses.Count; statusIndex++)
            {
                StatusInstance status = statuses[statusIndex];
                IReadOnlyList<BattleTriggerInstance> triggers = status.Triggers;
                for (var triggerIndex = 0; triggerIndex < triggers.Count; triggerIndex++)
                {
                    BattleTriggerInstance trigger = triggers[triggerIndex];
                    if (trigger.Timing != context.Timing)
                    {
                        continue;
                    }

                    if (!BattleConditionProgramEvaluator.Evaluate(trigger.ConditionProgram, conditionContext))
                    {
                        continue;
                    }

                    QueueTriggerEffects(world, context, trigger.Effects);
                }
            }
        }

        private static void QueueTriggerEffects(BattleWorld world, BattleTriggerContext context, IReadOnlyList<BattleReactionEffectInstance> effects)
        {
            for (var effectIndex = 0; effectIndex < effects.Count; effectIndex++)
            {
                BattleReactionEffectInstance effect = effects[effectIndex];
                EntityId target = ResolveTarget(effect.Target, context);
                if (!world.CanResolveReactionUnit(target))
                {
                    continue;
                }

                BattleEffectData effectData = effect.Effect;
                world.CommandBuffer.QueueReactionEffect(BattleEffectCommandFactory.Create(
                    context.Owner,
                    target,
                    effectData,
                    BattleEffectContext.Reaction(context.EffectContext, effectData.Type),
                    BattleEffectTriggerPolicy.SuppressReactions));
            }
        }

        private static EntityId ResolveTarget(BattleReactionTarget target, BattleTriggerContext context)
        {
            switch (target)
            {
                case BattleReactionTarget.Self:
                    return context.Owner;
                case BattleReactionTarget.Source:
                    return context.Source;
                case BattleReactionTarget.Target:
                    return context.Target;
                default:
                    throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported reaction target.");
            }
        }
    }
}
