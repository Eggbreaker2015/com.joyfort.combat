using System;
using System.Collections.Generic;
using Combat.Core.Battle;

namespace Combat.Runtime.Display
{
    public readonly struct VisualCommand
    {
        private static readonly IReadOnlyList<string> EmptyDamageTags = Array.Empty<string>();

        private readonly object _payload;

        private VisualCommand(VisualCommandType type, object payload)
        {
            Type = type;
            _payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public VisualCommandType Type { get; }
        public bool IsValid => _payload != null;

        public T GetPayload<T>()
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("Visual command is invalid because it does not carry a payload.");
            }

            if (_payload is T payload)
            {
                return payload;
            }

            throw new InvalidOperationException($"Visual command '{Type}' does not carry payload type '{typeof(T).Name}'.");
        }

        public UnitId UnitId
        {
            get
            {
                if (!IsValid)
                {
                    return default;
                }

                switch (Type)
                {
                    case VisualCommandType.CreateUnit:
                        return GetPayload<UnitSpawnViewSnapshot>().UnitId;
                    case VisualCommandType.MoveUnit:
                        return GetPayload<UnitMoveViewSnapshot>().UnitId;
                    case VisualCommandType.StopUnitMovement:
                    case VisualCommandType.EndAction:
                    case VisualCommandType.DestroyUnit:
                        return GetPayload<UnitCommandTarget>().UnitId;
                    case VisualCommandType.FaceUnit:
                        return GetPayload<UnitFacingViewSnapshot>().UnitId;
                    case VisualCommandType.SetUnitVisibility:
                        return GetPayload<UnitVisibilityViewSnapshot>().UnitId;
                    case VisualCommandType.PlayAction:
                        return GetPayload<ActionVisualCommandPayload>().Snapshot.SourceUnitId;
                    case VisualCommandType.PlayHit:
                        return GetPayload<DamageViewSnapshot>().SourceUnitId;
                    case VisualCommandType.PlayHeal:
                        return GetPayload<HealingViewSnapshot>().SourceUnitId;
                    case VisualCommandType.PlayProjectileHit:
                        return GetPayload<ProjectileHitViewSnapshot>().SourceUnitId;
                    case VisualCommandType.PlayStatusApplied:
                    case VisualCommandType.PlayStatusExpired:
                        return GetPayload<StatusViewSnapshot>().UnitId;
                    default:
                        return default;
                }
            }
        }

        public ProjectileId ProjectileId
        {
            get
            {
                if (!IsValid)
                {
                    return default;
                }

                switch (Type)
                {
                    case VisualCommandType.CreateProjectile:
                        return GetPayload<ProjectileViewSnapshot>().ProjectileId;
                    case VisualCommandType.MoveProjectile:
                        return GetPayload<ProjectileMoveViewSnapshot>().ProjectileId;
                    case VisualCommandType.PlayProjectileHit:
                        return GetPayload<ProjectileHitViewSnapshot>().ProjectileId;
                    case VisualCommandType.DestroyProjectile:
                        return GetPayload<ProjectileCommandTarget>().ProjectileId;
                    default:
                        return default;
                }
            }
        }

        public UnitId TargetUnitId
        {
            get
            {
                if (!IsValid)
                {
                    return default;
                }

                switch (Type)
                {
                    case VisualCommandType.PlayAction:
                        return GetPayload<ActionVisualCommandPayload>().Snapshot.TargetUnitId;
                    case VisualCommandType.PlayHit:
                        return GetPayload<DamageViewSnapshot>().TargetUnitId;
                    case VisualCommandType.PlayHeal:
                        return GetPayload<HealingViewSnapshot>().TargetUnitId;
                    case VisualCommandType.PlayProjectileHit:
                        return GetPayload<ProjectileHitViewSnapshot>().TargetUnitId;
                    default:
                        return default;
                }
            }
        }

        public TeamId TeamId
        {
            get
            {
                if (!IsValid)
                {
                    return default;
                }

                switch (Type)
                {
                    case VisualCommandType.CreateUnit:
                        return GetPayload<UnitSpawnViewSnapshot>().TeamId;
                    case VisualCommandType.CreateProjectile:
                        return GetPayload<ProjectileViewSnapshot>().TeamId;
                    default:
                        return default;
                }
            }
        }

        public BattleVector2 Position
        {
            get
            {
                if (!IsValid)
                {
                    return default;
                }

                switch (Type)
                {
                    case VisualCommandType.CreateUnit:
                        return GetPayload<UnitSpawnViewSnapshot>().Position;
                    case VisualCommandType.MoveUnit:
                        return GetPayload<UnitMoveViewSnapshot>().Position;
                    case VisualCommandType.CreateProjectile:
                        return GetPayload<ProjectileViewSnapshot>().Position;
                    case VisualCommandType.MoveProjectile:
                        return GetPayload<ProjectileMoveViewSnapshot>().Position;
                    case VisualCommandType.PlayProjectileHit:
                        return GetPayload<ProjectileHitViewSnapshot>().Position;
                    default:
                        return default;
                }
            }
        }

