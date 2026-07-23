using System;

namespace Combat.Core.Battle
{
    internal static class BattleEffectCommandFactory
    {
        public static BattleEffectCommand Create(
            EntityId source,
            EntityId target,
            BattleEffectData effect,
            BattleEffectContext context,
            BattleEffectTriggerPolicy damageTriggerPolicy = BattleEffectTriggerPolicy.CanTriggerReactions)
        {
            return Create(source, target, effect, context, false, default, damageTriggerPolicy);
        }

        public static BattleEffectCommand Create(
            EntityId source,
            EntityId target,
            BattleEffectDefinition effect,
            BattleEffectContext context,
            BattleEffectTriggerPolicy damageTriggerPolicy = BattleEffectTriggerPolicy.CanTriggerReactions)
        {
            return Create(source, target, BattleEffectRuntimeDataFactory.CreateEffectData(effect), context, damageTriggerPolicy);
        }

        public static BattleEffectCommand CreateAt(
            EntityId source,
            EntityId target,
            BattleEffectData effect,
            BattleEffectContext context,
            BattleVector2 projectileEmitterOrigin,
            BattleEffectTriggerPolicy damageTriggerPolicy = BattleEffectTriggerPolicy.CanTriggerReactions)
        {
            return Create(source, target, effect, context, true, projectileEmitterOrigin, damageTriggerPolicy);
        }

        public static BattleEffectCommand CreateAt(
            EntityId source,
            EntityId target,
            BattleEffectDefinition effect,
            BattleEffectContext context,
            BattleVector2 projectileEmitterOrigin,
            BattleEffectTriggerPolicy damageTriggerPolicy = BattleEffectTriggerPolicy.CanTriggerReactions)
        {
            return CreateAt(source, target, BattleEffectRuntimeDataFactory.CreateEffectData(effect), context, projectileEmitterOrigin, damageTriggerPolicy);
        }

        private static BattleEffectCommand Create(
            EntityId source,
            EntityId target,
            BattleEffectData effect,
            BattleEffectContext context,
            bool hasProjectileEmitterOrigin,
            BattleVector2 projectileEmitterOrigin,
            BattleEffectTriggerPolicy damageTriggerPolicy)
        {
            switch (effect.Type)
            {
                case BattleEffectType.Damage:
                    return BattleEffectCommand.Damage(source, target, effect.Amount, context, damageTriggerPolicy);
                case BattleEffectType.Heal:
                    return BattleEffectCommand.Heal(source, target, effect.Amount, context);
                case BattleEffectType.AreaEffect:
                    return BattleEffectCommand.CreateAreaEffect(source, target, effect.AreaEffect, context, damageTriggerPolicy);
                case BattleEffectType.ApplyStatus:
                    return BattleEffectCommand.ApplyStatus(source, target, effect.Status, context);
                case BattleEffectType.SpawnProjectileEmitter:
                    return hasProjectileEmitterOrigin
                        ? BattleEffectCommand.SpawnProjectileEmitterAt(source, target, effect.ProjectileEmitter, projectileEmitterOrigin, context)
                        : BattleEffectCommand.SpawnProjectileEmitter(source, target, effect.ProjectileEmitter, context);
                default:
                    throw new ArgumentOutOfRangeException(nameof(effect), effect.Type, "Unsupported battle effect type.");
            }
        }
    }
}
