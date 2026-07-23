#if UNITY_EDITOR
using System.Collections.Generic;
using Combat.Core.Battle;
using Combat.Unity.Authoring;

namespace Combat.Unity.Editor
{
    public static partial class BattleAuthoringValidator
    {
        private static void CollectScenarioGraph(
            BattleScenarioAsset scenario,
            List<CombatantConfigAsset> combatants,
            List<AbilityConfigAsset> abilities,
            List<StatusConfigAsset> statuses,
            List<ProjectileEmitterConfigAsset> projectileEmitters,
            List<AreaEffectConfigAsset> areaEffects)
        {
            IReadOnlyList<SpawnEntry> spawns = scenario.InitialSpawns;
            for (var i = 0; i < spawns.Count; i++)
            {
                CollectCombatant(spawns[i].Combatant, combatants, abilities, statuses, projectileEmitters, areaEffects);
            }
        }

        private static void CollectCombatant(
            CombatantConfigAsset combatant,
            List<CombatantConfigAsset> combatants,
            List<AbilityConfigAsset> abilities,
            List<StatusConfigAsset> statuses,
            List<ProjectileEmitterConfigAsset> projectileEmitters,
            List<AreaEffectConfigAsset> areaEffects)
        {
            if (combatant == null || combatants.Contains(combatant))
            {
                return;
            }

            combatants.Add(combatant);
            CollectAbility(combatant.BasicAbility, abilities, statuses, projectileEmitters, areaEffects);
            IReadOnlyList<AbilityConfigAsset> combatantAbilities = combatant.Abilities;
            for (var i = 0; i < combatantAbilities.Count; i++)
            {
                CollectAbility(combatantAbilities[i], abilities, statuses, projectileEmitters, areaEffects);
            }
        }

        private static void CollectAbility(
            AbilityConfigAsset ability,
            List<AbilityConfigAsset> abilities,
            List<StatusConfigAsset> statuses,
            List<ProjectileEmitterConfigAsset> projectileEmitters,
            List<AreaEffectConfigAsset> areaEffects)
        {
            if (ability == null || abilities.Contains(ability))
            {
                return;
            }

            abilities.Add(ability);
            CollectAbilityEffectFrames(ability.EffectFrames, statuses, projectileEmitters, areaEffects);
        }

        private static void CollectAbilityEffectFrames(
            IReadOnlyList<AbilityEffectFrameConfig> frames,
            List<StatusConfigAsset> statuses,
            List<ProjectileEmitterConfigAsset> projectileEmitters,
            List<AreaEffectConfigAsset> areaEffects)
        {
            for (var frameIndex = 0; frameIndex < frames.Count; frameIndex++)
            {
                AbilityEffectFrameConfig frame = frames[frameIndex];
                if (frame != null)
                {
                    CollectBattleEffects(frame.Effects, statuses, projectileEmitters, areaEffects);
                }
            }
        }

        private static void CollectBattleEffects(
            IReadOnlyList<BattleEffectConfig> effects,
            List<StatusConfigAsset> statuses,
            List<ProjectileEmitterConfigAsset> projectileEmitters,
            List<AreaEffectConfigAsset> areaEffects)
        {
            for (var effectIndex = 0; effectIndex < effects.Count; effectIndex++)
            {
                BattleEffectConfig effect = effects[effectIndex];
                if (effect.Type == BattleEffectType.ApplyStatus)
                {
                    CollectStatus(effect.Status, statuses, projectileEmitters, areaEffects);
                }
                else if (effect.Type == BattleEffectType.SpawnProjectileEmitter)
                {
                    CollectProjectileEmitterAsset(effect.ProjectileEmitter, statuses, projectileEmitters, areaEffects);
                }
                else if (effect.Type == BattleEffectType.AreaEffect)
                {
                    CollectAreaEffectAsset(effect.AreaEffect, statuses, projectileEmitters, areaEffects);
                }
            }
        }

