using System;
using System.Collections.Generic;
using Combat.Foundation.Events;

namespace Combat.Core.Battle
{
    internal static class BattleActionResolver
    {
        public static void FlushActionCommands(
            BattleWorld world,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            var commands = world.CommandBuffer.ActionCommands;
            for (var i = 0; i < commands.Count; i++)
            {
                ResolveActionNow(world, commands[i], events, eventSequence, tick);
            }

            world.CommandBuffer.ClearActionCommands();
        }

        private static void ResolveActionNow(
            BattleWorld world,
            BattleActionCommand command,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            switch (command.Type)
            {
                case BattleActionType.UseAbility:
                    ResolveUseAbilityNow(world, command, events, eventSequence, tick);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported battle action type: {command.Type}");
            }
        }

        private static void ResolveUseAbilityNow(
            BattleWorld world,
            BattleActionCommand command,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            if (!world.IsAliveUnit(command.Source)
                || !world.IsAliveUnit(command.Target)
                || !UnitControlRules.CanStartAction(world, command.Source)
                || !world.TryGetUnitId(command.Source, out UnitId sourceUnitId)
                || !world.TryGetUnitId(command.Target, out UnitId targetUnitId)
                || !world.AbilityComponents.TryGet(command.Source, out AbilityComponent abilities)
                || command.AbilityIndex < 0
                || command.AbilityIndex >= abilities.Abilities.Count)
            {
                return;
            }

            AbilityState ability = abilities.Abilities[command.AbilityIndex];
            BattleEffectSourceKind sourceKind = command.AbilityIndex == AbilityComponent.BasicAbilityIndex
                ? BattleEffectSourceKind.BasicAbility
                : BattleEffectSourceKind.Ability;
            if (ability.CooldownRemainingTicks > 0)
            {
                return;
            }

            if (!AbilityTargeting.IsValidExplicitTarget(world, command.Source, command.Target, ability))
            {
                return;
            }

            int nextCooldown = ability.CooldownTicks > 0 ? ability.CooldownTicks - 1 : 0;
            BattleTick releaseTick = new BattleTick(tick.Value + GetFirstEffectFrameOffset(ability));
            BattleTick endTick = new BattleTick(tick.Value + GetLastEffectFrameOffset(ability) + ability.RecoveryTicks);
            BattleActionLocks locks = ability.ActionLocks;
            world.AbilityComponents.Set(command.Source, abilities.WithAbilityCooldownRemainingTicks(command.AbilityIndex, nextCooldown));
            world.UnitActionComponents.Set(
                command.Source,
                UnitActionComponent.Ability(
                    command.AbilityIndex,
                    ability.Id,
                    command.Target,
                    tick,
                    releaseTick,
                    endTick,
                    locks));
            if (events != null && eventSequence != null)
            {
                events.Write(BattleEvent.AbilityStarted(
                    eventSequence.Next(),
                    tick,
                    sourceUnitId,
                    targetUnitId,
                    ability.Id,
                    sourceKind,
                    locks));
            }
        }

        private static int GetFirstEffectFrameOffset(AbilityState ability)
        {
            IReadOnlyList<AbilityEffectFrameData> frames = ability.EffectFrames;
            var first = frames[0].TickOffset;
            for (var i = 1; i < frames.Count; i++)
            {
                if (frames[i].TickOffset < first)
                {
                    first = frames[i].TickOffset;
                }
            }

            return first;
        }

        private static int GetLastEffectFrameOffset(AbilityState ability)
        {
            IReadOnlyList<AbilityEffectFrameData> frames = ability.EffectFrames;
            var last = frames[0].TickOffset;
            for (var i = 1; i < frames.Count; i++)
            {
                if (frames[i].TickOffset > last)
                {
                    last = frames[i].TickOffset;
                }
            }

            return last;
        }
    }
}
