using Combat.Core.Battle;
using Combat.Foundation.Events;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleSimulationAbilityTests
    {
        [Test]
        public void Step_AbilityDamageTakesPriorityOverBasicAbility()
        {
            var attacker = TestCombatants.Create(
                "attacker",
                maxHealth: 30,
                moveSpeed: 0f,
                attackRange: 2f,
                attackDamage: 1,
                attackCooldownTicks: 2,
                abilities: new[] { TestCombatants.Ability("slash", range: 2f, damage: 7, cooldownTicks: 3, appliedStatuses: new StatusDefinition[0], projectileEmitters: new ProjectileEmitterSpawnData[0]) });
            var defender = TestCombatants.Create(
                "defender",
                maxHealth: 30,
                moveSpeed: 0f,
                attackRange: 2f,
                attackDamage: 1,
                attackCooldownTicks: 2);
            var simulation = CreateDuel(attacker, defender);

            simulation.Step(BattleInputFrame.Empty);
            AssertDamageEvents(simulation.Events, new ExpectedDamage[0]);

            simulation.Step(BattleInputFrame.Empty);
            AssertDamageEvents(
                simulation.Events,
                new[]
                {
                    new ExpectedDamage(new UnitId(1), new UnitId(2), 7, BattleEffectSourceKind.Ability, BattleEffectType.Damage, "slash"),
                    new ExpectedDamage(new UnitId(2), new UnitId(1), 1)
                });
        }

        [Test]
        public void Step_AbilityCooldownAllowsBasicAbilityFallback()
        {
            var attacker = TestCombatants.Create(
                "attacker",
                maxHealth: 30,
                moveSpeed: 0f,
                attackRange: 2f,
                attackDamage: 2,
                attackCooldownTicks: 2,
                abilities: new[] { TestCombatants.Ability("slash", range: 2f, damage: 7, cooldownTicks: 3, appliedStatuses: new StatusDefinition[0], projectileEmitters: new ProjectileEmitterSpawnData[0]) });
            var defender = TestCombatants.Create(
                "defender",
                maxHealth: 30,
                moveSpeed: 0f,
                attackRange: 2f,
                attackDamage: 1,
                attackCooldownTicks: 2);
            var simulation = CreateDuel(attacker, defender);

            simulation.Step(BattleInputFrame.Empty);
            AssertDamageEvents(simulation.Events, new ExpectedDamage[0]);
            simulation.Step(BattleInputFrame.Empty);
            AssertDamageEvents(
                simulation.Events,
                new[]
                {
                    new ExpectedDamage(new UnitId(1), new UnitId(2), 7, BattleEffectSourceKind.Ability, BattleEffectType.Damage, "slash"),
                    new ExpectedDamage(new UnitId(2), new UnitId(1), 1)
                });

            simulation.Step(BattleInputFrame.Empty);
            AssertDamageEvents(
                simulation.Events,
                new[]
                {
                    new ExpectedDamage(new UnitId(1), new UnitId(2), 2),
                    new ExpectedDamage(new UnitId(2), new UnitId(1), 1)
                });
        }

        [Test]
        public void Step_CoolingHigherPriorityAbilityUsesNextReadyAbility()
        {
            var attacker = TestCombatants.Create(
                "attacker",
                maxHealth: 30,
                moveSpeed: 0f,
                attackRange: 2f,
                attackDamage: 1,
                attackCooldownTicks: 2,
                abilities: new[]
                {
                    TestCombatants.Ability("heavy", range: 2f, damage: 8, cooldownTicks: 3, appliedStatuses: new StatusDefinition[0], projectileEmitters: new ProjectileEmitterSpawnData[0]),
                    TestCombatants.Ability("quick", range: 2f, damage: 4, cooldownTicks: 0, appliedStatuses: new StatusDefinition[0], projectileEmitters: new ProjectileEmitterSpawnData[0])
                });
            var defender = TestCombatants.Create(
                "defender",
                maxHealth: 40,
                moveSpeed: 0f,
                attackRange: 2f,
                attackDamage: 1,
                attackCooldownTicks: 2);
            var simulation = CreateDuel(attacker, defender);

            simulation.Step(BattleInputFrame.Empty);
            AssertDamageEvents(simulation.Events, new ExpectedDamage[0]);
            simulation.Step(BattleInputFrame.Empty);
            AssertDamageEvents(
                simulation.Events,
                new[]
                {
                    new ExpectedDamage(new UnitId(1), new UnitId(2), 8, BattleEffectSourceKind.Ability, BattleEffectType.Damage, "heavy"),
                    new ExpectedDamage(new UnitId(2), new UnitId(1), 1)
                });

            simulation.Step(BattleInputFrame.Empty);
            AssertDamageEvents(
                simulation.Events,
                new[]
                {
                    new ExpectedDamage(new UnitId(1), new UnitId(2), 4),
                    new ExpectedDamage(new UnitId(2), new UnitId(1), 1)
                });
        }

        [Test]
        public void Step_AreaHealAbilityHealsWoundedAllyOnThirdTick()
        {
            var groupHeal = TestCombatants.Ability(
                "group-heal",
                range: 5f,
                cooldownTicks: 1,
                effects: new[]
                {
                    BattleEffectDefinition.AreaEffect(new AreaEffectDefinition(
                        BattleScalar.FromFloat(1.5f),
                        AreaEffectTargetFilter.Allies,
                        new[] { BattleEffectDefinition.Heal(4) }))
                });
            CombatantDefinition healer = TestCombatants.Create("healer", maxHealth: 20, moveSpeed: 0f, attackRange: 5f, attackDamage: 1, attackCooldownTicks: 5, abilities: new[] { groupHeal });
            CombatantDefinition enemy = TestCombatants.Create("enemy", maxHealth: 30, moveSpeed: 0f, attackRange: 1f, attackDamage: 5, attackCooldownTicks: 1);
            var simulation = new BattleSimulation(new BattleConfig(10, 20, new[]
            {
                new InitialCombatantSpawn(new TeamId(1), healer, new BattleVector2(0f, 0f)),
                new InitialCombatantSpawn(new TeamId(2), enemy, new BattleVector2(1f, 0f))
            }));

            simulation.Step(BattleInputFrame.Empty);
            Assert.AreEqual(0, CountEvents(simulation, BattleEventType.HealingApplied));
            simulation.Step(BattleInputFrame.Empty);
            Assert.AreEqual(0, CountEvents(simulation, BattleEventType.HealingApplied));

            simulation.Step(BattleInputFrame.Empty);
            Assert.AreEqual(1, CountEvents(simulation, BattleEventType.HealingApplied));
            BattleEvent heal = FirstEvent(simulation, BattleEventType.HealingApplied);
            Assert.AreEqual(new UnitId(1), heal.TargetUnitId);
            Assert.AreEqual(4, heal.Amount);
        }

        private static BattleSimulation CreateDuel(CombatantDefinition attacker, CombatantDefinition defender)
        {
            return new BattleSimulation(new BattleConfig(
                ticksPerSecond: 1,
                maxTicks: 10,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), attacker, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), defender, new BattleVector2(1f, 0f))
                }));
        }

        private static void AssertDamageEvents(EventStream<BattleEvent> events, ExpectedDamage[] expected)
        {
            var damageIndex = 0;
            for (var i = 0; i < events.Count; i++)
            {
                BattleEvent battleEvent = events[i];
                if (battleEvent.Type != BattleEventType.DamageApplied)
                {
                    continue;
                }

                Assert.Less(damageIndex, expected.Length, "Unexpected extra damage event.");
                ExpectedDamage expectedDamage = expected[damageIndex];
                Assert.AreEqual(expectedDamage.Source, battleEvent.UnitId);
                Assert.AreEqual(expectedDamage.Target, battleEvent.TargetUnitId);
                Assert.AreEqual(expectedDamage.Amount, battleEvent.Amount);
                if (expectedDamage.SourceKind != BattleEffectSourceKind.Unknown)
                {
                    Assert.AreEqual(expectedDamage.SourceKind, battleEvent.EffectSourceKind);
                    Assert.AreEqual(expectedDamage.EffectType, battleEvent.EffectType);
                    Assert.AreEqual(expectedDamage.SourceId, battleEvent.AbilityId ?? battleEvent.EffectStatusId);
                }

                damageIndex++;
            }

            Assert.AreEqual(expected.Length, damageIndex, "Unexpected damage event count.");
        }

        private static int CountEvents(BattleSimulation simulation, BattleEventType type)
        {
            var count = 0;
            EventStream<BattleEvent> events = simulation.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }

        private static BattleEvent FirstEvent(BattleSimulation simulation, BattleEventType type)
        {
            EventStream<BattleEvent> events = simulation.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    return events[i];
                }
            }

            Assert.Fail($"Expected event {type}.");
            return default;
        }

        private readonly struct ExpectedDamage
        {
            public ExpectedDamage(UnitId source, UnitId target, int amount)
                : this(source, target, amount, BattleEffectSourceKind.Unknown, default, null)
            {
            }

            public ExpectedDamage(UnitId source, UnitId target, int amount, BattleEffectSourceKind sourceKind, BattleEffectType effectType, string sourceId)
            {
                Source = source;
                Target = target;
                Amount = amount;
                SourceKind = sourceKind;
                EffectType = effectType;
                SourceId = sourceId;
            }

            public UnitId Source { get; }
            public UnitId Target { get; }
            public int Amount { get; }
            public BattleEffectSourceKind SourceKind { get; }
            public BattleEffectType EffectType { get; }
            public string SourceId { get; }
        }
    }
}
