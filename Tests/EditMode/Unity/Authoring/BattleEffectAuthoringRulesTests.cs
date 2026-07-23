using Combat.Core.Battle;
using Combat.Unity.Authoring;
using NUnit.Framework;

namespace Combat.Tests.Unity.Authoring
{
    public sealed class BattleEffectAuthoringRulesTests
    {
        [Test]
        public void AllowsDirectHeal_OnlyForExplicitTargetContexts()
        {
            Assert.IsTrue(BattleEffectAuthoringRules.AllowsDirectHeal(BattleEffectAuthoringScope.Ability, AbilityTargetSelection.Self));
            Assert.IsTrue(BattleEffectAuthoringRules.AllowsDirectHeal(BattleEffectAuthoringScope.Ability, AbilityTargetSelection.LowestHealthAlly));
            Assert.IsTrue(BattleEffectAuthoringRules.AllowsDirectHeal(BattleEffectAuthoringScope.AreaChild, AbilityTargetSelection.CurrentEnemyTarget));
            Assert.IsTrue(BattleEffectAuthoringRules.AllowsDirectHeal(BattleEffectAuthoringScope.StatusReaction, AbilityTargetSelection.CurrentEnemyTarget));

            Assert.IsFalse(BattleEffectAuthoringRules.AllowsDirectHeal(BattleEffectAuthoringScope.Ability, AbilityTargetSelection.CurrentEnemyTarget));
            Assert.IsFalse(BattleEffectAuthoringRules.AllowsDirectHeal(BattleEffectAuthoringScope.ProjectileImpact, AbilityTargetSelection.Self));
            Assert.IsFalse(BattleEffectAuthoringRules.AllowsDirectHeal(BattleEffectAuthoringScope.AreaChildProjectileImpact, AbilityTargetSelection.Self));
        }

        [TestCase(BattleEffectAuthoringScope.Ability, BattleEffectAuthoringScope.ProjectileImpact)]
        [TestCase(BattleEffectAuthoringScope.ProjectileImpact, BattleEffectAuthoringScope.ProjectileImpact)]
        [TestCase(BattleEffectAuthoringScope.AreaChildProjectileImpact, BattleEffectAuthoringScope.ProjectileImpact)]
        [TestCase(BattleEffectAuthoringScope.AreaChild, BattleEffectAuthoringScope.AreaChildProjectileImpact)]
        [TestCase(BattleEffectAuthoringScope.StatusReaction, BattleEffectAuthoringScope.ProjectileImpact)]
        public void ProjectileImpactScopeForParent_MapsAllAuthoringScopes(
            BattleEffectAuthoringScope parentScope,
            BattleEffectAuthoringScope expectedImpactScope)
        {
            Assert.AreEqual(expectedImpactScope, BattleEffectAuthoringRules.ProjectileImpactScopeForParent(parentScope));
        }

        [Test]
        public void AllowsAreaEffect_BlocksNestedAreaEffectContexts()
        {
            Assert.IsTrue(BattleEffectAuthoringRules.AllowsAreaEffect(BattleEffectAuthoringScope.Ability));
            Assert.IsTrue(BattleEffectAuthoringRules.AllowsAreaEffect(BattleEffectAuthoringScope.ProjectileImpact));
            Assert.IsTrue(BattleEffectAuthoringRules.AllowsAreaEffect(BattleEffectAuthoringScope.StatusReaction));

            Assert.IsFalse(BattleEffectAuthoringRules.AllowsAreaEffect(BattleEffectAuthoringScope.AreaChild));
            Assert.IsFalse(BattleEffectAuthoringRules.AllowsAreaEffect(BattleEffectAuthoringScope.AreaChildProjectileImpact));
        }
    }
}
