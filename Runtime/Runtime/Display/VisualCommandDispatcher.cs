using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using Combat.Foundation.Events;

namespace Combat.Runtime.Display
{
    public sealed class VisualCommandDispatcher
    {
        private readonly IVisualCommandSink _commandSink;

        public VisualCommandDispatcher(ICombatViewPort viewPort)
            : this(new ImmediateVisualCommandSink(viewPort))
        {
        }

        public VisualCommandDispatcher(IVisualCommandSink commandSink)
        {
            _commandSink = commandSink ?? throw new ArgumentNullException(nameof(commandSink));
        }

        public void Dispatch(EventStream<BattleEvent> events)
        {
            for (var i = 0; i < events.Count; i++)
            {
                Dispatch(events[i]);
            }
        }

        public void Dispatch(IReadOnlyList<BattleEvent> events)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            for (var i = 0; i < events.Count; i++)
            {
                Dispatch(events[i]);
            }
        }

        public void Dispatch(BattleEvent battleEvent)
        {
            switch (battleEvent.Type)
            {
                case BattleEventType.UnitSpawned:
                    _commandSink.Dispatch(VisualCommand.CreateUnit(new UnitSpawnViewSnapshot(
                        battleEvent.UnitId,
                        battleEvent.TeamId,
                        battleEvent.DefinitionId,
                        battleEvent.Position,
                        battleEvent.Facing)));
                    break;
                case BattleEventType.UnitMoved:
                    _commandSink.Dispatch(VisualCommand.MoveUnit(battleEvent.UnitId, battleEvent.Position));
                    break;
                case BattleEventType.UnitFacingChanged:
                    _commandSink.Dispatch(VisualCommand.FaceUnit(battleEvent.UnitId, battleEvent.Facing));
                    break;
                case BattleEventType.UnitGarrisoned:
                    _commandSink.Dispatch(VisualCommand.SetUnitVisibility(battleEvent.UnitId, false));
                    break;
                case BattleEventType.UnitDeployed:
                    _commandSink.Dispatch(VisualCommand.SetUnitVisibility(battleEvent.UnitId, true));
                    break;
                case BattleEventType.AbilityStarted:
                    _commandSink.Dispatch(VisualCommand.PlayAction(new ActionViewSnapshot(
                        battleEvent.SourceUnitId,
                        battleEvent.TargetUnitId,
                        battleEvent.AbilityId,
                        battleEvent.EffectSourceKind),
                        battleEvent.ActionLocks));
                    break;
                case BattleEventType.AbilityReleased:
                    break;
                case BattleEventType.AbilityEnded:
                    _commandSink.Dispatch(VisualCommand.EndAction(battleEvent.SourceUnitId));
                    break;
                case BattleEventType.DamageApplied:
                    _commandSink.Dispatch(VisualCommand.PlayHit(new DamageViewSnapshot(
                        battleEvent.SourceUnitId,
                        battleEvent.TargetUnitId,
                        battleEvent.Amount,
                        battleEvent.EffectSourceKind,
                        battleEvent.HasEffectType,
                        battleEvent.EffectType,
                        battleEvent.AbilityId,
                        battleEvent.EffectStatusId,
                        battleEvent.EffectProjectileId,
                        battleEvent.DamageTags)));
                    break;
                case BattleEventType.HealingApplied:
                    _commandSink.Dispatch(VisualCommand.PlayHeal(new HealingViewSnapshot(
                        battleEvent.SourceUnitId,
                        battleEvent.TargetUnitId,
                        battleEvent.Amount,
                        battleEvent.EffectSourceKind,
                        battleEvent.HasEffectType,
                        battleEvent.EffectType,
                        battleEvent.AbilityId,
                        battleEvent.EffectStatusId,
                        battleEvent.EffectProjectileId)));
                    break;
                case BattleEventType.UnitDied:
                    _commandSink.Dispatch(VisualCommand.DestroyUnit(battleEvent.UnitId));
                    break;
                case BattleEventType.ProjectileSpawned:
                    _commandSink.Dispatch(VisualCommand.CreateProjectile(new ProjectileViewSnapshot(
                        battleEvent.ProjectileId,
                        battleEvent.TeamId,
                        battleEvent.SourceUnitId,
                        battleEvent.Position)));
                    break;
                case BattleEventType.ProjectileMoved:
                    _commandSink.Dispatch(VisualCommand.MoveProjectile(battleEvent.ProjectileId, battleEvent.Position));
                    break;
                case BattleEventType.ProjectileHit:
                    _commandSink.Dispatch(VisualCommand.PlayProjectileHit(new ProjectileHitViewSnapshot(
                        battleEvent.ProjectileId,
                        battleEvent.SourceUnitId,
                        battleEvent.TargetUnitId,
                        battleEvent.Position)));
                    break;
                case BattleEventType.ProjectileDestroyed:
                    _commandSink.Dispatch(VisualCommand.DestroyProjectile(battleEvent.ProjectileId));
                    break;
                case BattleEventType.BattleEnded:
                    _commandSink.Dispatch(VisualCommand.ShowBattleResult(battleEvent.WinningTeamId));
                    break;
                case BattleEventType.StatusApplied:
                    _commandSink.Dispatch(VisualCommand.PlayStatusApplied(new StatusViewSnapshot(
                        battleEvent.TargetUnitId,
                        battleEvent.SourceUnitId,
                        battleEvent.StatusId,
                        battleEvent.StatusPolarity)));
                    break;
                case BattleEventType.StatusExpired:
                    _commandSink.Dispatch(VisualCommand.PlayStatusExpired(new StatusViewSnapshot(
                        battleEvent.UnitId,
                        default,
                        battleEvent.StatusId,
                        battleEvent.StatusPolarity)));
                    break;
            }
        }
    }
}
