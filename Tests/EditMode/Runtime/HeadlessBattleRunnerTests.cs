using System;
using Combat.Core.Battle;
using Combat.Runtime.Runner;
using Combat.Tests.Core;
using NUnit.Framework;

namespace Combat.Tests.Runtime
{
    public sealed class HeadlessBattleRunnerTests
    {
        [Test]
        public void RunToEnd_AdvancesBattleInstanceUntilTerminalResult()
        {
            var instance = new BattleInstance(DecisiveConfig());

            BattleResult result = HeadlessBattleRunner.RunToEnd(instance, maxTicks: 10);

            Assert.That(result.IsFinished, Is.True);
            Assert.That(result.WinningTeamId, Is.EqualTo(new TeamId(1)));
            Assert.That(instance.Simulation.CurrentTick.Value, Is.LessThanOrEqualTo(10));
        }

        [Test]
        public void RunToEnd_StopsAtCallerTickLimit()
        {
            var instance = new BattleInstance(StalemateConfig(maxTicks: 20));

            BattleResult result = HeadlessBattleRunner.RunToEnd(instance, maxTicks: 3);

            Assert.That(result.IsFinished, Is.False);
            Assert.That(instance.Simulation.CurrentTick.Value, Is.EqualTo(3));
        }

        [Test]
        public void RunToEnd_RejectsMissingInstanceAndNonPositiveLimit()
        {
            Assert.Throws<ArgumentNullException>(() =>
                HeadlessBattleRunner.RunToEnd(null, maxTicks: 1));

            var instance = new BattleInstance(StalemateConfig(maxTicks: 20));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                HeadlessBattleRunner.RunToEnd(instance, maxTicks: 0));
        }

        private static BattleConfig DecisiveConfig()
        {
            CombatantDefinition attacker = TestCombatants.Create(
                "headless-attacker", 10, 0f, 2f, 10, 1);
            CombatantDefinition defender = TestCombatants.Create(
                "headless-defender", 1, 0f, 2f, 0, 1);
            return new BattleConfig(10, 20, new[]
            {
                new InitialCombatantSpawn(new TeamId(1), attacker, BattleVector2.Zero),
                new InitialCombatantSpawn(
                    new TeamId(2), defender, new BattleVector2(1f, 0f))
            });
        }

        private static BattleConfig StalemateConfig(int maxTicks)
        {
            CombatantDefinition passive = TestCombatants.Create(
                "headless-passive", 10, 0f, 0f, 0, 1);
            return new BattleConfig(
                10,
                maxTicks,
                new[]
                {
                    new InitialCombatantSpawn(
                        new TeamId(1), passive, BattleVector2.Zero),
                    new InitialCombatantSpawn(
                        new TeamId(2), passive, new BattleVector2(10f, 0f))
                },
                default,
                automaticVictoryEnabled: false);
        }
    }
}