        public BattleVector2 Facing
        {
            get
            {
                if (!IsValid)
                {
                    return default;
                }

                switch (Type)
                {
                    case VisualCommandType.CreateUnit:
                        return GetPayload<UnitSpawnViewSnapshot>().Facing;
                    case VisualCommandType.FaceUnit:
                        return GetPayload<UnitFacingViewSnapshot>().Facing;
                    default:
                        return BattleVector2.Right;
                }
            }
        }

        public int Amount
        {
            get
            {
                if (!IsValid)
                {
                    return 0;
                }

                switch (Type)
                {
                    case VisualCommandType.PlayHit:
                        return GetPayload<DamageViewSnapshot>().Amount;
                    case VisualCommandType.PlayHeal:
                        return GetPayload<HealingViewSnapshot>().Amount;
                    default:
                        return 0;
                }
            }
        }

        public TeamId WinningTeamId => IsValid && Type == VisualCommandType.ShowBattleResult
            ? GetPayload<BattleResultViewSnapshot>().WinningTeamId
            : default;

        public string DefinitionId => IsValid && Type == VisualCommandType.CreateUnit
            ? GetPayload<UnitSpawnViewSnapshot>().DefinitionId
            : null;

        public UnitId SourceUnitId
        {
            get
            {
                if (!IsValid)
                {
                    return default;
                }

                switch (Type)
                {
                    case VisualCommandType.PlayAction:
                        return GetPayload<ActionVisualCommandPayload>().Snapshot.SourceUnitId;
                    case VisualCommandType.PlayHit:
                        return GetPayload<DamageViewSnapshot>().SourceUnitId;
                    case VisualCommandType.PlayHeal:
                        return GetPayload<HealingViewSnapshot>().SourceUnitId;
                    case VisualCommandType.EndAction:
                        return GetPayload<UnitCommandTarget>().UnitId;
                    case VisualCommandType.CreateProjectile:
                        return GetPayload<ProjectileViewSnapshot>().SourceUnitId;
                    case VisualCommandType.PlayProjectileHit:
                        return GetPayload<ProjectileHitViewSnapshot>().SourceUnitId;
                    case VisualCommandType.PlayStatusApplied:
                    case VisualCommandType.PlayStatusExpired:
                        return GetPayload<StatusViewSnapshot>().SourceUnitId;
                    default:
                        return default;
                }
            }
        }

        public string StatusId
        {
            get
            {
                if (!IsValid)
                {
                    return null;
                }

                switch (Type)
                {
                    case VisualCommandType.PlayStatusApplied:
                    case VisualCommandType.PlayStatusExpired:
                        return GetPayload<StatusViewSnapshot>().StatusId;
                    default:
                        return null;
                }
            }
        }

        public StatusPolarity StatusPolarity
        {
            get
            {
                if (!IsValid)
                {
                    return default;
                }

                switch (Type)
                {
                    case VisualCommandType.PlayStatusApplied:
                    case VisualCommandType.PlayStatusExpired:
                        return GetPayload<StatusViewSnapshot>().Polarity;
                    default:
                        return default;
                }
            }
        }

        public BattleEffectSourceKind EffectSourceKind
        {
            get
            {
                if (!IsValid)
                {
                    return BattleEffectSourceKind.Unknown;
                }

                switch (Type)
                {
                    case VisualCommandType.PlayAction:
                        return GetPayload<ActionVisualCommandPayload>().Snapshot.SourceKind;
                    case VisualCommandType.PlayHit:
                        return GetPayload<DamageViewSnapshot>().SourceKind;
                    case VisualCommandType.PlayHeal:
                        return GetPayload<HealingViewSnapshot>().SourceKind;
                    default:
                        return BattleEffectSourceKind.Unknown;
                }
            }
        }

        public bool HasEffectType
        {
            get
            {
                if (!IsValid)
                {
                    return false;
                }

                switch (Type)
                {
                    case VisualCommandType.PlayHit:
                        return GetPayload<DamageViewSnapshot>().HasEffectType;
                    case VisualCommandType.PlayHeal:
                        return GetPayload<HealingViewSnapshot>().HasEffectType;
                    default:
                        return false;
                }
            }
        }

        public BattleEffectType EffectType
        {
            get
            {
                if (!IsValid)
                {
                    return default;
                }

                switch (Type)
                {
                    case VisualCommandType.PlayHit:
                        return GetPayload<DamageViewSnapshot>().EffectType;
                    case VisualCommandType.PlayHeal:
                        return GetPayload<HealingViewSnapshot>().EffectType;
                    default:
                        return default;
                }
            }
        }

        public string AbilityId
        {
            get
            {
                if (!IsValid)
                {
                    return null;
                }

                switch (Type)
                {
                    case VisualCommandType.PlayAction:
                        return GetPayload<ActionVisualCommandPayload>().Snapshot.AbilityId;
                    case VisualCommandType.PlayHit:
                        return GetPayload<DamageViewSnapshot>().AbilityId;
                    case VisualCommandType.PlayHeal:
                        return GetPayload<HealingViewSnapshot>().AbilityId;
                    default:
                        return null;
                }
            }
        }