        private static void CollectProjectileEmitterAsset(
            ProjectileEmitterConfigAsset projectileEmitter,
            List<StatusConfigAsset> statuses,
            List<ProjectileEmitterConfigAsset> projectileEmitters,
            List<AreaEffectConfigAsset> areaEffects)
        {
            if (projectileEmitter == null || projectileEmitters.Contains(projectileEmitter))
            {
                return;
            }

            projectileEmitters.Add(projectileEmitter);
            if (projectileEmitter.Projectile != null)
            {
                CollectBattleEffects(projectileEmitter.Projectile.ImpactEffects, statuses, projectileEmitters, areaEffects);
            }
        }

        private static void CollectAreaEffectAsset(
            AreaEffectConfigAsset areaEffect,
            List<StatusConfigAsset> statuses,
            List<ProjectileEmitterConfigAsset> projectileEmitters,
            List<AreaEffectConfigAsset> areaEffects)
        {
            if (areaEffect == null || areaEffects.Contains(areaEffect))
            {
                return;
            }

            areaEffects.Add(areaEffect);
            CollectBattleEffects(areaEffect.Effects, statuses, projectileEmitters, areaEffects);
        }

        private static void CollectStatus(
            StatusConfigAsset status,
            List<StatusConfigAsset> statuses,
            List<ProjectileEmitterConfigAsset> projectileEmitters,
            List<AreaEffectConfigAsset> areaEffects)
        {
            if (status == null || statuses.Contains(status))
            {
                return;
            }

            statuses.Add(status);
            IReadOnlyList<StatusTriggerConfig> triggers = status.Triggers;
            for (var triggerIndex = 0; triggerIndex < triggers.Count; triggerIndex++)
            {
                CollectStatusConditionReferences(triggers[triggerIndex].Conditions, statuses, projectileEmitters, areaEffects);
                IReadOnlyList<StatusReactionEffectConfig> effects = triggers[triggerIndex].Effects;
                for (var effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                {
                    StatusReactionEffectConfig effect = effects[effectIndex];
                    if (effect.Effect.Type == BattleEffectType.ApplyStatus)
                    {
                        CollectStatus(effect.Effect.Status, statuses, projectileEmitters, areaEffects);
                    }
                    else if (effect.Effect.Type == BattleEffectType.SpawnProjectileEmitter)
                    {
                        CollectProjectileEmitterAsset(effect.Effect.ProjectileEmitter, statuses, projectileEmitters, areaEffects);
                    }
                    else if (effect.Effect.Type == BattleEffectType.AreaEffect)
                    {
                        CollectAreaEffectAsset(effect.Effect.AreaEffect, statuses, projectileEmitters, areaEffects);
                    }
                }
            }
        }

        private static void CollectStatusConditionReferences(
            IReadOnlyList<BattleConditionConfig> conditions,
            List<StatusConfigAsset> statuses,
            List<ProjectileEmitterConfigAsset> projectileEmitters,
            List<AreaEffectConfigAsset> areaEffects)
        {
            for (var conditionIndex = 0; conditionIndex < conditions.Count; conditionIndex++)
            {
                BattleConditionConfig condition = conditions[conditionIndex];
                CollectStatusConditionOperandReferences(condition.Left, statuses, projectileEmitters, areaEffects);
                CollectStatusConditionOperandReferences(condition.Right, statuses, projectileEmitters, areaEffects);
            }
        }

        private static void CollectStatusConditionOperandReferences(
            BattleConditionOperandConfig operand,
            List<StatusConfigAsset> statuses,
            List<ProjectileEmitterConfigAsset> projectileEmitters,
            List<AreaEffectConfigAsset> areaEffects)
        {
            StatusConfigAsset referencedStatus = operand?.ReferencedStatus;
            if (referencedStatus != null)
            {
                CollectStatus(referencedStatus, statuses, projectileEmitters, areaEffects);
            }
        }
    }
}
#endif
