using Combat.Core.Battle;

namespace Combat.Unity.Authoring
{
    public enum BattleEffectAuthoringScope
    {
        Ability,
        ProjectileImpact,
        AreaChildProjectileImpact,
        AreaChild,
        StatusReaction
    }

    public static class BattleEffectAuthoringRules
    {
        public static bool AllowsDirectHeal(BattleEffectAuthoringScope scope, AbilityTargetSelection abilityTargetSelection)
        {
            return scope == BattleEffectAuthoringScope.AreaChild
                || scope == BattleEffectAuthoringScope.StatusReaction
                || (scope == BattleEffectAuthoringScope.Ability
                    && (abilityTargetSelection == AbilityTargetSelection.LowestHealthAlly
                        || abilityTargetSelection == AbilityTargetSelection.Self));
        }

        public static bool AllowsAreaEffect(BattleEffectAuthoringScope scope)
        {
            return scope != BattleEffectAuthoringScope.AreaChild
                && scope != BattleEffectAuthoringScope.AreaChildProjectileImpact;
        }

        public static BattleEffectAuthoringScope ProjectileImpactScopeForParent(BattleEffectAuthoringScope scope)
        {
            return scope == BattleEffectAuthoringScope.AreaChild
                ? BattleEffectAuthoringScope.AreaChildProjectileImpact
                : BattleEffectAuthoringScope.ProjectileImpact;
        }
    }
}
