using System;
using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleSimulationCreationTests
    {
        [Test]
        public void Create_WithTwoUnits_EmitsSpawnEventsWithStableIds()
        {
            var config = TestBattleConfigs.TwoUnitsInRange();

            var simulation = new BattleSimulation(config);
            var events = simulation.Events;

            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(BattleEventType.UnitSpawned, events[0].Type);
            Assert.AreEqual(new UnitId(1), events[0].UnitId);
            Assert.AreEqual(new TeamId(1), events[0].TeamId);
            Assert.AreEqual("melee", events[0].DefinitionId);
            Assert.AreEqual(new BattleVector2(0f, 0f), events[0].Position);
            Assert.AreEqual(BattleEventType.UnitSpawned, events[1].Type);
            Assert.AreEqual(new UnitId(2), events[1].UnitId);
            Assert.AreEqual(new TeamId(2), events[1].TeamId);
            Assert.AreEqual("melee", events[1].DefinitionId);
            Assert.AreEqual(new BattleVector2(1f, 0f), events[1].Position);
        }

        [Test]
        public void Step_ClearsPreviousEventsAndAdvancesTick()
        {
            var simulation = new BattleSimulation(TestBattleConfigs.TwoStationaryEnemyUnitsOutOfRange());

            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(new BattleTick(1), simulation.CurrentTick);
            Assert.AreEqual(0, CountEvents(simulation, BattleEventType.UnitSpawned));
            Assert.AreEqual(0, CountEvents(simulation, BattleEventType.UnitMoved));
            Assert.AreEqual(0, CountEvents(simulation, BattleEventType.DamageApplied));
        }

        [Test]
        public void Create_ExpandsCombatantStatsIntoSimulationRuntime()
        {
            var attacker = TestCombatants.Create(
                "attacker",
                maxHealth: 10,
                moveSpeed: 0f,
                attackRange: 2f,
                attackDamage: 10,
                attackCooldownTicks: 2);
            var defender = TestCombatants.Create(
                "defender",
                maxHealth: 10,
                moveSpeed: 0f,
                attackRange: 2f,
                attackDamage: 1,
                attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), attacker, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), defender, new BattleVector2(1f, 0f))
                }));

            simulation.Step(BattleInputFrame.Empty);
            Assert.AreEqual(0, CountEvents(simulation, BattleEventType.DamageApplied));
            Assert.AreEqual(0, CountEvents(simulation, BattleEventType.UnitDied));

            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(10, FindEvent(simulation, BattleEventType.DamageApplied).Amount);
            Assert.AreEqual(BattleEventType.UnitDied, FindEvent(simulation, BattleEventType.UnitDied).Type);
        }

        [Test]
        public void BattleConfig_CopiesInitialSpawnsSoCallerCannotMutateSetup()
        {
            var melee = TestCombatants.Create("melee", maxHealth: 10, moveSpeed: 1f, attackRange: 1f, attackDamage: 4, attackCooldownTicks: 2);
            var spawns = new[]
            {
                new InitialCombatantSpawn(new TeamId(1), melee, new BattleVector2(0f, 0f))
            };
            var config = new BattleConfig(ticksPerSecond: 10, maxTicks: 100, initialSpawns: spawns);

            spawns[0] = new InitialCombatantSpawn(new TeamId(9), melee, new BattleVector2(9f, 9f));

            var simulation = new BattleSimulation(config);

            Assert.IsFalse(config.LocalAvoidanceEnabled);
            Assert.AreEqual(1, simulation.Events.Count);
            Assert.AreEqual(BattleEventType.UnitSpawned, simulation.Events[0].Type);
            Assert.AreEqual(new TeamId(1), simulation.Events[0].TeamId);
            Assert.AreEqual("melee", simulation.Events[0].DefinitionId);
            Assert.AreEqual(new BattleVector2(0f, 0f), simulation.Events[0].Position);
        }

        [Test]
        public void BattleConfig_RejectsDefaultInitialSpawnEntries()
        {
            Assert.Throws<ArgumentException>(() => new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[] { default(InitialCombatantSpawn) }));
        }

        [Test]
        public void BattleConfig_RejectsEmptyInitialSpawns()
        {
            Assert.Throws<ArgumentException>(() => new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new InitialCombatantSpawn[0]));
        }

        private static BattleEvent FindEvent(BattleSimulation simulation, BattleEventType type)
        {
            for (var i = 0; i < simulation.Events.Count; i++)
            {
                if (simulation.Events[i].Type == type)
                {
                    return simulation.Events[i];
                }
            }

            Assert.Fail($"No {type} event.");
            return default;
        }

        private static int CountEvents(BattleSimulation simulation, BattleEventType type)
        {
            var count = 0;
            for (var i = 0; i < simulation.Events.Count; i++)
            {
                if (simulation.Events[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }

    }

    internal static class TestBattleConfigs
    {
        public static BattleConfig TwoUnitsInRange()
        {
            var melee = TestCombatants.Create("melee", maxHealth: 10, moveSpeed: 1f, attackRange: 1.5f, attackDamage: 4, attackCooldownTicks: 2);
            return new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), melee, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), melee, new BattleVector2(1f, 0f))
                });
        }

        public static BattleConfig SingleUnit()
        {
            var melee = TestCombatants.Create("melee", maxHealth: 10, moveSpeed: 1f, attackRange: 1.5f, attackDamage: 4, attackCooldownTicks: 2);
            return new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), melee, new BattleVector2(0f, 0f))
                });
        }

        public static BattleConfig TwoUnitsOutOfRange()
        {
            var melee = TestCombatants.Create("melee", maxHealth: 10, moveSpeed: 1f, attackRange: 1f, attackDamage: 4, attackCooldownTicks: 2);
            return new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), melee, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), melee, new BattleVector2(5f, 0f))
                });
        }

        public static BattleConfig TwoStationaryEnemyUnitsOutOfRange()
        {
            var idle = TestCombatants.Create("idle", maxHealth: 10, moveSpeed: 0f, attackRange: 1f, attackDamage: 4, attackCooldownTicks: 2);
            return new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), idle, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), idle, new BattleVector2(5f, 0f))
                });
        }
    }
}