        public string EffectStatusId
        {
            get
            {
                if (!IsValid)
                {
                    return null;
                }

                switch (Type)
                {
                    case VisualCommandType.PlayHit:
                        return GetPayload<DamageViewSnapshot>().StatusId;
                    case VisualCommandType.PlayHeal:
                        return GetPayload<HealingViewSnapshot>().StatusId;
                    default:
                        return null;
                }
            }
        }

        public ProjectileId EffectProjectileId
        {
            get
            {
                if (!IsValid)
                {
                    return default;
                }

                switch (Type)
                {
                    case VisualCommandType.PlayHit:
                        return GetPayload<DamageViewSnapshot>().ProjectileId;
                    case VisualCommandType.PlayHeal:
                        return GetPayload<HealingViewSnapshot>().ProjectileId;
                    default:
                        return default;
                }
            }
        }

        public BattleActionLocks ActionLocks => IsValid && Type == VisualCommandType.PlayAction
            ? GetPayload<ActionVisualCommandPayload>().ActionLocks
            : BattleActionLocks.None;

        public IReadOnlyList<string> DamageTags => IsValid && Type == VisualCommandType.PlayHit
            ? GetPayload<DamageViewSnapshot>().DamageTags
            : EmptyDamageTags;

        public static VisualCommand CreateUnit(UnitSpawnViewSnapshot snapshot)
        {
            return new VisualCommand(VisualCommandType.CreateUnit, snapshot);
        }

        public static VisualCommand MoveUnit(UnitId unitId, BattleVector2 position)
        {
            return new VisualCommand(VisualCommandType.MoveUnit, new UnitMoveViewSnapshot(unitId, position));
        }

        public static VisualCommand StopUnitMovement(UnitId unitId)
        {
            return new VisualCommand(VisualCommandType.StopUnitMovement, new UnitCommandTarget(unitId));
        }

        public static VisualCommand FaceUnit(UnitId unitId, BattleVector2 facing)
        {
            return new VisualCommand(VisualCommandType.FaceUnit, new UnitFacingViewSnapshot(unitId, facing));
        }

        public static VisualCommand SetUnitVisibility(UnitId unitId, bool isVisible)
        {
            return new VisualCommand(VisualCommandType.SetUnitVisibility, new UnitVisibilityViewSnapshot(unitId, isVisible));
        }

        public static VisualCommand PlayAction(ActionViewSnapshot snapshot, BattleActionLocks actionLocks = BattleActionLocks.None)
        {
            return new VisualCommand(VisualCommandType.PlayAction, new ActionVisualCommandPayload(snapshot, actionLocks));
        }

        public static VisualCommand EndAction(UnitId unitId)
        {
            return new VisualCommand(VisualCommandType.EndAction, new UnitCommandTarget(unitId));
        }

        public static VisualCommand PlayHit(DamageViewSnapshot snapshot)
        {
            return new VisualCommand(VisualCommandType.PlayHit, snapshot);
        }

        public static VisualCommand PlayHeal(HealingViewSnapshot snapshot)
        {
            return new VisualCommand(VisualCommandType.PlayHeal, snapshot);
        }

        public static VisualCommand PlayProjectileHit(ProjectileHitViewSnapshot snapshot)
        {
            return new VisualCommand(VisualCommandType.PlayProjectileHit, snapshot);
        }

        public static VisualCommand DestroyUnit(UnitId unitId)
        {
            return new VisualCommand(VisualCommandType.DestroyUnit, new UnitCommandTarget(unitId));
        }

        public static VisualCommand CreateProjectile(ProjectileViewSnapshot snapshot)
        {
            return new VisualCommand(VisualCommandType.CreateProjectile, snapshot);
        }

        public static VisualCommand MoveProjectile(ProjectileId projectileId, BattleVector2 position)
        {
            return new VisualCommand(VisualCommandType.MoveProjectile, new ProjectileMoveViewSnapshot(projectileId, position));
        }

        public static VisualCommand DestroyProjectile(ProjectileId projectileId)
        {
            return new VisualCommand(VisualCommandType.DestroyProjectile, new ProjectileCommandTarget(projectileId));
        }

        public static VisualCommand PlayStatusApplied(StatusViewSnapshot snapshot)
        {
            return new VisualCommand(VisualCommandType.PlayStatusApplied, snapshot);
        }

        public static VisualCommand PlayStatusExpired(StatusViewSnapshot snapshot)
        {
            return new VisualCommand(VisualCommandType.PlayStatusExpired, snapshot);
        }

        public static VisualCommand ShowBattleResult(TeamId winningTeamId)
        {
            return new VisualCommand(VisualCommandType.ShowBattleResult, new BattleResultViewSnapshot(winningTeamId));
        }
    }
}
