using System.Collections.Generic;
using Combat.Core.Battle;
using Combat.Foundation.Events;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleWorldActionTests
    {
        [Test]
        public void FlushActionCommands_BasicAbilityStartsActionWithoutQueuingEffects()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 4);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 0));

            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertStartedAbilityAction(world, source, 0, "basic-slash", target, new BattleTick(1));
        }

        [Test]
        public void FlushActionCommands_UseAbilityStartsActionAndWritesAbilityStarted()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 4);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 0));
            world.FlushActionCommands(events, new EventSequence(), new BattleTick(1));

            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(1, stream.Count);
            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertStartedAbilityAction(world, source, 0, "basic-slash", target, new BattleTick(1));
            Assert.AreEqual(BattleEventType.AbilityStarted, stream[0].Type);
            Assert.AreEqual(new UnitId(1), stream[0].UnitId);
            Assert.AreEqual(new UnitId(1), stream[0].SourceUnitId);
            Assert.AreEqual(new UnitId(2), stream[0].TargetUnitId);
            Assert.AreEqual("basic-slash", stream[0].AbilityId);
            Assert.AreEqual(BattleEffectSourceKind.BasicAbility, stream[0].EffectSourceKind);
            Assert.AreEqual(BattleActionLocks.Movement | BattleActionLocks.StartAnotherAction, stream[0].ActionLocks);
            Assert.AreEqual(1, stream[0].Sequence);
            Assert.AreEqual(new BattleTick(1), stream[0].Tick);
        }

        [Test]
        public void FlushActionCommands_UseAbilityUsesAbilityActionLocks()
        {
            var world = new BattleWorld();
            SpawnCombatant(
                world,
                new UnitId(1),
                new TeamId(1),
                new BattleVector2(0f, 0f),
                basicAbility: new AbilitySpawnData(
                    "moving-slash",
                    BattleScalar.FromFloat(2f),
                    cooldownTicks: 0,
                    windupTicks: 0,
                    recoveryTicks: 1,
                    AbilityTargetSelection.CurrentEnemyTarget,
                    new[] { new AbilityEffectFrameData("release", 0, 0, new[] { BattleEffectData.Damage(1) }) },
                    BattleActionLocks.StartAnotherAction));
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 0));
            world.FlushActionCommands(events, new EventSequence(), new BattleTick(1));

            Assert.IsTrue(world.UnitActionComponents.TryGet(source, out UnitActionComponent action));
            Assert.AreEqual(BattleActionLocks.StartAnotherAction, action.Locks);
            Assert.AreEqual(BattleActionLocks.StartAnotherAction, events.AsStream()[0].ActionLocks);
        }

        [Test]
        public void FlushActionCommands_UseAbilityWithTimingStartsActionAtReleaseAndEndTicks()
        {
            var world = new BattleWorld();
            SpawnCombatant(
                world,
                new UnitId(1),
                new TeamId(1),
                new BattleVector2(0f, 0f),
                attackDamage: 1,
                abilities: new[] { TestCombatants.AbilitySpawn("heavy", 2f, 4, 6, windupTicks: 2, recoveryTicks: 3) });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 1));
            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(7));

            AssertStartedAbilityAction(
                world,
                source,
                1,
                "heavy",
                target,
                new BattleTick(7),
                new BattleTick(9),
                new BattleTick(12));
        }

        [Test]
        public void AbilitySystem_Run_DoesNotQueueActionWhenSourceHasActiveAction()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 4);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.TargetComponents.Set(source, new TargetComponent(target));
            world.UnitActionComponents.Set(
                source,
                UnitActionComponent.Ability(
                    0,
                    "basic-slash",
                    target,
                    new BattleTick(1),
                    new BattleTick(1),
                    new BattleTick(1),
                    BattleActionLocks.Movement | BattleActionLocks.StartAnotherAction));

            AbilitySystem.Run(world, new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(2));

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            AssertStartedAbilityAction(world, source, 0, "basic-slash", target, new BattleTick(1));
        }

        [Test]
        public void AbilitySystem_Run_DecrementsCooldownWhileSourceHasActiveActionWithoutQueuing()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 4, abilities: new[] { TestCombatants.AbilitySpawn("slash", 2f, 6, 3) });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.TargetComponents.Set(source, new TargetComponent(target));
            world.AbilityComponents.Set(source, world.AbilityComponents.Get(source).WithAbilityCooldownRemainingTicks(1, 2));
            world.UnitActionComponents.Set(
                source,
                UnitActionComponent.Ability(
                    0,
                    "basic-slash",
                    target,
                    new BattleTick(1),
                    new BattleTick(2),
                    new BattleTick(3),
                    BattleActionLocks.Movement | BattleActionLocks.StartAnotherAction));

            AbilitySystem.Run(world, new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(2));

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(1, world.AbilityComponents.Get(source).Abilities[1].CooldownRemainingTicks);
            AssertStartedAbilityAction(
                world,
                source,
                0,
                "basic-slash",
                target,
                new BattleTick(1),
                new BattleTick(2),
                new BattleTick(3));
        }

        [Test]
        public void FlushActionCommands_UseAbilityIgnoresSourceWithActiveAction()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 4, abilities: new[] { TestCombatants.AbilitySpawn("slash", 2f, 6, 3) });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            var events = new EventBuffer<BattleEvent>();
            world.UnitActionComponents.Set(
                source,
                UnitActionComponent.Ability(
                    0,
                    "basic-slash",
                    target,
                    new BattleTick(1),
                    new BattleTick(2),
                    new BattleTick(3),
                    BattleActionLocks.Movement | BattleActionLocks.StartAnotherAction));

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 1));
            world.FlushActionCommands(events, new EventSequence(), new BattleTick(2));

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(0, events.Count);
            Assert.AreEqual(0, world.AbilityComponents.Get(source).Abilities[1].CooldownRemainingTicks);
            AssertStartedAbilityAction(
                world,
                source,
                0,
                "basic-slash",
                target,
                new BattleTick(1),
                new BattleTick(2),
                new BattleTick(3));
        }

        [Test]
        public void FlushActionCommands_PositiveDamageBasicAbilityAdvancesCooldown()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 4);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 0));
            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertStartedAbilityAction(world, source, 0, "basic-slash", target, new BattleTick(1));
            Assert.AreEqual(2, world.AbilityComponents.Get(source).Abilities[0].CooldownRemainingTicks);
        }

        [Test]
        public void FlushActionCommands_BasicAbilityWithStatusesAndProjectileEmittersStartsAction()
        {
            var world = new BattleWorld();
            var burn = new StatusApplicationData("burn", StatusPolarity.Debuff, 3, 1, 2, new BattleModifierData[0], new BattleTriggerData[0]);
            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, 0.1f, 2f, 5, new[] { BattleEffectDefinition.Damage(3) });
            var emitter = new ProjectileEmitterSpawnData(ProjectileEmitterAnchorMode.FollowSource, default, 2, 1, ProjectilePattern.Single(new BattleVector2(1f, 0f)), payload);
            SpawnCombatant(
                world,
                new UnitId(1),
                new TeamId(1),
                new BattleVector2(0f, 0f),
                TestCombatants.AbilitySpawn("basic-fire", 2f, 0, 3, new[] { burn }, new[] { emitter }));
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 0));
            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertStartedAbilityAction(world, source, 0, "basic-fire", target, new BattleTick(1));
            Assert.AreEqual(2, world.AbilityComponents.Get(source).Abilities[0].CooldownRemainingTicks);
        }

        [Test]
        public void FlushActionCommands_SourceDeadIgnoresBasicAbility()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 4);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.SetComponent(source, new LifeStateComponent(LifeState.Dead));

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 0));
            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertNoActiveAction(world, source);
        }

        [Test]
        public void FlushActionCommands_TargetDeadIgnoresBasicAbility()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 4);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.SetComponent(target, new LifeStateComponent(LifeState.Dead));

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 0));
            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertNoActiveAction(world, source);
        }

        [Test]
        public void FlushActionCommands_SourceWithoutAbilityComponentIgnoresBasicAbility()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 4);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.RemoveComponent<AbilityComponent>(source);

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 0));
            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertNoActiveAction(world, source);
        }

        [Test]
        public void FlushActionCommands_SourceWithoutPositionComponentIgnoresBasicAbility()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 4);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.RemoveComponent<PositionComponent>(source);

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 0));
            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertNoActiveAction(world, source);
        }

        [Test]
        public void FlushActionCommands_TargetWithoutPositionComponentIgnoresBasicAbility()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 4);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.RemoveComponent<PositionComponent>(target);

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 0));
            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertNoActiveAction(world, source);
        }

        [Test]
        public void FlushActionCommands_TargetOutOfRangeIgnoresBasicAbility()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 4);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(5f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 0));
            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertNoActiveAction(world, source);
        }

        [Test]
        public void FlushActionCommands_ZeroDamageBasicAbilityAdvancesCooldownWithoutDamageEffect()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 0);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 0));
            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertStartedAbilityAction(world, source, 0, "basic-slash", target, new BattleTick(1));
            Assert.AreEqual(2, world.AbilityComponents.Get(source).Abilities[0].CooldownRemainingTicks);
        }

        [Test]
        public void FlushActionCommands_UseAbilityStartsActionAndStartsCooldown()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1, abilities: new[] { TestCombatants.AbilitySpawn("slash", 2f, 6, 3) });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 1));
            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertStartedAbilityAction(world, source, 1, "slash", target, new BattleTick(1));
            Assert.AreEqual(2, world.AbilityComponents.Get(source).Abilities[1].CooldownRemainingTicks);
        }

        [Test]
        public void FlushActionCommands_UseLowestHealthAllyAbilityStartsActionForAlly()
        {
            var world = new BattleWorld();
            SpawnCombatant(
                world,
                new UnitId(1),
                new TeamId(1),
                new BattleVector2(0f, 0f),
                attackDamage: 1,
                abilities: new[]
                {
                    new AbilitySpawnData(
                        "mend",
                        BattleScalar.FromFloat(3f),
                        cooldownTicks: 2,
                        windupTicks: 0,
                        recoveryTicks: 0,
                        AbilityTargetSelection.LowestHealthAlly,
                        TestCombatants.EffectFrameData(0, new[] { BattleEffectData.Heal(4) }))
                });
            SpawnCombatant(world, new UnitId(2), new TeamId(1), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId ally);
            world.HealthComponents.Set(ally, new HealthComponent(4));

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, ally, abilityIndex: 1));
            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertStartedAbilityAction(world, source, 1, "mend", ally, new BattleTick(1));
            Assert.AreEqual(4, world.HealthComponents.Get(ally).Current);
            Assert.AreEqual(1, world.AbilityComponents.Get(source).Abilities[1].CooldownRemainingTicks);
        }

        [Test]
        public void FlushActionCommands_UseLowestHealthAllyAbilityAcceptsLowestHealthInRangeAlly()
        {
            var world = new BattleWorld();
            SpawnCombatant(
                world,
                new UnitId(1),
                new TeamId(1),
                new BattleVector2(0f, 0f),
                attackDamage: 1,
                abilities: new[]
                {
                    new AbilitySpawnData(
                        "mend",
                        BattleScalar.FromFloat(3f),
                        cooldownTicks: 2,
                        windupTicks: 0,
                        recoveryTicks: 0,
                        AbilityTargetSelection.LowestHealthAlly,
                        TestCombatants.EffectFrameData(0, new[] { BattleEffectData.Heal(4) }))
                });
            SpawnCombatant(world, new UnitId(2), new TeamId(1), new BattleVector2(2f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(3), new TeamId(1), new BattleVector2(5f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId woundedInRangeAlly);
            world.TryFindEntity(new UnitId(3), out EntityId woundedOutOfRangeAlly);
            world.HealthComponents.Set(woundedInRangeAlly, new HealthComponent(4));
            world.HealthComponents.Set(woundedOutOfRangeAlly, new HealthComponent(1));

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, woundedInRangeAlly, abilityIndex: 1));
            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertStartedAbilityAction(world, source, 1, "mend", woundedInRangeAlly, new BattleTick(1));
            Assert.AreEqual(4, world.HealthComponents.Get(woundedInRangeAlly).Current);
            Assert.AreEqual(1, world.AbilityComponents.Get(source).Abilities[1].CooldownRemainingTicks);
        }

        [Test]
        public void FlushActionCommands_UseSelfAbilityStartsActionForSource()
        {
            var world = new BattleWorld();
            var focus = new StatusApplicationData("focus", StatusPolarity.Buff, 3, 3, 0, new BattleModifierData[0], new BattleTriggerData[0]);
            SpawnCombatant(
                world,
                new UnitId(1),
                new TeamId(1),
                new BattleVector2(0f, 0f),
                attackDamage: 1,
                abilities: new[]
                {
                    new AbilitySpawnData(
                        "focus",
                        BattleScalar.Zero,
                        cooldownTicks: 2,
                        windupTicks: 0,
                        recoveryTicks: 0,
                        AbilityTargetSelection.Self,
                        TestCombatants.EffectFrameData(0, new[] { BattleEffectData.ApplyStatus(focus) }))
                });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId enemy);

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, enemy, abilityIndex: 1));
            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, source, abilityIndex: 1));
            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertStartedAbilityAction(world, source, 1, "focus", source, new BattleTick(1));
            Assert.AreEqual(1, world.AbilityComponents.Get(source).Abilities[1].CooldownRemainingTicks);
        }

        [Test]
        public void FlushActionCommands_UseAbilityZeroDamageStartsCooldownWithoutDamageEffect()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1, abilities: new[] { TestCombatants.AbilitySpawn("guard", 2f, 0, 3) });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 1));
            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertStartedAbilityAction(world, source, 1, "guard", target, new BattleTick(1));
            Assert.AreEqual(2, world.AbilityComponents.Get(source).Abilities[1].CooldownRemainingTicks);
        }

        [Test]
        public void FlushActionCommands_UseAbilityWithStatusStartsActionAndStartsCooldown()
        {
            var world = new BattleWorld();
            var burn = new StatusApplicationData("burn", StatusPolarity.Debuff, 3, 1, 2, new BattleModifierData[0], new BattleTriggerData[0]);
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1, abilities: new[] { TestCombatants.AbilitySpawn("firebolt", 2f, 0, 3, new[] { burn }, new ProjectileEmitterSpawnData[0]) });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 1));
            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertStartedAbilityAction(world, source, 1, "firebolt", target, new BattleTick(1));
            Assert.AreEqual(2, world.AbilityComponents.Get(source).Abilities[1].CooldownRemainingTicks);
        }

        [Test]
        public void FlushEffectCommands_ApplyStatusAddsStatusComponentAndWritesEvent()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.ApplyStatus(
                source,
                target,
                new StatusApplicationData(
                    "burn",
                    StatusPolarity.Debuff,
                    3,
                    1,
                    2,
                    new[]
                    {
                        BattleModifierData.Damage(BattleDamageModifierStat.DamageTaken, BattleModifierOperation.Flat, BattleScalar.FromInt(3))
                    },
                    new BattleTriggerData[0])));
            world.FlushEffectCommands(events, sequence, new BattleTick(1));

            Assert.IsTrue(world.StatusComponents.Has(target));
            StatusInstance status = world.StatusComponents.Get(target).Statuses[0];
            Assert.AreEqual("burn", status.Id);
            Assert.AreEqual(source, status.Source);
            Assert.AreEqual(3, status.DurationRemainingTicks);
            Assert.AreEqual(1, status.TicksUntilNextPeriodicEffect);
            Assert.AreEqual(1, status.Modifiers.Count);
            Assert.AreEqual(BattleModifierTarget.Damage, status.Modifiers[0].Target);
            Assert.AreEqual(BattleDamageModifierStat.DamageTaken, status.Modifiers[0].DamageStat);
            Assert.AreEqual(BattleModifierOperation.Flat, status.Modifiers[0].Operation);
            Assert.AreEqual(BattleScalar.FromInt(3), status.Modifiers[0].Value);
            Assert.AreEqual(1, events.Count);
            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(BattleEventType.StatusApplied, stream[0].Type);
            Assert.AreEqual(new UnitId(1), stream[0].SourceUnitId);
            Assert.AreEqual(new UnitId(2), stream[0].UnitId);
            Assert.AreEqual("burn", stream[0].StatusId);
        }

        [Test]
        public void FlushEffectCommands_ApplyStatusRefreshesExistingStatus()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);

            world.CommandBuffer.QueueEffect(BattleEffectCommand.ApplyStatus(source, target, new StatusApplicationData("burn", StatusPolarity.Debuff, 3, 1, 2, new BattleModifierData[0], new BattleTriggerData[0])));
            world.FlushEffectCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));
            world.CommandBuffer.QueueEffect(BattleEffectCommand.ApplyStatus(
                source,
                target,
                new StatusApplicationData(
                    "burn",
                    StatusPolarity.Debuff,
                    5,
                    2,
                    4,
                    new[]
                    {
                        BattleModifierData.Damage(BattleDamageModifierStat.DamageTaken, BattleModifierOperation.PercentAdd, BattleScalar.FromFloat(0.5f))
                    },
                    new BattleTriggerData[0])));
            world.FlushEffectCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(2));

            Assert.AreEqual(1, world.StatusComponents.Get(target).Statuses.Count);
            StatusInstance status = world.StatusComponents.Get(target).Statuses[0];
            Assert.AreEqual(5, status.DurationRemainingTicks);
            Assert.AreEqual(2, status.TickIntervalTicks);
            Assert.AreEqual(2, status.TicksUntilNextPeriodicEffect);
            Assert.AreEqual(4, status.PeriodicDamage);
            Assert.AreEqual(1, status.Modifiers.Count);
            Assert.AreEqual(BattleModifierOperation.PercentAdd, status.Modifiers[0].Operation);
            Assert.AreEqual(BattleScalar.FromFloat(0.5f), status.Modifiers[0].Value);
        }

        [Test]
        public void FlushEffectCommands_ReapplyingSameStatusStacksAndRefreshesTiming()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();
            var status = new StatusApplicationData(
                "rage",
                StatusPolarity.Buff,
                durationTicks: 5,
                tickIntervalTicks: 5,
                periodicDamage: 0,
                modifiers: new[]
                {
                    BattleModifierData.Damage(BattleDamageModifierStat.DamageDealt, BattleModifierOperation.Flat, BattleScalar.FromInt(1))
                },
                triggers: new BattleTriggerData[0],
                maxStacks: 3);

            world.CommandBuffer.QueueEffect(BattleEffectCommand.ApplyStatus(source, target, status));
            world.FlushEffectCommands(events, sequence, new BattleTick(1));
            StatusInstance first = world.StatusComponents.Get(target).Statuses[0];
            StatusInstance aged = first.WithTiming(durationRemainingTicks: 2, ticksUntilNextPeriodicEffect: 2);
            world.StatusComponents.Set(target, new StatusComponent(new[] { aged }));

            world.CommandBuffer.QueueEffect(BattleEffectCommand.ApplyStatus(source, target, status));
            world.CommandBuffer.QueueEffect(BattleEffectCommand.ApplyStatus(source, target, status));
            world.CommandBuffer.QueueEffect(BattleEffectCommand.ApplyStatus(source, target, status));
            world.FlushEffectCommands(events, sequence, new BattleTick(2));

            StatusInstance stacked = world.StatusComponents.Get(target).Statuses[0];
            Assert.AreEqual(3, stacked.StackCount);
            Assert.AreEqual(3, stacked.MaxStacks);
            Assert.AreEqual(5, stacked.DurationRemainingTicks);
            Assert.AreEqual(5, stacked.TicksUntilNextPeriodicEffect);
        }

        [Test]
        public void FlushEffectCommands_ApplyStatusStoresTriggersOnStatusInstance()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            var trigger = new BattleTriggerData(
                BattleTriggerTiming.AfterDamageTaken,
                new[]
                {
                    BattleReactionEffectData.Create(BattleReactionTarget.Source, BattleEffectData.Damage(3))
                });

            world.CommandBuffer.QueueEffect(BattleEffectCommand.ApplyStatus(
                source,
                target,
                new StatusApplicationData(
                    "thorns",
                    StatusPolarity.Buff,
                    durationTicks: 3,
                    tickIntervalTicks: 1,
                    periodicDamage: 0,
                    modifiers: new BattleModifierData[0],
                    triggers: new[] { trigger })));
            world.FlushEffectCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            StatusInstance status = world.StatusComponents.Get(target).Statuses[0];
            Assert.AreEqual(1, status.Triggers.Count);
            Assert.AreEqual(BattleTriggerTiming.AfterDamageTaken, status.Triggers[0].Timing);
            Assert.AreEqual(1, status.Triggers[0].Effects.Count);
            Assert.AreEqual(BattleEffectType.Damage, status.Triggers[0].Effects[0].Effect.Type);
            Assert.AreEqual(BattleReactionTarget.Source, status.Triggers[0].Effects[0].Target);
            Assert.AreEqual(3, status.Triggers[0].Effects[0].Effect.Amount);
        }

        [Test]
        public void FlushEffectCommands_DamageEmitsDeathThroughDeathCheck()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(source, target, 10));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(LifeState.Dead, world.LifeStateComponents.Get(target).State);
            Assert.AreEqual(0, world.CommandBuffer.DeathCheckCommands.Count);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(BattleEventType.DamageApplied, events.AsStream()[0].Type);
            Assert.AreEqual(BattleEventType.UnitDied, events.AsStream()[1].Type);
        }

        [Test]
        public void FlushEffectCommands_DamageRecordsAttackerForNextTargetingPass()
        {
            var world = new BattleWorld();
            SpawnCombatant(
                world,
                new UnitId(1),
                new TeamId(1),
                new BattleVector2(8f, 0f),
                attackDamage: 1);
            SpawnCombatant(
                world,
                new UnitId(2),
                new TeamId(2),
                BattleVector2.Zero,
                attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId attacker);
            world.TryFindEntity(new UnitId(2), out EntityId defender);
            world.IntentComponents.Set(
                defender,
                new IntentComponent(BattleIntent.Auto(defender)));
            world.TargetingBehaviorComponents.Set(
                defender,
                new TargetingBehaviorComponent(
                    TargetingBehaviorSpawnData.Restricted(
                        acquisitionRange: BattleScalar.FromInt(4),
                        noProgressTimeoutTicks: 3,
                        minimumProgressDistance: BattleScalar.FromFloat(0.1f),
                        rejectedTargetCooldownTicks: 2)));

            world.CommandBuffer.QueueEffect(
                BattleEffectCommand.Damage(attacker, defender, 1));
            world.FlushEffectCommands(
                new EventBuffer<BattleEvent>(),
                new EventSequence(),
                new BattleTick(1));

            Assert.AreEqual(
                attacker,
                world.TargetingStateComponents.Get(defender).PendingAttacker);
            Assert.IsFalse(world.TargetComponents.Get(defender).Target.IsValid);

            TargetingSystem.Run(
                world,
                events: null,
                eventSequence: null,
                tick: new BattleTick(2));

            Assert.AreEqual(
                attacker,
                world.TargetComponents.Get(defender).Target);
            Assert.IsFalse(
                world.TargetingStateComponents.Get(defender).PendingAttacker.IsValid);
        }

        [Test]
        public void FlushEffectCommands_HealClampsToMaxHealthAndWritesEvent()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(2), new TeamId(1), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.HealthComponents.Set(target, new HealthComponent(current: 4));
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Heal(source, target, 9, BattleEffectContext.Ability("mend", BattleEffectType.Heal)));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(5));

            Assert.AreEqual(10, world.HealthComponents.Get(target).Current);
            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(1, stream.Count);
            Assert.AreEqual(BattleEventType.HealingApplied, stream[0].Type);
            Assert.AreEqual(new UnitId(1), stream[0].SourceUnitId);
            Assert.AreEqual(new UnitId(2), stream[0].TargetUnitId);
            Assert.AreEqual(6, stream[0].Amount);
            Assert.AreEqual(BattleEffectSourceKind.Ability, stream[0].EffectSourceKind);
            Assert.AreEqual(BattleEffectType.Heal, stream[0].EffectType);
            Assert.AreEqual("mend", stream[0].AbilityId);
        }

        [Test]
        public void FlushEffectCommands_HealClampsToEffectiveMaxHealth()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(2), new TeamId(1), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.HealthComponents.Set(target, new HealthComponent(current: 8));
            world.StatusComponents.Set(target, new StatusComponent(new[]
            {
                Status(
                    "fortitude",
                    StatusPolarity.Buff,
                    source,
                    BattleModifierInstance.Stat(BattleStatId.MaxHealth, BattleModifierOperation.Flat, BattleScalar.FromInt(5)))
            }));
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Heal(source, target, 20, BattleEffectContext.Ability("mend", BattleEffectType.Heal)));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(5));

            Assert.AreEqual(15, world.HealthComponents.Get(target).Current);
            Assert.AreEqual(15, BattleStatResolver.ResolveMaxHealth(world, target));
            Assert.AreEqual(7, events.AsStream()[0].Amount);
        }

        [Test]
        public void FlushEffectCommands_MaxHealthIncreaseDoesNotHealCurrentHealth()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(2), new TeamId(1), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.HealthComponents.Set(target, new HealthComponent(current: 4));
            var status = new StatusApplicationData(
                "fortitude",
                StatusPolarity.Buff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new[]
                {
                    BattleModifierData.Stat(BattleStatId.MaxHealth, BattleModifierOperation.Flat, BattleScalar.FromInt(5))
                },
                triggers: new BattleTriggerData[0]);

            world.CommandBuffer.QueueEffect(BattleEffectCommand.ApplyStatus(source, target, status));
            world.FlushEffectCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(5));

            Assert.AreEqual(4, world.HealthComponents.Get(target).Current);
            Assert.AreEqual(15, BattleStatResolver.ResolveMaxHealth(world, target));
        }

        [Test]
        public void FlushEffectCommands_HealAtFullHealthDoesNotWriteEvent()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(2), new TeamId(1), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Heal(source, target, 3, BattleEffectContext.Unknown(BattleEffectType.Heal)));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(10, world.HealthComponents.Get(target).Current);
            Assert.AreEqual(0, events.AsStream().Count);
        }

        [Test]
        public void FlushEffectCommands_AreaEffectDamagesEnemiesInUnitIdOrder()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(-1f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(4), new TeamId(2), new BattleVector2(0f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(0.5f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(3), new TeamId(1), new BattleVector2(0.25f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(5), new TeamId(2), new BattleVector2(3f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(4), out EntityId target);
            var area = new AreaEffectData(BattleScalar.FromFloat(1f), AreaEffectTargetFilter.Enemies, new[] { BattleEffectData.Damage(2) });
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.CreateAreaEffect(source, target, area, BattleEffectContext.Ability("nova", BattleEffectType.AreaEffect)));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(2, stream.Count);
            Assert.AreEqual(new UnitId(2), stream[0].TargetUnitId);
            Assert.AreEqual(new UnitId(4), stream[1].TargetUnitId);
            Assert.AreEqual(8, GetHealth(world, new UnitId(2)));
            Assert.AreEqual(8, GetHealth(world, new UnitId(4)));
            Assert.AreEqual(10, GetHealth(world, new UnitId(3)));
            Assert.AreEqual(10, GetHealth(world, new UnitId(5)));
        }

        [Test]
        public void FlushEffectCommands_AreaEffectCanHealAllies()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(2), new TeamId(1), new BattleVector2(0.5f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(3), new TeamId(2), new BattleVector2(0.5f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.HealthComponents.Set(source, new HealthComponent(6));
            world.HealthComponents.Set(target, new HealthComponent(7));
            world.TryFindEntity(new UnitId(3), out EntityId enemy);
            world.HealthComponents.Set(enemy, new HealthComponent(7));
            var area = new AreaEffectData(BattleScalar.FromFloat(1f), AreaEffectTargetFilter.Allies, new[] { BattleEffectData.Heal(3) });
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.CreateAreaEffect(source, target, area, BattleEffectContext.Ability("group-heal", BattleEffectType.AreaEffect)));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(9, GetHealth(world, new UnitId(1)));
            Assert.AreEqual(10, GetHealth(world, new UnitId(2)));
            Assert.AreEqual(7, GetHealth(world, new UnitId(3)));
            Assert.AreEqual(2, events.AsStream().Count);
        }

        [Test]
        public void FlushEffectCommands_AreaEffectChildDamageKeepsSourceContextAndTags()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(-1f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(0f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            var area = new AreaEffectData(BattleScalar.FromFloat(1f), AreaEffectTargetFilter.Enemies, new[] { BattleEffectData.Damage(2) });
            var context = new BattleEffectContext(
                BattleEffectSourceKind.Ability,
                BattleEffectType.AreaEffect,
                abilityId: "nova",
                statusId: null,
                projectileId: default,
                damageTags: new[] { "fire", "area" });
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.CreateAreaEffect(source, target, area, context));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(1, stream.Count);
            Assert.AreEqual(BattleEventType.DamageApplied, stream[0].Type);
            Assert.AreEqual(BattleEffectSourceKind.Ability, stream[0].EffectSourceKind);
            Assert.AreEqual(BattleEffectType.Damage, stream[0].EffectType);
            Assert.AreEqual("nova", stream[0].AbilityId);
            Assert.AreEqual(2, stream[0].DamageTags.Count);
            Assert.AreEqual("fire", stream[0].DamageTags[0]);
            Assert.AreEqual("area", stream[0].DamageTags[1]);
        }

        [Test]
        public void FlushEffectCommands_ReactionAreaDamageSuppressesNestedReactions()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(0.5f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(3), new TeamId(1), new BattleVector2(0.75f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId attacker);
            world.TryFindEntity(new UnitId(2), out EntityId defender);
            world.StatusComponents.Set(attacker, new StatusComponent(new[]
            {
                Status(
                    "counter",
                    attacker,
                    BattleTriggerTiming.AfterDamageTaken,
                    BattleReactionEffectInstance.Create(BattleReactionTarget.Source, BattleEffectData.Damage(1)))
            }));
            world.StatusComponents.Set(defender, new StatusComponent(new[]
            {
                Status(
                    "retaliation",
                    defender,
                    BattleTriggerTiming.AfterDamageTaken,
                    BattleReactionEffectInstance.Create(
                        BattleReactionTarget.Source,
                        BattleEffectData.CreateAreaEffect(new AreaEffectData(
                            BattleScalar.FromFloat(1f),
                            AreaEffectTargetFilter.Enemies,
                            new[] { BattleEffectData.Damage(2) }))))
            }));
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(attacker, defender, 3));
            world.FlushEffectCommands(events, sequence, new BattleTick(1));
            world.FlushEffectCommands(events, sequence, new BattleTick(2));

            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(3, CountEvents(stream, BattleEventType.DamageApplied));
            Assert.AreEqual(7, GetHealth(world, new UnitId(2)));
            Assert.AreEqual(8, GetHealth(world, new UnitId(1)));
            Assert.AreEqual(8, GetHealth(world, new UnitId(3)));
            Assert.AreEqual(0, world.CommandBuffer.ReactionEffectCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
        }

        [Test]
        public void FlushEffectCommands_AreaEffectAllUnitsIncludesBoundaryTargets()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(-1f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(0f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(3), new TeamId(1), new BattleVector2(1f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(4), new TeamId(2), new BattleVector2(1.25f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            var area = new AreaEffectData(BattleScalar.FromFloat(1f), AreaEffectTargetFilter.AllUnits, new[] { BattleEffectData.Damage(1) });
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.CreateAreaEffect(source, target, area, BattleEffectContext.Ability("pulse", BattleEffectType.AreaEffect)));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(3, stream.Count);
            Assert.AreEqual(new UnitId(1), stream[0].TargetUnitId);
            Assert.AreEqual(new UnitId(2), stream[1].TargetUnitId);
            Assert.AreEqual(new UnitId(3), stream[2].TargetUnitId);
            Assert.AreEqual(9, GetHealth(world, new UnitId(1)));
            Assert.AreEqual(9, GetHealth(world, new UnitId(2)));
            Assert.AreEqual(9, GetHealth(world, new UnitId(3)));
            Assert.AreEqual(10, GetHealth(world, new UnitId(4)));
        }

        [Test]
        public void FlushEffectCommands_ReactionEffectCanResolveBeforePendingDeathCheck()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId attacker);
            world.TryFindEntity(new UnitId(2), out EntityId defender);
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(attacker, defender, 10));
            world.CommandBuffer.QueueReactionEffect(BattleEffectCommand.Damage(defender, attacker, 3, BattleEffectTriggerPolicy.SuppressReactions));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.HealthComponents.Get(defender).Current);
            Assert.AreEqual(LifeState.Dead, world.LifeStateComponents.Get(defender).State);
            Assert.AreEqual(7, world.HealthComponents.Get(attacker).Current);
            Assert.AreEqual(3, events.Count);
            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(BattleEventType.DamageApplied, stream[0].Type);
            Assert.AreEqual(new UnitId(1), stream[0].SourceUnitId);
            Assert.AreEqual(new UnitId(2), stream[0].TargetUnitId);
            Assert.AreEqual(10, stream[0].Amount);
            Assert.AreEqual(BattleEventType.DamageApplied, stream[1].Type);
            Assert.AreEqual(new UnitId(2), stream[1].SourceUnitId);
            Assert.AreEqual(new UnitId(1), stream[1].TargetUnitId);
            Assert.AreEqual(3, stream[1].Amount);
            Assert.AreEqual(BattleEventType.UnitDied, stream[2].Type);
            Assert.AreEqual(new UnitId(2), stream[2].UnitId);
        }

        [Test]
        public void FlushEffectCommands_DuplicateDeathChecksEmitSingleDeathEvent()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId target);
            world.SetComponent(target, new HealthComponent(0));
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueDeathCheck(new DeathCheckCommand(target));
            world.CommandBuffer.QueueDeathCheck(new DeathCheckCommand(target));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(LifeState.Dead, world.LifeStateComponents.Get(target).State);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(BattleEventType.UnitDied, events.AsStream()[0].Type);
            Assert.AreEqual(new UnitId(1), events.AsStream()[0].UnitId);
        }

        [Test]
        public void FlushEffectCommands_AlreadyDeadDeathCheckDoesNotEmitDeathEvent()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId target);
            world.SetComponent(target, new HealthComponent(0));
            world.SetComponent(target, new LifeStateComponent(LifeState.Dead));
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueDeathCheck(new DeathCheckCommand(target));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(LifeState.Dead, world.LifeStateComponents.Get(target).State);
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void FlushEffectCommands_ApplyStatusIgnoresDeadSource()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.SetComponent(source, new LifeStateComponent(LifeState.Dead));
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.ApplyStatus(source, target, new StatusApplicationData("burn", StatusPolarity.Debuff, 3, 1, 2, new BattleModifierData[0], new BattleTriggerData[0])));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.IsFalse(world.StatusComponents.Has(target));
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void FlushEffectCommands_ApplyStatusIgnoresDeadTarget()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.SetComponent(target, new LifeStateComponent(LifeState.Dead));
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.QueueEffect(BattleEffectCommand.ApplyStatus(source, target, new StatusApplicationData("burn", StatusPolarity.Debuff, 3, 1, 2, new BattleModifierData[0], new BattleTriggerData[0])));
            world.FlushEffectCommands(events, new EventSequence(), new BattleTick(1));

            Assert.IsFalse(world.StatusComponents.Has(target));
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void FlushActionCommands_UseAbilityIgnoresInvalidAbilityIndex()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1, abilities: new[] { TestCombatants.AbilitySpawn("slash", 2f, 6, 3) });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 2));
            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertNoActiveAction(world, source);
            Assert.AreEqual(0, world.AbilityComponents.Get(source).Abilities[0].CooldownRemainingTicks);
        }

        [Test]
        public void FlushActionCommands_UseAbilityIgnoresCooldown()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1, abilities: new[] { TestCombatants.AbilitySpawn("slash", 2f, 6, 3) });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.AbilityComponents.Set(source, world.AbilityComponents.Get(source).WithAbilityCooldownRemainingTicks(1, 1));

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 1));
            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertNoActiveAction(world, source);
            Assert.AreEqual(1, world.AbilityComponents.Get(source).Abilities[1].CooldownRemainingTicks);
        }

        [Test]
        public void FlushActionCommands_UseAbilityIgnoresOutOfRangeTarget()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1, abilities: new[] { TestCombatants.AbilitySpawn("slash", 2f, 6, 3) });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(3f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);

            world.CommandBuffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 1));
            world.FlushActionCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            AssertNoActiveAction(world, source);
            Assert.AreEqual(0, world.AbilityComponents.Get(source).Abilities[1].CooldownRemainingTicks);
        }

        [Test]
        public void FlushEffectCommands_SpawnProjectileEmitterCreatesEmitterForNextTick()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(2f, 3f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, 0.1f, 2f, 5, new[] { BattleEffectDefinition.Damage(3) });
            var emitter = new ProjectileEmitterSpawnData(ProjectileEmitterAnchorMode.FollowSource, new BattleVector2(0.5f, 0f), 2, 1, ProjectilePattern.Single(new BattleVector2(1f, 0f)), payload);

            world.CommandBuffer.QueueEffect(BattleEffectCommand.SpawnProjectileEmitter(source, default, emitter));
            world.FlushEffectCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(7));

            Assert.AreEqual(1, world.ProjectileEmitterComponents.Entities.Count);
            EntityId emitterEntity = world.ProjectileEmitterComponents.Entities[0];
            ProjectileEmitterComponent component = world.ProjectileEmitterComponents.Get(emitterEntity);
            Assert.AreEqual(source, component.Source);
            Assert.AreEqual(new TeamId(1), component.TeamId);
            Assert.AreEqual(new BattleTick(8), component.ActivateOnTick);
            Assert.AreEqual(0, component.TicksUntilNextFire);
        }

        [Test]
        public void FlushEffectCommands_SpawnProjectileEmitterIgnoresDeadSource()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(2f, 3f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.SetComponent(source, new LifeStateComponent(LifeState.Dead));
            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, 0.1f, 2f, 5, new[] { BattleEffectDefinition.Damage(3) });
            var emitter = new ProjectileEmitterSpawnData(ProjectileEmitterAnchorMode.FollowSource, default, 2, 1, ProjectilePattern.Single(new BattleVector2(1f, 0f)), payload);

            world.CommandBuffer.QueueEffect(BattleEffectCommand.SpawnProjectileEmitter(source, default, emitter));
            world.FlushEffectCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(7));

            Assert.AreEqual(0, world.ProjectileEmitterComponents.Entities.Count);
        }

        [Test]
        public void FlushSpawnProjectileCommands_CreatesProjectileAndWritesSpawnedEvent()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, 0.25f, 3f, 5, new[] { BattleEffectDefinition.Damage(4) });
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.SpawnProjectile(new SpawnProjectileCommand(source, new TeamId(1), new BattleVector2(2f, 3f), new BattleVector2(1f, 0f), payload, new BattleTick(9)));
            world.FlushSpawnProjectileCommands(events, new EventSequence(), new BattleTick(8));

            Assert.AreEqual(1, world.ProjectileComponents.Entities.Count);
            EntityId projectileEntity = world.ProjectileComponents.Entities[0];
            ProjectileComponent projectile = world.ProjectileComponents.Get(projectileEntity);
            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(new ProjectileId(1), projectile.ProjectileId);
            Assert.AreEqual(new BattleTick(9), projectile.ActivateOnTick);
            Assert.AreEqual(BattleEventType.ProjectileSpawned, stream[0].Type);
            Assert.AreEqual(new ProjectileId(1), stream[0].ProjectileId);
            Assert.AreEqual(new BattleVector2(2f, 3f), stream[0].Position);
        }

        [Test]
        public void FlushSpawnProjectileCommands_IgnoresDeadSource()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.SetComponent(source, new LifeStateComponent(LifeState.Dead));
            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, 0.25f, 3f, 5, new[] { BattleEffectDefinition.Damage(4) });
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.SpawnProjectile(new SpawnProjectileCommand(source, new TeamId(1), new BattleVector2(2f, 3f), new BattleVector2(1f, 0f), payload, new BattleTick(9)));
            world.FlushSpawnProjectileCommands(events, new EventSequence(), new BattleTick(8));

            Assert.AreEqual(0, world.ProjectileComponents.Entities.Count);
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void FlushSpawnProjectileCommands_IgnoresNonUnitSource()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), attackDamage: 1);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, 0.25f, 3f, 5, new[] { BattleEffectDefinition.Damage(4) });
            var emitter = new ProjectileEmitterSpawnData(ProjectileEmitterAnchorMode.FollowSource, default, 2, 1, ProjectilePattern.Single(new BattleVector2(1f, 0f)), payload);
            world.CommandBuffer.QueueEffect(BattleEffectCommand.SpawnProjectileEmitter(source, default, emitter));
            world.FlushEffectCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(7));
            EntityId nonUnitSource = world.ProjectileEmitterComponents.Entities[0];
            var events = new EventBuffer<BattleEvent>();

            world.CommandBuffer.SpawnProjectile(new SpawnProjectileCommand(nonUnitSource, new TeamId(1), new BattleVector2(2f, 3f), new BattleVector2(1f, 0f), payload, new BattleTick(9)));
            world.FlushSpawnProjectileCommands(events, new EventSequence(), new BattleTick(8));

            Assert.AreEqual(0, world.ProjectileComponents.Entities.Count);
            Assert.AreEqual(0, events.Count);
        }

        private static void SpawnCombatant(BattleWorld world, UnitId unitId, TeamId teamId, BattleVector2 position, int attackDamage, IReadOnlyList<AbilitySpawnData> abilities = null)
        {
            SpawnCombatant(
                world,
                unitId,
                teamId,
                position,
                TestCombatants.AbilitySpawn(
                    "basic-slash",
                    range: 1.5f,
                    damage: attackDamage,
                    cooldownTicks: 3,
                    appliedStatuses: new StatusApplicationData[0],
                    projectileEmitters: new ProjectileEmitterSpawnData[0]),
                abilities);
        }

        private static void SpawnCombatant(BattleWorld world, UnitId unitId, TeamId teamId, BattleVector2 position, AbilitySpawnData basicAbility, IReadOnlyList<AbilitySpawnData> abilities = null)
        {
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();

            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                unitId,
                new CombatantSpawnData(
                    teamId,
                    "melee",
                    position,
                    maxHealth: 10,
                    radius: BattleScalar.FromFloat(0.25f),
                    moveSpeed: BattleScalar.Zero,
                    basicAbility: basicAbility,
                    abilities: abilities ?? new AbilitySpawnData[0])));
            world.FlushSpawnCombatantCommands(events, sequence, new BattleTick(0));
        }

        private static int GetHealth(BattleWorld world, UnitId unitId)
        {
            Assert.IsTrue(world.TryFindEntity(unitId, out EntityId entity));
            return world.HealthComponents.Get(entity).Current;
        }

        private static void AssertStartedAbilityAction(BattleWorld world, EntityId source, int abilityIndex, string abilityId, EntityId target, BattleTick tick, BattleTick? releaseTick = null, BattleTick? endTick = null)
        {
            Assert.IsTrue(world.UnitActionComponents.TryGet(source, out UnitActionComponent action));
            Assert.IsTrue(action.IsActive);
            Assert.AreEqual(UnitActionType.Ability, action.Type);
            Assert.AreEqual(abilityIndex, action.AbilityIndex);
            Assert.AreEqual(abilityId, action.AbilityId);
            Assert.AreEqual(target, action.Target);
            Assert.AreEqual(tick, action.StartedTick);
            Assert.AreEqual(releaseTick ?? tick, action.ReleaseTick);
            Assert.AreEqual(endTick ?? tick, action.EndTick);
            Assert.AreEqual(BattleActionLocks.Movement | BattleActionLocks.StartAnotherAction, action.Locks);
            Assert.IsFalse(action.HasReleased);
        }

        private static void AssertNoActiveAction(BattleWorld world, EntityId entity)
        {
            if (world.UnitActionComponents.TryGet(entity, out UnitActionComponent action))
            {
                Assert.IsFalse(action.IsActive);
            }
        }

        private static StatusInstance Status(string id, EntityId source, BattleTriggerTiming timing, BattleReactionEffectInstance effect)
        {
            return new StatusInstance(
                id,
                StatusPolarity.Buff,
                source,
                durationRemainingTicks: 3,
                tickIntervalTicks: 1,
                ticksUntilNextPeriodicEffect: 1,
                periodicDamage: 0,
                modifiers: new BattleModifierInstance[0],
                triggers: new[] { new BattleTriggerInstance(timing, new[] { effect }) });
        }

        private static StatusInstance Status(string id, StatusPolarity polarity, EntityId source, BattleModifierInstance modifier)
        {
            return new StatusInstance(
                id,
                polarity,
                source,
                durationRemainingTicks: 3,
                tickIntervalTicks: 1,
                ticksUntilNextPeriodicEffect: 1,
                periodicDamage: 0,
                modifiers: new[] { modifier },
                triggers: new BattleTriggerInstance[0]);
        }

        private static int CountEvents(EventStream<BattleEvent> stream, BattleEventType type)
        {
            var count = 0;
            for (var i = 0; i < stream.Count; i++)
            {
                if (stream[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
