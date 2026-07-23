using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using Combat.Foundation.Events;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleConditionProgramEvaluatorTests
    {
        [Test]
        public void Evaluate_HealthPercentCompare_UsesBattleScalarPercent()
        {
            var world = new BattleWorld();
            EntityId unit = SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10);
            world.HealthComponents.Set(unit, new HealthComponent(current: 2));
            BattleConditionProgram program = BattleConditionCompiler.Compile(new BattleConditionGroup(
                BattleConditionMatchMode.All,
                new[]
                {
                    BattleConditionDefinition.Compare(
                        BattleConditionOperandDefinition.HealthPercent(BattleConditionSubject.Source),
                        BattleConditionComparison.LessOrEqual,
                        BattleConditionOperandDefinition.LiteralPercentBasisPoints(2000))
                }));
            var context = new BattleConditionEvaluationContext(
                world,
                new BattleTick(1),
                owner: unit,
                source: unit,
                target: unit,
                BattleEffectContext.Unknown());

            bool result = BattleConditionProgramEvaluator.Evaluate(program, context);

            Assert.IsTrue(result);
        }

        [Test]
        public void Evaluate_HealthPercentCompare_UsesEffectiveMaxHealth()
        {
            var world = new BattleWorld();
            EntityId unit = SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10);
            world.HealthComponents.Set(unit, new HealthComponent(current: 10));
            world.StatusComponents.Set(unit, new StatusComponent(new[]
            {
                Status(
                    "fortitude",
                    StatusPolarity.Buff,
                    unit,
                    BattleModifierInstance.Stat(BattleStatId.MaxHealth, BattleModifierOperation.Flat, BattleScalar.FromInt(10)))
            }));
            BattleConditionProgram program = BattleConditionCompiler.Compile(new BattleConditionGroup(
                BattleConditionMatchMode.All,
                new[]
                {
                    BattleConditionDefinition.Compare(
                        BattleConditionOperandDefinition.HealthPercent(BattleConditionSubject.Source),
                        BattleConditionComparison.Equal,
                        BattleConditionOperandDefinition.LiteralPercentBasisPoints(5000))
                }));
            var context = new BattleConditionEvaluationContext(
                world,
                new BattleTick(1),
                owner: unit,
                source: unit,
                target: unit,
                BattleEffectContext.Unknown());

            bool result = BattleConditionProgramEvaluator.Evaluate(program, context);

            Assert.IsTrue(result);
        }

        [Test]
        public void Evaluate_StatusCountCompare_UsesCompiledStatusFilter()
        {
            var world = new BattleWorld();
            EntityId source = SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10);
            EntityId target = SpawnCombatant(world, new UnitId(2), new TeamId(2), maxHealth: 10);
            world.StatusComponents.Set(target, new StatusComponent(new[]
            {
                Status("poison", StatusPolarity.Debuff, source)
            }));
            BattleConditionProgram program = BattleConditionCompiler.Compile(new BattleConditionGroup(
                BattleConditionMatchMode.All,
                new[]
                {
                    BattleConditionDefinition.Compare(
                        BattleConditionOperandDefinition.StatusCount(
                            BattleConditionSubject.Target,
                            BattleStatusConditionFilterDefinition.Polarity(StatusPolarity.Debuff)),
                        BattleConditionComparison.GreaterOrEqual,
                        BattleConditionOperandDefinition.LiteralInt(1))
                }));
            var context = new BattleConditionEvaluationContext(
                world,
                new BattleTick(1),
                owner: source,
                source: source,
                target: target,
                BattleEffectContext.Unknown());

            bool result = BattleConditionProgramEvaluator.Evaluate(program, context);

            Assert.IsTrue(result);
        }

        [Test]
        public void Evaluate_StatusStackCountCompare_UsesMatchedStatusStacks()
        {
            var world = new BattleWorld();
            EntityId source = SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10);
            EntityId target = SpawnCombatant(world, new UnitId(2), new TeamId(2), maxHealth: 10);
            world.StatusComponents.Set(target, new StatusComponent(new[]
            {
                Status("poison", StatusPolarity.Debuff, source, stackCount: 3)
            }));
            BattleConditionProgram program = BattleConditionCompiler.Compile(new BattleConditionGroup(
                BattleConditionMatchMode.All,
                new[]
                {
                    BattleConditionDefinition.Compare(
                        BattleConditionOperandDefinition.StatusStackCount(
                            BattleConditionSubject.Target,
                            BattleStatusConditionFilterDefinition.StatusId("poison")),
                        BattleConditionComparison.GreaterOrEqual,
                        BattleConditionOperandDefinition.LiteralInt(3))
                }));
            var context = new BattleConditionEvaluationContext(
                world,
                new BattleTick(1),
                owner: source,
                source: source,
                target: target,
                BattleEffectContext.Unknown());

            bool result = BattleConditionProgramEvaluator.Evaluate(program, context);

            Assert.IsTrue(result);
        }

        [Test]
        public void Evaluate_StatValueCompare_ReadsRuntimeStatComponents()
        {
            var world = new BattleWorld();
            EntityId source = SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10, moveSpeed: 2.5f);
            BattleConditionProgram program = BattleConditionCompiler.Compile(new BattleConditionGroup(
                BattleConditionMatchMode.All,
                new[]
                {
                    BattleConditionDefinition.Compare(
                        BattleConditionOperandDefinition.StatValue(BattleConditionSubject.Source, BattleStatId.MoveSpeed),
                        BattleConditionComparison.GreaterOrEqual,
                        BattleConditionOperandDefinition.LiteralScalar(BattleScalar.FromFloat(2.5f)))
                }));
            var context = new BattleConditionEvaluationContext(
                world,
                new BattleTick(1),
                owner: source,
                source: source,
                target: source,
                BattleEffectContext.Unknown());

            bool result = BattleConditionProgramEvaluator.Evaluate(program, context);

            Assert.IsTrue(result);
        }

        [Test]
        public void Evaluate_StatValueCompare_ReadsEffectiveMoveSpeed()
        {
            var world = new BattleWorld();
            EntityId source = SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10, moveSpeed: 2f);
            world.StatusComponents.Set(source, new StatusComponent(new[]
            {
                Status(
                    "slow",
                    StatusPolarity.Debuff,
                    source,
                    BattleModifierInstance.Stat(
                        BattleStatId.MoveSpeed,
                        BattleModifierOperation.PercentAdd,
                        BattleScalar.FromFloat(-0.5f)))
            }));
            BattleConditionProgram program = BattleConditionCompiler.Compile(new BattleConditionGroup(
                BattleConditionMatchMode.All,
                new[]
                {
                    BattleConditionDefinition.Compare(
                        BattleConditionOperandDefinition.StatValue(BattleConditionSubject.Source, BattleStatId.MoveSpeed),
                        BattleConditionComparison.Equal,
                        BattleConditionOperandDefinition.LiteralScalar(BattleScalar.FromFloat(1f)))
                }));
            var context = new BattleConditionEvaluationContext(
                world,
                new BattleTick(1),
                owner: source,
                source: source,
                target: source,
                BattleEffectContext.Unknown());

            bool result = BattleConditionProgramEvaluator.Evaluate(program, context);

            Assert.IsTrue(result);
            Assert.AreEqual(BattleScalar.FromFloat(2f), world.BaseStatsComponents.Get(source).Stats.RequireScalar(BattleStatId.MoveSpeed, "test"));
        }

        [Test]
        public void Evaluate_StatValueCompare_ReadsEffectiveMaxHealth()
        {
            var world = new BattleWorld();
            EntityId source = SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10, moveSpeed: 2f);
            world.StatusComponents.Set(source, new StatusComponent(new[]
            {
                Status(
                    "fortitude",
                    StatusPolarity.Buff,
                    source,
                    BattleModifierInstance.Stat(
                        BattleStatId.MaxHealth,
                        BattleModifierOperation.PercentAdd,
                        BattleScalar.FromFloat(0.5f)))
            }));
            BattleConditionProgram program = BattleConditionCompiler.Compile(new BattleConditionGroup(
                BattleConditionMatchMode.All,
                new[]
                {
                    BattleConditionDefinition.Compare(
                        BattleConditionOperandDefinition.StatValue(BattleConditionSubject.Source, BattleStatId.MaxHealth),
                        BattleConditionComparison.Equal,
                        BattleConditionOperandDefinition.LiteralScalar(BattleScalar.FromInt(15)))
                }));
            var context = new BattleConditionEvaluationContext(
                world,
                new BattleTick(1),
                owner: source,
                source: source,
                target: source,
                BattleEffectContext.Unknown());

            bool result = BattleConditionProgramEvaluator.Evaluate(program, context);

            Assert.IsTrue(result);
        }

        [Test]
        public void Evaluate_DistanceBetweenCompare_UsesBattleVectorDistance()
        {
            var world = new BattleWorld();
            EntityId source = SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10, position: new BattleVector2(0f, 0f));
            EntityId target = SpawnCombatant(world, new UnitId(2), new TeamId(2), maxHealth: 10, position: new BattleVector2(3f, 4f));
            BattleConditionProgram program = BattleConditionCompiler.Compile(new BattleConditionGroup(
                BattleConditionMatchMode.All,
                new[]
                {
                    BattleConditionDefinition.Compare(
                        BattleConditionOperandDefinition.DistanceBetween(BattleConditionSubject.Source, BattleConditionSubject.Target),
                        BattleConditionComparison.Equal,
                        BattleConditionOperandDefinition.LiteralScalar(BattleScalar.FromFloat(5f)))
                }));
            var context = new BattleConditionEvaluationContext(
                world,
                new BattleTick(1),
                owner: source,
                source: source,
                target: target,
                BattleEffectContext.Unknown());

            bool result = BattleConditionProgramEvaluator.Evaluate(program, context);

            Assert.IsTrue(result);
        }

        [Test]
        public void Evaluate_AnyReturnsTrueWhenOneConditionPasses()
        {
            var world = new BattleWorld();
            EntityId source = SpawnCombatant(world, new UnitId(1), new TeamId(1), maxHealth: 10);
            EntityId target = SpawnCombatant(world, new UnitId(2), new TeamId(2), maxHealth: 10);
            BattleConditionProgram program = BattleConditionCompiler.Compile(new BattleConditionGroup(
                BattleConditionMatchMode.Any,
                new[]
                {
                    BattleConditionDefinition.Compare(
                        BattleConditionOperandDefinition.LiteralInt(1),
                        BattleConditionComparison.Equal,
                        BattleConditionOperandDefinition.LiteralInt(1)),
                    BattleConditionDefinition.Compare(
                        BattleConditionOperandDefinition.HealthPercent(BattleConditionSubject.Target),
                        BattleConditionComparison.Equal,
                        BattleConditionOperandDefinition.LiteralPercentBasisPoints(0))
                }));
            var context = new BattleConditionEvaluationContext(
                world,
                new BattleTick(1),
                owner: source,
                source: source,
                target: target,
                BattleEffectContext.Unknown());

            bool result = BattleConditionProgramEvaluator.Evaluate(program, context);

            Assert.IsTrue(result);
        }

        [Test]
        public void Evaluate_NullProgram_ReturnsTrue()
        {
            bool result = BattleConditionProgramEvaluator.Evaluate(null, Context(new BattleWorld()));

            Assert.IsTrue(result);
        }

        [Test]
        public void Evaluate_NullWorld_ThrowsBeforeAlwaysTrue()
        {
            Assert.Throws<ArgumentNullException>(() =>
                BattleConditionProgramEvaluator.Evaluate(BattleConditionProgram.AlwaysTrue, Context(null)));
        }

        [Test]
        public void Evaluate_NotInvertsChild()
        {
            var program = new BattleConditionProgram(
                new[]
                {
                    BattleConditionInstruction.Constant(false),
                    BattleConditionInstruction.Not(0)
                },
                new BattleConditionOperandData[0],
                new BattleStatusConditionFilterData[0],
                rootInstructionIndex: 1);

            bool result = BattleConditionProgramEvaluator.Evaluate(program, Context(new BattleWorld()));

            Assert.IsTrue(result);
        }

        [Test]
        public void Evaluate_AllReturnsFalseWhenAConditionFails()
        {
            var program = new BattleConditionProgram(
                new[]
                {
                    BattleConditionInstruction.Constant(true),
                    BattleConditionInstruction.Constant(false),
                    BattleConditionInstruction.All(0, 2)
                },
                new BattleConditionOperandData[0],
                new BattleStatusConditionFilterData[0],
                rootInstructionIndex: 2);

            bool result = BattleConditionProgramEvaluator.Evaluate(program, Context(new BattleWorld()));

            Assert.IsFalse(result);
        }

        [Test]
        public void Evaluate_AnyReturnsFalseWhenNoConditionPasses()
        {
            var program = new BattleConditionProgram(
                new[]
                {
                    BattleConditionInstruction.Constant(false),
                    BattleConditionInstruction.Constant(false),
                    BattleConditionInstruction.Any(0, 2)
                },
                new BattleConditionOperandData[0],
                new BattleStatusConditionFilterData[0],
                rootInstructionIndex: 2);

            bool result = BattleConditionProgramEvaluator.Evaluate(program, Context(new BattleWorld()));

            Assert.IsFalse(result);
        }

        private static BattleConditionEvaluationContext Context(BattleWorld world)
        {
            return new BattleConditionEvaluationContext(
                world,
                new BattleTick(1),
                owner: default,
                source: default,
                target: default,
                BattleEffectContext.Unknown());
        }

        private static EntityId SpawnCombatant(
            BattleWorld world,
            UnitId unitId,
            TeamId teamId,
            int maxHealth,
            BattleVector2 position = default,
            float moveSpeed = 0f)
        {
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                unitId,
                new CombatantSpawnData(
                    teamId,
                    "unit",
                    position,
                    maxHealth,
                    radius: BattleScalar.FromFloat(0.25f),
                    BattleScalar.FromFloat(moveSpeed),
                    basicAbility: TestCombatants.AbilitySpawn("basic-attack", 1f, 1, 1),
                    abilities: new AbilitySpawnData[0])));
            world.FlushSpawnCombatantCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(0));
            Assert.IsTrue(world.TryFindEntity(unitId, out EntityId entity));
            return entity;
        }

        private static StatusInstance Status(string id, StatusPolarity polarity, EntityId source, int stackCount = 1)
        {
            return Status(id, polarity, source, Array.Empty<BattleModifierInstance>(), stackCount);
        }

        private static StatusInstance Status(
            string id,
            StatusPolarity polarity,
            EntityId source,
            BattleModifierInstance modifier,
            int stackCount = 1)
        {
            return Status(id, polarity, source, new[] { modifier }, stackCount);
        }

        private static StatusInstance Status(
            string id,
            StatusPolarity polarity,
            EntityId source,
            IReadOnlyList<BattleModifierInstance> modifiers,
            int stackCount = 1)
        {
            return new StatusInstance(
                id,
                polarity,
                source,
                durationRemainingTicks: 3,
                tickIntervalTicks: 1,
                ticksUntilNextPeriodicEffect: 1,
                periodicDamage: 0,
                modifiers,
                triggers: new BattleTriggerInstance[0],
                stackCount,
                maxStacks: Math.Max(stackCount, 1));
        }
    }
}
