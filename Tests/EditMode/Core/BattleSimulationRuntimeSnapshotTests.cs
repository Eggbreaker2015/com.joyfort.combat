using Combat.Core.Battle;
using Combat.Foundation.Events;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleSimulationRuntimeSnapshotTests
    {
        [Test]
        public void TryGetUnitRuntimeSnapshot_ReturnsInitialUnitState()
        {
            var simulation = new BattleSimulation(CreateConfig());

            bool found = simulation.TryGetUnitRuntimeSnapshot(new UnitId(1), out UnitRuntimeSnapshot snapshot);

            Assert.IsTrue(found);
            Assert.AreEqual(new UnitId(1), snapshot.UnitId);
            Assert.AreEqual("attacker", snapshot.DefinitionId);
            Assert.AreEqual(new TeamId(1), snapshot.TeamId);
            Assert.AreEqual(new BattleVector2(0f, 0f), snapshot.Position);
            Assert.AreEqual(new BattleVector2(1f, 0f), snapshot.Facing);
            Assert.AreEqual(0.25f, snapshot.Radius);
            Assert.AreEqual(10, snapshot.CurrentHealth);
            Assert.AreEqual(10, snapshot.MaxHealth);
            Assert.AreEqual("Alive", snapshot.LifeState);
            Assert.AreEqual(0f, snapshot.MoveSpeed);
            Assert.AreEqual(1, snapshot.Abilities.Count);
            Assert.AreEqual(0, snapshot.Abilities[0].SlotIndex);
            Assert.IsTrue(snapshot.Abilities[0].IsBasic);
            Assert.AreEqual("basic-attack", snapshot.Abilities[0].Id);
            Assert.AreEqual(2f, snapshot.Abilities[0].Range);
            Assert.AreEqual(10, snapshot.Abilities[0].Damage);
            Assert.AreEqual(2, snapshot.Abilities[0].CooldownTicks);
            Assert.AreEqual(0, snapshot.Abilities[0].CooldownRemainingTicks);
            Assert.AreEqual(0, snapshot.Statuses.Count);
        }

        [Test]
        public void TryGetUnitRuntimeSnapshot_ReflectsTickChanges()
        {
            var simulation = new BattleSimulation(CreateConfig());

            simulation.Step(BattleInputFrame.Empty);
            simulation.Step(BattleInputFrame.Empty);

            Assert.IsTrue(simulation.TryGetUnitRuntimeSnapshot(new UnitId(2), out UnitRuntimeSnapshot defender));
            Assert.AreEqual(0, defender.CurrentHealth);
            Assert.AreEqual(10, defender.MaxHealth);
            Assert.AreEqual("Dead", defender.LifeState);
        }

        [Test]
        public void TryGetUnitRuntimeSnapshot_UpdatesFacingTowardCurrentTarget()
        {
            var combatant = TestCombatants.Create(
                "duelist",
                maxHealth: 20,
                moveSpeed: 0f,
                attackRange: 10f,
                attackDamage: 0,
                attackCooldownTicks: 1);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), combatant, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), combatant, new BattleVector2(0f, 5f))
                }));

            simulation.Step(BattleInputFrame.Empty);

            Assert.IsTrue(simulation.TryGetUnitRuntimeSnapshot(new UnitId(1), out UnitRuntimeSnapshot attacker));
            Assert.IsTrue(simulation.TryGetUnitRuntimeSnapshot(new UnitId(2), out UnitRuntimeSnapshot defender));
            Assert.AreEqual(new BattleVector2(0f, 1f), attacker.Facing);
            Assert.AreEqual(new BattleVector2(0f, -1f), defender.Facing);
        }

        [Test]
        public void TryGetUnitRuntimeSnapshot_IncludesStatusStackFields()
        {
            var world = new BattleWorld();
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                new UnitId(1),
                new CombatantSpawnData(
                    new TeamId(1),
                    "unit",
                    new BattleVector2(0f, 0f),
                    maxHealth: 10,
                    radius: BattleScalar.FromFloat(0.25f),
                    moveSpeed: BattleScalar.Zero,
                    basicAbility: TestCombatants.AbilitySpawn("basic", 1f, 1, 1),
                    abilities: new AbilitySpawnData[0])));
            world.FlushSpawnCombatantCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(0));
            world.TryFindEntity(new UnitId(1), out EntityId entity);
            world.StatusComponents.Set(entity, new StatusComponent(new[]
            {
                new StatusInstance(
                    "rage",
                    StatusPolarity.Buff,
                    entity,
                    durationRemainingTicks: 5,
                    tickIntervalTicks: 5,
                    ticksUntilNextPeriodicEffect: 5,
                    periodicDamage: 0,
                    modifiers: new BattleModifierInstance[0],
                    triggers: new BattleTriggerInstance[0],
                    stackCount: 3,
                    maxStacks: 5)
            }));

            bool found = world.TryGetUnitRuntimeSnapshot(new UnitId(1), new BattleTick(1), out UnitRuntimeSnapshot snapshot);

            Assert.IsTrue(found);
            Assert.AreEqual(3, snapshot.Statuses[0].StackCount);
            Assert.AreEqual(5, snapshot.Statuses[0].MaxStacks);
        }

        [Test]
        public void TryGetUnitRuntimeSnapshot_UsesEffectiveStats()
        {
            var world = new BattleWorld();
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                new UnitId(1),
                new CombatantSpawnData(
                    new TeamId(1),
                    "unit",
                    new BattleVector2(0f, 0f),
                    maxHealth: 10,
                    radius: BattleScalar.FromFloat(0.25f),
                    moveSpeed: BattleScalar.FromFloat(2f),
                    basicAbility: TestCombatants.AbilitySpawn("basic", 1f, 1, 1),
                    abilities: new AbilitySpawnData[0])));
            world.FlushSpawnCombatantCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(0));
            world.TryFindEntity(new UnitId(1), out EntityId entity);
            world.StatusComponents.Set(entity, new StatusComponent(new[]
            {
                new StatusInstance(
                    "fortitude",
                    StatusPolarity.Buff,
                    entity,
                    durationRemainingTicks: 5,
                    tickIntervalTicks: 5,
                    ticksUntilNextPeriodicEffect: 5,
                    periodicDamage: 0,
                    modifiers: new[]
                    {
                        BattleModifierInstance.Stat(BattleStatId.MaxHealth, BattleModifierOperation.Flat, BattleScalar.FromInt(5)),
                        BattleModifierInstance.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.PercentAdd, BattleScalar.FromFloat(0.5f))
                    },
                    triggers: new BattleTriggerInstance[0])
            }));

            bool found = world.TryGetUnitRuntimeSnapshot(new UnitId(1), new BattleTick(1), out UnitRuntimeSnapshot snapshot);

            Assert.IsTrue(found);
            Assert.AreEqual(15, snapshot.MaxHealth);
            Assert.AreEqual(3f, snapshot.MoveSpeed);
        }

        [Test]
        public void TryGetUnitRuntimeSnapshot_ReturnsFalseForMissingUnit()
        {
            var simulation = new BattleSimulation(CreateConfig());

            bool found = simulation.TryGetUnitRuntimeSnapshot(new UnitId(99), out UnitRuntimeSnapshot snapshot);

            Assert.IsFalse(found);
            Assert.AreEqual(default(UnitRuntimeSnapshot), snapshot);
        }

        private static BattleConfig CreateConfig()
        {
            CombatantDefinition attacker = TestCombatants.Create(
                "attacker",
                maxHealth: 10,
                moveSpeed: 0f,
                attackRange: 2f,
                attackDamage: 10,
                attackCooldownTicks: 2);
            CombatantDefinition defender = TestCombatants.Create(
                "defender",
                maxHealth: 10,
                moveSpeed: 0f,
                attackRange: 1f,
                attackDamage: 1,
                attackCooldownTicks: 2);

            return new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), attacker, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), defender, new BattleVector2(1f, 0f))
                });
        }
    }
}
