using System;
using System.Collections.Generic;
using Combat.Foundation.Events;

namespace Combat.Core.Battle
{
    internal static class BattleSpawnResolver
    {
        public static void FlushSpawnCombatantCommands(
            BattleWorld world,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            IReadOnlyList<SpawnCombatantCommand> commands = world.CommandBuffer.SpawnCombatantCommands;
            ValidateSpawnCombatantCommands(world, commands);

            for (var i = 0; i < commands.Count; i++)
            {
                SpawnCombatantNow(world, commands[i], events, eventSequence, tick);
            }

            world.CommandBuffer.ClearSpawnCombatantCommands();
        }

        public static void FlushSpawnProjectileCommands(
            BattleWorld world,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            IReadOnlyList<SpawnProjectileCommand> commands = world.CommandBuffer.SpawnProjectileCommands;
            for (var i = 0; i < commands.Count; i++)
            {
                SpawnProjectileNow(world, commands[i], events, eventSequence, tick);
            }

            world.CommandBuffer.ClearSpawnProjectileCommands();
        }

        private static void ValidateSpawnCombatantCommands(BattleWorld world, IReadOnlyList<SpawnCombatantCommand> commands)
        {
            var pendingUnitIds = new HashSet<UnitId>();
            for (var i = 0; i < commands.Count; i++)
            {
                UnitId unitId = commands[i].UnitId;
                if (world.TryFindEntity(unitId, out _))
                {
                    throw new InvalidOperationException($"UnitId already exists in BattleWorld: {unitId}.");
                }

                if (!pendingUnitIds.Add(unitId))
                {
                    throw new InvalidOperationException($"Duplicate spawn UnitId in command buffer: {unitId}.");
                }
            }
        }

        private static void SpawnCombatantNow(
            BattleWorld world,
            SpawnCombatantCommand command,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            CombatantSpawnData spawn = command.Spawn;
            EntityId entity = world.CreateEntity();

            world.UnitComponents.Add(entity, new UnitComponent(command.UnitId, spawn.DefinitionId));
            world.TeamComponents.Add(entity, new TeamComponent(spawn.TeamId));
            world.PositionComponents.Add(entity, new PositionComponent(spawn.Position, spawn.Radius));
            world.FacingComponents.Add(entity, new FacingComponent(BattleVector2.Right));
            world.BaseStatsComponents.Add(entity, new BaseStatsComponent(spawn.BaseStats));
            world.HealthComponents.Add(entity, new HealthComponent(BattleStatResolver.ResolveMaxHealth(world, entity)));
            world.LifeStateComponents.Add(entity, new LifeStateComponent(LifeState.Alive));
            if (spawn.Brain.HasBrain)
            {
                world.BrainComponents.Add(entity, new BrainComponent(spawn.Brain.DefinitionId, spawn.Brain.Kind, BrainState.Idle, tick));
            }

            world.TargetComponents.Add(entity, new TargetComponent(default));
            world.TargetingBehaviorComponents.Add(
                entity,
                new TargetingBehaviorComponent(spawn.TargetingBehavior));
            world.TargetingStateComponents.Add(entity, default);
            world.AbilityComponents.Add(entity, CreateAbilityComponent(spawn.BasicAbility, spawn.Abilities));
            world.UnitActionComponents.Add(entity, UnitActionComponent.None);

            events.Write(BattleEvent.UnitSpawned(eventSequence.Next(), tick, command.UnitId, spawn.TeamId, spawn.DefinitionId, spawn.Position, world.FacingComponents.Get(entity).Direction));
        }

        private static void SpawnProjectileNow(
            BattleWorld world,
            SpawnProjectileCommand command,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            if (!world.IsAliveUnit(command.Source)
                || !world.TryGetUnitId(command.Source, out UnitId sourceUnitId))
            {
                return;
            }

            ProjectilePayload payload = command.Payload;
            EntityId entity = world.CreateEntity();
            ProjectileId projectileId = world.AllocateProjectileId();
            var component = new ProjectileComponent(
                projectileId,
                command.Source,
                command.TeamId,
                command.Position,
                command.Velocity,
                payload.Radius,
                payload.LifetimeTicks,
                payload.Behavior,
                payload.HitPolicy,
                payload.ImpactEffectData,
                command.ActivateOnTick);

            world.ProjectileComponents.Add(entity, component);

            events.Write(BattleEvent.ProjectileSpawned(eventSequence.Next(), tick, projectileId, command.TeamId, sourceUnitId, command.Position));
        }

        private static AbilityComponent CreateAbilityComponent(AbilitySpawnData basicAbility, IReadOnlyList<AbilitySpawnData> abilities)
        {
            var states = new AbilityState[abilities.Count + 1];
            states[AbilityComponent.BasicAbilityIndex] = CreateAbilityState(basicAbility);
            for (var i = 0; i < abilities.Count; i++)
            {
                states[i + AbilityComponent.FirstSkillAbilityIndex] = CreateAbilityState(abilities[i]);
            }

            return new AbilityComponent(states);
        }

        private static AbilityState CreateAbilityState(AbilitySpawnData ability)
        {
            return new AbilityState(
                ability.Id,
                ability.Range,
                ability.CooldownTicks,
                0,
                ability.WindupTicks,
                ability.RecoveryTicks,
                ability.TargetSelection,
                ability.EffectFrames,
                ability.ActionLocks);
        }
    }
}
