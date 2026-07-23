using System;
using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleModifierResolverTests
    {
        private static readonly EntityId Source = new EntityId(1, 1);

        [Test]
        public void ResolveScalarStat_AppliesFlatPercentStacksAndClamp()
        {
            StatusInstance haste = Status(
                "haste",
                BattleModifierInstance.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.Flat, BattleScalar.FromFloat(0.5f)),
                stackCount: 2,
                maxStacks: 3);
            StatusInstance slow = Status(
                "slow",
                BattleModifierInstance.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.PercentAdd, BattleScalar.FromFloat(-0.25f)));
            StatusInstance floor = Status(
                "floor",
                BattleModifierInstance.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.MinClamp, BattleScalar.Zero));

            BattleScalar result = BattleModifierResolver.ResolveScalarStat(
                BattleScalar.FromFloat(2f),
                new[] { haste, slow, floor },
                BattleStatId.MoveSpeed);

            Assert.AreEqual(BattleScalar.FromFloat(2.25f), result);
        }

        [Test]
        public void ResolveScalarStat_PercentAddStacksAdditively()
        {
            StatusInstance haste = Status(
                "haste",
                BattleModifierInstance.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.PercentAdd, BattleScalar.FromFloat(0.25f)),
                stackCount: 2,
                maxStacks: 2);

            BattleScalar result = BattleModifierResolver.ResolveScalarStat(
                BattleScalar.FromFloat(2f),
                new[] { haste },
                BattleStatId.MoveSpeed);

            Assert.AreEqual(BattleScalar.FromFloat(3f), result);
        }

        [Test]
        public void ResolveScalarStat_RejectsMultipleOverrides()
        {
            StatusInstance first = Status(
                "first",
                BattleModifierInstance.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.Override, BattleScalar.FromFloat(1f)));
            StatusInstance second = Status(
                "second",
                BattleModifierInstance.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.Override, BattleScalar.FromFloat(2f)));

            Assert.Throws<InvalidOperationException>(() =>
                BattleModifierResolver.ResolveScalarStat(
                    BattleScalar.FromFloat(3f),
                    new[] { first, second },
                    BattleStatId.MoveSpeed));
        }

        [Test]
        public void ResolveScalarStat_OverrideWinsAfterFlatAndPercent()
        {
            StatusInstance flat = Status(
                "flat",
                BattleModifierInstance.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.Flat, BattleScalar.FromFloat(5f)));
            StatusInstance percent = Status(
                "percent",
                BattleModifierInstance.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.PercentAdd, BattleScalar.FromFloat(1f)));
            StatusInstance overrideStatus = Status(
                "override",
                BattleModifierInstance.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.Override, BattleScalar.FromFloat(3f)));

            BattleScalar result = BattleModifierResolver.ResolveScalarStat(
                BattleScalar.FromFloat(10f),
                new[] { flat, percent, overrideStatus },
                BattleStatId.MoveSpeed);

            Assert.AreEqual(BattleScalar.FromFloat(3f), result);
        }

        [Test]
        public void ResolveScalarStat_OverrideDoesNotScaleByStackCount()
        {
            StatusInstance overrideStatus = Status(
                "override",
                BattleModifierInstance.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.Override, BattleScalar.FromFloat(4f)),
                stackCount: 3,
                maxStacks: 3);

            BattleScalar result = BattleModifierResolver.ResolveScalarStat(
                BattleScalar.FromFloat(10f),
                new[] { overrideStatus },
                BattleStatId.MoveSpeed);

            Assert.AreEqual(BattleScalar.FromFloat(4f), result);
        }

        [Test]
        public void ResolveScalarStat_MinClampDoesNotScaleByStackCount()
        {
            StatusInstance floor = Status(
                "floor",
                BattleModifierInstance.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.MinClamp, BattleScalar.FromFloat(6f)),
                stackCount: 3,
                maxStacks: 3);

            BattleScalar result = BattleModifierResolver.ResolveScalarStat(
                BattleScalar.FromFloat(4f),
                new[] { floor },
                BattleStatId.MoveSpeed);

            Assert.AreEqual(BattleScalar.FromFloat(6f), result);
        }

        [Test]
        public void ResolveScalarStat_AppliesMaxClampWithoutScalingByStackCount()
        {
            StatusInstance ceiling = Status(
                "ceiling",
                BattleModifierInstance.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.MaxClamp, BattleScalar.FromFloat(8f)),
                stackCount: 3,
                maxStacks: 3);

            BattleScalar result = BattleModifierResolver.ResolveScalarStat(
                BattleScalar.FromFloat(20f),
                new[] { ceiling },
                BattleStatId.MoveSpeed);

            Assert.AreEqual(BattleScalar.FromFloat(8f), result);
        }

        [Test]
        public void ResolveScalarStat_RejectsMinClampGreaterThanMaxClamp()
        {
            StatusInstance floor = Status(
                "floor",
                BattleModifierInstance.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.MinClamp, BattleScalar.FromFloat(10f)));
            StatusInstance ceiling = Status(
                "ceiling",
                BattleModifierInstance.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.MaxClamp, BattleScalar.FromFloat(5f)));

            Assert.Throws<InvalidOperationException>(() =>
                BattleModifierResolver.ResolveScalarStat(
                    BattleScalar.FromFloat(7f),
                    new[] { floor, ceiling },
                    BattleStatId.MoveSpeed));
        }

        [Test]
        public void ResolveScalarStat_ClampsMoveSpeedAtZero()
        {
            StatusInstance stop = Status(
                "stop",
                BattleModifierInstance.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.Flat, BattleScalar.FromFloat(-10f)));

            BattleScalar result = BattleModifierResolver.ResolveScalarStat(
                BattleScalar.FromFloat(2f),
                new[] { stop },
                BattleStatId.MoveSpeed);

            Assert.AreEqual(BattleScalar.Zero, result);
        }

        [Test]
        public void ResolveScalarStat_ClampsMaxHealthAtOne()
        {
            StatusInstance wound = Status(
                "wound",
                BattleModifierInstance.Stat(BattleStatId.MaxHealth, BattleModifierOperation.Flat, BattleScalar.FromInt(-99)));

            BattleScalar result = BattleModifierResolver.ResolveScalarStat(
                BattleScalar.FromInt(10),
                new[] { wound },
                BattleStatId.MaxHealth);

            Assert.AreEqual(BattleScalar.One, result);
        }

        [Test]
        public void StatModifierFactories_AcceptRuntimeStatsAndRejectInvalidIds()
        {
            Assert.DoesNotThrow(() =>
                BattleModifierDefinition.Stat(BattleStatId.MaxHealth, BattleModifierOperation.Flat, BattleScalar.One));
            Assert.DoesNotThrow(() =>
                BattleModifierData.Stat(BattleStatId.MaxHealth, BattleModifierOperation.Flat, BattleScalar.One));
            Assert.DoesNotThrow(() =>
                BattleModifierInstance.Stat(BattleStatId.MaxHealth, BattleModifierOperation.Flat, BattleScalar.One));

            var invalid = (BattleStatId)999;
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                BattleModifierDefinition.Stat(invalid, BattleModifierOperation.Flat, BattleScalar.One));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                BattleModifierData.Stat(invalid, BattleModifierOperation.Flat, BattleScalar.One));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                BattleModifierInstance.Stat(invalid, BattleModifierOperation.Flat, BattleScalar.One));
        }

        [Test]
        public void ResolveDamage_UsesSourceDealtAndTargetTakenModifiers()
        {
            StatusInstance source = Status(
                "rage",
                BattleModifierInstance.Damage(BattleDamageModifierStat.DamageDealt, BattleModifierOperation.Flat, BattleScalar.FromInt(2)));
            StatusInstance target = Status(
                "vulnerable",
                BattleModifierInstance.Damage(BattleDamageModifierStat.DamageTaken, BattleModifierOperation.PercentAdd, BattleScalar.FromFloat(0.5f)));

            int result = BattleModifierResolver.ResolveDamage(
                10,
                new[] { source },
                new[] { target },
                BattleEffectContext.Ability("slash", BattleEffectType.Damage));

            Assert.AreEqual(18, result);
        }

        [Test]
        public void ResolveDamage_RoundsHalfUpAndSaturates()
        {
            StatusInstance plusHalf = Status(
                "plus-half",
                BattleModifierInstance.Damage(BattleDamageModifierStat.DamageDealt, BattleModifierOperation.PercentAdd, BattleScalar.FromFloat(0.5f)));
            StatusInstance huge = Status(
                "huge",
                BattleModifierInstance.Damage(BattleDamageModifierStat.DamageDealt, BattleModifierOperation.Flat, BattleScalar.FromInt(int.MaxValue)));

            Assert.AreEqual(5, BattleModifierResolver.ResolveDamage(3, new[] { plusHalf }, Array.Empty<StatusInstance>(), BattleEffectContext.Unknown(BattleEffectType.Damage)));
            Assert.AreEqual(int.MaxValue, BattleModifierResolver.ResolveDamage(int.MaxValue, new[] { huge }, Array.Empty<StatusInstance>(), BattleEffectContext.Unknown(BattleEffectType.Damage)));
        }

        [Test]
        public void ResolveDamage_AppliesSourcePercentBeforeTargetFlat()
        {
            StatusInstance source = Status(
                "rage",
                BattleModifierInstance.Damage(BattleDamageModifierStat.DamageDealt, BattleModifierOperation.PercentAdd, BattleScalar.FromFloat(0.5f)));
            StatusInstance target = Status(
                "vulnerable",
                BattleModifierInstance.Damage(BattleDamageModifierStat.DamageTaken, BattleModifierOperation.Flat, BattleScalar.FromInt(2)));

            int result = BattleModifierResolver.ResolveDamage(
                10,
                new[] { source },
                new[] { target },
                BattleEffectContext.Unknown(BattleEffectType.Damage));

            Assert.AreEqual(17, result);
        }

        [Test]
        public void ResolveDamage_DamageDealtOverrideParticipatesInSourcePass()
        {
            StatusInstance source = StatusWithModifiers(
                "rage",
                BattleModifierInstance.Damage(BattleDamageModifierStat.DamageDealt, BattleModifierOperation.Flat, BattleScalar.FromInt(5)),
                BattleModifierInstance.Damage(BattleDamageModifierStat.DamageDealt, BattleModifierOperation.Override, BattleScalar.FromInt(7)));
            StatusInstance target = Status(
                "vulnerable",
                BattleModifierInstance.Damage(BattleDamageModifierStat.DamageTaken, BattleModifierOperation.Flat, BattleScalar.FromInt(1)));

            int result = BattleModifierResolver.ResolveDamage(
                10,
                new[] { source },
                new[] { target },
                BattleEffectContext.Unknown(BattleEffectType.Damage));

            Assert.AreEqual(8, result);
        }

        [Test]
        public void ResolveDamage_DamageTakenMinAndMaxClampParticipateInTargetPass()
        {
            StatusInstance minimum = Status(
                "minimum",
                BattleModifierInstance.Damage(BattleDamageModifierStat.DamageTaken, BattleModifierOperation.MinClamp, BattleScalar.FromInt(5)));
            StatusInstance maximum = Status(
                "maximum",
                BattleModifierInstance.Damage(BattleDamageModifierStat.DamageTaken, BattleModifierOperation.MaxClamp, BattleScalar.FromInt(8)));

            int raised = BattleModifierResolver.ResolveDamage(
                3,
                Array.Empty<StatusInstance>(),
                new[] { minimum },
                BattleEffectContext.Unknown(BattleEffectType.Damage));
            int lowered = BattleModifierResolver.ResolveDamage(
                10,
                Array.Empty<StatusInstance>(),
                new[] { maximum },
                BattleEffectContext.Unknown(BattleEffectType.Damage));

            Assert.AreEqual(5, raised);
            Assert.AreEqual(8, lowered);
        }

        [Test]
        public void ResolveDamage_DuplicateOverridesWithinSingleDamagePassThrow()
        {
            StatusInstance first = Status(
                "first",
                BattleModifierInstance.Damage(BattleDamageModifierStat.DamageDealt, BattleModifierOperation.Override, BattleScalar.FromInt(7)));
            StatusInstance second = Status(
                "second",
                BattleModifierInstance.Damage(BattleDamageModifierStat.DamageDealt, BattleModifierOperation.Override, BattleScalar.FromInt(9)));

            Assert.Throws<InvalidOperationException>(() =>
                BattleModifierResolver.ResolveDamage(
                    10,
                    new[] { first, second },
                    Array.Empty<StatusInstance>(),
                    BattleEffectContext.Unknown(BattleEffectType.Damage)));
        }

        [Test]
        public void ResolveDamage_IgnoresWrongSideDamageModifiers()
        {
            StatusInstance sourceTaken = Status(
                "wrong-source",
                BattleModifierInstance.Damage(BattleDamageModifierStat.DamageTaken, BattleModifierOperation.Flat, BattleScalar.FromInt(100)));
            StatusInstance targetDealt = Status(
                "wrong-target",
                BattleModifierInstance.Damage(BattleDamageModifierStat.DamageDealt, BattleModifierOperation.Flat, BattleScalar.FromInt(100)));

            int result = BattleModifierResolver.ResolveDamage(
                10,
                new[] { sourceTaken },
                new[] { targetDealt },
                BattleEffectContext.Unknown(BattleEffectType.Damage));

            Assert.AreEqual(10, result);
        }

        private static StatusInstance Status(
            string id,
            BattleModifierInstance modifier,
            int stackCount = 1,
            int maxStacks = 1)
        {
            return StatusWithModifiers(id, stackCount, maxStacks, modifier);
        }

        private static StatusInstance StatusWithModifiers(
            string id,
            params BattleModifierInstance[] modifiers)
        {
            return StatusWithModifiers(id, stackCount: 1, maxStacks: 1, modifiers: modifiers);
        }

        private static StatusInstance StatusWithModifiers(
            string id,
            int stackCount,
            int maxStacks,
            params BattleModifierInstance[] modifiers)
        {
            return new StatusInstance(
                id,
                StatusPolarity.Buff,
                Source,
                durationRemainingTicks: 3,
                tickIntervalTicks: 1,
                ticksUntilNextPeriodicEffect: 1,
                periodicDamage: 0,
                modifiers,
                triggers: Array.Empty<BattleTriggerInstance>(),
                stackCount,
                maxStacks);
        }
    }
}
