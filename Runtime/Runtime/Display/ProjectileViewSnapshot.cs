using Combat.Core.Battle;

namespace Combat.Runtime.Display
{
    public readonly struct ProjectileViewSnapshot
    {
        public ProjectileViewSnapshot(ProjectileId projectileId, TeamId teamId, UnitId sourceUnitId, BattleVector2 position)
        {
            ProjectileId = projectileId;
            TeamId = teamId;
            SourceUnitId = sourceUnitId;
            Position = position;
        }

        public ProjectileId ProjectileId { get; }
        public TeamId TeamId { get; }
        public UnitId SourceUnitId { get; }
        public BattleVector2 Position { get; }
    }
}
