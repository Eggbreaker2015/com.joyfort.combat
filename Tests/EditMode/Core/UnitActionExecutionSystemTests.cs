using Combat.Core.Battle;
using Combat.Foundation.Events;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class UnitActionExecutionSystemTests
    {
        [Test]
        public void Run_BeforeReleaseTickKeepsActionUnreleasedAndQueuesNoEffects()
        {
            BattleWorld world = CreateWorldWithSkillAction(out EntityId source, out EntityId target);
            var events = new EventBuffer<BattleEvent>();

            UnitActionExecutionSystem.Run(world, events, new EventSequence(), new BattleTick(2));

            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(0, events.Count);
            UnitActionComponent action = world.UnitActionComponents.Get(source);
            Assert.IsTrue(action.IsActive);
            Assert.IsFalse(action.HasReleased);
            Assert.AreEqual(target, action.Target);
        }

        [Test]
        public void Run_OnReleaseTickQueuesEffectsWritesAbilityReleasedAndMarksReleased()
        {
            BattleWorld world = CreateWorldWithSkillAction(out EntityId source, out EntityId target);
            var events = new EventBuffer<BattleEvent>();

            UnitActionExecutionSystem.Run(world, events, new EventSequence(), new BattleTick(3));

            Assert.AreEqual(1, world.CommandBuffer.EffectCommands.Count);
            BattleEffectCommand effect = world.CommandBuffer.EffectCommands[0];
            Assert.AreEqual(BattleEffectType.Damage, effect.Type);
            Assert.AreEqual(source, effect.Source);
            Assert.AreEqual(target, effect.Target);
            Assert.AreEqual(6, effect.Amount);
            Assert.AreEqual(BattleEffectSourceKind.Ability, effect.Context.SourceKind);
            Assert.AreEqual(BattleEffectType.Damage, effect.Context.EffectType);
            Assert.AreEqual("fire-slash", effect.Context.AbilityId);

            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(1, stream.Count);
            AssertAbilityEvent(
                stream[0],
                BattleEventType.AbilityReleased,
                new BattleTick(3),
                new UnitId(1),
                new UnitId(2),
                "fire-slash",
                BattleEffectSourceKind.Ability,
                sequence: 1);

            UnitActionComponent action = world.UnitActionComponents.Get(source);
            Assert.IsTrue(action.IsActive);
            Assert.IsTrue(action.HasReleased);
        }

        [Test]
        public void Run_OnEndTickAfterReleaseClearsActionAndWritesAbilityEnded()
        {
            BattleWorld world = CreateWorldWithSkillAction(out EntityId source, out _);
            world.UnitActionComponents.Set(source, world.UnitActionComponents.Get(source).WithReleased());
            var events = new EventBuffer<BattleEvent>();

            UnitActionExecutionSystem.Run(world, events, new EventSequence(), new BattleTick(5));

            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            Assert.IsFalse(world.UnitActionComponents.Get(source).IsActive);
            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(1, stream.Count);
            AssertAbilityEvent(
                stream[0],
                BattleEventType.AbilityEnded,
                new BattleTick(5),
                new UnitId(1),
                new UnitId(2),
                "fire-slash",
                BattleEffectSourceKind.Ability,
                sequence: 1);
        }

        [Test]
        public void Run_TargetOutOfRangeAtReleaseMarksReleasedWithoutEffectsOrAbilityReleased()
        {
            BattleWorld world = CreateWorldWithSkillAction(out EntityId source, out EntityId target);
            world.PositionComponents.Set(target, new PositionComponent(new BattleVector2(10f, 0f), BattleScalar.FromFloat(0.25f)));
            var events = new EventBuffer<BattleEvent>();

            UnitActionExecutionSystem.Run(world, events, new EventSequence(), new BattleTick(3));

            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(0, events.Count);
            UnitActionComponent action = world.UnitActionComponents.Get(source);
            Assert.IsTrue(action.IsActive);
            Assert.IsTrue(action.HasReleased);
        }

        [Test]
        public void Run_WhenReleaseAndEndShareTickReleasesThenEnds()
        {
            BattleWorld world = CreateWorldWithSkillAction(out EntityId source, out _);
            world.UnitActionComponents.Set(
                source,
                UnitActionComponent.Ability(
                    abilityIndex: 1,
                    abilityId: "fire-slash",
                    target: world.UnitActionComponents.Get(source).Target,
                    startedTick: new BattleTick(1),
                    releaseTick: new BattleTick(3),
                    endTick: new BattleTick(3),
                    locks: BattleActionLocks.Movement | BattleActionLocks.StartAnotherAction));
            var events = new EventBuffer<BattleEvent>();

            UnitActionExecutionSystem.Run(world, events, new EventSequence(), new BattleTick(3));

            Assert.AreEqual(1, world.CommandBuffer.EffectCommands.Count);
            Assert.IsFalse(world.UnitActionComponents.Get(source).IsActive);
            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(2, stream.Count);
            Assert.AreEqual(BattleEventType.AbilityReleased, stream[0].Type);
            Assert.AreEqual(BattleEventType.AbilityEnded, stream[1].Type);
            Assert.AreEqual("fire-slash", stream[0].AbilityId);
            Assert.AreEqual("fire-slash", stream[1].AbilityId);
            Assert.AreEqual(BattleEffectSourceKind.Ability, stream[0].EffectSourceKind);
            Assert.AreEqual(BattleEffectSourceKind.Ability, stream[1].EffectSourceKind);
        }

        [Test]
        public void Run_ReleasesOnlyEffectFramesDueOnCurrentTick()
        {
            BattleWorld world = CreateWorldWithMultiFrameSkillAction(out EntityId source, out _);
            var events = new EventBuffer<BattleEvent>();

            UnitActionExecutionSystem.Run(world, events, new EventSequence(), new BattleTick(3));

            Assert.AreEqual(1, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(3, world.CommandBuffer.EffectCommands[0].Amount);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(BattleEventType.AbilityReleased, events.AsStream()[0].Type);
            world.CommandBuffer.ClearEffectCommands();

            UnitActionExecutionSystem.Run(world, events, new EventSequence(), new BattleTick(5));

            Assert.AreEqual(1, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(7, world.CommandBuffer.EffectCommands[0].Amount);
            Assert.IsTrue(world.UnitActionComponents.Get(source).IsActive);
        }

        [Test]
        public void Run_ReleasesSameTickEffectFramesByOrder()
        {
            BattleWorld world = CreateWorldWithSameTickFrameAction(out _, out _);
            var events = new EventBuffer<BattleEvent>();

            UnitActionExecutionSystem.Run(world, events, new EventSequence(), new BattleTick(3));

            Assert.AreEqual(2, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(3, world.CommandBuffer.EffectCommands[0].Amount);
            Assert.AreEqual(6, world.CommandBuffer.EffectCommands[1].Amount);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(BattleEventType.AbilityReleased, events.AsStream()[0].Type);
            Assert.AreEqual(BattleEventType.AbilityReleased, events.AsStream()[1].Type);
        }

        [Test]
        public void Run_EndsAfterLastEffectFrameAndRecovery()
        {
            BattleWorld world = CreateWorldWithMultiFrameSkillAction(out EntityId source, out _);
            var events = new EventBuffer<BattleEvent>();

            UnitActionExecutionSystem.Run(world, events, new EventSequence(), new BattleTick(6));

            Assert.IsFalse(world.UnitActionComponents.Get(source).IsActive);
            Assert.AreEqual(BattleEventType.AbilityReleased, events.AsStream()[0].Type);
            Assert.AreEqual(BattleEventType.AbilityReleased, events.AsStream()[1].Type);
            Assert.AreEqual(BattleEventType.AbilityEnded, events.AsStream()[2].Type);
        }

        private static BattleWorld CreateWorldWithSkillAction(out EntityId source, out EntityId target)
        {
            var world = new BattleWorld();
            SpawnCombatant(
                world,
                new UnitId(1),
                new TeamId(1),
                new BattleVector2(0f, 0f),
                abilities: new[] { TestCombatants.AbilitySpawn("fire-slash", 2f, 6, 4) });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f));
            Assert.IsTrue(world.TryFindEntity(new UnitId(1), out source));
            Assert.IsTrue(world.TryFindEntity(new UnitId(2), out target));
            world.UnitActionComponents.Set(
                source,
                UnitActionComponent.Ability(
                    abilityIndex: 1,
                    abilityId: "fire-slash",
                    target: target,
                    startedTick: new BattleTick(1),
                    releaseTick: new BattleTick(3),
                    endTick: new BattleTick(5),
                    locks: BattleActionLocks.Movement | BattleActionLocks.StartAnotherAction));
            return world;
        }

        private static BattleWorld CreateWorldWithMultiFrameSkillAction(out EntityId source, out EntityId target)
        {
            return CreateWorldWithFrameAction(
                new[]
                {
                    new AbilityEffectFrameData("hit_01", tickOffset: 2, order: 0, effects: TestCombatants.EffectData(3)),
                    new AbilityEffectFrameData("hit_02", tickOffset: 4, order: 0, effects: TestCombatants.EffectData(7))
                },
                out source,
                out target);
        }

        private static BattleWorld CreateWorldWithSameTickFrameAction(out EntityId source, out EntityId target)
        {
            return CreateWorldWithFrameAction(
                new[]
                {
                    new AbilityEffectFrameData("late", tickOffset: 2, order: 1, effects: TestCombatants.EffectData(6)),
                    new AbilityEffectFrameData("early", tickOffset: 2, order: 0, effects: TestCombatants.EffectData(3))
                },
                out source,
                out target);
        }

        private static BattleWorld CreateWorldWithFrameAction(AbilityEffectFrameData[] frames, out EntityId source, out EntityId target)
        {
            var world = new BattleWorld();
            SpawnCombatant(
                world,
                new UnitId(1),
                new TeamId(1),
                new BattleVector2(0f, 0f),
                abilities: new[]
                {
                    new AbilitySpawnData(
                        "combo-slash",
                        BattleScalar.FromFloat(2f),
                        cooldownTicks: 4,
                        windupTicks: 2,
                        recoveryTicks: 1,
                        AbilityTargetSelection.CurrentEnemyTarget,
                        frames)
                });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f));
            Assert.IsTrue(world.TryFindEntity(new UnitId(1), out source));
            Assert.IsTrue(world.TryFindEntity(new UnitId(2), out target));
            world.UnitActionComponents.Set(
                source,
                UnitActionComponent.Ability(
                    abilityIndex: 1,
                    abilityId: "combo-slash",
                    target: target,
                    startedTick: new BattleTick(1),
                    releaseTick: new BattleTick(3),
                    endTick: new BattleTick(6),
                    locks: BattleActionLocks.Movement | BattleActionLocks.StartAnotherAction));
            return world;
        }

        private static void SpawnCombatant(
            BattleWorld world,
            UnitId unitId,
            TeamId teamId,
            BattleVector2 position,
            AbilitySpawnData[] abilities = null)
        {
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                unitId,
                new CombatantSpawnData(
                    teamId,
                    "unit",
                    position,
                    maxHealth: 20,
                    radius: BattleScalar.FromFloat(0.25f),
                    moveSpeed: BattleScalar.Zero,
                    basicAbility: TestCombatants.AbilitySpawn("basic-attack", 2f, 1, 2),
                    abilities: abilities ?? new AbilitySpawnData[0])));
            world.FlushSpawnCombatantCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(0));
        }

        private static void AssertAbilityEvent(
            BattleEvent battleEvent,
            BattleEventType type,
            BattleTick tick,
            UnitId source,
            UnitId target,
            string abilityId,
            BattleEffectSourceKind sourceKind,
            int sequence)
        {
            Assert.AreEqual(type, battleEvent.Type);
            Assert.AreEqual(sequence, battleEvent.Sequence);
            Assert.AreEqual(tick, battleEvent.Tick);
            Assert.AreEqual(source, battleEvent.UnitId);
            Assert.AreEqual(source, battleEvent.SourceUnitId);
            Assert.AreEqual(target, battleEvent.TargetUnitId);
            Assert.AreEqual(abilityId, battleEvent.AbilityId);
            Assert.AreEqual(sourceKind, battleEvent.EffectSourceKind);
        }
    }
}
