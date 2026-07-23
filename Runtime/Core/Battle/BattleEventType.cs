namespace Combat.Core.Battle
{
    public enum BattleEventType
    {
        UnitSpawned,
        UnitMoved,
        UnitFacingChanged,
        DamageApplied,
        HealingApplied,
        UnitDied,
        ProjectileSpawned,
        ProjectileMoved,
        ProjectileHit,
        ProjectileDestroyed,
        BattleEnded,
        StatusApplied,
        StatusExpired,
        AbilityStarted,
        AbilityReleased,
        AbilityEnded,
        UnitGarrisoned,
        UnitDeployed
    }
}
