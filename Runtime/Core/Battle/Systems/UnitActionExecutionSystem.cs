using System.Collections.Generic;
using Combat.Foundation.Events;

namespace Combat.Core.Battle
{
    internal static class UnitActionExecutionSystem
    {
        public static bool InterruptAction(
            BattleWorld world,
            EntityId entity,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            if (!world.UnitActionComponents.TryGet(entity, out UnitActionComponent action) ||
                !action.IsActive)
            {
                return false;
            }

            WriteAbilityEnded(world, entity, action, events, eventSequence, tick);
            world.UnitActionComponents.Set(entity, UnitActionComponent.None);
            return true;
        }

        public static void Run(BattleWorld world, EventBuffer<BattleEvent> events, EventSequence eventSequence, BattleTick tick)
        {
            IReadOnlyList<EntityId> entities = world.UnitActionComponents.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                EntityId entity = entities[i];
                if (!world.UnitActionComponents.TryGet(entity, out UnitActionComponent action) || !action.IsActive)
                {
                    continue;
                }

                if (!world.IsAliveUnit(entity))
                {
                    world.UnitActionComponents.Set(entity, UnitActionComponent.None);
                    continue;
                }

                if (action.Type == UnitActionType.Ability)
                {
                    action = RunAbilityAction(world, entity, action, events, eventSequence, tick);
                }

                if (action.IsActive && tick.Value >= action.EndTick.Value)
                {
                    WriteAbilityEnded(world, entity, action, events, eventSequence, tick);
                    action = UnitActionComponent.None;
                }

                world.UnitActionComponents.Set(entity, action);
            }
        }

        private static UnitActionComponent RunAbilityAction(
            BattleWorld world,
            EntityId source,
            UnitActionComponent action,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            if (tick.Value >= action.ReleaseTick.Value)
            {
                if (TryGetReleasableAbility(world, source, action, out AbilityState ability, out UnitId sourceUnitId, out UnitId targetUnitId, out BattleEffectSourceKind sourceKind))
                {
                    action = ReleaseDueEffectFrames(world, source, action, ability, sourceUnitId, targetUnitId, sourceKind, events, eventSequence, tick);
                }
                else
                {
                    action = action.WithReleased();
                }
            }

            return action;
        }

        private static UnitActionComponent ReleaseDueEffectFrames(
            BattleWorld world,
            EntityId source,
            UnitActionComponent action,
            AbilityState ability,
            UnitId sourceUnitId,
            UnitId targetUnitId,
            BattleEffectSourceKind sourceKind,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            IReadOnlyList<AbilityEffectFrameData> frames = ability.EffectFrames;
            var releasedFrameCount = action.ReleasedFrameCount;
            for (var frameIndex = releasedFrameCount; frameIndex < frames.Count; frameIndex++)
            {
                AbilityEffectFrameData frame = frames[frameIndex];
                if (tick.Value < action.StartedTick.Value + frame.TickOffset)
                {
                    break;
                }

                for (var effectIndex = 0; effectIndex < frame.Effects.Count; effectIndex++)
                {
                    BattleEffectData effect = frame.Effects[effectIndex];
                    world.CommandBuffer.QueueEffect(BattleEffectCommandFactory.Create(
                        source,
                        action.Target,
                        effect,
                        CreateEffectContext(action.AbilityId, sourceKind, effect.Type)));
                }

                if (events != null && eventSequence != null)
                {
                    events.Write(BattleEvent.AbilityReleased(eventSequence.Next(), tick, sourceUnitId, targetUnitId, action.AbilityId, sourceKind));
                }

                releasedFrameCount++;
            }

            return releasedFrameCount == action.ReleasedFrameCount
                ? action
                : action.WithReleasedFrameCount(releasedFrameCount);
        }

        private static bool TryGetReleasableAbility(
            BattleWorld world,
            EntityId source,
            UnitActionComponent action,
            out AbilityState ability,
            out UnitId sourceUnitId,
            out UnitId targetUnitId,
            out BattleEffectSourceKind sourceKind)
        {
            ability = default;
            sourceUnitId = default;
            targetUnitId = default;
            sourceKind = default;

            if (!world.TryGetUnitId(source, out sourceUnitId)
                || !world.AbilityComponents.TryGet(source, out AbilityComponent abilities)
                || action.AbilityIndex < 0
                || action.AbilityIndex >= abilities.Abilities.Count
                || !world.IsAliveUnit(action.Target)
                || !world.TryGetUnitId(action.Target, out targetUnitId))
            {
                return false;
            }

            ability = abilities.Abilities[action.AbilityIndex];
            sourceKind = GetAbilitySourceKind(action.AbilityIndex);
            return AbilityTargeting.IsValidExplicitTarget(world, source, action.Target, ability);
        }

        private static void WriteAbilityEnded(
            BattleWorld world,
            EntityId source,
            UnitActionComponent action,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            if (events == null || eventSequence == null || !world.TryGetUnitId(source, out UnitId sourceUnitId))
            {
                return;
            }

            world.TryGetUnitId(action.Target, out UnitId targetUnitId);
            events.Write(BattleEvent.AbilityEnded(
                eventSequence.Next(),
                tick,
                sourceUnitId,
                targetUnitId,
                action.AbilityId,
                GetAbilitySourceKind(action.AbilityIndex)));
        }

        private static BattleEffectContext CreateEffectContext(string abilityId, BattleEffectSourceKind sourceKind, BattleEffectType effectType)
        {
            return sourceKind == BattleEffectSourceKind.BasicAbility
                ? BattleEffectContext.BasicAbility(abilityId, effectType)
                : BattleEffectContext.Ability(abilityId, effectType);
        }

        private static BattleEffectSourceKind GetAbilitySourceKind(int abilityIndex)
        {
            return abilityIndex == AbilityComponent.BasicAbilityIndex
                ? BattleEffectSourceKind.BasicAbility
                : BattleEffectSourceKind.Ability;
        }
    }
}
