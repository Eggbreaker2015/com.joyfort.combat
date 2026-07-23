using System;
using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleConditionCompilerTests
    {
        [Test]
        public void Compile_EmptyGroup_ReturnsAlwaysTrueProgram()
        {
            BattleConditionProgram program = BattleConditionCompiler.Compile(BattleConditionGroup.Empty);

            Assert.IsNotNull(program);
            Assert.IsTrue(program.IsAlwaysTrue);
        }

        [Test]
        public void Compile_NullGroup_ReturnsAlwaysTrueProgram()
        {
            BattleConditionProgram program = BattleConditionCompiler.Compile(null);

            Assert.IsNotNull(program);
            Assert.IsTrue(program.IsAlwaysTrue);
        }

        [Test]
        public void Compile_CompareCondition_EmitsCompareInstructionAndOperands()
        {
            var group = new BattleConditionGroup(
                BattleConditionMatchMode.All,
                new[]
                {
                    BattleConditionDefinition.Compare(
                        BattleConditionOperandDefinition.HealthPercent(BattleConditionSubject.Source),
                        BattleConditionComparison.LessOrEqual,
                        BattleConditionOperandDefinition.LiteralPercentBasisPoints(2000))
                });

            BattleConditionProgram program = BattleConditionCompiler.Compile(group);

            Assert.IsFalse(program.IsAlwaysTrue);
            Assert.AreEqual(2, program.Instructions.Count);
            Assert.AreEqual(BattleConditionInstructionOp.Compare, program.Instructions[0].Op);
            Assert.AreEqual(BattleConditionInstructionOp.All, program.Instructions[1].Op);
            Assert.AreEqual(1, program.RootInstructionIndex);
            Assert.AreEqual(2, program.Operands.Count);
            Assert.AreEqual(BattleConditionOperandOp.HealthPercent, program.Operands[0].Op);
            Assert.AreEqual(BattleConditionOperandOp.LiteralScalar, program.Operands[1].Op);
            Assert.AreEqual(BattleScalar.FromInt(2000) / BattleScalar.FromInt(10000), program.Operands[1].ScalarValue);
        }

        [Test]
        public void Compile_StatusCountCondition_EmitsStatusFilterData()
        {
            var group = new BattleConditionGroup(
                BattleConditionMatchMode.Any,
                new[]
                {
                    BattleConditionDefinition.Compare(
                        BattleConditionOperandDefinition.StatusCount(
                            BattleConditionSubject.Target,
                            BattleStatusConditionFilterDefinition.Polarity(StatusPolarity.Debuff)),
                        BattleConditionComparison.GreaterOrEqual,
                        BattleConditionOperandDefinition.LiteralInt(1))
                });

            BattleConditionProgram program = BattleConditionCompiler.Compile(group);

            Assert.AreEqual(BattleConditionInstructionOp.Any, program.Instructions[1].Op);
            Assert.AreEqual(1, program.StatusFilters.Count);
            Assert.AreEqual(BattleStatusConditionFilterOp.Polarity, program.StatusFilters[0].Op);
            Assert.AreEqual(StatusPolarity.Debuff, program.StatusFilters[0].Polarity);
            Assert.AreEqual(0, program.Operands[0].StatusFilterIndex);
        }

        [Test]
        public void Compile_ExtendedOperands_EmitRuntimeOperandData()
        {
            var group = new BattleConditionGroup(
                BattleConditionMatchMode.All,
                new[]
                {
                    BattleConditionDefinition.Compare(
                        BattleConditionOperandDefinition.StatusStackCount(
                            BattleConditionSubject.Target,
                            BattleStatusConditionFilterDefinition.StatusId("poison")),
                        BattleConditionComparison.GreaterOrEqual,
                        BattleConditionOperandDefinition.LiteralInt(2)),
                    BattleConditionDefinition.Compare(
                        BattleConditionOperandDefinition.StatValue(BattleConditionSubject.Source, BattleStatId.MoveSpeed),
                        BattleConditionComparison.GreaterOrEqual,
                        BattleConditionOperandDefinition.LiteralScalar(BattleScalar.FromFloat(2f))),
                    BattleConditionDefinition.Compare(
                        BattleConditionOperandDefinition.DistanceBetween(BattleConditionSubject.Source, BattleConditionSubject.Target),
                        BattleConditionComparison.LessOrEqual,
                        BattleConditionOperandDefinition.LiteralScalar(BattleScalar.FromFloat(5f)))
                });

            BattleConditionProgram program = BattleConditionCompiler.Compile(group);

            Assert.AreEqual(BattleConditionInstructionOp.All, program.Instructions[3].Op);
            Assert.AreEqual(6, program.Operands.Count);
            Assert.AreEqual(1, program.StatusFilters.Count);
            Assert.AreEqual(BattleConditionOperandOp.StatusStackCount, program.Operands[0].Op);
            Assert.AreEqual(0, program.Operands[0].StatusFilterIndex);
            Assert.AreEqual(BattleConditionOperandOp.StatValue, program.Operands[2].Op);
            Assert.AreEqual(BattleStatId.MoveSpeed, program.Operands[2].Stat);
            Assert.AreEqual(BattleConditionOperandOp.DistanceBetween, program.Operands[4].Op);
            Assert.AreEqual(BattleConditionSubject.Source, program.Operands[4].Subject);
            Assert.AreEqual(BattleConditionSubject.Target, program.Operands[4].OtherSubject);
        }

        [Test]
        public void ProgramConstructor_CompareWithMissingOperand_Throws()
        {
            var instructions = new[]
            {
                BattleConditionInstruction.Compare(0, BattleConditionComparison.Equal, 1, BattleConditionOperandValueKind.Int)
            };
            var operands = new[] { BattleConditionOperandData.LiteralInt(1) };

            Assert.Throws<ArgumentException>(() => new BattleConditionProgram(
                instructions,
                operands,
                Array.Empty<BattleStatusConditionFilterData>(),
                rootInstructionIndex: 0));
        }

        [Test]
        public void ProgramConstructor_SelfChildInstruction_Throws()
        {
            var instructions = new[] { BattleConditionInstruction.Not(0) };

            Assert.Throws<ArgumentException>(() => new BattleConditionProgram(
                instructions,
                Array.Empty<BattleConditionOperandData>(),
                Array.Empty<BattleStatusConditionFilterData>(),
                rootInstructionIndex: 0));
        }

        [Test]
        public void ProgramConstructor_StatusCountWithMissingFilter_Throws()
        {
            var instructions = new[] { BattleConditionInstruction.Constant(true) };
            var operands = new[] { BattleConditionOperandData.StatusCount(BattleConditionSubject.Owner, 0) };

            Assert.Throws<ArgumentException>(() => new BattleConditionProgram(
                instructions,
                operands,
                Array.Empty<BattleStatusConditionFilterData>(),
                rootInstructionIndex: 0));
        }

        [Test]
        public void ProgramConstructor_BoolCompareWithLess_Throws()
        {
            var instructions = new[]
            {
                BattleConditionInstruction.Compare(0, BattleConditionComparison.Less, 1, BattleConditionOperandValueKind.Bool)
            };
            var operands = new[]
            {
                BattleConditionOperandData.LiteralBool(false),
                BattleConditionOperandData.LiteralBool(true)
            };

            Assert.Throws<ArgumentException>(() => new BattleConditionProgram(
                instructions,
                operands,
                Array.Empty<BattleStatusConditionFilterData>(),
                rootInstructionIndex: 0));
        }
    }
}
