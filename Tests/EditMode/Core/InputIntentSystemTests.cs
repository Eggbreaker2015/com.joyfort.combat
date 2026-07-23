using System.Collections.Generic;
using System.Reflection;
using Combat.Core.Battle;
using Combat.Foundation.Events;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class InputIntentSystemTests
    {
        [Test]
        public void BattleWorld_IntentComponentsCleanUpWhenEntityIsDestroyed()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId entity);

            world.IntentComponents.Set(entity, new IntentComponent(BattleIntent.Auto(entity)));
            Assert.IsTrue(RawIntentStorageContains(world, entity));

            world.CommandBuffer.DestroyEntity(new DestroyEntityCommand(entity));
            world.ApplyStructuralCommands();

            Assert.IsFalse(RawIntentStorageContains(world, entity));
        }

        [Test]
        public void BattleWorld_DestroyEntityIgnoresLaterStructuralAddForSameEntity()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId entity);

            world.CommandBuffer.DestroyEntity(new DestroyEntityCommand(entity));
            world.CommandBuffer.AddComponent(entity, new IntentComponent(BattleIntent.Auto(entity)));

            Assert.DoesNotThrow(world.ApplyStructuralCommands);
            Assert.IsFalse(world.IsEntityAlive(entity));
            Assert.IsFalse(RawIntentStorageContains(world, entity));
        }

        [Test]
        public void Run_EmptyInputWritesAutoIntentForAliveUnits()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId entity);

            InputIntentSystem.Run(world, BattleInputFrame.Empty, null, null, new BattleTick(1));

            Assert.IsTrue(world.IntentComponents.TryGet(entity, out IntentComponent component));
            Assert.AreEqual(BattleIntentType.Auto, component.Intent.Type);
            Assert.AreEqual(entity, component.Intent.Source);
        }

        [Test]
        public void Run_InvalidAutoInputIsIgnoredAndDoesNotQueueWorldCommands()
        {
            var world = new BattleWorld();
            var frame = new BattleInputFrame(new[]
            {
                BattleInputCommand.Auto(new UnitId(404))
            });

            InputIntentSystem.Run(world, frame, null, null, new BattleTick(1));

            Assert.AreEqual(0, world.IntentComponents.Entities.Count);
            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
        }

        [Test]
        public void Run_HoldInputWritesHoldIntentForAliveUnit()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId entity);
            var frame = new BattleInputFrame(new[]
            {
                BattleInputCommand.Hold(new UnitId(1))
            });

            InputIntentSystem.Run(world, frame, null, null, new BattleTick(1));

            Assert.IsTrue(world.IntentComponents.TryGet(entity, out IntentComponent component));
            Assert.AreEqual(BattleIntentType.Hold, component.Intent.Type);
            Assert.AreEqual(entity, component.Intent.Source);
            Assert.IsFalse(BattleIntentFilters.AllowsAutoBehavior(world, entity));
        }

        [Test]
        public void Run_MoveToPositionClearsAutomaticTargetAndTargetingMemory()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), BattleVector2.Zero);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), BattleVector2.Right);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.TargetComponents.Set(source, new TargetComponent(target));
            world.TargetingStateComponents.Set(
                source,
                new TargetingStateComponent(
                    target,
                    BattleScalar.One,
                    noProgressTicks: 2,
                    rejectedTarget: target,
                    rejectedUntilTick: 10,
                    pendingAttacker: target));

            InputIntentSystem.Run(
                world,
                new BattleInputFrame(new[]
                {
                    BattleInputCommand.MoveToPosition(
                        new UnitId(1),
                        new BattleVector2(3f, 0f))
                }),
                events: null,
                eventSequence: null,
                tick: new BattleTick(3));

            Assert.IsFalse(world.TargetComponents.Get(source).Target.IsValid);
            TargetingStateComponent state =
                world.TargetingStateComponents.Get(source);
            Assert.IsFalse(state.TrackedTarget.IsValid);
            Assert.AreEqual(BattleScalar.Zero, state.ProgressBaseline);
            Assert.AreEqual(0, state.NoProgressTicks);
            Assert.IsFalse(state.RejectedTarget.IsValid);
            Assert.IsFalse(state.PendingAttacker.IsValid);
        }

        [Test]
        public void Run_FocusTargetInputWritesTargetIntentForAliveUnits()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            var frame = new BattleInputFrame(new[]
            {
                BattleInputCommand.FocusTarget(new UnitId(1), new UnitId(2))
            });

            InputIntentSystem.Run(world, frame, null, null, new BattleTick(1));

            Assert.IsTrue(world.IntentComponents.TryGet(source, out IntentComponent component));
            Assert.AreEqual(BattleIntentType.FocusTarget, component.Intent.Type);
            Assert.AreEqual(source, component.Intent.Source);
            Assert.AreEqual(target, component.Intent.Target);
        }

        [Test]
        public void Run_UseAbilityInputWritesIntentEvenWhenTargetUnitIdIsInvalid()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            var frame = new BattleInputFrame(new[]
            {
                BattleInputCommand.UseAbility(new UnitId(1), abilityIndex: 1, targetUnitId: new UnitId(404))
            });

            InputIntentSystem.Run(world, frame, null, null, new BattleTick(1));

            Assert.IsTrue(world.IntentComponents.TryGet(source, out IntentComponent component));
            Assert.AreEqual(BattleIntentType.UseAbility, component.Intent.Type);
            Assert.AreEqual(source, component.Intent.Source);
            Assert.AreEqual(default(EntityId), component.Intent.Target);
            Assert.AreEqual(1, component.Intent.AbilityIndex);
        }

        [Test]
        public void Run_EmptyInputClearsPreviousUseAbilityIntentBackToAuto()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.IntentComponents.Set(source, new IntentComponent(BattleIntent.UseAbility(source, abilityIndex: 1, target: target)));

            InputIntentSystem.Run(world, BattleInputFrame.Empty, null, null, new BattleTick(2));

            Assert.IsTrue(world.IntentComponents.TryGet(source, out IntentComponent component));
            Assert.AreEqual(BattleIntentType.Auto, component.Intent.Type);
        }

        [Test]
        public void Run_RemovesIntentFromDeadUnits()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId entity);
            world.IntentComponents.Set(entity, new IntentComponent(BattleIntent.Auto(entity)));
            world.SetComponent(entity, new LifeStateComponent(LifeState.Dead));

            InputIntentSystem.Run(world, BattleInputFrame.Empty, null, null, new BattleTick(1));

            Assert.IsFalse(world.IntentComponents.Has(entity));
        }

        [Test]
        public void BattleIntentFilters_MissingIntentAllowsAutoBehavior()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId entity);

            Assert.IsTrue(BattleIntentFilters.AllowsAutoBehavior(world, entity));
        }

        [Test]
        public void BattleIntentFilters_AutoIntentAllowsAutoBehavior()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId entity);
            world.IntentComponents.Set(entity, new IntentComponent(BattleIntent.Auto(entity)));

            Assert.IsTrue(BattleIntentFilters.AllowsAutoBehavior(world, entity));
        }

        [Test]
        public void BattleIntentFilters_FocusTargetAllowsAutoBehaviorAfterTargeting()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.IntentComponents.Set(source, new IntentComponent(BattleIntent.FocusTarget(source, target)));
            world.TargetComponents.Set(source, new TargetComponent(target));

            Assert.IsTrue(BattleIntentFilters.AllowsAutoBehavior(world, source));
        }

        [Test]
        public void MoveToPosition_InterruptsActionBeforeSameTickRelease()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), BattleVector2.Zero);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), BattleVector2.Right);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.UnitActionComponents.Set(source, UnitActionComponent.Ability(
                0,
                "basic-attack",
                target,
                new BattleTick(0),
                new BattleTick(1),
                new BattleTick(2),
                BattleActionLocks.Movement));
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();

            InputIntentSystem.Run(
                world,
                new BattleInputFrame(new[]
                {
                    BattleInputCommand.MoveToPosition(new UnitId(1), new BattleVector2(-1f, 0f))
                }),
                events,
                sequence,
                new BattleTick(1));
            UnitActionExecutionSystem.Run(world, events, sequence, new BattleTick(1));

            Assert.IsFalse(world.UnitActionComponents.Get(source).IsActive);
            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(BattleEventType.AbilityEnded, events.AsStream()[0].Type);
        }

        [Test]
        public void GarrisonedUnit_IsExcludedFromTargetingUntilAutoDeploysIt()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), BattleVector2.Zero);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), BattleVector2.Right);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);

            InputIntentSystem.Run(
                world,
                new BattleInputFrame(new[] { BattleInputCommand.Garrison(new UnitId(2)) }),
                null,
                null,
                new BattleTick(1));
            TargetingSystem.Run(world);

            Assert.IsTrue(world.GarrisonedComponents.Has(target));
            Assert.AreEqual(default(EntityId), world.TargetComponents.Get(source).Target);

            InputIntentSystem.Run(
                world,
                new BattleInputFrame(new[] { BattleInputCommand.Auto(new UnitId(2)) }),
                null,
                null,
                new BattleTick(2));
            TargetingSystem.Run(world);

            Assert.IsFalse(world.GarrisonedComponents.Has(target));
            Assert.AreEqual(target, world.TargetComponents.Get(source).Target);
        }

        [Test]
        public void MoveToPosition_DeploysGarrisonedUnitBeforeMovement()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), BattleVector2.Zero);
            world.TryFindEntity(new UnitId(1), out EntityId unit);
            world.GarrisonedComponents.Set(unit, default);

            InputIntentSystem.Run(
                world,
                new BattleInputFrame(new[]
                {
                    BattleInputCommand.MoveToPosition(new UnitId(1), BattleVector2.Right)
                }),
                null,
                null,
                new BattleTick(1));

            Assert.IsFalse(world.GarrisonedComponents.Has(unit));
            Assert.That(world.IntentComponents.Get(unit).Intent.Type,
                Is.EqualTo(BattleIntentType.MoveToPosition));
        }

        [Test]
        public void GarrisonAndDeploy_EmitTransitionEventsOnlyOnce()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), BattleVector2.Zero);
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();

            InputIntentSystem.Run(
                world,
                new BattleInputFrame(new[]
                {
                    BattleInputCommand.Garrison(new UnitId(1)),
                    BattleInputCommand.Garrison(new UnitId(1))
                }),
                events,
                sequence,
                new BattleTick(1));
            InputIntentSystem.Run(
                world,
                new BattleInputFrame(new[]
                {
                    BattleInputCommand.Auto(new UnitId(1)),
                    BattleInputCommand.Auto(new UnitId(1))
                }),
                events,
                sequence,
                new BattleTick(2));

            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(events.AsStream()[0].Type, Is.EqualTo(BattleEventType.UnitGarrisoned));
            Assert.That(events.AsStream()[0].UnitId, Is.EqualTo(new UnitId(1)));
            Assert.That(events.AsStream()[0].TeamId, Is.EqualTo(new TeamId(1)));
            Assert.That(events.AsStream()[1].Type, Is.EqualTo(BattleEventType.UnitDeployed));
            Assert.That(events.AsStream()[1].UnitId, Is.EqualTo(new UnitId(1)));
            Assert.That(events.AsStream()[1].TeamId, Is.EqualTo(new TeamId(1)));
        }

        private static bool RawIntentStorageContains(BattleWorld world, EntityId entity)
        {
            FieldInfo field = typeof(ComponentStorage<IntentComponent>).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var components = (Dictionary<EntityId, IntentComponent>)field.GetValue(world.IntentComponents);
            return components.ContainsKey(entity);
        }

        private static void SpawnCombatant(BattleWorld world, UnitId unitId, TeamId teamId, BattleVector2 position)
        {
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                unitId,
                new CombatantSpawnData(
                    teamId,
                    "unit",
                    position,
                    maxHealth: 20,
                    radius: BattleScalar.FromFloat(0.25f),
                    moveSpeed: BattleScalar.One,
                    basicAbility: TestCombatants.AbilitySpawn("basic-attack", 1f, 1, 1),
                    abilities: new AbilitySpawnData[0])));
            world.FlushSpawnCombatantCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(0));
        }
    }
}
