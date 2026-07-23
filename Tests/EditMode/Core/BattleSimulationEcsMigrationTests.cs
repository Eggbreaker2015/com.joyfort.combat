using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleSimulationEcsMigrationTests
    {
        [Test]
        public void BufferedDamage_DoesNotLetUnitKilledEarlierInFlushAttack()
        {
            var attacker = TestCombatants.Create("attacker", maxHealth: 10, moveSpeed: 0f, attackRange: 2f, attackDamage: 10, attackCooldownTicks: 2);
            var defender = TestCombatants.Create("defender", maxHealth: 10, moveSpeed: 0f, attackRange: 2f, attackDamage: 99, attackCooldownTicks: 2);
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
            Assert.IsFalse(HasDamage(simulation, new UnitId(2), new UnitId(1)));

            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(1, CountEvents(simulation, BattleEventType.DamageApplied));
            BattleEvent damage = FindEvent(simulation, BattleEventType.DamageApplied);
            BattleEvent death = FindEvent(simulation, BattleEventType.UnitDied);
            Assert.AreEqual(new UnitId(1), damage.UnitId);
            Assert.AreEqual(new UnitId(2), damage.TargetUnitId);
            Assert.AreEqual(new UnitId(2), death.UnitId);
            Assert.IsFalse(HasDamage(simulation, new UnitId(2), new UnitId(1)));
        }

        private static bool HasDamage(BattleSimulation simulation, UnitId sourceUnitId, UnitId targetUnitId)
        {
            for (var i = 0; i < simulation.Events.Count; i++)
            {
                BattleEvent battleEvent = simulation.Events[i];
                if (battleEvent.Type == BattleEventType.DamageApplied
                    && battleEvent.UnitId.Equals(sourceUnitId)
                    && battleEvent.TargetUnitId.Equals(targetUnitId))
                {
                    return true;
                }
            }

            return false;
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
    }
}
