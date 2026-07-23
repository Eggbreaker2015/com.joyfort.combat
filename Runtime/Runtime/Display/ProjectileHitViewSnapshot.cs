using Combat.Core.Battle;

namespace Combat.Runtime.Display
{
    public readonly struct ProjectileHitViewSnapshot
    {
        public ProjectileHitViewSnapshot(ProjectileId projectileId, UnitId sourceUnitId, UnitId targetUnitId, BattleVector2 position)
        {
            ProjectileId = projectileId;
            SourceUnitId = sourceUnitId;
            TargetUnitId = targetUnitId;
            Position = position;
        }

        public ProjectileId ProjectileId { get; }
        public UnitId SourceUnitId { get; }
        public UnitId TargetUnitId { get; }
        public BattleVector2 Position { get; }
    }
}
