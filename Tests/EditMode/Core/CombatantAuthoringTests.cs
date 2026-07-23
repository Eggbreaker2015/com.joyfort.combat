using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class CombatantAuthoringTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void CombatantDefinition_RejectsMissingId(string id)
        {
            Assert.Throws<ArgumentException>(() => new CombatantDefinition(
                id,
                radius: BattleScalar.FromFloat(0.25f),
                stats: TestCombatants.Stats(),
                basicAbility: BasicAbilityDefinition(),
                abilities: new AbilityDefinition[0]));
        }

        [Test]
        public void CombatantDefinition_RejectsMissingStats()
        {
            Assert.Throws<ArgumentNullException>(() => new CombatantDefinition(
                "bad",
                radius: BattleScalar.FromFloat(0.25f),
                stats: null,
                basicAbility: BasicAbilityDefinition(),
                abilities: new AbilityDefinition[0]));
        }

        [Test]
        public void CombatantDefinition_RejectsInvalidRequiredStats()
        {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => TestCombatants.Create(maxHealth: 0));

            Assert.That(exception.Message, Does.Contain("MaxHealth"));
            Assert.Throws<ArgumentOutOfRangeException>(() => TestCombatants.Create(moveSpeed: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => TestCombatants.Create(attackRange: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => TestCombatants.Create(attackDamage: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => TestCombatants.Create(radius: -0.1f));
        }

        [Test]
        public void CombatantDefinition_RejectsMissingRequiredStat()
        {
            var stats = new BattleStatBlock(new[]
            {
                new BattleStatEntry(BattleStatId.MaxHealth, 10)
            });

            Exception exception = Assert.Throws<ArgumentException>(() => new CombatantDefinition(
                "bad",
                radius: BattleScalar.FromFloat(0.25f),
                stats: stats,
                basicAbility: BasicAbilityDefinition(),
                abilities: new AbilityDefinition[0]));

            Assert.That(exception.Message, Does.Contain("MoveSpeed"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void AbilityDefinition_RejectsMissingId(string id)
        {
            Assert.Throws<ArgumentException>(() => TestCombatants.Ability(id, range: 1f, damage: 3, cooldownTicks: 2, appliedStatuses: new StatusDefinition[0], projectileEmitters: new ProjectileEmitterSpawnData[0]));
        }

        [Test]
        public void AbilityDefinition_RejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TestCombatants.Ability("slash", range: -0.1f, damage: 3, cooldownTicks: 2, appliedStatuses: new StatusDefinition[0], projectileEmitters: new ProjectileEmitterSpawnData[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => TestCombatants.Ability("slash", range: 1f, damage: -1, cooldownTicks: 2, appliedStatuses: new StatusDefinition[0], projectileEmitters: new ProjectileEmitterSpawnData[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => TestCombatants.Ability("slash", range: 1f, damage: 3, cooldownTicks: -1, appliedStatuses: new StatusDefinition[0], projectileEmitters: new ProjectileEmitterSpawnData[0]));
        }

        [Test]
        public void AbilityDefinition_StoresValidatedValues()
        {
            var ability = TestCombatants.Ability("slash", range: 1.5f, damage: 4, cooldownTicks: 3, appliedStatuses: new StatusDefinition[0], projectileEmitters: new ProjectileEmitterSpawnData[0]);

            Assert.AreEqual("slash", ability.Id);
            Assert.AreEqual(BattleScalar.FromFloat(1.5f), ability.Range);
            Assert.AreEqual(1, AbilityEffects(ability).Count);
            Assert.AreEqual(BattleEffectType.Damage, AbilityEffects(ability)[0].Type);
            Assert.AreEqual(4, AbilityEffects(ability)[0].Amount);
            Assert.AreEqual(3, ability.CooldownTicks);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void StatusDefinition_RejectsMissingId(string id)
        {
            Assert.Throws<ArgumentException>(() => new StatusDefinition(
                id,
                StatusPolarity.Debuff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 2,
                modifiers: new BattleModifierDefinition[0],
                triggers: new BattleTriggerDefinition[0]));
        }

        [Test]
        public void StatusDefinition_RejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StatusDefinition("burn", StatusPolarity.Debuff, durationTicks: 0, tickIntervalTicks: 1, periodicDamage: 2, modifiers: new BattleModifierDefinition[0], triggers: new BattleTriggerDefinition[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StatusDefinition("burn", StatusPolarity.Debuff, durationTicks: 3, tickIntervalTicks: 0, periodicDamage: 2, modifiers: new BattleModifierDefinition[0], triggers: new BattleTriggerDefinition[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StatusDefinition("burn", StatusPolarity.Debuff, durationTicks: 3, tickIntervalTicks: 1, periodicDamage: -1, modifiers: new BattleModifierDefinition[0], triggers: new BattleTriggerDefinition[0]));
        }

        [Test]
        public void StatusDefinition_StoresValidatedValues()
        {
            var status = new StatusDefinition("burn", StatusPolarity.Debuff, durationTicks: 3, tickIntervalTicks: 1, periodicDamage: 2, modifiers: new BattleModifierDefinition[0], triggers: new BattleTriggerDefinition[0]);

            Assert.AreEqual("burn", status.Id);
            Assert.AreEqual(StatusPolarity.Debuff, status.Polarity);
            Assert.AreEqual(3, status.DurationTicks);
            Assert.AreEqual(1, status.TickIntervalTicks);
            Assert.AreEqual(2, status.PeriodicDamage);
        }

        [Test]
        public void BattleModifierDefinition_StoresValues()
        {
            BattleModifierDefinition modifier = BattleModifierDefinition.Damage(
                BattleDamageModifierStat.DamageDealt,
                BattleModifierOperation.PercentAdd,
                BattleScalar.FromFloat(0.5f));

            Assert.AreEqual(BattleModifierTarget.Damage, modifier.Target);
            Assert.AreEqual(BattleDamageModifierStat.DamageDealt, modifier.DamageStat);
            Assert.AreEqual(BattleModifierOperation.PercentAdd, modifier.Operation);
            Assert.AreEqual(BattleScalar.FromFloat(0.5f), modifier.Value);
        }

        [Test]
        public void BattleEffectDefinition_StoresCurrentEffectTypes()
        {
            var mark = new StatusDefinition(
                "mark",
                StatusPolarity.Debuff,
                durationTicks: 2,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new BattleModifierDefinition[0],
                triggers: new BattleTriggerDefinition[0]);
            ProjectileEmitterSpawnData emitter = CreateProjectileEmitter();

            BattleEffectDefinition damage = BattleEffectDefinition.Damage(3);
            BattleEffectDefinition applyStatus = BattleEffectDefinition.ApplyStatus(mark);
            BattleEffectDefinition spawnEmitter = BattleEffectDefinition.SpawnProjectileEmitter(emitter);

            Assert.AreEqual(BattleEffectType.Damage, damage.Type);
            Assert.AreEqual(3, damage.Amount);
            Assert.AreEqual(BattleEffectType.ApplyStatus, applyStatus.Type);
            Assert.AreSame(mark, applyStatus.Status);
            Assert.AreEqual(BattleEffectType.SpawnProjectileEmitter, spawnEmitter.Type);
            Assert.AreEqual(ProjectileEmitterAnchorMode.FollowSource, spawnEmitter.ProjectileEmitter.AnchorMode);
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleEffectDefinition.Damage(0));
            Assert.Throws<ArgumentNullException>(() => BattleEffectDefinition.ApplyStatus(null));
        }

        [Test]
        public void BattleEffectDefinition_StoresHealAndAreaEffect()
        {
            BattleEffectDefinition heal = BattleEffectDefinition.Heal(6);
            var area = new AreaEffectDefinition(
                BattleScalar.FromFloat(2.5f),
                AreaEffectTargetFilter.Allies,
                new[] { BattleEffectDefinition.Heal(4) });
            BattleEffectDefinition areaEffect = BattleEffectDefinition.AreaEffect(area);

            Assert.AreEqual(BattleEffectType.Heal, heal.Type);
            Assert.AreEqual(6, heal.Amount);
            Assert.AreEqual(BattleEffectType.AreaEffect, areaEffect.Type);
            Assert.AreEqual(BattleScalar.FromFloat(2.5f), areaEffect.Area.Radius);
            Assert.AreEqual(AreaEffectTargetFilter.Allies, areaEffect.Area.TargetFilter);
            Assert.AreEqual(BattleEffectType.Heal, areaEffect.Area.Effects[0].Type);
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleEffectDefinition.Heal(0));
            Assert.Throws<ArgumentNullException>(() => BattleEffectDefinition.AreaEffect(null));
        }

        [Test]
        public void AreaEffectDefinition_RejectsInvalidValuesAndNestedAreaEffect()
        {
            var child = BattleEffectDefinition.Heal(3);
            var nested = new AreaEffectDefinition(
                BattleScalar.FromFloat(1f),
                AreaEffectTargetFilter.AllUnits,
                new[] { child });

            Assert.Throws<ArgumentOutOfRangeException>(() => new AreaEffectDefinition(BattleScalar.Zero, AreaEffectTargetFilter.AllUnits, new[] { child }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AreaEffectDefinition(BattleScalar.FromFloat(1f), (AreaEffectTargetFilter)999, new[] { child }));
            Assert.Throws<ArgumentNullException>(() => new AreaEffectDefinition(BattleScalar.FromFloat(1f), AreaEffectTargetFilter.AllUnits, null));
            Assert.Throws<ArgumentException>(() => new AreaEffectDefinition(BattleScalar.FromFloat(1f), AreaEffectTargetFilter.AllUnits, Array.Empty<BattleEffectDefinition>()));
            Assert.Throws<ArgumentNullException>(() => new AreaEffectDefinition(BattleScalar.FromFloat(1f), AreaEffectTargetFilter.AllUnits, new BattleEffectDefinition[] { null }));
            Assert.Throws<ArgumentException>(() => new AreaEffectDefinition(BattleScalar.FromFloat(1f), AreaEffectTargetFilter.AllUnits, new[] { BattleEffectDefinition.AreaEffect(nested) }));
        }

        [Test]
        public void BattleReactionEffectDefinition_DamageStoresDataAndValidatesAmount()
        {
            BattleReactionEffectDefinition effect = BattleReactionEffectDefinition.Create(
                BattleReactionTarget.Source,
                BattleEffectDefinition.Damage(3));

            Assert.AreEqual(BattleReactionTarget.Source, effect.Target);
            Assert.AreEqual(BattleEffectType.Damage, effect.Effect.Type);
            Assert.AreEqual(3, effect.Effect.Amount);
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleReactionEffectDefinition.Create(BattleReactionTarget.Target, BattleEffectDefinition.Damage(0)));
        }

        [Test]
        public void BattleReactionEffectDefinition_ApplyStatusStoresDataAndValidatesStatus()
        {
            var mark = new StatusDefinition(
                "mark",
                StatusPolarity.Debuff,
                durationTicks: 2,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new BattleModifierDefinition[0],
                triggers: new BattleTriggerDefinition[0]);

            BattleReactionEffectDefinition effect = BattleReactionEffectDefinition.Create(
                BattleReactionTarget.Target,
                BattleEffectDefinition.ApplyStatus(mark));

            Assert.AreEqual(BattleReactionTarget.Target, effect.Target);
            Assert.AreEqual(BattleEffectType.ApplyStatus, effect.Effect.Type);
            Assert.AreSame(mark, effect.Effect.Status);
            Assert.Throws<ArgumentNullException>(() => BattleReactionEffectDefinition.Create(BattleReactionTarget.Self, null));
        }

        [Test]
        public void BattleReactionEffectDefinition_RejectsInvalidTarget()
        {
            BattleReactionTarget invalidTarget = (BattleReactionTarget)999;
            var mark = new StatusDefinition(
                "mark",
                StatusPolarity.Debuff,
                durationTicks: 2,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new BattleModifierDefinition[0],
                triggers: new BattleTriggerDefinition[0]);

            Assert.Throws<ArgumentOutOfRangeException>(() => BattleReactionEffectDefinition.Create(invalidTarget, BattleEffectDefinition.Damage(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleReactionEffectDefinition.Create(invalidTarget, BattleEffectDefinition.ApplyStatus(mark)));
        }

        [Test]
        public void BattleTriggerDefinition_CopiesEffectsAndValidatesNulls()
        {
            var input = new[]
            {
                BattleReactionEffectDefinition.Create(BattleReactionTarget.Source, BattleEffectDefinition.Damage(4))
            };

            var trigger = new BattleTriggerDefinition(BattleTriggerTiming.AfterDamageTaken, input);
            input[0] = BattleReactionEffectDefinition.Create(BattleReactionTarget.Target, BattleEffectDefinition.Damage(9));

            Assert.AreEqual(BattleTriggerTiming.AfterDamageTaken, trigger.Timing);
            Assert.AreEqual(1, trigger.Effects.Count);
            Assert.AreEqual(BattleReactionTarget.Source, trigger.Effects[0].Target);
            Assert.AreEqual(4, trigger.Effects[0].Effect.Amount);
            Assert.IsFalse(trigger.Effects is BattleReactionEffectDefinition[]);
            Assert.Throws<ArgumentNullException>(() => new BattleTriggerDefinition(BattleTriggerTiming.AfterDamageDealt, null));
            Assert.Throws<ArgumentNullException>(() => new BattleTriggerDefinition(BattleTriggerTiming.AfterDamageDealt, new BattleReactionEffectDefinition[] { null }));
        }

        [Test]
        public void BattleTriggerDefinition_RejectsInvalidTiming()
        {
            BattleTriggerTiming invalidTiming = (BattleTriggerTiming)999;

            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleTriggerDefinition(
                invalidTiming,
                new[]
                {
                    BattleReactionEffectDefinition.Create(BattleReactionTarget.Source, BattleEffectDefinition.Damage(1))
                }));
        }

        [Test]
        public void StatusDefinition_StoresTriggersAndCopiesInput()
        {
            var triggers = new[]
            {
                new BattleTriggerDefinition(
                    BattleTriggerTiming.AfterDamageTaken,
                    new[]
                    {
                        BattleReactionEffectDefinition.Create(BattleReactionTarget.Source, BattleEffectDefinition.Damage(2))
                    })
            };

            var status = new StatusDefinition(
                "thorns",
                StatusPolarity.Buff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new BattleModifierDefinition[0],
                triggers: triggers);
            triggers[0] = new BattleTriggerDefinition(
                BattleTriggerTiming.AfterDamageDealt,
                new[]
                {
                    BattleReactionEffectDefinition.Create(BattleReactionTarget.Target, BattleEffectDefinition.Damage(9))
                });

            Assert.AreEqual(1, status.Triggers.Count);
            Assert.AreEqual(BattleTriggerTiming.AfterDamageTaken, status.Triggers[0].Timing);
            Assert.AreEqual(BattleReactionTarget.Source, status.Triggers[0].Effects[0].Target);
            Assert.AreEqual(2, status.Triggers[0].Effects[0].Effect.Amount);
            Assert.IsFalse(status.Triggers is BattleTriggerDefinition[]);
            Assert.Throws<ArgumentNullException>(() => new StatusDefinition("bad", StatusPolarity.Neutral, 1, 1, 0, new BattleModifierDefinition[0], null));
            Assert.Throws<ArgumentNullException>(() => new StatusDefinition("bad", StatusPolarity.Neutral, 1, 1, 0, new BattleModifierDefinition[0], new BattleTriggerDefinition[] { null }));
        }

        [Test]
        public void StatusDefinition_StoresAndCopiesModifiers()
        {
            var modifiers = new[]
            {
                BattleModifierDefinition.Damage(BattleDamageModifierStat.DamageTaken, BattleModifierOperation.Flat, BattleScalar.FromInt(3))
            };

            var status = new StatusDefinition(
                "vulnerable",
                StatusPolarity.Debuff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers,
                triggers: new BattleTriggerDefinition[0]);

            modifiers[0] = BattleModifierDefinition.Damage(BattleDamageModifierStat.DamageDealt, BattleModifierOperation.Flat, BattleScalar.FromInt(9));

            Assert.AreEqual(1, status.Modifiers.Count);
            Assert.AreEqual(BattleModifierTarget.Damage, status.Modifiers[0].Target);
            Assert.AreEqual(BattleDamageModifierStat.DamageTaken, status.Modifiers[0].DamageStat);
            Assert.AreEqual(BattleModifierOperation.Flat, status.Modifiers[0].Operation);
            Assert.AreEqual(BattleScalar.FromInt(3), status.Modifiers[0].Value);
            Assert.IsFalse(status.Modifiers is BattleModifierDefinition[]);
        }

        [Test]
        public void StatusDefinition_RejectsInvalidModifiers()
        {
            Assert.Throws<ArgumentNullException>(() => new StatusDefinition(
                "vulnerable",
                StatusPolarity.Debuff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: null,
                triggers: new BattleTriggerDefinition[0]));

            Assert.Throws<ArgumentNullException>(() => new StatusDefinition(
                "vulnerable",
                StatusPolarity.Debuff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new BattleModifierDefinition[] { null },
                triggers: new BattleTriggerDefinition[0]));
        }

        [Test]
        public void AbilityDefinition_StoresAppliedStatuses()
        {
            var burn = new StatusDefinition("burn", StatusPolarity.Debuff, durationTicks: 3, tickIntervalTicks: 1, periodicDamage: 2, modifiers: new BattleModifierDefinition[0], triggers: new BattleTriggerDefinition[0]);
            var ability = TestCombatants.Ability("firebolt", range: 2f, damage: 4, cooldownTicks: 3, appliedStatuses: new[] { burn }, projectileEmitters: new ProjectileEmitterSpawnData[0]);

            Assert.AreEqual(2, AbilityEffects(ability).Count);
            Assert.AreEqual(BattleEffectType.Damage, AbilityEffects(ability)[0].Type);
            Assert.AreEqual(4, AbilityEffects(ability)[0].Amount);
            Assert.AreEqual(BattleEffectType.ApplyStatus, AbilityEffects(ability)[1].Type);
            Assert.AreSame(burn, AbilityEffects(ability)[1].Status);
        }

        [Test]
        public void AbilityDefinition_RejectsInvalidAppliedStatuses()
        {
            Assert.Throws<ArgumentNullException>(() => new AbilityDefinition(
                "firebolt",
                BattleScalar.FromFloat(2f),
                cooldownTicks: 3,
                windupTicks: 0,
                recoveryTicks: 0,
                AbilityTargetSelection.CurrentEnemyTarget,
                null));
            Assert.Throws<ArgumentNullException>(() => TestCombatants.Ability("firebolt", 2f, 4, 3, new StatusDefinition[] { null }, new ProjectileEmitterSpawnData[0]));
            Assert.Throws<ArgumentException>(() => TestCombatants.Ability(
                "firebolt",
                2f,
                4,
                3,
                new[]
                {
                    new StatusDefinition("burn", StatusPolarity.Debuff, 3, 1, 2, modifiers: new BattleModifierDefinition[0], triggers: new BattleTriggerDefinition[0]),
                    new StatusDefinition("burn", StatusPolarity.Debuff, 4, 1, 3, modifiers: new BattleModifierDefinition[0], triggers: new BattleTriggerDefinition[0])
                },
                new ProjectileEmitterSpawnData[0]));
        }

        [Test]
        public void AbilityDefinition_CopiesAppliedStatuses()
        {
            var statuses = new[]
            {
                new StatusDefinition("burn", StatusPolarity.Debuff, 3, 1, 2, modifiers: new BattleModifierDefinition[0], triggers: new BattleTriggerDefinition[0])
            };
            var ability = TestCombatants.Ability("firebolt", 2f, 4, 3, statuses, new ProjectileEmitterSpawnData[0]);

            statuses[0] = new StatusDefinition("poison", StatusPolarity.Debuff, 3, 1, 1, modifiers: new BattleModifierDefinition[0], triggers: new BattleTriggerDefinition[0]);

            Assert.AreEqual("burn", AbilityEffects(ability)[1].Status.Id);
        }

        [Test]
        public void CombatantDefinition_StoresValidatedValues()
        {
            var ability = TestCombatants.Ability("slash", range: 1.5f, damage: 4, cooldownTicks: 3, appliedStatuses: new StatusDefinition[0], projectileEmitters: new ProjectileEmitterSpawnData[0]);
            var basicAbility = BasicAbilityDefinition();
            var stats = TestCombatants.Stats(maxHealth: 20, moveSpeed: 2f);
            var definition = new CombatantDefinition("melee", radius: BattleScalar.FromFloat(0.25f), stats: stats, basicAbility: basicAbility, abilities: new[] { ability });

            Assert.AreEqual("melee", definition.Id);
            Assert.AreSame(stats, definition.Stats);
            Assert.AreEqual(20, definition.Stats.RequireInt(BattleStatId.MaxHealth, "test"));
            Assert.AreEqual(2f, definition.Stats.RequireFloat(BattleStatId.MoveSpeed, "test"));
            Assert.AreEqual(BattleScalar.FromFloat(0.25f), definition.Radius);
            Assert.AreSame(basicAbility, definition.BasicAbility);
            Assert.AreEqual(1, definition.Abilities.Count);
            Assert.AreSame(ability, definition.Abilities[0]);
            Assert.IsFalse(definition.Abilities is AbilityDefinition[]);
        }

        [Test]
        public void CombatantDefinition_RejectsInvalidAbilities()
        {
            Assert.Throws<ArgumentNullException>(() => new CombatantDefinition("bad", BattleScalar.FromFloat(0.25f), TestCombatants.Stats(), null, new AbilityDefinition[0]));
            Assert.Throws<ArgumentNullException>(() => new CombatantDefinition("bad", BattleScalar.FromFloat(0.25f), TestCombatants.Stats(), BasicAbilityDefinition(), null));
            Assert.Throws<ArgumentNullException>(() => new CombatantDefinition("bad", BattleScalar.FromFloat(0.25f), TestCombatants.Stats(), BasicAbilityDefinition(), new AbilityDefinition[] { null }));
            Assert.Throws<ArgumentException>(() => new CombatantDefinition(
                "bad",
                BattleScalar.FromFloat(0.25f),
                TestCombatants.Stats(),
                BasicAbilityDefinition(),
                new[]
                {
                    TestCombatants.Ability("slash", 1f, 1, 1),
                    TestCombatants.Ability("slash", 2f, 2, 2)
                }));
            Assert.Throws<ArgumentException>(() => new CombatantDefinition(
                "bad",
                BattleScalar.FromFloat(0.25f),
                TestCombatants.Stats(),
                BasicAbilityDefinition("slash"),
                new[]
                {
                    TestCombatants.Ability("slash", 1f, 1, 1)
                }));
        }

        [Test]
        public void CombatantDefinition_CopiesAbilities()
        {
            var abilities = new[]
            {
                TestCombatants.Ability("slash", 1f, 1, 1)
            };
            var definition = new CombatantDefinition("melee", BattleScalar.FromFloat(0.25f), TestCombatants.Stats(), BasicAbilityDefinition(), abilities);

            abilities[0] = TestCombatants.Ability("changed", 2f, 2, 2);

            Assert.AreEqual("slash", definition.Abilities[0].Id);
        }

        [Test]
        public void InitialCombatantSpawn_RejectsNullDefinition()
        {
            Assert.Throws<ArgumentNullException>(() => new InitialCombatantSpawn(
                new TeamId(1),
                null,
                new BattleVector2(0f, 0f)));
        }

        [Test]
        public void InitialCombatantSpawn_StoresAuthoringValues()
        {
            var definition = TestCombatants.Create();

            var spawn = new InitialCombatantSpawn(new TeamId(1), definition, new BattleVector2(2f, 3f));

            Assert.AreEqual(new TeamId(1), spawn.TeamId);
            Assert.AreSame(definition, spawn.Definition);
            Assert.AreEqual(new BattleVector2(2f, 3f), spawn.Position);
        }

        [Test]
        public void CombatantSpawnData_RejectsInvalidPayload()
        {
            Assert.Throws<ArgumentException>(() => new CombatantSpawnData(new TeamId(1), null, new BattleVector2(0f, 0f), 10, BattleScalar.FromFloat(0.25f), BattleScalar.One, BasicAbilitySpawn(), new AbilitySpawnData[0]));
            Assert.Throws<ArgumentException>(() => new CombatantSpawnData(new TeamId(1), " ", new BattleVector2(0f, 0f), 10, BattleScalar.FromFloat(0.25f), BattleScalar.One, BasicAbilitySpawn(), new AbilitySpawnData[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatantSpawnData(new TeamId(1), "bad", new BattleVector2(0f, 0f), 0, BattleScalar.FromFloat(0.25f), BattleScalar.One, BasicAbilitySpawn(), new AbilitySpawnData[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatantSpawnData(new TeamId(1), "bad", new BattleVector2(0f, 0f), 10, BattleScalar.FromFloat(-0.1f), BattleScalar.One, BasicAbilitySpawn(), new AbilitySpawnData[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatantSpawnData(new TeamId(1), "bad", new BattleVector2(0f, 0f), 10, BattleScalar.FromFloat(0.25f), BattleScalar.FromFloat(-0.1f), BasicAbilitySpawn(), new AbilitySpawnData[0]));
            Assert.Throws<ArgumentException>(() => new CombatantSpawnData(new TeamId(1), "bad", new BattleVector2(0f, 0f), 10, BattleScalar.FromFloat(0.25f), BattleScalar.One, default, new AbilitySpawnData[0]));
            Assert.Throws<ArgumentNullException>(() => new CombatantSpawnData(new TeamId(1), "bad", new BattleVector2(0f, 0f), 10, BattleScalar.FromFloat(0.25f), BattleScalar.One, BasicAbilitySpawn(), null));
        }

        [Test]
        public void CombatantSpawnData_StoresExpandedRuntimeValues()
        {
            var ability = TestCombatants.AbilitySpawn(
                "slash",
                range: 1.5f,
                damage: 4,
                cooldownTicks: 3,
                appliedStatuses: new[]
                {
                    new StatusApplicationData("burn", StatusPolarity.Debuff, durationTicks: 3, tickIntervalTicks: 1, periodicDamage: 2, modifiers: new BattleModifierData[0], triggers: new BattleTriggerData[0])
                },
                projectileEmitters: new ProjectileEmitterSpawnData[0]);
            var data = new CombatantSpawnData(
                new TeamId(1),
                "melee",
                new BattleVector2(2f, 3f),
                maxHealth: 20,
                radius: BattleScalar.FromFloat(0.25f),
                moveSpeed: BattleScalar.FromFloat(2f),
                basicAbility: BasicAbilitySpawn(),
                abilities: new[] { ability });

            Assert.AreEqual(new TeamId(1), data.TeamId);
            Assert.AreEqual("melee", data.DefinitionId);
            Assert.AreEqual(new BattleVector2(2f, 3f), data.Position);
            Assert.AreEqual(20, data.MaxHealth);
            Assert.AreEqual(20, data.BaseStats.RequireInt(BattleStatId.MaxHealth, "test"));
            Assert.AreEqual(BattleScalar.FromFloat(0.25f), data.Radius);
            Assert.AreEqual(BattleScalar.FromFloat(2f), data.MoveSpeed);
            Assert.AreEqual(BattleScalar.FromFloat(2f), data.BaseStats.RequireScalar(BattleStatId.MoveSpeed, "test"));
            Assert.AreEqual("basic-attack", data.BasicAbility.Id);
            Assert.AreEqual(BattleScalar.FromFloat(1f), data.BasicAbility.Range);
            Assert.AreEqual(1, AbilityEffects(data.BasicAbility).Count);
            Assert.AreEqual(BattleEffectType.Damage, AbilityEffects(data.BasicAbility)[0].Type);
            Assert.AreEqual(1, AbilityEffects(data.BasicAbility)[0].Amount);
            Assert.AreEqual(1, data.BasicAbility.CooldownTicks);
            Assert.AreEqual(1, data.Abilities.Count);
            Assert.AreEqual("slash", data.Abilities[0].Id);
            Assert.AreEqual(BattleScalar.FromFloat(1.5f), data.Abilities[0].Range);
            Assert.AreEqual(2, AbilityEffects(data.Abilities[0]).Count);
            Assert.AreEqual(BattleEffectType.Damage, AbilityEffects(data.Abilities[0])[0].Type);
            Assert.AreEqual(4, AbilityEffects(data.Abilities[0])[0].Amount);
            Assert.AreEqual(3, data.Abilities[0].CooldownTicks);
            Assert.AreEqual(BattleEffectType.ApplyStatus, AbilityEffects(data.Abilities[0])[1].Type);
            Assert.AreEqual("burn", AbilityEffects(data.Abilities[0])[1].Status.Id);
            Assert.AreEqual(StatusPolarity.Debuff, AbilityEffects(data.Abilities[0])[1].Status.Polarity);
            Assert.AreEqual(3, AbilityEffects(data.Abilities[0])[1].Status.DurationTicks);
            Assert.AreEqual(1, AbilityEffects(data.Abilities[0])[1].Status.TickIntervalTicks);
            Assert.AreEqual(2, AbilityEffects(data.Abilities[0])[1].Status.PeriodicDamage);
            Assert.IsFalse(data.Abilities is AbilitySpawnData[]);
        }

        private static AbilityDefinition BasicAbilityDefinition(string id = "basic-attack")
        {
            return TestCombatants.Ability(id, 1f, 1, 1);
        }

        private static AbilitySpawnData BasicAbilitySpawn()
        {
            return TestCombatants.AbilitySpawn("basic-attack", 1f, 1, 1);
        }

        private static ProjectileEmitterSpawnData CreateProjectileEmitter()
        {
            var payload = new ProjectilePayload(
                ProjectileBehavior.Linear,
                ProjectileHitPolicy.DestroyOnFirstHit,
                radius: 0.1f,
                speed: 1f,
                lifetimeTicks: 3,
                impactEffects: new[] { BattleEffectDefinition.Damage(1) });

            return new ProjectileEmitterSpawnData(
                ProjectileEmitterAnchorMode.FollowSource,
                BattleVector2.Zero,
                durationTicks: 1,
                fireIntervalTicks: 1,
                ProjectilePattern.Single(new BattleVector2(1f, 0f)),
                payload);
        }

        private static IReadOnlyList<BattleEffectDefinition> AbilityEffects(AbilityDefinition ability)
        {
            return ability.EffectFrames[0].Effects;
        }

        private static IReadOnlyList<BattleEffectData> AbilityEffects(AbilitySpawnData ability)
        {
            return ability.EffectFrames[0].Effects;
        }
    }
}
