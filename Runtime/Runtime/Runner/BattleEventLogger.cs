using System.Globalization;
using Combat.Core.Battle;
using Combat.Foundation.Diagnostics;
using Combat.Foundation.Events;

namespace Combat.Runtime.Runner
{
    internal sealed class BattleEventLogger
    {
        private readonly CombatLogger _logger;

        public BattleEventLogger(CombatLogger logger)
        {
            _logger = logger ?? CombatLogger.Disabled;
        }

        public void Log(EventStream<BattleEvent> events)
        {
            for (var i = 0; i < events.Count; i++)
            {
                Log(events[i]);
            }
        }

        private void Log(BattleEvent battleEvent)
        {
            switch (battleEvent.Type)
            {
                case BattleEventType.UnitSpawned:
                    _logger.Info(CombatLogTags.Runtime, () => $"{Prefix(battleEvent)} Team {battleEvent.TeamId.Value} Unit {battleEvent.UnitId.Value} spawned '{battleEvent.DefinitionId}' at {FormatPosition(battleEvent.Position)}.");
                    break;
                case BattleEventType.UnitMoved:
                    _logger.Debug(CombatLogTags.Runtime, () => $"{Prefix(battleEvent)} Unit {battleEvent.UnitId.Value} moved to {FormatPosition(battleEvent.Position)}.");
                    break;
                case BattleEventType.UnitGarrisoned:
                    _logger.Info(CombatLogTags.Runtime, () => $"{Prefix(battleEvent)} Team {battleEvent.TeamId.Value} Unit {battleEvent.UnitId.Value} garrisoned.");
                    break;
                case BattleEventType.UnitDeployed:
                    _logger.Info(CombatLogTags.Runtime, () => $"{Prefix(battleEvent)} Team {battleEvent.TeamId.Value} Unit {battleEvent.UnitId.Value} deployed.");
                    break;
                case BattleEventType.AbilityStarted:
                    _logger.Info(CombatLogTags.Runtime, () => $"{Prefix(battleEvent)} Unit {battleEvent.SourceUnitId.Value} started {battleEvent.EffectSourceKind} '{battleEvent.AbilityId}' on Unit {battleEvent.TargetUnitId.Value}.");
                    break;
                case BattleEventType.DamageApplied:
                    _logger.Info(CombatLogTags.Runtime, () => $"{Prefix(battleEvent)} Unit {battleEvent.SourceUnitId.Value} dealt {battleEvent.Amount} damage to Unit {battleEvent.TargetUnitId.Value} via {FormatEffectSource(battleEvent)}.");
                    break;
                case BattleEventType.UnitDied:
                    _logger.Info(CombatLogTags.Runtime, () => $"{Prefix(battleEvent)} Team {battleEvent.TeamId.Value} Unit {battleEvent.UnitId.Value} died.");
                    break;
                case BattleEventType.ProjectileSpawned:
                    _logger.Info(CombatLogTags.Runtime, () => $"{Prefix(battleEvent)} Team {battleEvent.TeamId.Value} Projectile {battleEvent.ProjectileId.Value} spawned from Unit {battleEvent.SourceUnitId.Value} at {FormatPosition(battleEvent.Position)}.");
                    break;
                case BattleEventType.ProjectileMoved:
                    _logger.Debug(CombatLogTags.Runtime, () => $"{Prefix(battleEvent)} Projectile {battleEvent.ProjectileId.Value} moved to {FormatPosition(battleEvent.Position)}.");
                    break;
                case BattleEventType.ProjectileHit:
                    _logger.Info(CombatLogTags.Runtime, () => $"{Prefix(battleEvent)} Projectile {battleEvent.ProjectileId.Value} from Unit {battleEvent.SourceUnitId.Value} hit Unit {battleEvent.TargetUnitId.Value} at {FormatPosition(battleEvent.Position)}.");
                    break;
                case BattleEventType.ProjectileDestroyed:
                    _logger.Info(CombatLogTags.Runtime, () => $"{Prefix(battleEvent)} Projectile {battleEvent.ProjectileId.Value} destroyed.");
                    break;
                case BattleEventType.BattleEnded:
                    _logger.Info(CombatLogTags.Runtime, () => $"{Prefix(battleEvent)} Battle ended. Winning Team {battleEvent.WinningTeamId.Value}.");
                    break;
                case BattleEventType.StatusApplied:
                    _logger.Info(CombatLogTags.Runtime, () => $"{Prefix(battleEvent)} Unit {battleEvent.SourceUnitId.Value} applied status '{battleEvent.StatusId}' ({battleEvent.StatusPolarity}) to Unit {battleEvent.TargetUnitId.Value}.");
                    break;
                case BattleEventType.StatusExpired:
                    _logger.Info(CombatLogTags.Runtime, () => $"{Prefix(battleEvent)} Status '{battleEvent.StatusId}' ({battleEvent.StatusPolarity}) expired on Unit {battleEvent.UnitId.Value}.");
                    break;
            }
        }

        private static string Prefix(BattleEvent battleEvent)
        {
            return $"Tick {battleEvent.Tick.Value} #{battleEvent.Sequence} |";
        }

        private static string FormatPosition(BattleVector2 position)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:0.###}, {1:0.###})",
                position.X,
                position.Y);
        }

        private static string FormatEffectSource(BattleEvent battleEvent)
        {
            switch (battleEvent.EffectSourceKind)
            {
                case BattleEffectSourceKind.BasicAbility:
                case BattleEffectSourceKind.Ability:
                    return $"{battleEvent.EffectSourceKind} '{battleEvent.AbilityId}'";
                case BattleEffectSourceKind.Status:
                    return $"Status '{battleEvent.EffectStatusId}'";
                case BattleEffectSourceKind.Projectile:
                    return $"Projectile {battleEvent.EffectProjectileId.Value}";
                case BattleEffectSourceKind.Reaction:
                    return "Reaction";
                case BattleEffectSourceKind.Unknown:
                    return "Unknown";
                default:
                    return battleEvent.EffectSourceKind.ToString();
            }
        }
    }
}
