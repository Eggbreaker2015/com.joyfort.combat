using System;

namespace Combat.Core.Battle
{
    internal static class BattleEffectRuntimeDataFactory
    {
        public static BattleReactionEffectData CreateReactionEffectData(BattleReactionEffectDefinition effect)
        {
            if (effect == null)
            {
                throw new ArgumentNullException(nameof(effect));
            }

            return BattleReactionEffectData.Create(effect.Target, CreateEffectData(effect.Effect));
        }

        public static BattleEffectData CreateEffectData(BattleEffectDefinition effect)
        {
            if (effect == null)
            {
                throw new ArgumentNullException(nameof(effect));
            }

            switch (effect.Type)
            {
                case BattleEffectType.Damage:
                    return BattleEffectData.Damage(effect.Amount);
                case BattleEffectType.Heal:
                    return BattleEffectData.Heal(effect.Amount);
                case BattleEffectType.ApplyStatus:
                    return BattleEffectData.ApplyStatus(StatusApplicationDataFactory.Create(effect.Status));
                case BattleEffectType.SpawnProjectileEmitter:
                    return BattleEffectData.SpawnProjectileEmitter(effect.ProjectileEmitter);
                case BattleEffectType.AreaEffect:
                    return BattleEffectData.CreateAreaEffect(CreateAreaEffectData(effect.Area));
                default:
                    throw new ArgumentOutOfRangeException(nameof(effect), effect.Type, "Unsupported battle effect type.");
            }
        }

        private static AreaEffectData CreateAreaEffectData(AreaEffectDefinition areaEffect)
        {
            if (areaEffect == null)
            {
                throw new ArgumentNullException(nameof(areaEffect));
            }

            var effects = new BattleEffectData[areaEffect.Effects.Count];
            for (var effectIndex = 0; effectIndex < areaEffect.Effects.Count; effectIndex++)
            {
                effects[effectIndex] = CreateEffectData(areaEffect.Effects[effectIndex]);
            }

            return new AreaEffectData(areaEffect.Radius, areaEffect.TargetFilter, effects);
        }
    }
}
