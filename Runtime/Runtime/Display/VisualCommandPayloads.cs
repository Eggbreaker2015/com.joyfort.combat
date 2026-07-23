using Combat.Core.Battle;

namespace Combat.Runtime.Display
{
    public readonly struct UnitMoveViewSnapshot
    {
        public UnitMoveViewSnapshot(UnitId unitId, BattleVector2 position)
        {
            UnitId = unitId;
            Position = position;
        }

        public UnitId UnitId { get; }
        public BattleVector2 Position { get; }
    }

    public readonly struct UnitFacingViewSnapshot
    {
        public UnitFacingViewSnapshot(UnitId unitId, BattleVector2 facing)
        {
            UnitId = unitId;
            Facing = facing.SqrMagnitude <= 0.00001f ? BattleVector2.Right : facing.Normalized;
        }

        public UnitId UnitId { get; }
        public BattleVector2 Facing { get; }
    }

    public readonly struct UnitVisibilityViewSnapshot
    {
        public UnitVisibilityViewSnapshot(UnitId unitId, bool isVisible)
        {
            UnitId = unitId;
            IsVisible = isVisible;
        }

        public UnitId UnitId { get; }
        public bool IsVisible { get; }
    }

    public readonly struct UnitCommandTarget
    {
        public UnitCommandTarget(UnitId unitId)
        {
            UnitId = unitId;
        }

        public UnitId UnitId { get; }
    }

    public readonly struct ProjectileMoveViewSnapshot
    {
        public ProjectileMoveViewSnapshot(ProjectileId projectileId, BattleVector2 position)
        {
            ProjectileId = projectileId;
            Position = position;
        }

        public ProjectileId ProjectileId { get; }
        public BattleVector2 Position { get; }
    }

    public readonly struct ProjectileCommandTarget
    {
        public ProjectileCommandTarget(ProjectileId projectileId)
        {
            ProjectileId = projectileId;
        }

        public ProjectileId ProjectileId { get; }
    }

    public readonly struct BattleResultViewSnapshot
    {
        public BattleResultViewSnapshot(TeamId winningTeamId)
        {
            WinningTeamId = winningTeamId;
        }

        public TeamId WinningTeamId { get; }
    }

    public readonly struct ActionVisualCommandPayload
    {
        public ActionVisualCommandPayload(ActionViewSnapshot snapshot, BattleActionLocks actionLocks)
        {
            Snapshot = snapshot;
            ActionLocks = actionLocks;
        }

        public ActionViewSnapshot Snapshot { get; }
        public BattleActionLocks ActionLocks { get; }
    }
}
