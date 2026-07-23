using System.Collections.Generic;
using Combat.Foundation.Events;

namespace Combat.Core.Battle
{
    internal static class AbilitySystem
    {
        public static void Run(BattleWorld world)
        {
            Run(world, null, null, default);
        }

        public static void Run(BattleWorld world, EventBuffer<BattleEvent> events, EventSequence eventSequence, BattleTick tick)
        {
            IReadOnlyList<EntityId> entities = world.UnitComponents.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                EntityId entity = entities[i];
                if (!world.IsBattlefieldActiveUnit(entity)
                    || !world.AbilityComponents.TryGet(entity, out AbilityComponent abilities)
                    || abilities.Abilities.Count == 0)
                {
                    continue;
                }

                AbilityComponent updatedAbilities = abilities.TickCooldowns(out bool changedCooldown);

                if (UnitControlRules.CanStartAction(world, entity))
                {
                    if (BattleIntentFilters.TryGetUseAbility(world, entity, out BattleIntent useAbilityIntent))
                    {
                        TryQueueRequestedAbility(world, entity, updatedAbilities, useAbilityIntent, events, eventSequence, tick);
                    }
                    else if (BattleIntentFilters.AllowsAutoBehavior(world, entity)
                        && TrySelectReadyAbility(world, entity, updatedAbilities, out int selectedAbilityIndex, out EntityId selectedTarget))
                    {
                        QueueAbility(world, entity, selectedTarget, selectedAbilityIndex, events, eventSequence, tick);
                    }
                }

                if (changedCooldown)
                {
                    world.AbilityComponents.Set(entity, updatedAbilities);
                }
            }
        }

        private static bool TrySelectReadyAbility(BattleWorld world, EntityId entity, AbilityComponent abilities, out int abilityIndex, out EntityId target)
        {
            for (var i = AbilityComponent.FirstSkillAbilityIndex; i < abilities.Abilities.Count; i++)
            {
                if (CanUse(world, entity, abilities.Abilities[i], out target))
                {
                    abilityIndex = i;
                    return true;
                }
            }

            if (abilities.Abilities.Count > AbilityComponent.BasicAbilityIndex
                && CanUse(world, entity, abilities.Abilities[AbilityComponent.BasicAbilityIndex], out target))
            {
                abilityIndex = AbilityComponent.BasicAbilityIndex;
                return true;
            }

            abilityIndex = -1;
            target = default;
            return false;
        }

        private static bool CanUse(BattleWorld world, EntityId entity, AbilityState ability, out EntityId target)
        {
            if (ability.CooldownRemainingTicks > 0)
            {
                target = default;
                return false;
            }

            return AbilityTargeting.TrySelectTarget(world, entity, ability, out target);
        }

        private static bool TryQueueRequestedAbility(
            BattleWorld world,
            EntityId entity,
            AbilityComponent abilities,
            BattleIntent intent,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            int abilityIndex = intent.AbilityIndex;
            if (abilityIndex < 0 || abilityIndex >= abilities.Abilities.Count)
            {
                return false;
            }

            AbilityState ability = abilities.Abilities[abilityIndex];
            if (ability.CooldownRemainingTicks > 0
                || !AbilityTargeting.IsValidExplicitTarget(world, entity, intent.Target, ability))
            {
                return false;
            }

            QueueAbility(world, entity, intent.Target, abilityIndex, events, eventSequence, tick);
            return true;
        }

        private static void QueueAbility(
            BattleWorld world,
            EntityId entity,
            EntityId target,
            int abilityIndex,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            if (UnitControlRules.CanTurn(world, entity))
            {
                world.TryFaceUnitTowards(entity, target, events, eventSequence, tick);
            }

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(entity, target, abilityIndex));
        }
    }
}
