using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class StatusTriggerResolverTests
    {
        [Test]
        public void BattleTriggerContext_AfterDamageFactoriesMapOwnerSourceAndTarget()
        {
            var source = new EntityId(1, 1);
            var target = new EntityId(2, 1);
            BattleEffectContext effectContext = BattleEffectContext.Ability("slash", BattleEffectType.Damage);
            var damage = new BattleDamageContext(
                source,
                target,
                baseAmount: 5,
                resolvedAmount: 3,
                BattleEffectTriggerPolicy.CanTriggerReactions,
                effectContext);

            BattleTriggerContext dealt = BattleTriggerContext.AfterDamageDealt(damage);
            BattleTriggerContext taken = BattleTriggerContext.AfterDamageTaken(damage);

            Assert.AreEqual(BattleTriggerTiming.AfterDamageDealt, dealt.Timing);
            Assert.AreEqual(source, dealt.Owner);
            Assert.AreEqual(source, dealt.Source);
            Assert.AreEqual(target, dealt.Target);
            Assert.AreEqual(3, dealt.Amount);
            Assert.AreEqual(effectContext, dealt.EffectContext);
            Assert.AreEqual(BattleEffectTriggerPolicy.CanTriggerReactions, dealt.TriggerPolicy);

            Assert.AreEqual(BattleTriggerTiming.AfterDamageTaken, taken.Timing);
            Assert.AreEqual(target, taken.Owner);
            Assert.AreEqual(source, taken.Source);
            Assert.AreEqual(target, taken.Target);
            Assert.AreEqual(3, taken.Amount);
            Assert.AreEqual(effectContext, taken.EffectContext);
            Assert.AreEqual(BattleEffectTriggerPolicy.CanTriggerReactions, taken.TriggerPolicy);
        }

        [Test]
        public void BattleTriggerContext_AfterEnemyKilledMapsKillerAsOwnerAndSource()
        {
            var killer = new EntityId(1, 1);
            var victim = new EntityId(2, 1);
            BattleEffectContext effectContext = BattleEffectContext.Ability("execute", BattleEffectType.Damage);

            BattleTriggerContext context = BattleTriggerContext.AfterEnemyKilled(
                killer,
                victim,
                effectContext,
                BattleEffectTriggerPolicy.CanTriggerReactions);

            Assert.AreEqual(BattleTriggerTiming.AfterEnemyKilled, context.Timing);
            Assert.AreEqual(killer, context.Owner);
            Assert.AreEqual(killer, context.Source);
            Assert.AreEqual(victim, context.Target);
            Assert.AreEqual(0, context.Amount);
            Assert.AreEqual(effectContext, context.EffectContext);
            Assert.AreEqual(BattleEffectTriggerPolicy.CanTriggerReactions, context.TriggerPolicy);
        }
    }
}
