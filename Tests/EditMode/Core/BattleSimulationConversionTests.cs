using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleSimulationConversionTests
    {
        [Test]
        public void StatusApplicationDataFactory_CopiesAuthoringTriggersAndNestedApplyStatus()
        {
            var mark = new StatusDefinition(
                "mark",
                StatusPolarity.Debuff,
                durationTicks: 2,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new BattleModifierDefinition[0],
                triggers: new BattleTriggerDefinition[0]);
            var thorns = new StatusDefinition(
                "thorns",
                StatusPolarity.Buff,
                durationTicks: 4,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new BattleModifierDefinition[0],
                triggers: new[]
                {
                    new BattleTriggerDefinition(
                        BattleTriggerTiming.AfterDamageTaken,
                        new[]
                        {
                            BattleReactionEffectDefinition.Create(BattleReactionTarget.Source, BattleEffectDefinition.ApplyStatus(mark))
                        })
                });

            StatusApplicationData status = StatusApplicationDataFactory.Create(thorns);

            Assert.AreEqual("thorns", status.Id);
            Assert.AreEqual(StatusStackPolicy.RefreshDurationAndAddStack, status.StackPolicy);
            Assert.AreEqual(1, status.Triggers.Count);
            Assert.AreEqual(BattleTriggerTiming.AfterDamageTaken, status.Triggers[0].Timing);
            Assert.AreEqual(1, status.Triggers[0].Effects.Count);
            Assert.AreEqual(BattleEffectType.ApplyStatus, status.Triggers[0].Effects[0].Effect.Type);
            Assert.AreEqual(BattleReactionTarget.Source, status.Triggers[0].Effects[0].Target);
            Assert.AreEqual("mark", status.Triggers[0].Effects[0].Effect.Status.Id);
            Assert.AreEqual(StatusPolarity.Debuff, status.Triggers[0].Effects[0].Effect.Status.Polarity);
        }

        [Test]
        public void BattleEffectRuntimeDataFactory_CopiesNestedAreaAndStatusEffects()
        {
            var mark = new StatusDefinition(
                "mark",
                StatusPolarity.Debuff,
                durationTicks: 2,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new BattleModifierDefinition[0],
                triggers: new BattleTriggerDefinition[0]);
            var area = new AreaEffectDefinition(
                BattleScalar.FromFloat(1.5f),
                AreaEffectTargetFilter.Enemies,
                new[]
                {
                    BattleEffectDefinition.Damage(4),
                    BattleEffectDefinition.ApplyStatus(mark)
                });

            BattleEffectData data = BattleEffectRuntimeDataFactory.CreateEffectData(BattleEffectDefinition.AreaEffect(area));

            Assert.AreEqual(BattleEffectType.AreaEffect, data.Type);
            Assert.AreEqual(BattleScalar.FromFloat(1.5f), data.AreaEffect.Radius);
            Assert.AreEqual(AreaEffectTargetFilter.Enemies, data.AreaEffect.TargetFilter);
            Assert.AreEqual(2, data.AreaEffect.Effects.Count);
            Assert.AreEqual(BattleEffectType.Damage, data.AreaEffect.Effects[0].Type);
            Assert.AreEqual(4, data.AreaEffect.Effects[0].Amount);
            Assert.AreEqual(BattleEffectType.ApplyStatus, data.AreaEffect.Effects[1].Type);
            Assert.AreEqual("mark", data.AreaEffect.Effects[1].Status.Id);
        }
    }
}
