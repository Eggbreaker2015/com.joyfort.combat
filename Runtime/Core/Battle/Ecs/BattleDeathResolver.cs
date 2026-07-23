using Combat.Foundation.Events;

namespace Combat.Core.Battle
{
    internal static class BattleDeathResolver
    {
        public static void FlushDeathCheckCommands(
            BattleWorld world,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            DeathCheckCommand[] commands = world.CommandBuffer.DrainDeathCheckCommands();
            for (var i = 0; i < commands.Length; i++)
            {
                DeathCheckNow(world, commands[i], events, eventSequence, tick);
            }
        }

        private static void DeathCheckNow(
            BattleWorld world,
            DeathCheckCommand command,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            if (!world.IsEntityAlive(command.Entity)
                || !world.LifeStateComponents.TryGet(command.Entity, out LifeStateComponent lifeState)
                || lifeState.State == LifeState.Dead
                || !world.HealthComponents.TryGet(command.Entity, out HealthComponent health)
                || health.Current > 0)
            {
                return;
            }

            MarkUnitDeadNow(world, command, events, eventSequence, tick);
        }

        private static void MarkUnitDeadNow(
            BattleWorld world,
            DeathCheckCommand command,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            EntityId entity = command.Entity;
            if (!world.LifeStateComponents.TryGet(entity, out LifeStateComponent lifeState)
                || lifeState.State == LifeState.Dead
                || !world.TryGetUnitId(entity, out UnitId unitId)
                || !world.TryGetTeamId(entity, out TeamId teamId))
            {
                return;
            }

            world.LifeStateComponents.Set(entity, new LifeStateComponent(LifeState.Dead));
            events.Write(BattleEvent.UnitDied(eventSequence.Next(), tick, unitId, teamId));
            QueueAfterEnemyKilledTriggers(world, command, entity, teamId, tick);
        }

        private static void QueueAfterEnemyKilledTriggers(BattleWorld world, DeathCheckCommand command, EntityId deadUnit, TeamId deadTeamId, BattleTick tick)
        {
            if (!command.Source.IsValid
                || command.TriggerPolicy != BattleEffectTriggerPolicy.CanTriggerReactions
                || !world.TryGetTeamId(command.Source, out TeamId sourceTeamId)
                || sourceTeamId.Equals(deadTeamId))
            {
                return;
            }

            StatusTriggerResolver.QueueTriggers(
                world,
                tick,
                BattleTriggerContext.AfterEnemyKilled(
                    command.Source,
                    deadUnit,
                    command.EffectContext,
                    command.TriggerPolicy));
        }
    }
}
