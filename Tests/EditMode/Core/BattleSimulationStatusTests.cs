using Combat.Core.Battle;
using Combat.Foundation.Events;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleSimulationStatusTests
    {
        [Test]
        public void Step_AbilityAppliesStatusAndDotTriggersNextTick()
        {
            var burn = new StatusDefinition("burn", StatusPolarity.Debuff, durationTicks: 2, tickIntervalTicks: 1, periodicDamage: 2, modifiers: new BattleModifierDefinition[0], triggers: new BattleTriggerDefinition[0]);
            var attacker = TestCombatants.Create("attacker", maxHealth: 20, moveSpeed: 0f, attackRange: 2f, attackDamage: 0, attackCooldownTicks: 2, abilities: new[] { TestCombatants.Ability("ignite", 2f, 0, 3, new[] { burn }, new ProjectileEmitterSpawnData[0]) });
            var defender = TestCombatants.Create("defender", maxHealth: 20, moveSpeed: 0f, attackRange: 2f, attackDamage: 0, attackCooldownTicks: 2);
            var simulation = CreateDuel(attacker, defender);

            simulation.Step(BattleInputFrame.Empty);
            AssertNoEvent(simulation.Events, BattleEventType.StatusApplied);
            AssertNoEvent(simulation.Events, BattleEventType.DamageApplied);

            simulation.Step(BattleInputFrame.Empty);
            AssertHasEvent(simulation.Events, BattleEventType.StatusApplied);
            AssertNoEvent(simulation.Events, BattleEventType.DamageApplied);

            simulation.Step(BattleInputFrame.Empty);
            AssertDamageEvents(simulation.Events, new[] { new ExpectedDamage(new UnitId(1), new UnitId(2), 2) });
            BattleEvent dotDamage = FirstEvent(simulation.Events, BattleEventType.DamageApplied);
            Assert.AreEqual(BattleEffectSourceKind.Status, dotDamage.EffectSourceKind);
            Assert.AreEqual(BattleEffectType.Damage, dotDamage.EffectType);
            Assert.AreEqual("burn", dotDamage.EffectStatusId);
        }

        [Test]
        public void Step_DotDamageTriggersAfterDamageTakenReaction()
        {
            var burn = new StatusDefinition(
                "burn",
                StatusPolarity.Debuff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 2,
                modifiers: new BattleModifierDefinition[0],
                triggers: new BattleTriggerDefinition[0]);
            var thorns = new StatusDefinition(
                "thorns",
                StatusPolarity.Buff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new BattleModifierDefinition[0],
                triggers: new[]
                {
                    new BattleTriggerDefinition(
                        BattleTriggerTiming.AfterDamageTaken,
                        new[]
                        {
                            BattleReactionEffectDefinition.Create(BattleReactionTarget.Source, BattleEffectDefinition.Damage(3))
                        })
            });
            var applier = TestCombatants.Ability("apply", range: 2f, damage: 0, cooldownTicks: 1, appliedStatuses: new[] { burn, thorns }, projectileEmitters: new ProjectileEmitterSpawnData[0]);
            var caster = TestCombatants.Create("caster", maxHealth: 10, moveSpeed: 0f, attackRange: 0f, attackDamage: 0, attackCooldownTicks: 1, radius: 0.25f, abilities: new[] { applier });
            var target = TestCombatants.Create("target", maxHealth: 10, moveSpeed: 0f, attackRange: 0f, attackDamage: 0, attackCooldownTicks: 1, radius: 0.25f);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 10,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), caster, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), target, new BattleVector2(1f, 0f))
                }));

            simulation.Step(BattleInputFrame.Empty);
            simulation.Step(BattleInputFrame.Empty);
            simulation.Step(BattleInputFrame.Empty);

            EventStream<BattleEvent> events = simulation.Events;
            AssertDamageEvents(events, new[]
            {
                new ExpectedDamage(new UnitId(1), new UnitId(2), 2),
                new ExpectedDamage(new UnitId(2), new UnitId(1), 3)
            });
        }

        [Test]
        public void Step_DotCanKillBeforeTargetActs()
        {
            var burn = new StatusDefinition("burn", StatusPolarity.Debuff, durationTicks: 1, tickIntervalTicks: 1, periodicDamage: 3, modifiers: new BattleModifierDefinition[0], triggers: new BattleTriggerDefinition[0]);
            var attacker = TestCombatants.Create("attacker", maxHealth: 20, moveSpeed: 0f, attackRange: 2f, attackDamage: 0, attackCooldownTicks: 2, abilities: new[] { TestCombatants.Ability("ignite", 2f, 0, 3, new[] { burn }, new ProjectileEmitterSpawnData[0]) });
            var defender = TestCombatants.Create("defender", maxHealth: 3, moveSpeed: 0f, attackRange: 2f, attackDamage: 10, attackCooldownTicks: 1);
            var simulation = CreateDuel(attacker, defender);

            simulation.Step(BattleInputFrame.Empty);
            simulation.Step(BattleInputFrame.Empty);
            simulation.Step(BattleInputFrame.Empty);

            AssertSequencesIncreaseInStreamOrder(simulation.Events);
            int statusExpiredIndex = IndexOf(simulation.Events, BattleEventType.StatusExpired);
            int damageIndex = IndexOf(simulation.Events, BattleEventType.DamageApplied);
            int unitDiedIndex = IndexOf(simulation.Events, BattleEventType.UnitDied);
            Assert.Less(statusExpiredIndex, damageIndex);
            Assert.Less(damageIndex, unitDiedIndex);
            Assert.AreEqual(new UnitId(1), simulation.Events[damageIndex].UnitId);
            Assert.AreEqual(new UnitId(2), simulation.Events[damageIndex].TargetUnitId);
            AssertHasEvent(simulation.Events, BattleEventType.UnitDied);
            AssertNoDamageFrom(simulation.Events, new UnitId(2));
        }

        [Test]
        public void Step_DotVictoryEndsBeforeActionPhase()
        {
            var burn = new StatusDefinition("burn", StatusPolarity.Debuff, durationTicks: 1, tickIntervalTicks: 1, periodicDamage: 3, modifiers: new BattleModifierDefinition[0], triggers: new BattleTriggerDefinition[0]);
            var attacker = TestCombatants.Create("attacker", maxHealth: 20, moveSpeed: 0f, attackRange: 2f, attackDamage: 0, attackCooldownTicks: 2, abilities: new[] { TestCombatants.Ability("ignite", 2f, 0, 3, new[] { burn }, new ProjectileEmitterSpawnData[0]) });
            var defender = TestCombatants.Create("defender", maxHealth: 3, moveSpeed: 0f, attackRange: 2f, attackDamage: 10, attackCooldownTicks: 1);
            var simulation = CreateDuel(attacker, defender);

            simulation.Step(BattleInputFrame.Empty);
            simulation.Step(BattleInputFrame.Empty);
            simulation.Step(BattleInputFrame.Empty);

            AssertSequencesIncreaseInStreamOrder(simulation.Events);
            int unitDiedIndex = IndexOf(simulation.Events, BattleEventType.UnitDied);
            int battleEndedIndex = IndexOf(simulation.Events, BattleEventType.BattleEnded);
            Assert.Less(unitDiedIndex, battleEndedIndex);
            AssertNoDamageFrom(simulation.Events, new UnitId(2));
            Assert.AreEqual(simulation.Events.Count - 1, battleEndedIndex);
            Assert.IsTrue(simulation.IsFinished);
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

        private static void AssertHasEvent(EventStream<BattleEvent> events, BattleEventType type)
        {
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    return;
                }
            }

            Assert.Fail($"Expected event {type}.");
        }

        private static void AssertNoEvent(EventStream<BattleEvent> events, BattleEventType type)
        {
            for (var i = 0; i < events.Count; i++)
            {
                Assert.AreNotEqual(type, events[i].Type);
            }
        }

        private static BattleEvent FirstEvent(EventStream<BattleEvent> events, BattleEventType type)
        {
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

        private static void AssertNoDamageFrom(EventStream<BattleEvent> events, UnitId source)
        {
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == BattleEventType.DamageApplied)
                {
                    Assert.AreNotEqual(source, events[i].UnitId);
                }
            }
        }

        private static void AssertDamageEvents(EventStream<BattleEvent> events, ExpectedDamage[] expected)
        {
            var damageIndex = 0;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type != BattleEventType.DamageApplied)
                {
                    continue;
                }

                ExpectedDamage damage = expected[damageIndex];
                Assert.AreEqual(damage.Source, events[i].UnitId);
                Assert.AreEqual(damage.Target, events[i].TargetUnitId);
                Assert.AreEqual(damage.Amount, events[i].Amount);
                damageIndex++;
            }

            Assert.AreEqual(expected.Length, damageIndex);
        }

        private static void AssertSequencesIncreaseInStreamOrder(EventStream<BattleEvent> events)
        {
            for (var i = 1; i < events.Count; i++)
            {
                Assert.Less(events[i - 1].Sequence, events[i].Sequence);
            }
        }

        private static int IndexOf(EventStream<BattleEvent> events, BattleEventType type)
        {
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    return i;
                }
            }

            Assert.Fail($"Expected event {type}.");
            return -1;
        }

        private readonly struct ExpectedDamage
        {
            public ExpectedDamage(UnitId source, UnitId target, int amount)
            {
                Source = source;
                Target = target;
                Amount = amount;
            }

            public UnitId Source { get; }
            public UnitId Target { get; }
            public int Amount { get; }
        }
    }
}
