using System.Collections.Generic;
using System.Text;
using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleReplayDeterminismTests
    {
        [Test]
        public void SameConfigAndTickInputs_ProducesSameEventSignature()
        {
            IReadOnlyList<string> first = RunEventSignatures(CreateConfig(), ticks: 8);
            IReadOnlyList<string> second = RunEventSignatures(CreateConfig(), ticks: 8);

            CollectionAssert.AreEqual(first, second);
        }

        private static IReadOnlyList<string> RunEventSignatures(BattleConfig config, int ticks)
        {
            var simulation = new BattleSimulation(config);
            var signatures = new List<string>();
            AppendEvents(simulation, signatures);

            for (var i = 0; i < ticks && !simulation.IsFinished; i++)
            {
                simulation.Step(BattleInputFrame.Empty);
                AppendEvents(simulation, signatures);
            }

            return signatures;
        }

        private static void AppendEvents(BattleSimulation simulation, ICollection<string> signatures)
        {
            for (var i = 0; i < simulation.Events.Count; i++)
            {
                signatures.Add(Signature(simulation.Events[i]));
            }
        }

        private static string Signature(BattleEvent battleEvent)
        {
            var builder = new StringBuilder();
            builder.Append(battleEvent.Sequence);
            builder.Append('|').Append(battleEvent.Tick.Value);
            builder.Append('|').Append(battleEvent.Type);
            builder.Append('|').Append(battleEvent.UnitId.Value);
            builder.Append('|').Append(battleEvent.TeamId.Value);
            builder.Append('|').Append(battleEvent.SourceUnitId.Value);
            builder.Append('|').Append(battleEvent.TargetUnitId.Value);
            builder.Append('|').Append(battleEvent.ProjectileId.Value);
            builder.Append('|').Append(Format(battleEvent.Position));
            builder.Append('|').Append(Format(battleEvent.Facing));
            builder.Append('|').Append(battleEvent.Amount);
            builder.Append('|').Append(battleEvent.WinningTeamId.Value);
            builder.Append('|').Append(battleEvent.DefinitionId ?? string.Empty);
            builder.Append('|').Append(battleEvent.StatusId ?? string.Empty);
            builder.Append('|').Append(battleEvent.StatusPolarity);
            return builder.ToString();
        }

        private static string Format(BattleVector2 value)
        {
            return value.XRaw + "," + value.YRaw;
        }

        private static BattleConfig CreateConfig()
        {
            var firebolt = TestCombatants.Ability(
                "firebolt",
                range: 5f,
                damage: 2,
                cooldownTicks: 2,
                appliedStatuses: new StatusDefinition[0],
                projectileEmitters: new[]
                {
                    new ProjectileEmitterSpawnData(
                        ProjectileEmitterAnchorMode.FollowSource,
                        BattleVector2.Zero,
                        durationTicks: 1,
                        fireIntervalTicks: 1,
                        ProjectilePattern.Single(BattleVector2.Right, ProjectileDirectionMode.TargetDirection),
                        new ProjectilePayload(
                            ProjectileBehavior.Linear,
                            ProjectileHitPolicy.DestroyOnFirstHit,
                            radius: 0.1f,
                            speed: 1f,
                            lifetimeTicks: 6,
                            impactEffects: new[] { BattleEffectDefinition.Damage(3) }))
                });
            CombatantDefinition caster = TestCombatants.Create(
                "caster",
                maxHealth: 20,
                moveSpeed: 1f,
                attackRange: 1f,
                attackDamage: 1,
                attackCooldownTicks: 2,
                abilities: new[] { firebolt });
            CombatantDefinition target = TestCombatants.Create("target", maxHealth: 20, moveSpeed: 0f, attackRange: 1f, attackDamage: 0, attackCooldownTicks: 2);

            return new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 64,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), caster, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), target, new BattleVector2(4f, 0f))
                });
        }
    }
}
