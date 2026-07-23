using Combat.Core.Battle;
using System.Collections.Generic;

namespace Combat.Tests.Core
{
    internal static class TestCombatants
    {
        public static CombatantDefinition Create(
            string id = "melee",
            int maxHealth = 10,
            float moveSpeed = 1f,
            float attackRange = 1f,
            int attackDamage = 1,
            int attackCooldownTicks = 1,
            float radius = 0.25f,
            AbilityDefinition[] abilities = null,
            AiDefinition aiDefinition = null)
        {
            return new CombatantDefinition(
                id,
                BattleScalar.FromFloat(radius),
                Stats(maxHealth, moveSpeed),
                Ability("basic-attack", attackRange, attackDamage, attackCooldownTicks),
                abilities ?? new AbilityDefinition[0],
                aiDefinition);
        }

        public static AbilityDefinition Ability(
            string id,
            float range,
            int damage,
            int cooldownTicks,
            StatusDefinition[] appliedStatuses = null,
            ProjectileEmitterSpawnData[] projectileEmitters = null,
            AbilityTargetSelection targetSelection = AbilityTargetSelection.CurrentEnemyTarget,
            int windupTicks = 0,
            int recoveryTicks = 0)
        {
            return new AbilityDefinition(
                id,
                BattleScalar.FromFloat(range),
                cooldownTicks,
                windupTicks,
                recoveryTicks,
                targetSelection,
                EffectFrames(windupTicks, Effects(damage, appliedStatuses, projectileEmitters)));
        }

        public static AbilityDefinition Ability(
            string id,
            float range,
            int cooldownTicks,
            IReadOnlyList<BattleEffectDefinition> effects,
            AbilityTargetSelection targetSelection = AbilityTargetSelection.CurrentEnemyTarget,
            int windupTicks = 0,
            int recoveryTicks = 0)
        {
            return new AbilityDefinition(
                id,
                BattleScalar.FromFloat(range),
                cooldownTicks,
                windupTicks,
                recoveryTicks,
                targetSelection,
                EffectFrames(windupTicks, effects));
        }

        public static AbilitySpawnData AbilitySpawn(
            string id,
            float range,
            int damage,
            int cooldownTicks,
            StatusApplicationData[] appliedStatuses = null,
            ProjectileEmitterSpawnData[] projectileEmitters = null,
            AbilityTargetSelection targetSelection = AbilityTargetSelection.CurrentEnemyTarget,
            int windupTicks = 0,
            int recoveryTicks = 0)
        {
            return new AbilitySpawnData(
                id,
                BattleScalar.FromFloat(range),
                cooldownTicks,
                windupTicks,
                recoveryTicks,
                targetSelection,
                EffectFrameData(windupTicks, EffectData(damage, appliedStatuses, projectileEmitters)));
        }

        public static AbilitySpawnData AbilitySpawn(
            string id,
            float range,
            int cooldownTicks,
            IReadOnlyList<BattleEffectData> effects,
            AbilityTargetSelection targetSelection = AbilityTargetSelection.CurrentEnemyTarget,
            int windupTicks = 0,
            int recoveryTicks = 0)
        {
            return new AbilitySpawnData(
                id,
                BattleScalar.FromFloat(range),
                cooldownTicks,
                windupTicks,
                recoveryTicks,
                targetSelection,
                EffectFrameData(windupTicks, effects));
        }

        public static AbilityEffectFrameDefinition[] EffectFrames(
            int tickOffset,
            IReadOnlyList<BattleEffectDefinition> effects,
            string frameId = "release",
            int order = 0)
        {
            return new[]
            {
                new AbilityEffectFrameDefinition(frameId, tickOffset, order, effects)
            };
        }

        public static AbilityEffectFrameData[] EffectFrameData(
            int tickOffset,
            IReadOnlyList<BattleEffectData> effects,
            string frameId = "release",
            int order = 0)
        {
            return new[]
            {
                new AbilityEffectFrameData(frameId, tickOffset, order, effects)
            };
        }

        public static BattleEffectDefinition[] Effects(
            int damage = 0,
            StatusDefinition[] statuses = null,
            ProjectileEmitterSpawnData[] projectileEmitters = null)
        {
            if (damage < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(damage));
            }

            int count = (damage > 0 ? 1 : 0) + (statuses?.Length ?? 0) + (projectileEmitters?.Length ?? 0);
            var effects = new BattleEffectDefinition[count];
            var index = 0;
            if (damage > 0)
            {
                effects[index++] = BattleEffectDefinition.Damage(damage);
            }

            if (statuses != null)
            {
                for (var i = 0; i < statuses.Length; i++)
                {
                    effects[index++] = BattleEffectDefinition.ApplyStatus(statuses[i]);
                }
            }

            if (projectileEmitters != null)
            {
                for (var i = 0; i < projectileEmitters.Length; i++)
                {
                    effects[index++] = BattleEffectDefinition.SpawnProjectileEmitter(projectileEmitters[i]);
                }
            }

            return effects;
        }

        public static BattleEffectData[] EffectData(
            int damage = 0,
            StatusApplicationData[] statuses = null,
            ProjectileEmitterSpawnData[] projectileEmitters = null)
        {
            if (damage < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(damage));
            }

            int count = (damage > 0 ? 1 : 0) + (statuses?.Length ?? 0) + (projectileEmitters?.Length ?? 0);
            var effects = new BattleEffectData[count];
            var index = 0;
            if (damage > 0)
            {
                effects[index++] = BattleEffectData.Damage(damage);
            }

            if (statuses != null)
            {
                for (var i = 0; i < statuses.Length; i++)
                {
                    effects[index++] = BattleEffectData.ApplyStatus(statuses[i]);
                }
            }

            if (projectileEmitters != null)
            {
                for (var i = 0; i < projectileEmitters.Length; i++)
                {
                    effects[index++] = BattleEffectData.SpawnProjectileEmitter(projectileEmitters[i]);
                }
            }

            return effects;
        }

        public static BattleStatBlock Stats(
            int maxHealth = 10,
            float moveSpeed = 1f,
            float attackRange = 1f,
            int attackDamage = 1,
            int attackCooldownTicks = 1)
        {
            return new BattleStatBlock(new[]
            {
                new BattleStatEntry(BattleStatId.MaxHealth, maxHealth),
                new BattleStatEntry(BattleStatId.MoveSpeed, moveSpeed)
            });
        }
    }
}
