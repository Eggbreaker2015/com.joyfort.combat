using Combat.Core.Battle;
using Combat.Runtime.Runner;

namespace Combat.Runtime.Display
{
    public readonly struct ActionViewSnapshot
    {
        public ActionViewSnapshot(UnitId sourceUnitId, UnitId targetUnitId, string abilityId, BattleEffectSourceKind sourceKind)
        {
            SourceUnitId = sourceUnitId;
            TargetUnitId = targetUnitId;
            AbilityId = abilityId;
            SourceKind = sourceKind;
        }

        public UnitId SourceUnitId { get; }
        public UnitId TargetUnitId { get; }
        public string AbilityId { get; }
        public BattleEffectSourceKind SourceKind { get; }
    }

    public readonly struct StatusViewSnapshot
    {
        public StatusViewSnapshot(UnitId unitId, UnitId sourceUnitId, string statusId, StatusPolarity polarity)
        {
            UnitId = unitId;
            SourceUnitId = sourceUnitId;
            StatusId = statusId;
            Polarity = polarity;
        }

        public UnitId UnitId { get; }
        public UnitId SourceUnitId { get; }
        public string StatusId { get; }
        public StatusPolarity Polarity { get; }
    }

    public readonly struct HealingViewSnapshot
    {
        public HealingViewSnapshot(
            UnitId sourceUnitId,
            UnitId targetUnitId,
            int amount,
            BattleEffectSourceKind sourceKind,
            bool hasEffectType,
            BattleEffectType effectType,
            string abilityId,
            string statusId,
            ProjectileId projectileId)
        {
            SourceUnitId = sourceUnitId;
            TargetUnitId = targetUnitId;
            Amount = amount;
            SourceKind = sourceKind;
            HasEffectType = hasEffectType;
            EffectType = hasEffectType ? effectType : default;
            AbilityId = abilityId;
            StatusId = statusId;
            ProjectileId = projectileId;
        }

        public UnitId SourceUnitId { get; }
        public UnitId TargetUnitId { get; }
        public int Amount { get; }
        public BattleEffectSourceKind SourceKind { get; }
        public bool HasEffectType { get; }
        public BattleEffectType EffectType { get; }
        public string AbilityId { get; }
        public string StatusId { get; }
        public ProjectileId ProjectileId { get; }
    }

    public interface ICombatViewPort
    {
        void CreateUnit(UnitSpawnViewSnapshot snapshot);
        void MoveUnit(UnitId unitId, BattleVector2 position);
        void StopUnitMovement(UnitId unitId);
        void FaceUnit(UnitId unitId, BattleVector2 facing);
        void SetUnitVisibility(UnitId unitId, bool isVisible);
        void PlayAction(ActionViewSnapshot snapshot);
        void PlayHit(DamageViewSnapshot snapshot);
        void PlayHeal(HealingViewSnapshot snapshot);
        void DestroyUnit(UnitId unitId);
        void CreateProjectile(ProjectileViewSnapshot snapshot);
        void MoveProjectile(ProjectileId projectileId, BattleVector2 position);
        void PlayProjectileHit(ProjectileHitViewSnapshot snapshot);
        void DestroyProjectile(ProjectileId projectileId);
        void PlayStatusApplied(StatusViewSnapshot snapshot);
        void PlayStatusExpired(StatusViewSnapshot snapshot);
        void ShowBattleResult(BattleResult result);
    }
}
