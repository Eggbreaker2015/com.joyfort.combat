using Combat.Core.Battle;
using Combat.Foundation.Events;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleDamageModifierSimulationTests
    {
        [Test]
        public void FlushEffectCommands_DamageUsesSourceDamageDealtModifier()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), 20);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), 20);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.StatusComponents.Set(source, new StatusComponent(new[]
            {
                Status("rage", source, BattleDamageModifierStat.DamageDealt, BattleModifierOperation.Flat, BattleScalar.FromInt(5))
            }));
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(source, target, 10));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(15, events.AsStream()[0].Amount);
            Assert.AreEqual(5, world.HealthComponents.Get(target).Current);
        }

        [Test]
        public void FlushEffectCommands_DamageUsesTargetDamageTakenModifier()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), 20);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), 20);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.StatusComponents.Set(target, new StatusComponent(new[]
            {
                Status("vulnerable", source, BattleDamageModifierStat.DamageTaken, BattleModifierOperation.PercentAdd, BattleScalar.FromFloat(0.5f))
            }));
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(source, target, 10));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(15, events.AsStream()[0].Amount);
            Assert.AreEqual(5, world.HealthComponents.Get(target).Current);
        }

        [Test]
        public void FlushEffectCommands_ModifierCanReduceDamageToZero()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), 20);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), 20);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.StatusComponents.Set(target, new StatusComponent(new[]
            {
                Status("shield-wall", source, BattleDamageModifierStat.DamageTaken, BattleModifierOperation.Flat, BattleScalar.FromInt(-20))
            }));
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(source, target, 10));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, events.Count);
            Assert.AreEqual(20, world.HealthComponents.Get(target).Current);
        }

        [Test]
        public void FlushEffectCommands_DamageAppliedCarriesCommandContext()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), 20);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), 20);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            var events = new EventBuffer<BattleEvent>();
            var context = new BattleEffectContext(
                BattleEffectSourceKind.Ability,
                BattleEffectType.Damage,
                abilityId: "slash",
                statusId: null,
                projectileId: default,
                damageTags: new[] { "fire" });

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(source, target, 4, context));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            BattleEvent damage = events.AsStream()[0];
            Assert.AreEqual(BattleEffectSourceKind.Ability, damage.EffectSourceKind);
            Assert.AreEqual(BattleEffectType.Damage, damage.EffectType);
            Assert.AreEqual("slash", damage.AbilityId);
            Assert.AreEqual(default(ProjectileId), damage.EffectProjectileId);
            Assert.AreEqual(1, damage.DamageTags.Count);
            Assert.AreEqual("fire", damage.DamageTags[0]);
        }

        [Test]
        public void BattleEvent_EmptyDamageTagsReusesSharedReadOnlyList()
        {
            BattleEvent moved = BattleEvent.UnitMoved(1, new BattleTick(1), new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            BattleEvent died = BattleEvent.UnitDied(2, new BattleTick(1), new UnitId(2), new TeamId(2));

            Assert.AreSame(moved.DamageTags, died.DamageTags);
        }

        [Test]
        public void FlushEffectCommands_ModifiedDamageStillClampsToCurrentHealth()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), 20);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), 7);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.StatusComponents.Set(source, new StatusComponent(new[]
            {
                Status("rage", source, BattleDamageModifierStat.DamageDealt, BattleModifierOperation.PercentAdd, BattleScalar.FromFloat(1f))
            }));
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(source, target, 10));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(7, events.AsStream()[0].Amount);
            Assert.AreEqual(0, world.HealthComponents.Get(target).Current);
        }

        [Test]
        public void StatusSystem_DotDamageUsesDamageModifiers()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), 20);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), 20);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.StatusComponents.Set(target, new StatusComponent(new[]
            {
                new StatusInstance("burn", StatusPolarity.Debuff, source, 2, 1, 1, 4, new BattleModifierInstance[0], new BattleTriggerInstance[0]),
                Status("vulnerable", source, BattleDamageModifierStat.DamageTaken, BattleModifierOperation.PercentAdd, BattleScalar.FromFloat(0.5f))
            }));
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();

            StatusSystem.Run(world, events, sequence, new BattleTick(1));
            world.FlushEffectCommands(events, sequence, new BattleTick(1));

            AssertDamage(events.AsStream(), 6);
            Assert.AreEqual(14, world.HealthComponents.Get(target).Current);
        }

        [Test]
        public void Step_AbilityAppliedModifierDoesNotAffectSameAbilityDamage()
        {
            var vulnerable = new StatusDefinition(
                "vulnerable",
                StatusPolarity.Debuff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new[]
                {
                    BattleModifierDefinition.Damage(BattleDamageModifierStat.DamageTaken, BattleModifierOperation.PercentAdd, BattleScalar.FromFloat(1f))
                },
                triggers: new BattleTriggerDefinition[0]);
            var attacker = TestCombatants.Create("attacker", maxHealth: 20, moveSpeed: 0f, attackRange: 2f, attackDamage: 0, attackCooldownTicks: 2, abilities: new[]
            {
                TestCombatants.Ability("marking-strike", 2f, 4, 3, new[] { vulnerable }, new ProjectileEmitterSpawnData[0])
            });
            var defender = TestCombatants.Create("defender", maxHealth: 20, moveSpeed: 0f, attackRange: 2f, attackDamage: 0, attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(1, 10, new[]
            {
                new InitialCombatantSpawn(new TeamId(1), attacker, new BattleVector2(0f, 0f)),
                new InitialCombatantSpawn(new TeamId(2), defender, new BattleVector2(1f, 0f))
            }));

            simulation.Step(BattleInputFrame.Empty);
            AssertNoEvent(simulation.Events, BattleEventType.DamageApplied);

            simulation.Step(BattleInputFrame.Empty);

            AssertDamage(simulation.Events, 4);
            AssertHasEvent(simulation.Events, BattleEventType.StatusApplied);
        }

        private static void SpawnCombatant(BattleWorld world, UnitId unitId, TeamId teamId, int maxHealth)
        {
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                unitId,
                new CombatantSpawnData(
                    teamId,
                    "unit",
                    new BattleVector2(0f, 0f),
                    maxHealth,
                    radius: BattleScalar.FromFloat(0.25f),
                    moveSpeed: BattleScalar.Zero,
                    basicAbility: BasicAbility(),
                    abilities: new AbilitySpawnData[0])));
            world.FlushSpawnCombatantCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(0));
        }

        private static AbilitySpawnData BasicAbility()
        {
            return TestCombatants.AbilitySpawn("basic-attack", 1f, 1, 1);
        }

        private static StatusInstance Status(string id, EntityId source, BattleDamageModifierStat stat, BattleModifierOperation operation, BattleScalar value)
        {
            return new StatusInstance(
                id,
                StatusPolarity.Buff,
                source,
                durationRemainingTicks: 3,
                tickIntervalTicks: 1,
                ticksUntilNextPeriodicEffect: 1,
                periodicDamage: 0,
                modifiers: new[]
                {
                    BattleModifierInstance.Damage(stat, operation, value)
                },
                triggers: new BattleTriggerInstance[0]);
        }

        private static void AssertDamage(EventStream<BattleEvent> events, int expectedAmount)
        {
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == BattleEventType.DamageApplied)
                {
                    Assert.AreEqual(expectedAmount, events[i].Amount);
                    return;
                }
            }

            Assert.Fail("Expected DamageApplied event.");
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
    }
}
