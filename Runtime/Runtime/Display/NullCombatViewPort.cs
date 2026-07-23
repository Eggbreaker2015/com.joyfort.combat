using Combat.Core.Battle;
using Combat.Runtime.Runner;

namespace Combat.Runtime.Display
{
    public sealed class NullCombatViewPort : ICombatViewPort
    {
        public void CreateUnit(UnitSpawnViewSnapshot snapshot)
        {
        }

        public void MoveUnit(UnitId unitId, BattleVector2 position)
        {
        }

        public void StopUnitMovement(UnitId unitId)
        {
        }

        public void FaceUnit(UnitId unitId, BattleVector2 facing)
        {
        }

        public void SetUnitVisibility(UnitId unitId, bool isVisible)
        {
        }

        public void PlayAction(ActionViewSnapshot snapshot)
        {
        }

        public void PlayHit(DamageViewSnapshot snapshot)
        {
        }

        public void PlayHeal(HealingViewSnapshot snapshot)
        {
        }

        public void DestroyUnit(UnitId unitId)
        {
        }

        public void CreateProjectile(ProjectileViewSnapshot snapshot)
        {
        }

        public void MoveProjectile(ProjectileId projectileId, BattleVector2 position)
        {
        }

        public void PlayProjectileHit(ProjectileHitViewSnapshot snapshot)
        {
        }

        public void DestroyProjectile(ProjectileId projectileId)
        {
        }

        public void PlayStatusApplied(StatusViewSnapshot snapshot)
        {
        }

        public void PlayStatusExpired(StatusViewSnapshot snapshot)
        {
        }

        public void ShowBattleResult(BattleResult result)
        {
        }
    }
}
