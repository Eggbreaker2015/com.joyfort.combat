using System;
using System.Collections.Generic;
using Combat.Foundation.Events;

namespace Combat.Core.Battle
{
    internal static class BattleEffectResolver
    {
        private static readonly StatusInstance[] EmptyStatuses = Array.Empty<StatusInstance>();

        private enum BattleEffectExecutionKind
        {
            Primary,
            Reaction
        }

        public static void FlushEffectCommands(
            BattleWorld world,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            FlushPrimaryEffectCommands(world, events, eventSequence, tick);
            FlushReactionEffectCommands(world, events, eventSequence, tick);
            BattleDeathResolver.FlushDeathCheckCommands(world, events, eventSequence, tick);
            FlushReactionEffectCommands(world, events, eventSequence, tick);
            BattleDeathResolver.FlushDeathCheckCommands(world, events, eventSequence, tick);
        }

        public static bool CanResolveReactionUnit(BattleWorld world, EntityId entity)
        {
            return IsReactionEffectParticipant(world, entity);
        }

        private static void FlushPrimaryEffectCommands(
            BattleWorld world,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            BattleEffectCommand[] commands = world.CommandBuffer.DrainEffectCommands();
            for (var i = 0; i < commands.Length; i++)
            {
                ApplyEffectNow(world, commands[i], BattleEffectExecutionKind.Primary, events, eventSequence, tick);
            }
        }

        private static void FlushReactionEffectCommands(
            BattleWorld world,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            BattleEffectCommand[] commands = world.CommandBuffer.DrainReactionEffectCommands();
            for (var i = 0; i < commands.Length; i++)
            {
                ApplyEffectNow(world, commands[i], BattleEffectExecutionKind.Reaction, events, eventSequence, tick);
            }
        }

        private static void ApplyEffectNow(
            BattleWorld world,
            BattleEffectCommand command,
            BattleEffectExecutionKind executionKind,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            switch (command.Type)
            {
                case BattleEffectType.Damage:
                    ApplyDamageEffectNow(world, command, executionKind, events, eventSequence, tick);
                    break;
                case BattleEffectType.Heal:
                    ApplyHealEffectNow(world, command, executionKind, events, eventSequence, tick);
                    break;
                case BattleEffectType.AreaEffect:
                    ApplyAreaEffectNow(world, command, executionKind, events, eventSequence, tick);
                    break;
                case BattleEffectType.ApplyStatus:
                    ApplyStatusEffectNow(world, command, executionKind, events, eventSequence, tick);
                    break;
                case BattleEffectType.SpawnProjectileEmitter:
                    SpawnProjectileEmitterEffectNow(world, command, tick);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported battle effect type: {command.Type}");
            }
        }

        private static void ApplyHealEffectNow(
            BattleWorld world,
            BattleEffectCommand command,
            BattleEffectExecutionKind executionKind,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            if (!CanApplyUnitEffect(world, command.Source, command.Target, executionKind)
                || !world.HealthComponents.TryGet(command.Target, out HealthComponent targetHealth)
                || !world.TryGetUnitId(command.Source, out UnitId sourceUnitId)
                || !world.TryGetUnitId(command.Target, out UnitId targetUnitId))
            {
                return;
            }

            int maxHealth = BattleStatResolver.ResolveMaxHealth(world, command.Target);
            int missingHealth = maxHealth - targetHealth.Current;
            int actualHeal = missingHealth <= command.Amount ? missingHealth : command.Amount;
            if (actualHeal <= 0)
            {
                return;
            }

            world.HealthComponents.Set(command.Target, new HealthComponent(targetHealth.Current + actualHeal));
            events.Write(BattleEvent.HealingApplied(eventSequence.Next(), tick, sourceUnitId, targetUnitId, actualHeal, command.Context));
        }

