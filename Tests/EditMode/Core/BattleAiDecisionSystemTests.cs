using Combat.Core.Battle;
using Combat.Foundation.Events;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleAiDecisionSystemTests
    {
        [Test]
        public void Step_AiMeleeChasesAndAttacksThroughExistingPipeline()
        {
            var attacker = TestCombatants.Create(
                "ai-melee",
                maxHealth: 20,
                moveSpeed: 10f,
                attackRange: 1f,
                attackDamage: 4,
                attackCooldownTicks: 2,
                aiDefinition: new AiDefinition("basic-melee"));
            var defender = TestCombatants.Create(
                "defender",
                maxHealth: 20,
                moveSpeed: 0f,
                attackRange: 0f,
                attackDamage: 0,
                attackCooldownTicks: 1);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), attacker, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), defender, new BattleVector2(3f, 0f))
                }));

            simulation.Step(BattleInputFrame.Empty);
            Assert.IsTrue(HasMove(simulation, new UnitId(1)));
            Assert.IsFalse(HasDamage(simulation, new UnitId(1), new UnitId(2)));

            bool attacked = false;
            for (int i = 0; i < 8 && !attacked; i++)
            {
                simulation.Step(BattleInputFrame.Empty);
                attacked = HasDamage(simulation, new UnitId(1), new UnitId(2));
            }

            Assert.IsTrue(attacked);
        }

        [Test]
        public void Run_UpdatesBrainStateAfterMovementReachesAbilityRange()
        {
            var world = new BattleWorld();
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                new UnitId(1),
                SpawnData(new TeamId(1), new BattleVector2(0f, 0f), hasBrain: true)));
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                new UnitId(2),
                SpawnData(new TeamId(2), new BattleVector2(1.5f, 0f), hasBrain: false)));
            world.FlushSpawnCombatantCommands(events, sequence, new BattleTick(0));
            world.TryFindEntity(new UnitId(1), out EntityId aiEntity);

            TargetingSystem.Run(world);
            var scratch = new MovementSystem.Scratch();
            BattleTick attackTick = default;
            for (int tickValue = 1; tickValue <= 8; tickValue++)
            {
                var tick = new BattleTick(tickValue);
                MovementSystem.Run(
                    world,
                    BattleScalar.FromFloat(0.1f),
                    events,
                    sequence,
                    tick,
                    scratch);
                AiDecisionSystem.Run(world, tick);
                if (world.BrainComponents.Get(aiEntity).State == BrainState.Attack)
                {
                    attackTick = tick;
                    break;
                }
            }

            BrainComponent brain = world.BrainComponents.Get(aiEntity);
            Assert.AreEqual("basic-melee", brain.DefinitionId);
            Assert.AreEqual(AiBrainKind.StateMachine, brain.Kind);
            Assert.AreEqual(BrainState.Attack, brain.State);
            Assert.AreEqual(attackTick, brain.StateEnteredTick);
            Assert.AreEqual(0, world.CommandBuffer.ActionCommands.Count);
        }

        [Test]
        public void Run_AttacksWhenReadySkillAbilityIsInRange()
        {
            var world = new BattleWorld();
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();
            var tick = new BattleTick(1);
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                new UnitId(1),
                SpawnData(
                    new TeamId(1),
                    new BattleVector2(0f, 0f),
                    hasBrain: true,
                    abilities: new[] { TestCombatants.AbilitySpawn("long-shot", 3f, 4, 2) })));
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                new UnitId(2),
                SpawnData(new TeamId(2), new BattleVector2(3f, 0f), hasBrain: false)));
            world.FlushSpawnCombatantCommands(events, sequence, new BattleTick(0));
            world.TryFindEntity(new UnitId(1), out EntityId aiEntity);

            TargetingSystem.Run(world);
            AiDecisionSystem.Run(world, tick);

            Assert.AreEqual(BrainState.Attack, world.BrainComponents.Get(aiEntity).State);
        }

        [Test]
        public void Run_ChasesWhenOnlyCoolingSkillAbilityCanReachTarget()
        {
            var world = new BattleWorld();
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();
            var tick = new BattleTick(1);
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                new UnitId(1),
                SpawnData(
                    new TeamId(1),
                    new BattleVector2(0f, 0f),
                    hasBrain: true,
                    abilities: new[] { TestCombatants.AbilitySpawn("long-shot", 3f, 4, 2) })));
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                new UnitId(2),
                SpawnData(new TeamId(2), new BattleVector2(3f, 0f), hasBrain: false)));
            world.FlushSpawnCombatantCommands(events, sequence, new BattleTick(0));
            world.TryFindEntity(new UnitId(1), out EntityId aiEntity);
            world.AbilityComponents.Set(aiEntity, world.AbilityComponents.Get(aiEntity).WithAbilityCooldownRemainingTicks(1, 1));

            TargetingSystem.Run(world);
            AiDecisionSystem.Run(world, tick);

            Assert.AreEqual(BrainState.Chase, world.BrainComponents.Get(aiEntity).State);
        }

        [Test]
        public void Targeting_AcquiresOnlyInsideAlertRangeAndKeepsStickyTargetOutsideRange()
        {
            var world = new BattleWorld();
            SpawnUnit(
                world,
                new UnitId(1),
                new TeamId(1),
                BattleVector2.Zero,
                TargetingBehaviorSpawnData.Restricted(
                    BattleScalar.FromInt(2),
                    noProgressTimeoutTicks: 10,
                    BattleScalar.FromRaw(BattleScalar.One.RawValue / 10),
                    rejectedTargetCooldownTicks: 3));
            SpawnUnit(world, new UnitId(2), new TeamId(2), new BattleVector2(3f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId enemy);

            TargetingSystem.Run(world, null, null, new BattleTick(1));

            Assert.IsFalse(world.TargetComponents.Get(source).Target.IsValid);

            world.PositionComponents.Set(enemy, new PositionComponent(new BattleVector2(1.5f, 0f), BattleScalar.FromFloat(0.25f)));
            TargetingSystem.Run(world, null, null, new BattleTick(2));
            Assert.AreEqual(enemy, world.TargetComponents.Get(source).Target);

            world.PositionComponents.Set(enemy, new PositionComponent(new BattleVector2(4f, 0f), BattleScalar.FromFloat(0.25f)));
            TargetingSystem.Run(world, null, null, new BattleTick(3));
            Assert.AreEqual(enemy, world.TargetComponents.Get(source).Target);
        }

        [Test]
        public void Movement_AutoApproachesEnemyOutsideAlertRangeWithoutAcquiringCombatTarget()
        {
            var world = new BattleWorld();
            SpawnUnit(
                world,
                new UnitId(1),
                new TeamId(1),
                BattleVector2.Zero,
                TargetingBehaviorSpawnData.Restricted(
                    BattleScalar.One,
                    noProgressTimeoutTicks: 3,
                    BattleScalar.FromRaw(BattleScalar.One.RawValue / 10),
                    rejectedTargetCooldownTicks: 2));
            SpawnUnit(world, new UnitId(2), new TeamId(2), new BattleVector2(5f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);

            TargetingSystem.Run(world, null, null, new BattleTick(1));
            MovementSystem.Run(
                world,
                ticksPerSecond: 10,
                new EventBuffer<BattleEvent>(),
                new EventSequence(),
                new BattleTick(1),
                new MovementSystem.Scratch());

            Assert.IsFalse(world.TargetComponents.Get(source).Target.IsValid);
            Assert.Greater(world.PositionComponents.Get(source).Position.XRaw, 0L);
        }

        [Test]
        public void Targeting_NoProgressReleasesAndTemporarilyRejectsOldTarget()
        {
            var world = new BattleWorld();
            SpawnUnit(
                world,
                new UnitId(1),
                new TeamId(1),
                BattleVector2.Zero,
                TargetingBehaviorSpawnData.Restricted(
                    BattleScalar.FromInt(5),
                    noProgressTimeoutTicks: 2,
                    BattleScalar.FromRaw(BattleScalar.One.RawValue / 10),
                    rejectedTargetCooldownTicks: 3));
            SpawnUnit(world, new UnitId(2), new TeamId(2), new BattleVector2(2f, 0f));
            SpawnUnit(world, new UnitId(3), new TeamId(2), new BattleVector2(3f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId first);
            world.TryFindEntity(new UnitId(3), out EntityId second);

            TargetingSystem.Run(world, null, null, new BattleTick(1));
            Assert.AreEqual(first, world.TargetComponents.Get(source).Target);

            TargetingSystem.Run(world, null, null, new BattleTick(2));
            TargetingSystem.Run(world, null, null, new BattleTick(3));

            Assert.AreEqual(second, world.TargetComponents.Get(source).Target);
        }

        [Test]
        public void Targeting_CumulativeProgressResetsNoProgressTimeout()
        {
            var world = new BattleWorld();
            SpawnUnit(
                world,
                new UnitId(1),
                new TeamId(1),
                BattleVector2.Zero,
                TargetingBehaviorSpawnData.Restricted(
                    BattleScalar.FromInt(5),
                    noProgressTimeoutTicks: 2,
                    BattleScalar.FromFloat(0.1f),
                    rejectedTargetCooldownTicks: 3));
            SpawnUnit(
                world,
                new UnitId(2),
                new TeamId(2),
                new BattleVector2(2f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);

            TargetingSystem.Run(world, null, null, new BattleTick(1));
            TargetingSystem.Run(world, null, null, new BattleTick(2));
            PositionComponent sourcePosition =
                world.PositionComponents.Get(source);
            world.PositionComponents.Set(
                source,
                new PositionComponent(
                    new BattleVector2(0.2f, 0f),
                    sourcePosition.Radius));

            TargetingSystem.Run(world, null, null, new BattleTick(3));

            Assert.AreEqual(target, world.TargetComponents.Get(source).Target);
            Assert.AreEqual(
                0,
                world.TargetingStateComponents.Get(source).NoProgressTicks);
        }

        [Test]
        public void Movement_AfterNoProgressReleaseExcludesRejectedTargetFromApproachGuide()
        {
            var world = new BattleWorld();
            SpawnUnit(
                world,
                new UnitId(1),
                new TeamId(1),
                BattleVector2.Zero,
                TargetingBehaviorSpawnData.Restricted(
                    BattleScalar.FromInt(5),
                    noProgressTimeoutTicks: 2,
                    BattleScalar.FromFloat(0.1f),
                    rejectedTargetCooldownTicks: 3));
            SpawnUnit(
                world,
                new UnitId(2),
                new TeamId(2),
                new BattleVector2(2f, 0f));
            SpawnUnit(
                world,
                new UnitId(3),
                new TeamId(2),
                new BattleVector2(-6f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);

            TargetingSystem.Run(world, null, null, new BattleTick(1));
            TargetingSystem.Run(world, null, null, new BattleTick(2));
            TargetingSystem.Run(world, null, null, new BattleTick(3));
            Assert.IsFalse(world.TargetComponents.Get(source).Target.IsValid);

            MovementSystem.Run(
                world,
                ticksPerSecond: 10,
                new EventBuffer<BattleEvent>(),
                new EventSequence(),
                new BattleTick(3),
                new MovementSystem.Scratch());

            Assert.Less(
                world.PositionComponents.Get(source).Position.XRaw,
                0L);
        }

        [Test]
        public void Targeting_InAttackRangeDoesNotAccumulateNoProgressTimeout()
        {
            var world = new BattleWorld();
            SpawnUnit(
                world,
                new UnitId(1),
                new TeamId(1),
                BattleVector2.Zero,
                TargetingBehaviorSpawnData.Restricted(
                    BattleScalar.FromInt(2),
                    noProgressTimeoutTicks: 1,
                    BattleScalar.FromRaw(BattleScalar.One.RawValue / 10),
                    rejectedTargetCooldownTicks: 2));
            SpawnUnit(world, new UnitId(2), new TeamId(2), new BattleVector2(0.8f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);

            for (var tick = 1; tick <= 5; tick++)
            {
                TargetingSystem.Run(world, null, null, new BattleTick(tick));
            }

            Assert.AreEqual(target, world.TargetComponents.Get(source).Target);
        }

        [Test]
        public void Targeting_NoCurrentTargetPrioritizesAttackerOutsideAlertRange()
        {
            var world = new BattleWorld();
            SpawnUnit(
                world,
                new UnitId(1),
                new TeamId(1),
                BattleVector2.Zero,
                TargetingBehaviorSpawnData.Restricted(
                    BattleScalar.One,
                    noProgressTimeoutTicks: 3,
                    BattleScalar.FromRaw(BattleScalar.One.RawValue / 10),
                    rejectedTargetCooldownTicks: 2));
            SpawnUnit(world, new UnitId(2), new TeamId(2), new BattleVector2(4f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId defender);
            world.TryFindEntity(new UnitId(2), out EntityId attacker);

            TargetingSystem.RecordDamageSource(world, attacker, defender);
            TargetingSystem.Run(world, null, null, new BattleTick(1));

            Assert.AreEqual(attacker, world.TargetComponents.Get(defender).Target);
        }

        [Test]
        public void Targeting_CurrentValidTargetIsNotReplacedByNewAttacker()
        {
            var world = new BattleWorld();
            SpawnUnit(
                world,
                new UnitId(1),
                new TeamId(1),
                BattleVector2.Zero,
                TargetingBehaviorSpawnData.Restricted(
                    BattleScalar.FromInt(3),
                    noProgressTimeoutTicks: 10,
                    BattleScalar.FromRaw(BattleScalar.One.RawValue / 10),
                    rejectedTargetCooldownTicks: 2));
            SpawnUnit(world, new UnitId(2), new TeamId(2), new BattleVector2(1.5f, 0f));
            SpawnUnit(world, new UnitId(3), new TeamId(2), new BattleVector2(2f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId defender);
            world.TryFindEntity(new UnitId(2), out EntityId current);
            world.TryFindEntity(new UnitId(3), out EntityId attacker);

            TargetingSystem.Run(world, null, null, new BattleTick(1));
            TargetingSystem.RecordDamageSource(world, attacker, defender);
            TargetingSystem.Run(world, null, null, new BattleTick(2));

            Assert.AreEqual(current, world.TargetComponents.Get(defender).Target);
        }

        private static CombatantSpawnData SpawnData(TeamId teamId, BattleVector2 position, bool hasBrain, AbilitySpawnData[] abilities = null)
        {
            return new CombatantSpawnData(
                teamId,
                "melee",
                position,
                maxHealth: 20,
                radius: BattleScalar.FromFloat(0.25f),
                moveSpeed: BattleScalar.FromFloat(10f),
                basicAbility: TestCombatants.AbilitySpawn("basic-attack", 1f, 4, 2),
                abilities: abilities ?? new AbilitySpawnData[0],
                brain: hasBrain ? new BrainSpawnData("basic-melee", AiBrainKind.StateMachine) : BrainSpawnData.None);
        }

        private static void SpawnUnit(
            BattleWorld world,
            UnitId unitId,
            TeamId teamId,
            BattleVector2 position,
            TargetingBehaviorSpawnData targetingBehavior = default)
        {
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                unitId,
                new CombatantSpawnData(
                    teamId,
                    "targeting-test",
                    position,
                    maxHealth: 20,
                    radius: BattleScalar.FromFloat(0.25f),
                    moveSpeed: BattleScalar.FromInt(1),
                    basicAbility: TestCombatants.AbilitySpawn("basic-attack", 1f, 1, 1),
                    abilities: new AbilitySpawnData[0],
                    brain: BrainSpawnData.None,
                    targetingBehavior: targetingBehavior)));
            world.FlushSpawnCombatantCommands(
                new EventBuffer<BattleEvent>(),
                new EventSequence(),
                new BattleTick(0));
        }

        private static bool HasMove(BattleSimulation simulation, UnitId unitId)
        {
            for (var i = 0; i < simulation.Events.Count; i++)
            {
                BattleEvent battleEvent = simulation.Events[i];
                if (battleEvent.Type == BattleEventType.UnitMoved
                    && battleEvent.UnitId.Equals(unitId))
                {
                    return true;
                }
            }

            return false;
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
    }
}
