using System.Collections.Generic;
using Combat.Core.Battle;
using Combat.Foundation.Events;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleUnitQueryTests
    {
        [Test]
        public void CollectAliveUnitsInRadius_FiltersEnemiesAndIncludesRadiusBoundaryInUnitIdOrder()
        {
            var world = new BattleWorld();
            Spawn(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), currentHealth: 10);
            Spawn(world, new UnitId(4), new TeamId(2), new BattleVector2(2f, 0f), currentHealth: 10);
            Spawn(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), currentHealth: 10);
            Spawn(world, new UnitId(3), new TeamId(1), new BattleVector2(1f, 0f), currentHealth: 10);
            Spawn(world, new UnitId(5), new TeamId(2), new BattleVector2(2.01f, 0f), currentHealth: 10);
            Spawn(world, new UnitId(6), new TeamId(2), new BattleVector2(1.5f, 0f), currentHealth: 0);
            var results = new List<BattleUnitQueryResult>();

            BattleUnitQuery.CollectAliveUnitsInRadius(
                world,
                new TeamId(1),
                BattleTargetTeamFilter.Enemies,
                BattleVector2.Zero,
                BattleScalar.FromFloat(2f),
                results);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(new UnitId(2), results[0].UnitId);
            Assert.AreEqual(new UnitId(4), results[1].UnitId);
        }

        [Test]
        public void TrySelectNearest_ChoosesNearestEnemyWithUnitIdTieBreak()
        {
            var world = new BattleWorld();
            Spawn(world, new UnitId(1), new TeamId(1), BattleVector2.Zero, currentHealth: 10);
            Spawn(world, new UnitId(4), new TeamId(2), new BattleVector2(5f, 0f), currentHealth: 10);
            Spawn(world, new UnitId(2), new TeamId(2), new BattleVector2(5f, 0f), currentHealth: 10);
            Spawn(world, new UnitId(3), new TeamId(2), new BattleVector2(7f, 0f), currentHealth: 10);

            bool selected = BattleUnitQuery.TrySelectNearest(
                world,
                BattleVector2.Zero,
                new TeamId(1),
                BattleTargetTeamFilter.Enemies,
                out BattleUnitQueryResult result);

            Assert.IsTrue(selected);
            Assert.AreEqual(new UnitId(2), result.UnitId);
        }

        [Test]
        public void TrySelectLowestHealth_ChoosesLowestCurrentHealthWithUnitIdTieBreak()
        {
            var world = new BattleWorld();
            Spawn(world, new UnitId(1), new TeamId(1), BattleVector2.Zero, currentHealth: 10);
            Spawn(world, new UnitId(5), new TeamId(2), new BattleVector2(1f, 0f), currentHealth: 4);
            Spawn(world, new UnitId(2), new TeamId(2), new BattleVector2(2f, 0f), currentHealth: 3);
            Spawn(world, new UnitId(4), new TeamId(2), new BattleVector2(3f, 0f), currentHealth: 3);
            Spawn(world, new UnitId(3), new TeamId(1), new BattleVector2(4f, 0f), currentHealth: 1);

            bool selected = BattleUnitQuery.TrySelectLowestHealth(
                world,
                new TeamId(1),
                BattleTargetTeamFilter.Enemies,
                out BattleUnitQueryResult result);

            Assert.IsTrue(selected);
            Assert.AreEqual(new UnitId(2), result.UnitId);
            Assert.AreEqual(3, result.CurrentHealth);
        }

        [Test]
        public void CollectAliveUnitsInRadius_UsesEffectiveMaxHealth()
        {
            var world = new BattleWorld();
            Spawn(world, new UnitId(1), new TeamId(1), BattleVector2.Zero, currentHealth: 10);
            Spawn(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), currentHealth: 10);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.StatusComponents.Set(target, new StatusComponent(new[]
            {
                new StatusInstance(
                    "fortitude",
                    StatusPolarity.Buff,
                    source,
                    durationRemainingTicks: 3,
                    tickIntervalTicks: 1,
                    ticksUntilNextPeriodicEffect: 1,
                    periodicDamage: 0,
                    modifiers: new[]
                    {
                        BattleModifierInstance.Stat(BattleStatId.MaxHealth, BattleModifierOperation.Flat, BattleScalar.FromInt(5))
                    },
                    triggers: new BattleTriggerInstance[0])
            }));
            var results = new List<BattleUnitQueryResult>();

            BattleUnitQuery.CollectAliveUnitsInRadius(
                world,
                new TeamId(1),
                BattleTargetTeamFilter.Enemies,
                BattleVector2.Zero,
                BattleScalar.FromFloat(3f),
                results);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(15, results[0].MaxHealth);
        }

        [Test]
        public void TrySelectLowestHealthInRadius_FiltersByRadiusBeforeHealthTieBreak()
        {
            var world = new BattleWorld();
            Spawn(world, new UnitId(1), new TeamId(1), BattleVector2.Zero, currentHealth: 10);
            Spawn(world, new UnitId(2), new TeamId(1), new BattleVector2(5f, 0f), currentHealth: 1);
            Spawn(world, new UnitId(4), new TeamId(1), new BattleVector2(1f, 0f), currentHealth: 4);
            Spawn(world, new UnitId(3), new TeamId(1), new BattleVector2(2f, 0f), currentHealth: 4);

            bool selected = BattleUnitQuery.TrySelectLowestHealthInRadius(
                world,
                new TeamId(1),
                BattleTargetTeamFilter.Allies,
                BattleVector2.Zero,
                BattleScalar.FromFloat(3f),
                out BattleUnitQueryResult result);

            Assert.IsTrue(selected);
            Assert.AreEqual(new UnitId(3), result.UnitId);
            Assert.AreEqual(4, result.CurrentHealth);
        }

        private static void Spawn(BattleWorld world, UnitId unitId, TeamId teamId, BattleVector2 position, int currentHealth)
        {
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                unitId,
                new CombatantSpawnData(
                    teamId,
                    "unit",
                    position,
                    maxHealth: 10,
                    radius: BattleScalar.FromFloat(0.25f),
                    moveSpeed: BattleScalar.Zero,
                    basicAbility: TestCombatants.AbilitySpawn("basic-attack", 1f, 1, 1),
                    abilities: new AbilitySpawnData[0])));
            world.FlushSpawnCombatantCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(0));

            if (currentHealth < 10 && world.TryFindEntity(unitId, out EntityId entity))
            {
                world.HealthComponents.Set(entity, new HealthComponent(currentHealth));
                if (currentHealth <= 0)
                {
                    world.LifeStateComponents.Set(entity, new LifeStateComponent(LifeState.Dead));
                }
            }
        }
    }
}