        private static void ApplyAreaEffectNow(
            BattleWorld world,
            BattleEffectCommand command,
            BattleEffectExecutionKind executionKind,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            if (!CanApplyEffectToUnit(world, command.Source, executionKind)
                || !world.IsAliveUnit(command.Target)
                || !world.PositionComponents.TryGet(command.Target, out PositionComponent centerPosition)
                || !world.TryGetTeamId(command.Source, out TeamId sourceTeamId))
            {
                return;
            }

            EntityId[] targets = CollectAreaEffectTargets(world, centerPosition.Position, sourceTeamId, command.AreaEffect);
            IReadOnlyList<BattleEffectData> effects = command.AreaEffect.Effects;
            for (var targetIndex = 0; targetIndex < targets.Length; targetIndex++)
            {
                EntityId areaTarget = targets[targetIndex];
                for (var effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                {
                    BattleEffectData childEffect = effects[effectIndex];
                    BattleEffectCommand childCommand = BattleEffectCommandFactory.Create(
                        command.Source,
                        areaTarget,
                        childEffect,
                        command.Context.WithEffectType(childEffect.Type),
                        command.TriggerPolicy);
                    ApplyEffectNow(world, childCommand, executionKind, events, eventSequence, tick);
                }
            }
        }

        private static EntityId[] CollectAreaEffectTargets(BattleWorld world, BattleVector2 center, TeamId sourceTeamId, AreaEffectData areaEffect)
        {
            var candidates = new List<BattleUnitQueryResult>();
            BattleUnitQuery.CollectAliveUnitsInRadius(
                world,
                sourceTeamId,
                BattleUnitQuery.FromAreaEffectTargetFilter(areaEffect.TargetFilter),
                center,
                areaEffect.Radius,
                candidates);

            var targets = new EntityId[candidates.Count];
            for (var i = 0; i < candidates.Count; i++)
            {
                targets[i] = candidates[i].Entity;
            }

            return targets;
        }

        private static void SpawnProjectileEmitterEffectNow(BattleWorld world, BattleEffectCommand command, BattleTick tick)
        {
            if (!world.IsAliveUnit(command.Source)
                || !world.TryGetTeamId(command.Source, out TeamId teamId)
                || !world.PositionComponents.TryGet(command.Source, out PositionComponent sourcePosition))
            {
                return;
            }

            ProjectileEmitterSpawnData spawn = command.ProjectileEmitter;
            BattleVector2 origin = command.HasProjectileEmitterOrigin ? command.ProjectileEmitterOrigin : sourcePosition.Position;
            EntityId entity = world.CreateEntity();
            world.ProjectileEmitterComponents.Add(entity, new ProjectileEmitterComponent(
                command.Source,
                command.Target,
                teamId,
                spawn.AnchorMode,
                spawn.AnchorOffset,
                origin,
                spawn.DurationTicks,
                spawn.FireIntervalTicks,
                ticksUntilNextFire: 0,
                spawn.Pattern,
                spawn.ProjectilePayload,
                tick.Next()));
        }

        private static void ApplyDamageEffectNow(
            BattleWorld world,
            BattleEffectCommand command,
            BattleEffectExecutionKind executionKind,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            if (!CanApplyUnitEffect(world, command.Source, command.Target, executionKind)
                || !world.HealthComponents.TryGet(command.Target, out HealthComponent targetHealth)
                || !world.TryGetUnitId(command.Source, out UnitId sourceUnitId)
                || !world.TryGetUnitId(command.Target, out UnitId targetUnitId))
            {
                return;
            }

            int modifiedDamage = BattleModifierResolver.ResolveDamage(
                command.Amount,
                GetStatusesOrEmpty(world, command.Source),
                GetStatusesOrEmpty(world, command.Target),
                command.Context);
            var context = new BattleDamageContext(
                command.Source,
                command.Target,
                command.Amount,
                modifiedDamage,
                command.TriggerPolicy,
                command.Context);
            int actualDamage = targetHealth.Current <= context.ResolvedAmount ? targetHealth.Current : context.ResolvedAmount;
            if (actualDamage <= 0)
            {
                return;
            }

            int nextHealth = targetHealth.Current - actualDamage;
            world.HealthComponents.Set(command.Target, new HealthComponent(nextHealth));
            events.Write(BattleEvent.DamageApplied(eventSequence.Next(), tick, sourceUnitId, targetUnitId, actualDamage, context.EffectContext));
            TargetingSystem.RecordDamageSource(
                world,
                command.Source,
                command.Target);

            if (command.TriggerPolicy == BattleEffectTriggerPolicy.CanTriggerReactions)
            {
                StatusTriggerResolver.QueueTriggers(world, tick, BattleTriggerContext.AfterDamageDealt(context));
                StatusTriggerResolver.QueueTriggers(world, tick, BattleTriggerContext.AfterDamageTaken(context));
            }

            if (nextHealth == 0)
            {
                world.CommandBuffer.QueueDeathCheck(new DeathCheckCommand(
                    command.Target,
                    command.Source,
                    context.EffectContext,
                    command.TriggerPolicy));
            }
        }

        private static IReadOnlyList<StatusInstance> GetStatusesOrEmpty(BattleWorld world, EntityId entity)
        {
            return world.StatusComponents.TryGet(entity, out StatusComponent statuses)
                ? statuses.Statuses
                : EmptyStatuses;
        }

        private static void ApplyStatusEffectNow(
            BattleWorld world,
            BattleEffectCommand command,
            BattleEffectExecutionKind executionKind,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick)
        {
            if (!CanApplyUnitEffect(world, command.Source, command.Target, executionKind)
                || !world.TryGetUnitId(command.Source, out UnitId sourceUnitId)
                || !world.TryGetUnitId(command.Target, out UnitId targetUnitId))
            {
                return;
            }

            StatusApplicationResult result = StatusApplicationResolver.ApplyOrRefresh(world, command.Source, command.Target, command.Status);
            BattleStatResolver.ClampHealthToEffectiveMax(world, command.Target);
            events.Write(BattleEvent.StatusApplied(eventSequence.Next(), tick, sourceUnitId, targetUnitId, result.Id, result.Polarity));
        }

        private static bool CanApplyUnitEffect(BattleWorld world, EntityId source, EntityId target, BattleEffectExecutionKind executionKind)
        {
            return CanApplyEffectToUnit(world, source, executionKind)
                && CanApplyEffectToUnit(world, target, executionKind);
        }

        private static bool CanApplyEffectToUnit(BattleWorld world, EntityId entity, BattleEffectExecutionKind executionKind)
        {
            switch (executionKind)
            {
                case BattleEffectExecutionKind.Primary:
                    return world.IsAliveUnit(entity);
                case BattleEffectExecutionKind.Reaction:
                    return IsReactionEffectParticipant(world, entity);
                default:
                    throw new ArgumentOutOfRangeException(nameof(executionKind), executionKind, "Unsupported effect execution kind.");
            }
        }

        private static bool IsReactionEffectParticipant(BattleWorld world, EntityId entity)
        {
            return world.LifeStateComponents.TryGet(entity, out LifeStateComponent lifeState)
                && lifeState.State == LifeState.Alive
                && world.HealthComponents.Has(entity)
                && world.TryGetUnitId(entity, out _);
        }
    }
}
