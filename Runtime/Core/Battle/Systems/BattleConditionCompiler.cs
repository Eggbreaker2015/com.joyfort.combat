using System;
using System.Collections.Generic;

namespace Combat.Core.Battle
{
    public static class BattleConditionCompiler
    {
        private static readonly BattleScalar PercentDenominator = BattleScalar.FromInt(10000);

        public static BattleConditionProgram Compile(BattleConditionGroup group)
        {
            if (group == null || !group.HasConditions)
            {
                return BattleConditionProgram.AlwaysTrue;
            }

            var builder = new Builder();
            int rootIndex = builder.CompileGroup(group);
            return builder.Build(rootIndex);
        }

        private sealed class Builder
        {
            private readonly List<BattleConditionInstruction> _instructions = new List<BattleConditionInstruction>();
            private readonly List<BattleConditionOperandData> _operands = new List<BattleConditionOperandData>();
            private readonly List<BattleStatusConditionFilterData> _statusFilters = new List<BattleStatusConditionFilterData>();

            public int CompileGroup(BattleConditionGroup group)
            {
                if (group == null)
                {
                    throw new ArgumentNullException(nameof(group));
                }

                if (!group.HasConditions)
                {
                    return AddInstruction(BattleConditionInstruction.Constant(true));
                }

                IReadOnlyList<BattleConditionDefinition> conditions = group.Conditions;
                int firstChildInstructionIndex = _instructions.Count;
                for (var i = 0; i < conditions.Count; i++)
                {
                    CompileCondition(conditions[i]);
                }

                int conditionCount = _instructions.Count - firstChildInstructionIndex;
                switch (group.MatchMode)
                {
                    case BattleConditionMatchMode.All:
                        return AddInstruction(BattleConditionInstruction.All(firstChildInstructionIndex, conditionCount));
                    case BattleConditionMatchMode.Any:
                        return AddInstruction(BattleConditionInstruction.Any(firstChildInstructionIndex, conditionCount));
                    default:
                        throw new ArgumentOutOfRangeException(nameof(group), group.MatchMode, "Unsupported battle condition match mode.");
                }
            }

            public BattleConditionProgram Build(int rootIndex)
            {
                return new BattleConditionProgram(_instructions, _operands, _statusFilters, rootIndex);
            }

            private int CompileCondition(BattleConditionDefinition condition)
            {
                if (condition == null)
                {
                    throw new ArgumentNullException(nameof(condition));
                }

                BattleConditionComparison comparison = BattleConditionDefinition.ValidateComparison(condition.Comparison);
                BattleConditionOperandData left = CompileOperand(condition.Left);
                BattleConditionOperandData right = CompileOperand(condition.Right);
                if (left.ValueKind != right.ValueKind)
                {
                    throw new ArgumentException("Condition operands must resolve to the same value kind.", nameof(condition));
                }

                ValidateComparison(left.ValueKind, comparison);

                int leftOperandIndex = AddOperand(left);
                int rightOperandIndex = AddOperand(right);
                return AddInstruction(BattleConditionInstruction.Compare(
                    leftOperandIndex,
                    comparison,
                    rightOperandIndex,
                    left.ValueKind));
            }

            private BattleConditionOperandData CompileOperand(BattleConditionOperandDefinition operand)
            {
                if (operand == null)
                {
                    throw new ArgumentNullException(nameof(operand));
                }

                switch (operand)
                {
                    case BattleLiteralIntConditionOperandDefinition literal:
                        return BattleConditionOperandData.LiteralInt(literal.Value);
                    case BattleLiteralPercentConditionOperandDefinition literal:
                        return BattleConditionOperandData.LiteralScalar(BattleScalar.FromInt(literal.BasisPoints) / PercentDenominator);
                    case BattleLiteralScalarConditionOperandDefinition literal:
                        return BattleConditionOperandData.LiteralScalar(literal.Value);
                    case BattleLiteralBoolConditionOperandDefinition literal:
                        return BattleConditionOperandData.LiteralBool(literal.Value);
                    case BattleLiteralIdentifierConditionOperandDefinition literal:
                        return BattleConditionOperandData.LiteralIdentifier(literal.Value);
                    case BattleHealthPercentConditionOperandDefinition healthPercent:
                        return BattleConditionOperandData.HealthPercent(healthPercent.Subject);
                    case BattleStatusCountConditionOperandDefinition statusCount:
                        int statusFilterIndex = AddStatusFilter(CompileStatusFilter(statusCount.Filter));
                        return BattleConditionOperandData.StatusCount(statusCount.Subject, statusFilterIndex);
                    case BattleStatusStackCountConditionOperandDefinition statusStackCount:
                        int stackFilterIndex = AddStatusFilter(CompileStatusFilter(statusStackCount.Filter));
                        return BattleConditionOperandData.StatusStackCount(statusStackCount.Subject, stackFilterIndex);
                    case BattleStatValueConditionOperandDefinition statValue:
                        return BattleConditionOperandData.StatValue(statValue.Subject, statValue.Stat);
                    case BattleDistanceBetweenConditionOperandDefinition distanceBetween:
                        return BattleConditionOperandData.DistanceBetween(distanceBetween.Subject, distanceBetween.OtherSubject);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(operand), operand.Kind, "Unsupported battle condition operand kind.");
                }
            }

            private static BattleStatusConditionFilterData CompileStatusFilter(BattleStatusConditionFilterDefinition filter)
            {
                if (filter == null)
                {
                    return BattleStatusConditionFilterData.Any();
                }

                switch (filter)
                {
                    case BattleAnyStatusConditionFilterDefinition _:
                        return BattleStatusConditionFilterData.Any();
                    case BattleStatusIdConditionFilterDefinition statusId:
                        return BattleStatusConditionFilterData.ForStatusId(statusId.Id);
                    case BattleStatusPolarityConditionFilterDefinition polarity:
                        return BattleStatusConditionFilterData.ForPolarity(polarity.StatusPolarity);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(filter), filter.Kind, "Unsupported status condition filter kind.");
                }
            }

            private static void ValidateComparison(BattleConditionOperandValueKind valueKind, BattleConditionComparison comparison)
            {
                BattleConditionDefinition.ValidateComparison(comparison);
                if ((valueKind == BattleConditionOperandValueKind.Bool || valueKind == BattleConditionOperandValueKind.Identifier)
                    && comparison != BattleConditionComparison.Equal
                    && comparison != BattleConditionComparison.NotEqual)
                {
                    throw new ArgumentException("Boolean and identifier condition operands only support Equal and NotEqual comparisons.", nameof(comparison));
                }
            }

            private int AddInstruction(BattleConditionInstruction instruction)
            {
                int index = _instructions.Count;
                _instructions.Add(instruction);
                return index;
            }

            private int AddOperand(BattleConditionOperandData operand)
            {
                int index = _operands.Count;
                _operands.Add(operand);
                return index;
            }

            private int AddStatusFilter(BattleStatusConditionFilterData filter)
            {
                int index = _statusFilters.Count;
                _statusFilters.Add(filter);
                return index;
            }
        }
    }
}
