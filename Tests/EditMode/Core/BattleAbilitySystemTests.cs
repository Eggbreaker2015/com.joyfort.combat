using System.Collections.Generic;
using Combat.Core.Battle;
using Combat.Foundation.Events;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleAbilitySystemTests
    {
        [Test]
        public void Run_QueuesFirstReadyAbility()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), new[] { TestCombatants.AbilitySpawn("slash", 2f, 6, 3) });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), new AbilitySpawnData[0]);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.TargetComponents.Set(source, new TargetComponent(target));

            AbilitySystem.Run(world);

            Assert.AreEqual(1, world.CommandBuffer.ActionCommands.Count);
            BattleActionCommand action = world.CommandBuffer.ActionCommands[0];
            Assert.AreEqual(BattleActionType.UseAbility, action.Type);
            Assert.AreEqual(1, action.AbilityIndex);
        }

        [Test]
        public void Run_DecrementsCoolingAbilityAndQueuesNextReadyAbility()
        {
            var world = new BattleWorld();
            SpawnCombatant(
                world,
                new UnitId(1),
                new TeamId(1),
                new BattleVector2(0f, 0f),
                new[]
                {
                    TestCombatants.AbilitySpawn("heavy", 2f, 9, 3),
                    TestCombatants.AbilitySpawn("quick", 2f, 4, 0)
                });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), new AbilitySpawnData[0]);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.TargetComponents.Set(source, new TargetComponent(target));
            world.AbilityComponents.Set(source, world.AbilityComponents.Get(source).WithAbilityCooldownRemainingTicks(1, 2));

            AbilitySystem.Run(world);

            Assert.AreEqual(1, world.AbilityComponents.Get(source).Abilities[1].CooldownRemainingTicks);
            Assert.AreEqual(1, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(2, world.CommandBuffer.ActionCommands[0].AbilityIndex);
        }

        [Test]
        public void Run_QueuesAbilityThatBecomesReadyAfterCooldownTick()
        {
            var world = new BattleWorld();
            SpawnCombatant(
                world,
                new UnitId(1),
                new TeamId(1),
                new BattleVector2(0f, 0f),
                new[] { TestCombatants.AbilitySpawn("heavy", 2f, 9, 3) },
                basicAbility: TestCombatants.AbilitySpawn("basic-slash", 2f, 1, 2));
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), new AbilitySpawnData[0]);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.TargetComponents.Set(source, new TargetComponent(target));
            world.AbilityComponents.Set(source, world.AbilityComponents.Get(source).WithAbilityCooldownRemainingTicks(1, 1));

            AbilitySystem.Run(world);

            Assert.AreEqual(0, world.AbilityComponents.Get(source).Abilities[1].CooldownRemainingTicks);
            Assert.AreEqual(1, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(1, world.CommandBuffer.ActionCommands[0].AbilityIndex);
        }

        [Test]
        public void Run_OutOfRangeAbilitiesDoNotQueueAction()
        {
            var world = new BattleWorld();
            SpawnCombatant(
                world,
                new UnitId(1),
                new TeamId(1),
                new BattleVector2(0f, 0f),
                new[] { TestCombatants.AbilitySpawn("slash", 1f, 6, 3) },
                basicAbility: TestCombatants.AbilitySpawn("basic-slash", 1f, 1, 2));
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(2f, 0f), new AbilitySpawnData[0]);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.TargetComponents.Set(source, new TargetComponent(target));

            AbilitySystem.Run(world);

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
        }

        [Test]
        public void Run_QueuesBasicAbilityWhenNoSkillCanAct()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), new[] { TestCombatants.AbilitySpawn("long-shot", 0.5f, 9, 3) });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), new AbilitySpawnData[0]);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.TargetComponents.Set(source, new TargetComponent(target));

            AbilitySystem.Run(world);

            Assert.AreEqual(1, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.ActionCommands[0].AbilityIndex);
        }

        [Test]
        public void Run_HoldIntentTicksCooldownButDoesNotQueueAction()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), new[] { TestCombatants.AbilitySpawn("slash", 2f, 6, 3) });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), new AbilitySpawnData[0]);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.TargetComponents.Set(source, new TargetComponent(target));
            world.IntentComponents.Set(source, new IntentComponent(BattleIntent.Hold(source)));
            world.AbilityComponents.Set(source, world.AbilityComponents.Get(source).WithAbilityCooldownRemainingTicks(1, 1));

            AbilitySystem.Run(world);

            Assert.AreEqual(0, world.AbilityComponents.Get(source).Abilities[1].CooldownRemainingTicks);
            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
        }

        [Test]
        public void Run_UseAbilityIntentQueuesRequestedReadyAbility()
        {
            var world = new BattleWorld();
            SpawnCombatant(
                world,
                new UnitId(1),
                new TeamId(1),
                new BattleVector2(0f, 0f),
                new[]
                {
                    TestCombatants.AbilitySpawn("heavy", 2f, 9, 3),
                    TestCombatants.AbilitySpawn("quick", 2f, 4, 0)
                });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), new AbilitySpawnData[0]);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.TargetComponents.Set(source, new TargetComponent(target));
            world.IntentComponents.Set(source, new IntentComponent(BattleIntent.UseAbility(source, abilityIndex: 2, target: target)));

            AbilitySystem.Run(world);

            Assert.AreEqual(1, world.CommandBuffer.ActionCommands.Count);
            BattleActionCommand action = world.CommandBuffer.ActionCommands[0];
            Assert.AreEqual(2, action.AbilityIndex);
            Assert.AreEqual(target, action.Target);
        }

        [Test]
        public void Run_UseAbilityIntentInvalidTargetDoesNotFallbackToAutoAbility()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), new[] { TestCombatants.AbilitySpawn("slash", 2f, 6, 3) });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), new AbilitySpawnData[0]);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.TargetComponents.Set(source, new TargetComponent(target));
            world.IntentComponents.Set(source, new IntentComponent(BattleIntent.UseAbility(source, abilityIndex: 1, target: default)));

            AbilitySystem.Run(world);

            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
        }

        [Test]
        public void Run_QueuesLowestHealthAllyAbilityAgainstLowestHealthAlly()
        {
            var world = new BattleWorld();
            SpawnCombatant(
                world,
                new UnitId(1),
                new TeamId(1),
                new BattleVector2(0f, 0f),
                new[]
                {
                    new AbilitySpawnData(
                        "mend",
                        BattleScalar.FromFloat(5f),
                        cooldownTicks: 1,
                        windupTicks: 0,
                        recoveryTicks: 0,
                        AbilityTargetSelection.LowestHealthAlly,
                        TestCombatants.EffectFrameData(0, new[] { BattleEffectData.Heal(4) }))
                });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), new AbilitySpawnData[0]);
            SpawnCombatant(world, new UnitId(3), new TeamId(1), new BattleVector2(2f, 0f), new AbilitySpawnData[0]);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId enemyTarget);
            world.TryFindEntity(new UnitId(3), out EntityId woundedAlly);
            world.HealthComponents.Set(woundedAlly, new HealthComponent(6));
            world.TargetComponents.Set(source, new TargetComponent(enemyTarget));

            AbilitySystem.Run(world);

            Assert.AreEqual(1, world.CommandBuffer.ActionCommands.Count);
            BattleActionCommand action = world.CommandBuffer.ActionCommands[0];
            Assert.AreEqual(1, action.AbilityIndex);
            Assert.AreEqual(woundedAlly, action.Target);
        }

        [Test]
        public void Run_QueuesLowestHealthAllyAbilityAgainstLowestHealthInRangeAlly()
        {
            var world = new BattleWorld();
            SpawnCombatant(
                world,
                new UnitId(1),
                new TeamId(1),
                new BattleVector2(0f, 0f),
                new[]
                {
                    new AbilitySpawnData(
                        "mend",
                        BattleScalar.FromFloat(3f),
                        cooldownTicks: 1,
                        windupTicks: 0,
                        recoveryTicks: 0,
                        AbilityTargetSelection.LowestHealthAlly,
                        TestCombatants.EffectFrameData(0, new[] { BattleEffectData.Heal(4) }))
                });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), new AbilitySpawnData[0]);
            SpawnCombatant(world, new UnitId(3), new TeamId(1), new BattleVector2(2f, 0f), new AbilitySpawnData[0]);
            SpawnCombatant(world, new UnitId(4), new TeamId(1), new BattleVector2(5f, 0f), new AbilitySpawnData[0]);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId enemyTarget);
            world.TryFindEntity(new UnitId(3), out EntityId woundedInRangeAlly);
            world.TryFindEntity(new UnitId(4), out EntityId woundedOutOfRangeAlly);
            world.HealthComponents.Set(woundedInRangeAlly, new HealthComponent(6));
            world.HealthComponents.Set(woundedOutOfRangeAlly, new HealthComponent(1));
            world.TargetComponents.Set(source, new TargetComponent(enemyTarget));

            AbilitySystem.Run(world);

            Assert.AreEqual(1, world.CommandBuffer.ActionCommands.Count);
            BattleActionCommand action = world.CommandBuffer.ActionCommands[0];
            Assert.AreEqual(1, action.AbilityIndex);
            Assert.AreEqual(woundedInRangeAlly, action.Target);
        }

        [Test]
        public void Run_QueuesSelfAbilityAgainstSource()
        {
            var world = new BattleWorld();
            SpawnCombatant(
                world,
                new UnitId(1),
                new TeamId(1),
                new BattleVector2(0f, 0f),
                new[]
                {
                    new AbilitySpawnData(
                        "focus",
                        BattleScalar.Zero,
                        cooldownTicks: 1,
                        windupTicks: 0,
                        recoveryTicks: 0,
                        AbilityTargetSelection.Self,
                        TestCombatants.EffectFrameData(0, new[] { BattleEffectData.ApplyStatus(new StatusApplicationData("focus", StatusPolarity.Buff, 3, 3, 0, new BattleModifierData[0], new BattleTriggerData[0])) }))
                });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), new AbilitySpawnData[0]);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId enemyTarget);
            world.TargetComponents.Set(source, new TargetComponent(enemyTarget));

            AbilitySystem.Run(world);

            Assert.AreEqual(1, world.CommandBuffer.ActionCommands.Count);
            BattleActionCommand action = world.CommandBuffer.ActionCommands[0];
            Assert.AreEqual(1, action.AbilityIndex);
            Assert.AreEqual(source, action.Target);
        }

        [Test]
        public void Run_QueuesBasicAbilityThatBecomesReadyAfterCooldownTick()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), new AbilitySpawnData[0]);
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f), new AbilitySpawnData[0]);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);
            world.TargetComponents.Set(source, new TargetComponent(target));
            world.AbilityComponents.Set(source, world.AbilityComponents.Get(source).WithAbilityCooldownRemainingTicks(0, 1));

            AbilitySystem.Run(world);

            Assert.AreEqual(0, world.AbilityComponents.Get(source).Abilities[0].CooldownRemainingTicks);
            Assert.AreEqual(1, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.ActionCommands[0].AbilityIndex);
        }

        [Test]
        public void Run_QueuesActionWithoutTurningWhenFacingIsLocked()
        {
            var world = new BattleWorld();
            SpawnCombatant(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), new[] { TestCombatants.AbilitySpawn("slash", 2f, 6, 3) });
            SpawnCombatant(world, new UnitId(2), new TeamId(2), new BattleVector2(0f, 1f), new AbilitySpawnData[0]);
            Assert.IsTrue(world.TryFindEntity(new UnitId(1), out EntityId source));
            Assert.IsTrue(world.TryFindEntity(new UnitId(2), out EntityId target));
            world.TargetComponents.Set(source, new TargetComponent(target));
            world.UnitActionComponents.Set(
                source,
                UnitActionComponent.Ability(
                    0,
                    "basic-slash",
                    target,
                    new BattleTick(1),
                    new BattleTick(2),
                    new BattleTick(3),
                    BattleActionLocks.Facing));
            var events = new EventBuffer<BattleEvent>();

            AbilitySystem.Run(world, events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(1, world.CommandBuffer.ActionCommands.Count);
            Assert.AreEqual(1, world.CommandBuffer.ActionCommands[0].AbilityIndex);
            Assert.AreEqual(new BattleVector2(1f, 0f), world.FacingComponents.Get(source).Direction);
            Assert.AreEqual(0, CountEvents(events.AsStream(), BattleEventType.UnitFacingChanged));
        }

        private static void SpawnCombatant(
            BattleWorld world,
            UnitId unitId,
            TeamId teamId,
            BattleVector2 position,
            IReadOnlyList<AbilitySpawnData> abilities,
            AbilitySpawnData? basicAbility = null)
        {
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                unitId,
                new CombatantSpawnData(
                    teamId,
                    "melee",
                    position,
                    maxHealth: 20,
                    radius: BattleScalar.FromFloat(0.25f),
                    moveSpeed: BattleScalar.Zero,
                    basicAbility: basicAbility ?? TestCombatants.AbilitySpawn("basic-slash", 2f, 1, 2),
                    abilities: abilities)));
            world.FlushSpawnCombatantCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(0));
        }

        private static int CountEvents(EventStream<BattleEvent> events, BattleEventType type)
        {
            var count = 0;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
