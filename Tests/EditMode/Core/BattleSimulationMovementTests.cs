using System;
using Combat.Core.Battle;
using Combat.Core.LocalAvoidance;
using Combat.Foundation.Events;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleSimulationMovementTests
    {
        [Test]
        public void Step_SelectsNearestEnemyWithStableTieBreak()
        {
            var melee = TestCombatants.Create("melee", maxHealth: 20, moveSpeed: 0f, attackRange: 10f, attackDamage: 1, attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), melee, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), melee, new BattleVector2(5f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), melee, new BattleVector2(5f, 0f))
                }));

            simulation.Step(BattleInputFrame.Empty);
            simulation.Step(BattleInputFrame.Empty);

            BattleEvent damage = FindDamageFrom(simulation, new UnitId(1));
            Assert.AreEqual(new UnitId(2), damage.TargetUnitId);
        }

        [Test]
        public void Step_MovesTowardTargetAndEmitsMoveEvent()
        {
            var melee = TestCombatants.Create("melee", maxHealth: 20, moveSpeed: 10f, attackRange: 1f, attackDamage: 0, attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), melee, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), melee, new BattleVector2(5f, 0f))
                },
                default,
                automaticVictoryEnabled: true,
                localAvoidanceEnabled: true,
                BattleSpatialMapDefinition.Empty));

            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(2, CountEvents(simulation, BattleEventType.UnitMoved));
            BattleVector2 first = FindMove(simulation, new UnitId(1)).Position;
            BattleVector2 second = FindMove(simulation, new UnitId(2)).Position;
            Assert.Greater(first.XRaw, 0L);
            Assert.Less(second.XRaw, new BattleVector2(5f, 0f).XRaw);
            Assert.LessOrEqual(
                BattleVector2.DistanceScalar(BattleVector2.Zero, first).RawValue,
                BattleScalar.One.RawValue);
            Assert.LessOrEqual(
                BattleVector2.DistanceScalar(new BattleVector2(5f, 0f), second).RawValue,
                BattleScalar.One.RawValue);
            AssertEnemyPairIsHardSafe(simulation, new UnitId(1), new UnitId(2));
        }

        [Test]
        public void Step_EmptyInputStillRunsAutomaticTargetMoveAndAbilityPipeline()
        {
            var melee = TestCombatants.Create("melee", maxHealth: 20, moveSpeed: 10f, attackRange: 1f, attackDamage: 3, attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), melee, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), melee, new BattleVector2(2f, 0f))
                }));

            simulation.Step(BattleInputFrame.Empty);

            Assert.IsFalse(HasDamage(simulation, new UnitId(1), new UnitId(2)));
            Assert.IsTrue(HasMove(simulation, new UnitId(1)));

            bool attacked = false;
            for (int i = 0; i < 8 && !attacked; i++)
            {
                simulation.Step(BattleInputFrame.Empty);
                attacked = HasDamage(simulation, new UnitId(1), new UnitId(2));
            }

            Assert.IsTrue(attacked);
        }

        [Test]
        public void Step_HoldInputPreventsAutomaticMovementAndAbility()
        {
            var melee = TestCombatants.Create("melee", maxHealth: 20, moveSpeed: 10f, attackRange: 1f, attackDamage: 3, attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), melee, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), melee, new BattleVector2(2f, 0f))
                }));
            var input = new BattleInputFrame(new[]
            {
                BattleInputCommand.Hold(new UnitId(1))
            });

            simulation.Step(input);

            Assert.IsFalse(HasMove(simulation, new UnitId(1)));
            Assert.IsFalse(HasDamage(simulation, new UnitId(1), new UnitId(2)));
        }

        [Test]
        public void Step_FocusTargetInputUsesSpecifiedEnemyInsteadOfNearestEnemy()
        {
            var ranged = TestCombatants.Create("ranged", maxHealth: 20, moveSpeed: 0f, attackRange: 10f, attackDamage: 3, attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), ranged, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), ranged, new BattleVector2(1f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), ranged, new BattleVector2(5f, 0f))
                }));

            simulation.Step(new BattleInputFrame(new[]
            {
                BattleInputCommand.FocusTarget(new UnitId(1), new UnitId(3))
            }));
            simulation.Step(new BattleInputFrame(new[]
            {
                BattleInputCommand.FocusTarget(new UnitId(1), new UnitId(3))
            }));

            Assert.IsTrue(HasDamage(simulation, new UnitId(1), new UnitId(3)));
            Assert.IsFalse(HasDamage(simulation, new UnitId(1), new UnitId(2)));
        }

        [Test]
        public void Step_HighSpeedOpponentsRemainHardSafe()
        {
            var melee = TestCombatants.Create("melee", maxHealth: 20, moveSpeed: 100f, attackRange: 1f, attackDamage: 0, attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), melee, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), melee, new BattleVector2(5f, 0f))
                },
                default,
                automaticVictoryEnabled: true,
                localAvoidanceEnabled: true,
                BattleSpatialMapDefinition.Empty));

            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(2, CountEvents(simulation, BattleEventType.UnitMoved));
            AssertEnemyPairIsHardSafe(simulation, new UnitId(1), new UnitId(2));
        }

        [Test]
        public void MoveToPosition_DoesNotCrossEnemyCollisionCircle()
        {
            var mover = TestCombatants.Create(
                "mover", maxHealth: 20, moveSpeed: 30f, attackRange: 1f,
                attackDamage: 0, attackCooldownTicks: 2);
            var blocker = TestCombatants.Create(
                "blocker", maxHealth: 20, moveSpeed: 0f, attackRange: 1f,
                attackDamage: 0, attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), mover, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), blocker, new BattleVector2(2f, 0f))
                },
                default,
                automaticVictoryEnabled: false,
                localAvoidanceEnabled: true,
                BattleSpatialMapDefinition.Empty));

            simulation.Step(new BattleInputFrame(new[]
            {
                BattleInputCommand.MoveToPosition(new UnitId(1), new BattleVector2(5f, 0f)),
                BattleInputCommand.Hold(new UnitId(2))
            }));

            BattleScalar combinedRadius =
                simulation.World.PositionComponents.Get(Entity(simulation, new UnitId(1))).Radius +
                simulation.World.PositionComponents.Get(Entity(simulation, new UnitId(2))).Radius;
            Assert.That(BattleVector2.DistanceScalar(
                    FindMove(simulation, new UnitId(1)).Position,
                    new BattleVector2(2f, 0f)),
                Is.GreaterThanOrEqualTo(combinedRadius));
        }

        [Test]
        public void Step_DefaultConfigBypassesLocalAvoidanceAndCommitsStraightPreferredStep()
        {
            var mover = TestCombatants.Create(
                "mover", maxHealth: 20, moveSpeed: 30f, attackRange: 1f,
                attackDamage: 0, attackCooldownTicks: 2);
            var blocker = TestCombatants.Create(
                "blocker", maxHealth: 20, moveSpeed: 0f, attackRange: 1f,
                attackDamage: 0, attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(
                        new TeamId(1),
                        mover,
                        BattleVector2.Zero),
                    new InitialCombatantSpawn(
                        new TeamId(2),
                        blocker,
                        new BattleVector2(2f, 0f))
                },
                default,
                automaticVictoryEnabled: false));

            simulation.Step(new BattleInputFrame(new[]
            {
                BattleInputCommand.MoveToPosition(
                    new UnitId(1),
                    new BattleVector2(5f, 0f)),
                BattleInputCommand.Hold(new UnitId(2))
            }));

            Assert.AreEqual(
                new BattleVector2(3f, 0f),
                FindMove(simulation, new UnitId(1)).Position);
            Assert.AreEqual(
                0,
                simulation.MovementSystemScratch.LastSolveStats
                    .CandidateEvaluationCount);
        }

        [Test]
        public void Step_DefaultConfigDoesNotRecoverExistingEnemyOverlap()
        {
            var unit = TestCombatants.Create(
                "unit", maxHealth: 20, moveSpeed: 0f, attackRange: 1f,
                attackDamage: 0, attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(
                        new TeamId(1),
                        unit,
                        BattleVector2.Zero),
                    new InitialCombatantSpawn(
                        new TeamId(2),
                        unit,
                        BattleVector2.Zero)
                },
                default,
                automaticVictoryEnabled: false));

            simulation.Step(new BattleInputFrame(new[]
            {
                BattleInputCommand.Hold(new UnitId(1)),
                BattleInputCommand.Hold(new UnitId(2))
            }));

            Assert.AreEqual(
                BattleVector2.Zero,
                simulation.World.PositionComponents.Get(
                    Entity(simulation, new UnitId(1))).Position);
            Assert.AreEqual(
                BattleVector2.Zero,
                simulation.World.PositionComponents.Get(
                    Entity(simulation, new UnitId(2))).Position);
            Assert.AreEqual(
                0,
                CountEvents(simulation, BattleEventType.UnitMoved));
            Assert.AreEqual(
                0,
                simulation.MovementSystemScratch.LastSolveStats
                    .CandidateEvaluationCount);
        }

        [Test]
        public void MoveToPosition_SoftlySeparatesFriendlyUnitsWithoutExceedingMoveBudget()
        {
            var mover = TestCombatants.Create(
                "mover", maxHealth: 20, moveSpeed: 10f, attackRange: 1f,
                attackDamage: 0, attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), mover, BattleVector2.Zero),
                    new InitialCombatantSpawn(new TeamId(1), mover, BattleVector2.Zero)
                },
                default,
                automaticVictoryEnabled: false,
                localAvoidanceEnabled: true,
                BattleSpatialMapDefinition.Empty));
            BattleInputFrame input = new BattleInputFrame(new[]
            {
                BattleInputCommand.MoveToPosition(new UnitId(1), new BattleVector2(5f, 0f)),
                BattleInputCommand.MoveToPosition(new UnitId(2), new BattleVector2(5f, 0f))
            });

            simulation.Step(input);

            BattleVector2 first = FindMove(simulation, new UnitId(1)).Position;
            BattleVector2 second = FindMove(simulation, new UnitId(2)).Position;
            Assert.That(BattleVector2.DistanceScalar(BattleVector2.Zero, first),
                Is.LessThanOrEqualTo(BattleScalar.One));
            Assert.That(BattleVector2.DistanceScalar(BattleVector2.Zero, second),
                Is.LessThanOrEqualTo(BattleScalar.One));
        }

        [Test]
        public void FriendlyAvoidance_DoesNotPushFinalPositionIntoEnemyCircle()
        {
            var mover = TestCombatants.Create(
                "mover", maxHealth: 20, moveSpeed: 10f, attackRange: 1f,
                attackDamage: 0, attackCooldownTicks: 2);
            var blocker = TestCombatants.Create(
                "blocker", maxHealth: 20, moveSpeed: 0f, attackRange: 1f,
                attackDamage: 0, attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), mover, BattleVector2.Zero),
                    new InitialCombatantSpawn(new TeamId(1), blocker, new BattleVector2(0.7f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), blocker, new BattleVector2(1f, -0.4f))
                },
                default,
                automaticVictoryEnabled: false,
                localAvoidanceEnabled: true,
                BattleSpatialMapDefinition.Empty));

            simulation.Step(new BattleInputFrame(new[]
            {
                BattleInputCommand.MoveToPosition(new UnitId(1), new BattleVector2(5f, 0f)),
                BattleInputCommand.Hold(new UnitId(2)),
                BattleInputCommand.Hold(new UnitId(3))
            }));

            PositionComponent moverPosition = simulation.World.PositionComponents.Get(
                Entity(simulation, new UnitId(1)));
            PositionComponent enemyPosition = simulation.World.PositionComponents.Get(
                Entity(simulation, new UnitId(3)));
            Assert.That(BattleVector2.DistanceScalar(
                    moverPosition.Position,
                    enemyPosition.Position),
                Is.GreaterThanOrEqualTo(moverPosition.Radius + enemyPosition.Radius));
        }

        [Test]
        public void ExistingEnemyOverlap_IsRecoveredEvenWhenBothUnitsHold()
        {
            var unit = TestCombatants.Create(
                "unit", maxHealth: 20, moveSpeed: 0f, attackRange: 1f,
                attackDamage: 0, attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), unit, BattleVector2.Zero),
                    new InitialCombatantSpawn(new TeamId(2), unit, BattleVector2.Zero)
                },
                default,
                automaticVictoryEnabled: false,
                localAvoidanceEnabled: true,
                BattleSpatialMapDefinition.Empty));

            simulation.Step(new BattleInputFrame(new[]
            {
                BattleInputCommand.Hold(new UnitId(1)),
                BattleInputCommand.Hold(new UnitId(2))
            }));

            PositionComponent first = simulation.World.PositionComponents.Get(
                Entity(simulation, new UnitId(1)));
            PositionComponent second = simulation.World.PositionComponents.Get(
                Entity(simulation, new UnitId(2)));
            Assert.That(BattleVector2.DistanceScalar(first.Position, second.Position),
                Is.GreaterThanOrEqualTo(first.Radius + second.Radius));
            Assert.That(CountEvents(simulation, BattleEventType.UnitMoved), Is.EqualTo(1));
        }

        [Test]
        public void ExistingEnemyOverlapRecovery_ProducesLegalEnemyPairsInStableUnitOrder()
        {
            var unit = TestCombatants.Create(
                "unit", maxHealth: 20, moveSpeed: 0f, attackRange: 1f,
                attackDamage: 0, attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), unit, BattleVector2.Zero),
                    new InitialCombatantSpawn(new TeamId(2), unit, BattleVector2.Zero),
                    new InitialCombatantSpawn(new TeamId(1), unit, new BattleVector2(0.1f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), unit, new BattleVector2(-0.1f, 0f))
                },
                default,
                automaticVictoryEnabled: false,
                localAvoidanceEnabled: true,
                BattleSpatialMapDefinition.Empty));

            simulation.Step(new BattleInputFrame(new[]
            {
                BattleInputCommand.Hold(new UnitId(1)),
                BattleInputCommand.Hold(new UnitId(2)),
                BattleInputCommand.Hold(new UnitId(3)),
                BattleInputCommand.Hold(new UnitId(4))
            }));

            for (var firstId = 1; firstId <= 4; firstId++)
            {
                for (var secondId = firstId + 1; secondId <= 4; secondId++)
                {
                    if ((firstId & 1) == (secondId & 1))
                    {
                        continue;
                    }

                    PositionComponent first = simulation.World.PositionComponents.Get(
                        Entity(simulation, new UnitId(firstId)));
                    PositionComponent second = simulation.World.PositionComponents.Get(
                        Entity(simulation, new UnitId(secondId)));
                    Assert.That(BattleVector2.DistanceScalar(first.Position, second.Position),
                        Is.GreaterThanOrEqualTo(first.Radius + second.Radius),
                        $"Illegal enemy overlap between UnitId {firstId} and {secondId}.");
                }
            }
        }

        [Test]
        public void Step_StopsAtReadySkillAbilityRange()
        {
            var attacker = TestCombatants.Create(
                "attacker",
                maxHealth: 20,
                moveSpeed: 100f,
                attackRange: 1f,
                attackDamage: 0,
                attackCooldownTicks: 2,
                abilities: new[] { TestCombatants.Ability("long-shot", 4f, 0, 1) });
            var defender = TestCombatants.Create("defender", maxHealth: 20, moveSpeed: 0f, attackRange: 1f, attackDamage: 0, attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), attacker, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), defender, new BattleVector2(10f, 0f))
                }));

            simulation.Step(BattleInputFrame.Empty);

            PositionComponent attackerPosition = simulation.World.PositionComponents.Get(
                Entity(simulation, new UnitId(1)));
            PositionComponent defenderPosition = simulation.World.PositionComponents.Get(
                Entity(simulation, new UnitId(2)));
            Assert.LessOrEqual(
                BattleVector2.DistanceScalar(
                    attackerPosition.Position,
                    defenderPosition.Position).RawValue,
                BattleScalar.FromInt(4).RawValue);
            Assert.GreaterOrEqual(
                BattleVector2.DistanceScalar(
                    attackerPosition.Position,
                    defenderPosition.Position).RawValue,
                (attackerPosition.Radius + defenderPosition.Radius).RawValue);
        }

        [Test]
        public void MovementSystem_UsesBasicAbilityRangeWhenSkillAbilityIsCooling()
        {
            var world = new BattleWorld();
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();
            Spawn(
                world,
                new UnitId(1),
                new TeamId(1),
                new BattleVector2(0f, 0f),
                moveSpeed: 100f,
                basicRange: 1f,
                abilities: new[] { TestCombatants.AbilitySpawn("long-shot", 4f, 0, 2) });
            Spawn(world, new UnitId(2), new TeamId(2), new BattleVector2(5f, 0f), moveSpeed: 0f, basicRange: 1f);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.AbilityComponents.Set(source, world.AbilityComponents.Get(source).WithAbilityCooldownRemainingTicks(1, 1));

            TargetingSystem.Run(world);
            MovementSystem.Run(
                world,
                BattleScalar.FromFloat(0.1f),
                events,
                sequence,
                new BattleTick(1),
                new MovementSystem.Scratch());

            BattleEvent movement = FindMove(events.AsStream(), new UnitId(1));
            Assert.AreEqual(new UnitId(1), movement.UnitId);
            PositionComponent sourcePosition = world.PositionComponents.Get(source);
            PositionComponent targetPosition = world.PositionComponents.Get(
                Entity(world, new UnitId(2)));
            Assert.Less(
                BattleVector2.DistanceScalar(
                    sourcePosition.Position,
                    targetPosition.Position).RawValue,
                BattleScalar.FromInt(4).RawValue);
        }

        [Test]
        public void MovementSystem_UsesEffectiveMoveSpeedFromStatusModifiers()
        {
            var world = new BattleWorld();
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();
            Spawn(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f), moveSpeed: 2f, basicRange: 1f);
            Spawn(world, new UnitId(2), new TeamId(2), new BattleVector2(10f, 0f), moveSpeed: 0f, basicRange: 1f);
            Assert.IsTrue(world.TryFindEntity(new UnitId(1), out EntityId source));
            Assert.IsTrue(world.TryFindEntity(new UnitId(2), out EntityId target));
            world.TargetComponents.Set(source, new TargetComponent(target));
            world.StatusComponents.Set(source, new StatusComponent(new[]
            {
                new StatusInstance(
                    "haste",
                    StatusPolarity.Buff,
                    source,
                    durationRemainingTicks: 3,
                    tickIntervalTicks: 1,
                    ticksUntilNextPeriodicEffect: 1,
                    periodicDamage: 0,
                    modifiers: new[]
                    {
                        BattleModifierInstance.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.Flat, BattleScalar.FromFloat(1f)),
                        BattleModifierInstance.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.PercentAdd, BattleScalar.FromFloat(0.5f))
                    },
                    triggers: Array.Empty<BattleTriggerInstance>())
            }));

            MovementSystem.Run(
                world,
                ticksPerSecond: 2,
                events,
                sequence,
                new BattleTick(1),
                new MovementSystem.Scratch());

            Assert.AreEqual(new BattleVector2(2.25f, 0f), events.AsStream()[0].Position);
        }

        [Test]
        public void Step_DoesNotMoveUnitWhileAbilityActionIsActive()
        {
            var attacker = TestCombatants.Create(
                "attacker",
                maxHealth: 20,
                moveSpeed: 10f,
                attackRange: 5f,
                attackDamage: 3,
                attackCooldownTicks: 2,
                abilities: new[] { TestCombatants.Ability("long-cast", 5f, 3, 2, windupTicks: 2, recoveryTicks: 2) });
            var defender = TestCombatants.Create("defender", maxHealth: 20, moveSpeed: 0f, attackRange: 1f, attackDamage: 0, attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), attacker, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), defender, new BattleVector2(3f, 0f))
                }));

            simulation.Step(BattleInputFrame.Empty);
            Assert.IsTrue(simulation.World.TryFindEntity(new UnitId(1), out EntityId source));
            Assert.IsTrue(simulation.World.TryFindEntity(new UnitId(2), out EntityId target));
            simulation.World.PositionComponents.Set(target, new PositionComponent(new BattleVector2(9f, 0f), BattleScalar.FromFloat(0.25f)));

            simulation.Step(BattleInputFrame.Empty);

            Assert.IsFalse(HasMove(simulation, new UnitId(1)));
            Assert.AreEqual(new BattleVector2(0f, 0f), simulation.World.PositionComponents.Get(source).Position);
        }

        [Test]
        public void MovementSystem_MovesWithoutTurningWhenFacingIsLocked()
        {
            var world = new BattleWorld();
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();
            Spawn(world, new UnitId(1), new TeamId(1), new BattleVector2(5f, 0f), moveSpeed: 10f, basicRange: 1f);
            Spawn(world, new UnitId(2), new TeamId(2), new BattleVector2(0f, 0f), moveSpeed: 0f, basicRange: 1f);
            Assert.IsTrue(world.TryFindEntity(new UnitId(1), out EntityId source));
            Assert.IsTrue(world.TryFindEntity(new UnitId(2), out EntityId target));
            world.TargetComponents.Set(source, new TargetComponent(target));
            world.UnitActionComponents.Set(
                source,
                UnitActionComponent.Ability(
                    0,
                    "basic-attack",
                    target,
                    new BattleTick(1),
                    new BattleTick(2),
                    new BattleTick(3),
                    BattleActionLocks.Facing));

            MovementSystem.Run(
                world,
                BattleScalar.FromFloat(0.1f),
                events,
                sequence,
                new BattleTick(1),
                new MovementSystem.Scratch());

            Assert.AreEqual(new BattleVector2(1f, 0f), world.FacingComponents.Get(source).Direction);
            Assert.AreEqual(1, CountEvents(events.AsStream(), BattleEventType.UnitMoved));
            Assert.AreEqual(0, CountEvents(events.AsStream(), BattleEventType.UnitFacingChanged));
        }

        [Test]
        public void Step_MovementUsesSharedSnapshotWithinSameTick()
        {
            var fast = TestCombatants.Create("fast", maxHealth: 20, moveSpeed: 100f, attackRange: 1f, attackDamage: 0, attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), fast, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), fast, new BattleVector2(5f, 0f))
                },
                default,
                automaticVictoryEnabled: true,
                localAvoidanceEnabled: true,
                BattleSpatialMapDefinition.Empty));

            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(2, CountEvents(simulation, BattleEventType.UnitMoved));
            AssertEnemyPairIsHardSafe(simulation, new UnitId(1), new UnitId(2));
        }

        [Test]
        public void Step_PermutedSpawnStorageWithStableUnitIdsProducesSameMovementFacts()
        {
            BattleWorld firstWorld = PermutedMovementWorld(new[] { 1, 2 });
            BattleWorld secondWorld = PermutedMovementWorld(new[] { 2, 1 });
            var firstEvents = new EventBuffer<BattleEvent>();
            var secondEvents = new EventBuffer<BattleEvent>();

            MovementSystem.Run(
                firstWorld,
                ticksPerSecond: 10,
                firstEvents,
                new EventSequence(),
                new BattleTick(1),
                new MovementSystem.Scratch());
            MovementSystem.Run(
                secondWorld,
                ticksPerSecond: 10,
                secondEvents,
                new EventSequence(),
                new BattleTick(1),
                new MovementSystem.Scratch());

            AssertMovementFactsEqual(firstEvents.AsStream(), secondEvents.AsStream());
        }

        [Test]
        public void Step_MeleeUnitsFanAroundSharedTargetInOpenSpace()
        {
            var melee = TestCombatants.Create(
                "melee",
                maxHealth: 20,
                moveSpeed: 10f,
                attackRange: 0.5f,
                attackDamage: 0,
                attackCooldownTicks: 2);
            var target = TestCombatants.Create(
                "target",
                maxHealth: 20,
                moveSpeed: 0f,
                attackRange: 0f,
                attackDamage: 0,
                attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), melee, new BattleVector2(0f, -0.25f)),
                    new InitialCombatantSpawn(new TeamId(1), melee, BattleVector2.Zero),
                    new InitialCombatantSpawn(new TeamId(1), melee, new BattleVector2(0f, 0.25f)),
                    new InitialCombatantSpawn(new TeamId(2), target, new BattleVector2(5f, 0f))
                },
                default,
                automaticVictoryEnabled: false,
                localAvoidanceEnabled: true,
                BattleSpatialMapDefinition.Empty));

            simulation.Step(BattleInputFrame.Empty);

            BattleVector2 first = FindMove(simulation, new UnitId(1)).Position;
            BattleVector2 second = FindMove(simulation, new UnitId(2)).Position;
            BattleVector2 third = FindMove(simulation, new UnitId(3)).Position;
            Assert.Greater(first.XRaw, 0L);
            Assert.Greater(second.XRaw, 0L);
            Assert.Greater(third.XRaw, 0L);
            Assert.Less(first.YRaw, new BattleVector2(0f, -0.25f).YRaw);
            Assert.Greater(third.YRaw, new BattleVector2(0f, 0.25f).YRaw);
        }

        [Test]
        public void Step_ActionLockedFrontLineRemainsAnchoredWhileRearMovesAroundIt()
        {
            var mover = TestCombatants.Create(
                "mover",
                maxHealth: 20,
                moveSpeed: 10f,
                attackRange: 1f,
                attackDamage: 0,
                attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), mover, BattleVector2.Zero),
                    new InitialCombatantSpawn(new TeamId(1), mover, new BattleVector2(0.5f, 0f))
                },
                default,
                automaticVictoryEnabled: false,
                localAvoidanceEnabled: true,
                BattleSpatialMapDefinition.Empty));
            EntityId rear = Entity(simulation, new UnitId(1));
            EntityId front = Entity(simulation, new UnitId(2));
            simulation.World.IntentComponents.Set(
                rear,
                new IntentComponent(BattleIntent.MoveToPosition(rear, new BattleVector2(5f, 0f))));
            simulation.World.IntentComponents.Set(
                front,
                new IntentComponent(BattleIntent.MoveToPosition(front, new BattleVector2(5f, 0f))));
            simulation.World.UnitActionComponents.Set(
                front,
                UnitActionComponent.Ability(
                    0,
                    "basic-attack",
                    rear,
                    new BattleTick(0),
                    new BattleTick(100),
                    new BattleTick(101),
                    BattleActionLocks.Movement));

            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(
                new BattleVector2(0.5f, 0f),
                simulation.World.PositionComponents.Get(front).Position);
            BattleVector2 rearPosition = simulation.World.PositionComponents.Get(rear).Position;
            Assert.Greater(rearPosition.XRaw, 0L);
            Assert.AreNotEqual(0L, rearPosition.YRaw);
            Assert.Greater(
                simulation.MovementSystemScratch.LastSolveStats.CandidateEvaluationCount,
                0);
            Assert.AreEqual(1, simulation.MovementSystemScratch.LastAnchoredAgentCount);
        }

        [Test]
        public void Step_GarrisonedAndDeadUnitsAreAbsentFromAvoidanceFrame()
        {
            var world = new BattleWorld();
            Spawn(world, new UnitId(1), new TeamId(1), Position(0), moveSpeed: 10f, basicRange: 1f);
            Spawn(world, new UnitId(2), new TeamId(1), Position(3), moveSpeed: 0f, basicRange: 1f);
            Spawn(world, new UnitId(3), new TeamId(1), Position(6), moveSpeed: 0f, basicRange: 1f);
            Spawn(world, new UnitId(4), new TeamId(1), Position(9), moveSpeed: 0f, basicRange: 1f);
            Assert.IsTrue(world.TryFindEntity(new UnitId(1), out EntityId moving));
            Assert.IsTrue(world.TryFindEntity(new UnitId(3), out EntityId garrisoned));
            Assert.IsTrue(world.TryFindEntity(new UnitId(4), out EntityId dead));
            world.IntentComponents.Set(
                moving,
                new IntentComponent(BattleIntent.MoveToPosition(moving, Position(2))));
            world.GarrisonedComponents.Set(garrisoned, default);
            world.LifeStateComponents.Set(dead, new LifeStateComponent(LifeState.Dead));
            var scratch = new MovementSystem.Scratch();

            MovementSystem.Run(
                world,
                ticksPerSecond: 10,
                new EventBuffer<BattleEvent>(),
                new EventSequence(),
                new BattleTick(1),
                scratch);

            Assert.AreEqual(2, scratch.Avoidance.AgentCount);
            Assert.AreEqual(1, scratch.LastAnchoredAgentCount);
            Assert.AreEqual(1, scratch.Agents[0].AgentId);
            Assert.AreEqual(2, scratch.Agents[1].AgentId);
        }

        [Test]
        public void MovementSystem_OneMovingAgentAmong127AnchorsOnlyEvaluatesMoverCandidates()
        {
            const int agentCount = 128;
            const int width = 16;
            const int spacing = 20;
            var world = new BattleWorld();
            Spawn(
                world,
                new UnitId(1),
                new TeamId(1),
                BattleVector2.Zero,
                moveSpeed: 10f,
                basicRange: 1f);
            Spawn(
                world,
                new UnitId(2),
                new TeamId(1),
                Position(1),
                moveSpeed: 0f,
                basicRange: 1f);
            for (var id = 3; id <= agentCount; id++)
            {
                int index = id - 3;
                Spawn(
                    world,
                    new UnitId(id),
                    new TeamId(1),
                    new BattleVector2(
                        BattleScalar.FromInt(40 + ((index % width) * spacing)),
                        BattleScalar.FromInt((index / width) * spacing)),
                    moveSpeed: 0f,
                    basicRange: 1f);
            }

            Assert.IsTrue(world.TryFindEntity(new UnitId(1), out EntityId mover));
            world.IntentComponents.Set(
                mover,
                new IntentComponent(BattleIntent.MoveToPosition(mover, Position(10))));
            var scratch = new MovementSystem.Scratch();

            MovementSystem.Run(
                world,
                ticksPerSecond: 10,
                new EventBuffer<BattleEvent>(),
                new EventSequence(),
                new BattleTick(1),
                scratch);

            Assert.AreEqual(agentCount, scratch.Avoidance.AgentCount);
            Assert.AreEqual(agentCount - 1, scratch.LastAnchoredAgentCount);
            Assert.AreEqual(
                LocalAvoidanceCandidateSet.Count,
                scratch.LastSolveStats.CandidateEvaluationCount);
            Assert.AreEqual(3, scratch.LastSolveStats.ActiveQueryCount);
            Assert.AreEqual(3, scratch.LastSolveStats.NeighborCheckCount);
            Assert.AreEqual(6, scratch.LastSolveStats.BroadphaseCandidateCount);
        }

        [Test]
        public void Step_MoveBudgetMatchesLegacyTickRateCalculationWithoutNeighbors()
        {
            var world = new BattleWorld();
            Spawn(world, new UnitId(1), new TeamId(1), BattleVector2.Zero, moveSpeed: 7f, basicRange: 1f);
            Assert.IsTrue(world.TryFindEntity(new UnitId(1), out EntityId mover));
            world.IntentComponents.Set(
                mover,
                new IntentComponent(BattleIntent.MoveToPosition(mover, Position(20))));
            var events = new EventBuffer<BattleEvent>();

            MovementSystem.Run(
                world,
                ticksPerSecond: 3,
                events,
                new EventSequence(),
                new BattleTick(1),
                new MovementSystem.Scratch());

            BattleScalar expectedDistance =
                BattleScalar.FromInt(7) / BattleScalar.FromInt(3);
            Assert.AreEqual(
                expectedDistance.RawValue,
                events.AsStream()[0].Position.XRaw);
            Assert.AreEqual(0L, events.AsStream()[0].Position.YRaw);
        }

        [Test]
        public void MoveToPosition_NearDestinationDoesNotOvershootWhenNeighborIsQueried()
        {
            var world = new BattleWorld();
            Spawn(world, new UnitId(1), new TeamId(1), BattleVector2.Zero, moveSpeed: 10f, basicRange: 1f);
            Spawn(world, new UnitId(2), new TeamId(1), Position(2), moveSpeed: 0f, basicRange: 1f);
            Assert.IsTrue(world.TryFindEntity(new UnitId(1), out EntityId mover));
            BattleVector2 destination = new BattleVector2(
                BattleScalar.One / BattleScalar.FromInt(5),
                BattleScalar.Zero);
            world.IntentComponents.Set(
                mover,
                new IntentComponent(BattleIntent.MoveToPosition(mover, destination)));
            var events = new EventBuffer<BattleEvent>();

            MovementSystem.Run(
                world,
                ticksPerSecond: 10,
                events,
                new EventSequence(),
                new BattleTick(1),
                new MovementSystem.Scratch());

            Assert.LessOrEqual(events.AsStream()[0].Position.XRaw, destination.XRaw);
        }

        [Test]
        public void MovementSystem_RebuildsPreferredStepAfterOverlapRecovery()
        {
            var world = new BattleWorld();
            Spawn(world, new UnitId(1), new TeamId(1), BattleVector2.Zero, moveSpeed: 0f, basicRange: 1f);
            Spawn(world, new UnitId(2), new TeamId(2), BattleVector2.Zero, moveSpeed: 10f, basicRange: 1f);
            Assert.IsTrue(world.TryFindEntity(new UnitId(2), out EntityId mover));
            world.IntentComponents.Set(
                mover,
                new IntentComponent(BattleIntent.MoveToPosition(mover, BattleVector2.Zero)));
            var scratch = new MovementSystem.Scratch();

            MovementSystem.Run(
                world,
                ticksPerSecond: 10,
                new EventBuffer<BattleEvent>(),
                new EventSequence(),
                new BattleTick(1),
                scratch);

            Assert.AreEqual(LocalAvoidanceMobility.Moving, scratch.Agents[1].Mobility);
            Assert.Less(scratch.Agents[1].PreferredStep.XRaw, 0L);
        }

        [Test]
        public void Step_EntersEngagementRangeWhenRemainingDistanceEqualsTickBudget()
        {
            var attacker = TestCombatants.Create(
                "attacker",
                maxHealth: 20,
                moveSpeed: 10f,
                attackRange: 1f,
                attackDamage: 0,
                attackCooldownTicks: 2);
            var defender = TestCombatants.Create(
                "defender",
                maxHealth: 20,
                moveSpeed: 0f,
                attackRange: 1f,
                attackDamage: 0,
                attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), attacker, BattleVector2.Zero),
                    new InitialCombatantSpawn(new TeamId(2), defender, Position(2))
                },
                default,
                automaticVictoryEnabled: false));

            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(Position(1), FindMove(simulation, new UnitId(1)).Position);
        }

        [Test]
        public void Step_KeepsExistingValidEnemyTargetEvenWhenAnotherEnemyBecomesCloser()
        {
            var stationary = TestCombatants.Create("stationary", maxHealth: 20, moveSpeed: 0f, attackRange: 1f, attackDamage: 5, attackCooldownTicks: 2);
            var harmlessFast = TestCombatants.Create("harmlessFast", maxHealth: 20, moveSpeed: 100f, attackRange: 1f, attackDamage: 0, attackCooldownTicks: 2);
            var simulation = new BattleSimulation(new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: 100,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), stationary, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), stationary, new BattleVector2(5f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), harmlessFast, new BattleVector2(10f, 0f))
                }));

            simulation.Step(BattleInputFrame.Empty);
            Assert.IsTrue(HasMove(simulation, new UnitId(3)));

            simulation.Step(BattleInputFrame.Empty);

            Assert.IsFalse(HasDamage(simulation, new UnitId(1), new UnitId(3)));
        }

        [Test]
        public void Step_DoesNotAdvanceBeyondMaxTicks()
        {
            var simulation = new BattleSimulation(StalemateConfig(maxTicks: 2));

            simulation.Step(BattleInputFrame.Empty);
            simulation.Step(BattleInputFrame.Empty);
            Assert.AreEqual(2, simulation.CurrentTick.Value);

            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(2, simulation.CurrentTick.Value);
            Assert.AreEqual(0, simulation.Events.Count);
            Assert.IsFalse(simulation.IsFinished);
        }

        private static BattleEvent FindDamageFrom(BattleSimulation simulation, UnitId sourceUnitId)
        {
            for (var i = 0; i < simulation.Events.Count; i++)
            {
                BattleEvent battleEvent = simulation.Events[i];
                if (battleEvent.Type == BattleEventType.DamageApplied && battleEvent.UnitId.Equals(sourceUnitId))
                {
                    return battleEvent;
                }
            }

            Assert.Fail($"No damage event from {sourceUnitId}.");
            return default;
        }

        private static BattleEvent FindMove(BattleSimulation simulation, UnitId unitId)
        {
            for (var i = 0; i < simulation.Events.Count; i++)
            {
                BattleEvent battleEvent = simulation.Events[i];
                if (battleEvent.Type == BattleEventType.UnitMoved && battleEvent.UnitId.Equals(unitId))
                {
                    return battleEvent;
                }
            }

            Assert.Fail($"No move event for {unitId}.");
            return default;
        }

        private static BattleEvent FindMove(
            EventStream<BattleEvent> events,
            UnitId unitId)
        {
            for (int i = 0; i < events.Count; i++)
            {
                BattleEvent battleEvent = events[i];
                if (battleEvent.Type == BattleEventType.UnitMoved
                    && battleEvent.UnitId.Equals(unitId))
                {
                    return battleEvent;
                }
            }

            Assert.Fail($"No move event for {unitId}.");
            return default;
        }

        private static EntityId Entity(BattleSimulation simulation, UnitId unitId)
        {
            Assert.That(simulation.World.TryFindEntity(unitId, out EntityId entity), Is.True);
            return entity;
        }

        private static EntityId Entity(BattleWorld world, UnitId unitId)
        {
            Assert.That(world.TryFindEntity(unitId, out EntityId entity), Is.True);
            return entity;
        }

        private static void AssertEnemyPairIsHardSafe(
            BattleSimulation simulation,
            UnitId firstUnitId,
            UnitId secondUnitId)
        {
            PositionComponent first = simulation.World.PositionComponents.Get(
                Entity(simulation, firstUnitId));
            PositionComponent second = simulation.World.PositionComponents.Get(
                Entity(simulation, secondUnitId));
            BattleScalar combinedRadius = first.Radius + second.Radius;
            Assert.GreaterOrEqual(
                BattleVector2.SqrDistanceScalar(
                    first.Position,
                    second.Position).RawValue,
                (combinedRadius * combinedRadius).RawValue);
        }

        private static bool HasMove(BattleSimulation simulation, UnitId unitId)
        {
            for (var i = 0; i < simulation.Events.Count; i++)
            {
                BattleEvent battleEvent = simulation.Events[i];
                if (battleEvent.Type == BattleEventType.UnitMoved && battleEvent.UnitId.Equals(unitId))
                {
                    return true;
                }
            }

            return false;
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

        private static BattleConfig StalemateConfig(int maxTicks)
        {
            var unit = TestCombatants.Create("stalemate", maxHealth: 20, moveSpeed: 0f, attackRange: 1f, attackDamage: 0, attackCooldownTicks: 1);

            return new BattleConfig(
                ticksPerSecond: 10,
                maxTicks: maxTicks,
                initialSpawns: new[]
                {
                    new InitialCombatantSpawn(new TeamId(1), unit, new BattleVector2(0f, 0f)),
                    new InitialCombatantSpawn(new TeamId(2), unit, new BattleVector2(10f, 0f))
                });
        }

        private static BattleWorld PermutedMovementWorld(int[] spawnOrder)
        {
            var world = new BattleWorld();
            for (var i = 0; i < spawnOrder.Length; i++)
            {
                int id = spawnOrder[i];
                Spawn(
                    world,
                    new UnitId(id),
                    new TeamId(id),
                    id == 1 ? BattleVector2.Zero : new BattleVector2(5f, 0f),
                    moveSpeed: 100f,
                    basicRange: 1f);
            }

            Assert.IsTrue(world.TryFindEntity(new UnitId(1), out EntityId first));
            Assert.IsTrue(world.TryFindEntity(new UnitId(2), out EntityId second));
            world.TargetComponents.Set(first, new TargetComponent(second));
            world.TargetComponents.Set(second, new TargetComponent(first));
            return world;
        }

        private static void AssertMovementFactsEqual(
            EventStream<BattleEvent> first,
            EventStream<BattleEvent> second)
        {
            int firstIndex = NextMoveIndex(first, 0);
            int secondIndex = NextMoveIndex(second, 0);
            int movementCount = 0;
            int previousUnitId = 0;
            while (firstIndex < first.Count && secondIndex < second.Count)
            {
                BattleEvent firstMove = first[firstIndex];
                BattleEvent secondMove = second[secondIndex];
                Assert.Greater(firstMove.UnitId.Value, previousUnitId);
                Assert.AreEqual(firstMove.UnitId, secondMove.UnitId);
                Assert.AreEqual(firstMove.Position.XRaw, secondMove.Position.XRaw);
                Assert.AreEqual(firstMove.Position.YRaw, secondMove.Position.YRaw);
                previousUnitId = firstMove.UnitId.Value;
                movementCount++;
                firstIndex = NextMoveIndex(first, firstIndex + 1);
                secondIndex = NextMoveIndex(second, secondIndex + 1);
            }

            Assert.AreEqual(first.Count, firstIndex);
            Assert.AreEqual(second.Count, secondIndex);
            Assert.Greater(movementCount, 0);
        }

        private static int NextMoveIndex(EventStream<BattleEvent> events, int startIndex)
        {
            for (int i = startIndex; i < events.Count; i++)
            {
                if (events[i].Type == BattleEventType.UnitMoved)
                {
                    return i;
                }
            }

            return events.Count;
        }

        private static BattleVector2 Position(int x)
        {
            return new BattleVector2(BattleScalar.FromInt(x), BattleScalar.Zero);
        }

        private static void Spawn(
            BattleWorld world,
            UnitId unitId,
            TeamId teamId,
            BattleVector2 position,
            float moveSpeed,
            float basicRange,
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
                    moveSpeed: BattleScalar.FromFloat(moveSpeed),
                    basicAbility: TestCombatants.AbilitySpawn("basic-attack", basicRange, 0, 2),
                    abilities: abilities ?? new AbilitySpawnData[0])));
            world.FlushSpawnCombatantCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(0));
        }
    }
}
