using System.Collections.Generic;
using Combat.Foundation.Events;

namespace Combat.Core.Battle
{
    internal static class InputIntentSystem
    {
        public static void Run(
            BattleWorld world,
            BattleInputFrame inputFrame,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            RemoveInvalidOrTransientIntents(world);
            ApplyInputCommands(world, inputFrame, events, eventSequence, tick);
            FillDefaultAutoIntents(world);
        }

        private static void RemoveInvalidOrTransientIntents(BattleWorld world)
        {
            IReadOnlyList<EntityId> entities = world.IntentComponents.Entities;
            for (var i = entities.Count - 1; i >= 0; i--)
            {
                EntityId entity = entities[i];
                if (!world.IsAliveUnit(entity)
                    || IsTransientIntent(world.IntentComponents.Get(entity).Intent))
                {
                    world.IntentComponents.Remove(entity);
                }
            }
        }

        private static bool IsTransientIntent(BattleIntent intent)
        {
            return intent.Type == BattleIntentType.UseAbility;
        }

        private static void ApplyInputCommands(
            BattleWorld world,
            BattleInputFrame inputFrame,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            IReadOnlyList<BattleInputCommand> commands = inputFrame.Commands;
            for (var i = 0; i < commands.Count; i++)
            {
                BattleInputCommand command = commands[i];
                switch (command.Type)
                {
                    case BattleInputCommandType.Auto:
                        ApplyAutoCommand(world, command, events, eventSequence, tick);
                        break;
                    case BattleInputCommandType.Hold:
                        ApplyHoldCommand(world, command, events, eventSequence, tick);
                        break;
                    case BattleInputCommandType.MoveToPosition:
                        ApplyMoveToPositionCommand(world, command, events, eventSequence, tick);
                        break;
                    case BattleInputCommandType.Garrison:
                        ApplyGarrisonCommand(world, command, events, eventSequence, tick);
                        break;
                    case BattleInputCommandType.FocusTarget:
                        ApplyFocusTargetCommand(world, command);
                        break;
                    case BattleInputCommandType.UseAbility:
                        ApplyUseAbilityCommand(world, command);
                        break;
                    default:
                        throw new System.ArgumentOutOfRangeException(nameof(inputFrame), command.Type, "Unsupported battle input command type.");
                }
            }
        }

        private static void ApplyAutoCommand(
            BattleWorld world,
            BattleInputCommand command,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            if (!TryFindAliveUnit(world, command.UnitId, out EntityId entity))
            {
                return;
            }

            bool wasGarrisoned = world.GarrisonedComponents.Has(entity);
            world.GarrisonedComponents.Remove(entity);
            world.IntentComponents.Set(entity, new IntentComponent(BattleIntent.Auto(entity)));
            if (wasGarrisoned)
            {
                WriteGarrisonStateChanged(world, entity, false, events, eventSequence, tick);
            }
        }

        private static void ApplyHoldCommand(
            BattleWorld world,
            BattleInputCommand command,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            if (!TryFindAliveUnit(world, command.UnitId, out EntityId source))
            {
                return;
            }

            UnitActionExecutionSystem.InterruptAction(
                world, source, events, eventSequence, tick);
            TargetingSystem.ClearAutomaticTarget(world, source);
            world.IntentComponents.Set(source, new IntentComponent(BattleIntent.Hold(source)));
        }

        private static void ApplyMoveToPositionCommand(
            BattleWorld world,
            BattleInputCommand command,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            if (!TryFindAliveUnit(world, command.UnitId, out EntityId source))
            {
                return;
            }

            UnitActionExecutionSystem.InterruptAction(
                world, source, events, eventSequence, tick);
            TargetingSystem.ClearAutomaticTarget(world, source);
            bool wasGarrisoned = world.GarrisonedComponents.Has(source);
            world.GarrisonedComponents.Remove(source);
            world.IntentComponents.Set(source, new IntentComponent(BattleIntent.MoveToPosition(
                source,
                command.Destination)));
            if (wasGarrisoned)
            {
                WriteGarrisonStateChanged(world, source, false, events, eventSequence, tick);
            }
        }

        private static void ApplyFocusTargetCommand(BattleWorld world, BattleInputCommand command)
        {
            if (!TryFindAliveUnit(world, command.UnitId, out EntityId source))
            {
                return;
            }

            TryFindAliveUnit(world, command.TargetUnitId, out EntityId target);
            world.IntentComponents.Set(source, new IntentComponent(BattleIntent.FocusTarget(source, target)));
        }

        private static void ApplyGarrisonCommand(
            BattleWorld world,
            BattleInputCommand command,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            if (!TryFindAliveUnit(world, command.UnitId, out EntityId source))
            {
                return;
            }

            UnitActionExecutionSystem.InterruptAction(
                world, source, events, eventSequence, tick);
            bool wasGarrisoned = world.GarrisonedComponents.Has(source);
            TargetingSystem.ClearAutomaticTarget(world, source);
            world.TargetComponents.Remove(source);
            world.IntentComponents.Set(source, new IntentComponent(BattleIntent.Hold(source)));
            world.GarrisonedComponents.Set(source, default);
            if (!wasGarrisoned)
            {
                WriteGarrisonStateChanged(world, source, true, events, eventSequence, tick);
            }
        }

        private static void WriteGarrisonStateChanged(
            BattleWorld world,
            EntityId entity,
            bool isGarrisoned,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            if (events == null
                || eventSequence == null
                || !world.TryGetUnitId(entity, out UnitId unitId)
                || !world.TryGetTeamId(entity, out TeamId teamId))
            {
                return;
            }

            events.Write(isGarrisoned
                ? BattleEvent.UnitGarrisoned(eventSequence.Next(), tick, unitId, teamId)
                : BattleEvent.UnitDeployed(eventSequence.Next(), tick, unitId, teamId));
        }

        private static void ApplyUseAbilityCommand(BattleWorld world, BattleInputCommand command)
        {
            if (!TryFindAliveUnit(world, command.UnitId, out EntityId source))
            {
                return;
            }

            TryFindAliveUnit(world, command.TargetUnitId, out EntityId target);
            world.IntentComponents.Set(source, new IntentComponent(BattleIntent.UseAbility(source, command.AbilityIndex, target)));
        }

        private static bool TryFindAliveUnit(BattleWorld world, UnitId unitId, out EntityId entity)
        {
            return world.TryFindEntity(unitId, out entity) && world.IsAliveUnit(entity);
        }

        private static void FillDefaultAutoIntents(BattleWorld world)
        {
            IReadOnlyList<EntityId> entities = world.UnitComponents.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                EntityId entity = entities[i];
                if (world.IsAliveUnit(entity) && !world.IntentComponents.Has(entity))
                {
                    world.IntentComponents.Set(entity, new IntentComponent(BattleIntent.Auto(entity)));
                }
            }
        }
    }
}
