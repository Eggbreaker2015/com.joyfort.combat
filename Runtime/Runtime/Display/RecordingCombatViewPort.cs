using System.Collections.Generic;
using System.Collections.ObjectModel;
using Combat.Core.Battle;
using Combat.Runtime.Runner;

namespace Combat.Runtime.Display
{
    public sealed class RecordingCombatViewPort : ICombatViewPort
    {
        private readonly List<VisualCommand> _commands = new List<VisualCommand>();
        private readonly ReadOnlyCollection<VisualCommand> _readOnlyCommands;

        public RecordingCombatViewPort()
        {
            _readOnlyCommands = _commands.AsReadOnly();
        }

        public ReadOnlyCollection<VisualCommand> Commands => _readOnlyCommands;

        public void CreateUnit(UnitSpawnViewSnapshot snapshot)
        {
            _commands.Add(VisualCommand.CreateUnit(snapshot));
        }

        public void MoveUnit(UnitId unitId, BattleVector2 position)
        {
            _commands.Add(VisualCommand.MoveUnit(unitId, position));
        }

        public void StopUnitMovement(UnitId unitId)
        {
            _commands.Add(VisualCommand.StopUnitMovement(unitId));
        }

        public void FaceUnit(UnitId unitId, BattleVector2 facing)
        {
            _commands.Add(VisualCommand.FaceUnit(unitId, facing));
        }

        public void SetUnitVisibility(UnitId unitId, bool isVisible)
        {
            _commands.Add(VisualCommand.SetUnitVisibility(unitId, isVisible));
        }

        public void PlayAction(ActionViewSnapshot snapshot)
        {
            _commands.Add(VisualCommand.PlayAction(snapshot));
        }

        public void PlayHit(DamageViewSnapshot snapshot)
        {
            _commands.Add(VisualCommand.PlayHit(snapshot));
        }

        public void PlayHeal(HealingViewSnapshot snapshot)
        {
            _commands.Add(VisualCommand.PlayHeal(snapshot));
        }

        public void DestroyUnit(UnitId unitId)
        {
            _commands.Add(VisualCommand.DestroyUnit(unitId));
        }

        public void CreateProjectile(ProjectileViewSnapshot snapshot)
        {
            _commands.Add(VisualCommand.CreateProjectile(snapshot));
        }

        public void MoveProjectile(ProjectileId projectileId, BattleVector2 position)
        {
            _commands.Add(VisualCommand.MoveProjectile(projectileId, position));
        }

        public void PlayProjectileHit(ProjectileHitViewSnapshot snapshot)
        {
            _commands.Add(VisualCommand.PlayProjectileHit(snapshot));
        }

        public void DestroyProjectile(ProjectileId projectileId)
        {
            _commands.Add(VisualCommand.DestroyProjectile(projectileId));
        }

        public void PlayStatusApplied(StatusViewSnapshot snapshot)
        {
            _commands.Add(VisualCommand.PlayStatusApplied(snapshot));
        }

        public void PlayStatusExpired(StatusViewSnapshot snapshot)
        {
            _commands.Add(VisualCommand.PlayStatusExpired(snapshot));
        }

        public void ShowBattleResult(BattleResult result)
        {
            _commands.Add(VisualCommand.ShowBattleResult(result.WinningTeamId));
        }

        public void Clear()
        {
            _commands.Clear();
        }
    }
}
