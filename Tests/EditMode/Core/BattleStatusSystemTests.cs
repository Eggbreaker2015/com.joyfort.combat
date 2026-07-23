using Combat.Core.Battle;
using Combat.Foundation.Events;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleStatusSystemTests
    {
        [Test]
        public void Run_IntervalOneQueuesDamageAndExpiresAfterDuration()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.StatusComponents.Set(target, new StatusComponent(new[]
            {
                new StatusInstance("burn", StatusPolarity.Debuff, source, durationRemainingTicks: 2, tickIntervalTicks: 1, ticksUntilNextPeriodicEffect: 1, periodicDamage: 2, modifiers: new BattleModifierInstance[0], triggers: new BattleTriggerInstance[0])
            }));
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();

            StatusSystem.Run(world, events, sequence, new BattleTick(1));

            Assert.AreEqual(1, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(BattleEffectType.Damage, world.CommandBuffer.EffectCommands[0].Type);
            Assert.AreEqual(1, world.StatusComponents.Get(target).Statuses[0].DurationRemainingTicks);
            Assert.AreEqual(1, world.StatusComponents.Get(target).Statuses[0].TicksUntilNextPeriodicEffect);

            world.CommandBuffer.ClearEffectCommands();
            StatusSystem.Run(world, events, sequence, new BattleTick(2));

            Assert.IsFalse(world.StatusComponents.Has(target));
            Assert.AreEqual(BattleEventType.StatusExpired, events.AsStream()[0].Type);
            Assert.AreEqual(new UnitId(2), events.AsStream()[0].UnitId);
            Assert.AreEqual("burn", events.AsStream()[0].StatusId);
        }

        [Test]
        public void Run_IntervalGreaterThanOneTriggersOnInterval()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.StatusComponents.Set(target, new StatusComponent(new[]
            {
                new StatusInstance("poison", StatusPolarity.Debuff, source, durationRemainingTicks: 3, tickIntervalTicks: 2, ticksUntilNextPeriodicEffect: 2, periodicDamage: 1, modifiers: new BattleModifierInstance[0], triggers: new BattleTriggerInstance[0])
            }));

            StatusSystem.Run(world, new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);

            StatusSystem.Run(world, new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(2));
            Assert.AreEqual(1, world.CommandBuffer.EffectCommands.Count);
        }

        [Test]
        public void Run_GarrisonedOwnerStillAdvancesAndQueuesPeriodicDamage()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), BattleVector2.Zero);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), BattleVector2.Right);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.GarrisonedComponents.Set(target, default);
            world.StatusComponents.Set(target, new StatusComponent(new[]
            {
                new StatusInstance(
                    "burn",
                    StatusPolarity.Debuff,
                    source,
                    durationRemainingTicks: 2,
                    tickIntervalTicks: 1,
                    ticksUntilNextPeriodicEffect: 1,
                    periodicDamage: 2,
                    modifiers: new BattleModifierInstance[0],
                    triggers: new BattleTriggerInstance[0])
            }));

            StatusSystem.Run(
                world,
                new EventBuffer<BattleEvent>(),
                new EventSequence(),
                new BattleTick(1));

            Assert.That(world.CommandBuffer.EffectCommands, Has.Count.EqualTo(1));
            Assert.That(world.CommandBuffer.EffectCommands[0].Target, Is.EqualTo(target));
            Assert.That(world.StatusComponents.Get(target).Statuses[0].DurationRemainingTicks,
                Is.EqualTo(1));
        }

        [Test]
        public void Run_DeadOwnerRemovesStatusComponentWithoutDamageOrExpiredEvent()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.StatusComponents.Set(target, new StatusComponent(new[]
            {
                new StatusInstance("burn", StatusPolarity.Debuff, source, 2, 1, 1, 2, new BattleModifierInstance[0], new BattleTriggerInstance[0])
            }));
            world.SetComponent(target, new LifeStateComponent(LifeState.Dead));
            var events = new EventBuffer<BattleEvent>();

            StatusSystem.Run(world, events, new EventSequence(), new BattleTick(1));

            Assert.IsFalse(world.StatusComponents.Has(target));
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void Run_DeadSourceAdvancesAndExpiresWithoutDamage()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.StatusComponents.Set(target, new StatusComponent(new[]
            {
                new StatusInstance("burn", StatusPolarity.Debuff, source, 1, 1, 1, 2, new BattleModifierInstance[0], new BattleTriggerInstance[0])
            }));
            world.SetComponent(source, new LifeStateComponent(LifeState.Dead));
            var events = new EventBuffer<BattleEvent>();

            StatusSystem.Run(world, events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            Assert.IsFalse(world.StatusComponents.Has(target));
            Assert.AreEqual(BattleEventType.StatusExpired, events.AsStream()[0].Type);
        }

        [Test]
        public void Run_ZeroPeriodicDamageExpiresWithoutQueuingDamage()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.StatusComponents.Set(target, new StatusComponent(new[]
            {
                new StatusInstance("guard-break", StatusPolarity.Debuff, source, 1, 1, 1, 0, new BattleModifierInstance[0], new BattleTriggerInstance[0])
            }));
            var events = new EventBuffer<BattleEvent>();

            StatusSystem.Run(world, events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            Assert.IsFalse(world.StatusComponents.Has(target));
            Assert.AreEqual(BattleEventType.StatusExpired, events.AsStream()[0].Type);
            Assert.AreEqual("guard-break", events.AsStream()[0].StatusId);
        }

        [Test]
        public void Run_ExpiredStatusDoesNotRemoveRemainingStatus()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.StatusComponents.Set(target, new StatusComponent(new[]
            {
                new StatusInstance("burn", StatusPolarity.Debuff, source, 1, 1, 1, 0, new BattleModifierInstance[0], new BattleTriggerInstance[0]),
                new StatusInstance("poison", StatusPolarity.Debuff, source, 2, 1, 1, 0, new BattleModifierInstance[0], new BattleTriggerInstance[0])
            }));
            var events = new EventBuffer<BattleEvent>();

            StatusSystem.Run(world, events, new EventSequence(), new BattleTick(1));

            Assert.IsTrue(world.StatusComponents.Has(target));
            Assert.AreEqual(1, world.StatusComponents.Get(target).Statuses.Count);
            Assert.AreEqual("poison", world.StatusComponents.Get(target).Statuses[0].Id);
            Assert.AreEqual(1, world.StatusComponents.Get(target).Statuses[0].DurationRemainingTicks);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("burn", events.AsStream()[0].StatusId);
        }

        [Test]
        public void Run_MaxHealthModifierExpirationClampsCurrentHealth()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.StatusComponents.Set(target, new StatusComponent(new[]
            {
                new StatusInstance(
                    "fortitude",
                    StatusPolarity.Buff,
                    source,
                    durationRemainingTicks: 1,
                    tickIntervalTicks: 1,
                    ticksUntilNextPeriodicEffect: 1,
                    periodicDamage: 0,
                    modifiers: new[]
                    {
                        BattleModifierInstance.Stat(BattleStatId.MaxHealth, BattleModifierOperation.Flat, BattleScalar.FromInt(5))
                    },
                    triggers: new BattleTriggerInstance[0])
            }));
            world.HealthComponents.Set(target, new HealthComponent(current: 15));

            StatusSystem.Run(world, new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.IsFalse(world.StatusComponents.Has(target));
            Assert.AreEqual(10, BattleStatResolver.ResolveMaxHealth(world, target));
            Assert.AreEqual(10, world.HealthComponents.Get(target).Current);
        }

        [Test]
        public void Run_RemovingOneOwnerDoesNotSkipNextStatusOwner()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f));
            SpawnCombatant(world, new UnitId(3), new TeamId(2), new BattleVector2(2f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId removedOwner);
            world.TryFindEntity(new UnitId(3), out EntityId activeOwner);
            world.StatusComponents.Set(removedOwner, new StatusComponent(new[]
            {
                new StatusInstance("burn", StatusPolarity.Debuff, source, 2, 1, 1, 2, new BattleModifierInstance[0], new BattleTriggerInstance[0])
            }));
            world.StatusComponents.Set(activeOwner, new StatusComponent(new[]
            {
                new StatusInstance("poison", StatusPolarity.Debuff, source, 2, 1, 1, 1, new BattleModifierInstance[0], new BattleTriggerInstance[0])
            }));
            world.SetComponent(removedOwner, new LifeStateComponent(LifeState.Dead));

            StatusSystem.Run(world, new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.IsFalse(world.StatusComponents.Has(removedOwner));
            Assert.AreEqual(1, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(activeOwner, world.CommandBuffer.EffectCommands[0].Target);
            Assert.AreEqual(1, world.StatusComponents.Get(activeOwner).Statuses[0].DurationRemainingTicks);
        }

        private static void SpawnCombatant(BattleWorld world, UnitId unitId, TeamId teamId, BattleVector2 position)
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
                    basicAbility: BasicAbility(),
                    abilities: new AbilitySpawnData[0])));
            world.FlushSpawnCombatantCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(0));
        }

        private static AbilitySpawnData BasicAbility()
        {
            return TestCombatants.AbilitySpawn("basic-attack", 1f, 1, 1);
        }
    }
}
