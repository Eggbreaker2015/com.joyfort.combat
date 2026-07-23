using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleSimulationProjectileTests
    {
        [Test]
        public void Step_ProjectileEmitterActivatesNextTickAndProjectileHitsFollowingTick()
        {
            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, radius: 0.5f, speed: 1f, lifetimeTicks: 5, impactEffects: new[] { BattleEffectDefinition.Damage(3) });
            var emitter = new ProjectileEmitterSpawnData(
                ProjectileEmitterAnchorMode.FollowSource,
                default,
                durationTicks: 1,
                fireIntervalTicks: 1,
                ProjectilePattern.Single(new BattleVector2(1f, 0f)),
                payload);
            var caster = TestCombatants.Create(
                "caster",
                maxHealth: 20,
                moveSpeed: 0f,
                attackRange: 0f,
                attackDamage: 0,
                attackCooldownTicks: 1,
                radius: 0.5f,
                abilities: new[] { TestCombatants.Ability("shot", 2f, 0, 1, new StatusDefinition[0], new[] { emitter }) });
            var target = TestCombatants.Create(
                "target",
                maxHealth: 20,
                moveSpeed: 0f,
                attackRange: 0f,
                attackDamage: 0,
                attackCooldownTicks: 1,
                radius: 0.5f);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 1,
                maxTicks: 10,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), caster, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), target, new BattleVector2(2f, 0f))
                }));

            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(0, CountEvents(simulation, BattleEventType.ProjectileSpawned));

            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(0, CountEvents(simulation, BattleEventType.ProjectileSpawned));
            Assert.AreEqual(0, CountEvents(simulation, BattleEventType.DamageApplied));

            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(1, CountEvents(simulation, BattleEventType.ProjectileSpawned));
            Assert.AreEqual(0, CountEvents(simulation, BattleEventType.DamageApplied));

            simulation.Step(BattleInputFrame.Empty);

            int projectileHitIndex = IndexOf(simulation, BattleEventType.ProjectileHit);
            int damageIndex = IndexOf(simulation, BattleEventType.DamageApplied);
            Assert.GreaterOrEqual(projectileHitIndex, 0);
            Assert.Greater(damageIndex, projectileHitIndex);
            Assert.AreEqual(1, CountEvents(simulation, BattleEventType.DamageApplied));
            Assert.AreEqual(3, FirstEvent(simulation, BattleEventType.DamageApplied).Amount);
            BattleEvent projectileDamage = FirstEvent(simulation, BattleEventType.DamageApplied);
            BattleEvent projectileHit = FirstEvent(simulation, BattleEventType.ProjectileHit);
            Assert.AreEqual(BattleEffectSourceKind.Projectile, projectileDamage.EffectSourceKind);
            Assert.AreEqual(BattleEffectType.Damage, projectileDamage.EffectType);
            Assert.AreEqual(projectileHit.ProjectileId, projectileDamage.EffectProjectileId);
        }

        [Test]
        public void Step_ProjectilesInterpretPayloadSpeedAsUnitsPerSecond()
        {
            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, radius: 0.1f, speed: 10f, lifetimeTicks: 10, impactEffects: new[] { BattleEffectDefinition.Damage(3) });
            var emitter = new ProjectileEmitterSpawnData(
                ProjectileEmitterAnchorMode.FollowSource,
                default,
                durationTicks: 1,
                fireIntervalTicks: 1,
                ProjectilePattern.Single(new BattleVector2(1f, 0f)),
                payload);
            var caster = TestCombatants.Create(
                "caster",
                maxHealth: 20,
                moveSpeed: 0f,
                attackRange: 0f,
                attackDamage: 0,
                attackCooldownTicks: 1,
                radius: 0.1f,
                abilities: new[] { TestCombatants.Ability("shot", 100f, 0, 1, new StatusDefinition[0], new[] { emitter }) });
            var target = TestCombatants.Create(
                "target",
                maxHealth: 20,
                moveSpeed: 0f,
                attackRange: 0f,
                attackDamage: 0,
                attackCooldownTicks: 1,
                radius: 0.1f);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 10,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), caster, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), target, new BattleVector2(50f, 0f))
                }));

            simulation.Step(BattleInputFrame.Empty);
            simulation.Step(BattleInputFrame.Empty);
            simulation.Step(BattleInputFrame.Empty);
            simulation.Step(BattleInputFrame.Empty);

            BattleEvent moved = FirstEvent(simulation, BattleEventType.ProjectileMoved);
            Assert.AreEqual(new BattleVector2(1f, 0f), moved.Position);
        }

        [Test]
        public void Step_WhenBattleEndsWithActiveProjectile_EmitsProjectileDestroyed()
        {
            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, radius: 0.1f, speed: 1f, lifetimeTicks: 20, impactEffects: new[] { BattleEffectDefinition.Damage(1) });
            var emitter = new ProjectileEmitterSpawnData(
                ProjectileEmitterAnchorMode.FollowSource,
                default,
                durationTicks: 1,
                fireIntervalTicks: 1,
                ProjectilePattern.Single(new BattleVector2(1f, 0f)),
                payload);
            var archer = TestCombatants.Create(
                "archer",
                maxHealth: 20,
                moveSpeed: 0f,
                attackRange: 100f,
                attackDamage: 0,
                attackCooldownTicks: 1,
                radius: 0.1f,
                abilities: new[] { TestCombatants.Ability("slow-shot", 100f, 0, 10, new StatusDefinition[0], new[] { emitter }) });
            var finisher = TestCombatants.Create(
                "finisher",
                maxHealth: 20,
                moveSpeed: 0f,
                attackRange: 100f,
                attackDamage: 0,
                attackCooldownTicks: 1,
                radius: 0.1f,
                abilities: new[] { TestCombatants.Ability("finisher-shot", 100f, 5, 1, windupTicks: 3) });
            var target = TestCombatants.Create(
                "target",
                maxHealth: 5,
                moveSpeed: 0f,
                attackRange: 0f,
                attackDamage: 0,
                attackCooldownTicks: 1,
                radius: 0.1f);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 1,
                maxTicks: 10,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), archer, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(1), finisher, new BattleVector2(0f, 1f)),
                    new InitialCombatantSpawn(new TeamId(2), target, new BattleVector2(10f, 0f))
                }));

            simulation.Step(BattleInputFrame.Empty);
            simulation.Step(BattleInputFrame.Empty);
            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(1, CountEvents(simulation, BattleEventType.ProjectileSpawned));

            simulation.Step(BattleInputFrame.Empty);

            int battleEndedIndex = IndexOf(simulation, BattleEventType.BattleEnded);
            int projectileDestroyedIndex = IndexOf(simulation, BattleEventType.ProjectileDestroyed);
            Assert.GreaterOrEqual(battleEndedIndex, 0);
            Assert.Greater(projectileDestroyedIndex, battleEndedIndex);
            Assert.AreEqual(1, CountEvents(simulation, BattleEventType.ProjectileDestroyed));
        }

        [Test]
        public void Step_ProjectileDamageTriggersAfterDamageTakenReaction()
        {
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
                            BattleReactionEffectDefinition.Create(BattleReactionTarget.Source, BattleEffectDefinition.Damage(4))
                        })
                });
            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, radius: 0.5f, speed: 1f, lifetimeTicks: 5, impactEffects: new[] { BattleEffectDefinition.Damage(3) });
            var emitter = new ProjectileEmitterSpawnData(
                ProjectileEmitterAnchorMode.FollowSource,
                default,
                durationTicks: 1,
                fireIntervalTicks: 1,
                ProjectilePattern.Single(new BattleVector2(1f, 0f)),
                payload);
            var caster = TestCombatants.Create(
                "caster",
                maxHealth: 20,
                moveSpeed: 0f,
                attackRange: 0f,
                attackDamage: 0,
                attackCooldownTicks: 1,
                radius: 0.5f,
                abilities: new[] { TestCombatants.Ability("shot", 2f, 0, 10, new[] { thorns }, new[] { emitter }) });
            var target = TestCombatants.Create(
                "target",
                maxHealth: 20,
                moveSpeed: 0f,
                attackRange: 0f,
                attackDamage: 0,
                attackCooldownTicks: 1,
                radius: 0.5f);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 1,
                maxTicks: 10,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), caster, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), target, new BattleVector2(2f, 0f))
                }));

            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(0, CountEvents(simulation, BattleEventType.DamageApplied));

            simulation.Step(BattleInputFrame.Empty);

            AssertHasStatusApplied(simulation, new UnitId(1), new UnitId(2), "thorns");

            simulation.Step(BattleInputFrame.Empty);
            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(2, CountEvents(simulation, BattleEventType.DamageApplied));
            Assert.AreEqual(3, EventAt(simulation, BattleEventType.DamageApplied, 0).Amount);
            Assert.AreEqual(new UnitId(1), EventAt(simulation, BattleEventType.DamageApplied, 0).UnitId);
            Assert.AreEqual(new UnitId(2), EventAt(simulation, BattleEventType.DamageApplied, 0).TargetUnitId);
            Assert.AreEqual(4, EventAt(simulation, BattleEventType.DamageApplied, 1).Amount);
            Assert.AreEqual(new UnitId(2), EventAt(simulation, BattleEventType.DamageApplied, 1).UnitId);
            Assert.AreEqual(new UnitId(1), EventAt(simulation, BattleEventType.DamageApplied, 1).TargetUnitId);
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

        private static void AssertHasStatusApplied(BattleSimulation simulation, UnitId source, UnitId target, string statusId)
        {
            for (var i = 0; i < simulation.Events.Count; i++)
            {
                BattleEvent battleEvent = simulation.Events[i];
                if (battleEvent.Type == BattleEventType.StatusApplied
                    && battleEvent.SourceUnitId.Equals(source)
                    && battleEvent.TargetUnitId.Equals(target)
                    && battleEvent.StatusId == statusId)
                {
                    return;
                }
            }

            Assert.Fail($"Expected status {statusId} applied from {source} to {target}.");
        }

        private static BattleEvent FirstEvent(BattleSimulation simulation, BattleEventType type)
        {
            for (var i = 0; i < simulation.Events.Count; i++)
            {
                if (simulation.Events[i].Type == type)
                {
                    return simulation.Events[i];
                }
            }

            Assert.Fail($"Expected event of type {type}.");
            return default;
        }

        private static int IndexOf(BattleSimulation simulation, BattleEventType type)
        {
            for (var i = 0; i < simulation.Events.Count; i++)
            {
                if (simulation.Events[i].Type == type)
                {
                    return i;
                }
            }

            return -1;
        }

        private static BattleEvent EventAt(BattleSimulation simulation, BattleEventType type, int occurrence)
        {
            var seen = 0;
            for (var i = 0; i < simulation.Events.Count; i++)
            {
                if (simulation.Events[i].Type != type)
                {
                    continue;
                }

                if (seen == occurrence)
                {
                    return simulation.Events[i];
                }

                seen++;
            }

            Assert.Fail($"Expected event of type {type} at occurrence {occurrence}.");
            return default;
        }
    }
}
