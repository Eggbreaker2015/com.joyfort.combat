using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleSimulationCombatResolutionTests
    {
        [Test]
        public void Step_WhenInRange_EmitsDamageDeathAndBattleEnded()
        {
            var attacker = TestCombatants.Create("attacker", maxHealth: 10, moveSpeed: 0f, attackRange: 2f, attackDamage: 10, attackCooldownTicks: 2);
            var defender = TestCombatants.Create("defender", maxHealth: 10, moveSpeed: 0f, attackRange: 2f, attackDamage: 1, attackCooldownTicks: 2);
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
            Assert.AreEqual(0, CountEvents(simulation, BattleEventType.BattleEnded));
            Assert.IsFalse(simulation.IsFinished);

            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(1, CountEvents(simulation, BattleEventType.DamageApplied));
            Assert.AreEqual(1, CountEvents(simulation, BattleEventType.UnitDied));
            Assert.AreEqual(1, CountEvents(simulation, BattleEventType.BattleEnded));
            BattleEvent damage = FindEvent(simulation, BattleEventType.DamageApplied);
            BattleEvent death = FindEvent(simulation, BattleEventType.UnitDied);
            BattleEvent battleEnded = FindEvent(simulation, BattleEventType.BattleEnded);
            Assert.AreEqual(new UnitId(1), damage.UnitId);
            Assert.AreEqual(new UnitId(2), damage.TargetUnitId);
            Assert.AreEqual(10, damage.Amount);
            Assert.AreEqual(BattleEffectSourceKind.BasicAbility, damage.EffectSourceKind);
            Assert.AreEqual(BattleEffectType.Damage, damage.EffectType);
            Assert.AreEqual("basic-attack", damage.AbilityId);
            Assert.AreEqual(new UnitId(2), death.UnitId);
            Assert.AreEqual(new TeamId(1), battleEnded.WinningTeamId);
            Assert.IsTrue(simulation.IsFinished);
        }

        [Test]
        public void Step_WhenDamageOverkills_EmitsActualAppliedDamage()
        {
            var attacker = TestCombatants.Create("attacker", maxHealth: 10, moveSpeed: 0f, attackRange: 2f, attackDamage: 10, attackCooldownTicks: 2);
            var defender = TestCombatants.Create("defender", maxHealth: 3, moveSpeed: 0f, attackRange: 2f, attackDamage: 1, attackCooldownTicks: 2);
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
            Assert.AreEqual(0, CountEvents(simulation, BattleEventType.BattleEnded));

            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(1, CountEvents(simulation, BattleEventType.DamageApplied));
            Assert.AreEqual(1, CountEvents(simulation, BattleEventType.UnitDied));
            Assert.AreEqual(1, CountEvents(simulation, BattleEventType.BattleEnded));
            BattleEvent damage = FindEvent(simulation, BattleEventType.DamageApplied);
            BattleEvent death = FindEvent(simulation, BattleEventType.UnitDied);
            BattleEvent battleEnded = FindEvent(simulation, BattleEventType.BattleEnded);
            Assert.AreEqual(new UnitId(1), damage.UnitId);
            Assert.AreEqual(new UnitId(2), damage.TargetUnitId);
            Assert.AreEqual(3, damage.Amount);
            Assert.AreEqual(new UnitId(2), death.UnitId);
            Assert.AreEqual(new TeamId(1), battleEnded.WinningTeamId);
        }

        [Test]
        public void BasicAbilityCooldown_PreventsDamageOnEveryTick()
        {
            var fighter = TestCombatants.Create("fighter", maxHealth: 30, moveSpeed: 0f, attackRange: 2f, attackDamage: 3, attackCooldownTicks: 3);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), fighter, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), fighter, new BattleVector2(1f, 0f))
                }));

            simulation.Step(BattleInputFrame.Empty);
            Assert.AreEqual(0, CountEvents(simulation, BattleEventType.DamageApplied));

            simulation.Step(BattleInputFrame.Empty);
            Assert.AreEqual(2, CountEvents(simulation, BattleEventType.DamageApplied));

            simulation.Step(BattleInputFrame.Empty);
            Assert.AreEqual(0, CountEvents(simulation, BattleEventType.DamageApplied));

            simulation.Step(BattleInputFrame.Empty);
            Assert.AreEqual(2, CountEvents(simulation, BattleEventType.DamageApplied));

            simulation.Step(BattleInputFrame.Empty);
            Assert.AreEqual(0, CountEvents(simulation, BattleEventType.DamageApplied));
        }

        [Test]
        public void VictorySystem_WhenNoLivingUnits_ReturnsNoWinner()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1));
            SpawnCombatant(world, new UnitId(2), new TeamId(2));
            world.TryFindEntity(new UnitId(1), out EntityId teamOne);
            world.TryFindEntity(new UnitId(2), out EntityId teamTwo);
            world.SetComponent(teamOne, new HealthComponent(0));
            world.SetComponent(teamTwo, new HealthComponent(0));

            bool hasWinner = VictorySystem.TryGetWinningTeam(world, out TeamId winningTeam);

            Assert.IsFalse(hasWinner);
            Assert.AreEqual(default(TeamId), winningTeam);
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

        private static void SpawnCombatant(BattleWorld world, UnitId unitId, TeamId teamId)
        {
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                unitId,
                new CombatantSpawnData(
                    teamId,
                    "unit",
                    new BattleVector2(0f, 0f),
                    maxHealth: 10,
                    radius: BattleScalar.FromFloat(0.25f),
                    moveSpeed: BattleScalar.Zero,
                    basicAbility: TestCombatants.AbilitySpawn("basic-attack", 1f, 1, 1),
                    abilities: new AbilitySpawnData[0])));
            world.FlushSpawnCombatantCommands(new Combat.Foundation.Events.EventBuffer<BattleEvent>(), new Combat.Foundation.Events.EventSequence(), new BattleTick(0));
        }
    }
}
