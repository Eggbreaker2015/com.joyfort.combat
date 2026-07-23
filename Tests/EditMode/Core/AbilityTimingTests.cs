using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class AbilityTimingTests
    {
        [Test]
        public void AbilityDefinition_DefaultTimingIsZero()
        {
            var ability = TestCombatants.Ability("slash", 1f, damage: 3, cooldownTicks: 2);

            Assert.AreEqual(0, ability.WindupTicks);
            Assert.AreEqual(0, ability.RecoveryTicks);
        }

        [Test]
        public void AbilityDefinition_StoresWindupAndRecoveryTicks()
        {
            var ability = new AbilityDefinition(
                "slash",
                BattleScalar.FromFloat(1f),
                cooldownTicks: 2,
                windupTicks: 3,
                recoveryTicks: 4,
                AbilityTargetSelection.CurrentEnemyTarget,
                TestCombatants.EffectFrames(3, TestCombatants.Effects(3)));

            Assert.AreEqual(3, ability.WindupTicks);
            Assert.AreEqual(4, ability.RecoveryTicks);
        }

        [Test]
        public void AbilityEffectFrameDefinition_StoresFrameTimingOrderAndEffects()
        {
            var frame = new AbilityEffectFrameDefinition(
                "hit_02",
                tickOffset: 5,
                order: 2,
                effects: TestCombatants.Effects(3));

            Assert.AreEqual("hit_02", frame.FrameId);
            Assert.AreEqual(5, frame.TickOffset);
            Assert.AreEqual(2, frame.Order);
            Assert.AreEqual(1, frame.Effects.Count);
            Assert.AreEqual(BattleEffectType.Damage, frame.Effects[0].Type);
        }

        [Test]
        public void AbilityDefinition_StoresExplicitEffectFrame()
        {
            var ability = new AbilityDefinition(
                "slash",
                BattleScalar.FromFloat(1f),
                cooldownTicks: 2,
                windupTicks: 3,
                recoveryTicks: 4,
                AbilityTargetSelection.CurrentEnemyTarget,
                TestCombatants.EffectFrames(3, TestCombatants.Effects(3)));

            Assert.AreEqual(1, ability.EffectFrames.Count);
            Assert.AreEqual("release", ability.EffectFrames[0].FrameId);
            Assert.AreEqual(3, ability.EffectFrames[0].TickOffset);
            Assert.AreEqual(0, ability.EffectFrames[0].Order);
            Assert.AreEqual(1, ability.EffectFrames[0].Effects.Count);
        }

        [Test]
        public void AbilitySpawnData_StoresExplicitEffectFrame()
        {
            var ability = new AbilitySpawnData(
                "slash",
                BattleScalar.FromFloat(1f),
                cooldownTicks: 2,
                windupTicks: 3,
                recoveryTicks: 4,
                AbilityTargetSelection.CurrentEnemyTarget,
                TestCombatants.EffectFrameData(3, TestCombatants.EffectData(3)));

            Assert.AreEqual(1, ability.EffectFrames.Count);
            Assert.AreEqual("release", ability.EffectFrames[0].FrameId);
            Assert.AreEqual(3, ability.EffectFrames[0].TickOffset);
            Assert.AreEqual(0, ability.EffectFrames[0].Order);
            Assert.AreEqual(1, ability.EffectFrames[0].Effects.Count);
        }

        [Test]
        public void AbilityState_StoresExplicitEffectFrame()
        {
            var ability = new AbilityState(
                "slash",
                BattleScalar.FromFloat(1f),
                cooldownTicks: 2,
                cooldownRemainingTicks: 0,
                windupTicks: 3,
                recoveryTicks: 4,
                AbilityTargetSelection.CurrentEnemyTarget,
                TestCombatants.EffectFrameData(3, TestCombatants.EffectData(3)));

            Assert.AreEqual(1, ability.EffectFrames.Count);
            Assert.AreEqual("release", ability.EffectFrames[0].FrameId);
            Assert.AreEqual(3, ability.EffectFrames[0].TickOffset);
            Assert.AreEqual(0, ability.EffectFrames[0].Order);
            Assert.AreEqual(1, ability.EffectFrames[0].Effects.Count);
        }

        [Test]
        public void AbilityDefinition_RejectsNegativeTiming()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new AbilityDefinition(
                "slash",
                BattleScalar.FromFloat(1f),
                cooldownTicks: 2,
                windupTicks: -1,
                recoveryTicks: 0,
                AbilityTargetSelection.CurrentEnemyTarget,
                TestCombatants.EffectFrames(0, TestCombatants.Effects(3))));

            Assert.Throws<System.ArgumentOutOfRangeException>(() => new AbilityDefinition(
                "slash",
                BattleScalar.FromFloat(1f),
                cooldownTicks: 2,
                windupTicks: 0,
                recoveryTicks: -1,
                AbilityTargetSelection.CurrentEnemyTarget,
                TestCombatants.EffectFrames(0, TestCombatants.Effects(3))));
        }

        [Test]
        public void AbilitySpawnData_StoresTiming()
        {
            var ability = new AbilitySpawnData(
                "slash",
                BattleScalar.FromFloat(1f),
                cooldownTicks: 2,
                windupTicks: 3,
                recoveryTicks: 4,
                AbilityTargetSelection.CurrentEnemyTarget,
                TestCombatants.EffectFrameData(3, TestCombatants.EffectData(3)));

            Assert.AreEqual(3, ability.WindupTicks);
            Assert.AreEqual(4, ability.RecoveryTicks);
        }

        [Test]
        public void AbilityState_StoresTiming()
        {
            var ability = new AbilityState(
                "slash",
                BattleScalar.FromFloat(1f),
                cooldownTicks: 2,
                cooldownRemainingTicks: 0,
                windupTicks: 3,
                recoveryTicks: 4,
                AbilityTargetSelection.CurrentEnemyTarget,
                TestCombatants.EffectFrameData(3, TestCombatants.EffectData(3)));

            Assert.AreEqual(3, ability.WindupTicks);
            Assert.AreEqual(4, ability.RecoveryTicks);
        }
    }
}
