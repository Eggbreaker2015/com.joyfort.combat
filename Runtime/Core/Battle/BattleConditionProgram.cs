using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    public sealed class BattleConditionProgram
    {
        private readonly BattleConditionInstruction[] _instructions;
        private readonly BattleConditionOperandData[] _operands;
        private readonly BattleStatusConditionFilterData[] _statusFilters;
        private readonly ReadOnlyCollection<BattleConditionInstruction> _readOnlyInstructions;
        private readonly ReadOnlyCollection<BattleConditionOperandData> _readOnlyOperands;
        private readonly ReadOnlyCollection<BattleStatusConditionFilterData> _readOnlyStatusFilters;

        public BattleConditionProgram(
            IReadOnlyList<BattleConditionInstruction> instructions,
            IReadOnlyList<BattleConditionOperandData> operands,
            IReadOnlyList<BattleStatusConditionFilterData> statusFilters,
            int rootInstructionIndex)
        {
            _instructions = CopyList(instructions, nameof(instructions));
            _operands = CopyList(operands, nameof(operands));
            _statusFilters = CopyList(statusFilters, nameof(statusFilters));
            RootInstructionIndex = ValidateRootInstructionIndex(rootInstructionIndex, _instructions.Length);
            ValidateStructure();

            _readOnlyInstructions = new ReadOnlyCollection<BattleConditionInstruction>(_instructions);
            _readOnlyOperands = new ReadOnlyCollection<BattleConditionOperandData>(_operands);
            _readOnlyStatusFilters = new ReadOnlyCollection<BattleStatusConditionFilterData>(_statusFilters);
        }

        public static BattleConditionProgram AlwaysTrue { get; } = new BattleConditionProgram(
            new[] { BattleConditionInstruction.Constant(true) },
            Array.Empty<BattleConditionOperandData>(),
            Array.Empty<BattleStatusConditionFilterData>(),
            rootInstructionIndex: 0);

        public IReadOnlyList<BattleConditionInstruction> Instructions => _readOnlyInstructions;
        public IReadOnlyList<BattleConditionOperandData> Operands => _readOnlyOperands;
        public IReadOnlyList<BattleStatusConditionFilterData> StatusFilters => _readOnlyStatusFilters;
        public int RootInstructionIndex { get; }

        public bool IsAlwaysTrue
        {
            get
            {
                return _instructions.Length == 1
                    && _operands.Length == 0
                    && _statusFilters.Length == 0
                    && RootInstructionIndex == 0
                    && _instructions[0].Op == BattleConditionInstructionOp.ConstantBool
                    && _instructions[0].BoolValue;
            }
        }

        private static T[] CopyList<T>(IReadOnlyList<T> source, string parameterName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new T[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }

        private static int ValidateRootInstructionIndex(int rootInstructionIndex, int instructionCount)
        {
            if (instructionCount <= 0)
            {
                throw new ArgumentException("Battle condition program requires at least one instruction.");
            }

            if (rootInstructionIndex < 0 || rootInstructionIndex >= instructionCount)
            {
                throw new ArgumentOutOfRangeException(nameof(rootInstructionIndex), rootInstructionIndex, "Root instruction index must refer to an instruction in the program.");
            }

            return rootInstructionIndex;
        }

        private void ValidateStructure()
        {
            for (var i = 0; i < _instructions.Length; i++)
            {
                BattleConditionInstruction instruction = _instructions[i];
                switch (instruction.Op)
                {
                    case BattleConditionInstructionOp.ConstantBool:
                        break;
                    case BattleConditionInstructionOp.Compare:
                        ValidateCompareInstruction(instruction, i);
                        break;
                    case BattleConditionInstructionOp.All:
                    case BattleConditionInstructionOp.Any:
                        ValidateInstructionSpan(instruction.FirstChildInstructionIndex, instruction.ChildCount, i, instruction.Op);
                        break;
                    case BattleConditionInstructionOp.Not:
                        ValidateNotInstruction(instruction, i);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(instruction), instruction.Op, "Unsupported battle condition instruction op.");
                }
            }

            for (var i = 0; i < _operands.Length; i++)
            {
                BattleConditionOperandData operand = _operands[i];
                if (operand.Op == BattleConditionOperandOp.StatusCount
                    || operand.Op == BattleConditionOperandOp.StatusStackCount)
                {
                    ValidateStatusFilterIndex(operand.StatusFilterIndex, i, operand.Op);
                }
            }
        }

        private void ValidateCompareInstruction(BattleConditionInstruction instruction, int instructionIndex)
        {
            BattleConditionOperandData left = GetOperand(instruction.LeftOperandIndex, instructionIndex, "left");
            BattleConditionOperandData right = GetOperand(instruction.RightOperandIndex, instructionIndex, "right");
            if (left.ValueKind != right.ValueKind || left.ValueKind != instruction.ValueKind)
            {
                throw new ArgumentException(
                    $"Compare instruction {instructionIndex} operand value kinds must match the instruction value kind.");
            }

            if ((instruction.ValueKind == BattleConditionOperandValueKind.Bool || instruction.ValueKind == BattleConditionOperandValueKind.Identifier)
                && instruction.Comparison != BattleConditionComparison.Equal
                && instruction.Comparison != BattleConditionComparison.NotEqual)
            {
                throw new ArgumentException(
                    $"Compare instruction {instructionIndex} uses a comparison that is not valid for {instruction.ValueKind} operands.");
            }
        }

        private BattleConditionOperandData GetOperand(int operandIndex, int instructionIndex, string side)
        {
            if (operandIndex < 0 || operandIndex >= _operands.Length)
            {
                throw new ArgumentException(
                    $"Compare instruction {instructionIndex} has an invalid {side} operand index.");
            }

            return _operands[operandIndex];
        }

        private void ValidateInstructionSpan(
            int firstChildInstructionIndex,
            int childCount,
            int instructionIndex,
            BattleConditionInstructionOp op)
        {
            if (firstChildInstructionIndex < 0
                || childCount <= 0
                || childCount > instructionIndex - firstChildInstructionIndex)
            {
                throw new ArgumentException(
                    $"{op} instruction {instructionIndex} child span must refer to a contiguous range of earlier instructions in the program.");
            }
        }

        private void ValidateNotInstruction(BattleConditionInstruction instruction, int instructionIndex)
        {
            if (instruction.ChildCount != 1)
            {
                throw new ArgumentException($"Not instruction {instructionIndex} child count must be 1.");
            }

            if (instruction.FirstChildInstructionIndex < 0 || instruction.FirstChildInstructionIndex >= instructionIndex)
            {
                throw new ArgumentException($"Not instruction {instructionIndex} child index must refer to an earlier instruction in the program.");
            }
        }

        private void ValidateStatusFilterIndex(int statusFilterIndex, int operandIndex, BattleConditionOperandOp op)
        {
            if (statusFilterIndex < 0 || statusFilterIndex >= _statusFilters.Length)
            {
                throw new ArgumentException($"{op} operand {operandIndex} status filter index must refer to a status filter in the program.");
            }
        }
    }

    public enum BattleConditionInstructionOp
    {
        ConstantBool,
        Compare,
        All,
        Any,
        Not
    }

    public readonly struct BattleConditionInstruction
    {
        private BattleConditionInstruction(
            BattleConditionInstructionOp op,
            bool boolValue,
            int leftOperandIndex,
            int rightOperandIndex,
            BattleConditionComparison comparison,
            BattleConditionOperandValueKind valueKind,
            int firstChildInstructionIndex,
            int childCount)
        {
            Op = ValidateOp(op);
            BoolValue = boolValue;
            LeftOperandIndex = leftOperandIndex;
            RightOperandIndex = rightOperandIndex;
            Comparison = ValidateComparison(comparison);
            ValueKind = ValidateValueKind(valueKind);
            FirstChildInstructionIndex = firstChildInstructionIndex;
            ChildCount = childCount;
        }

        public BattleConditionInstructionOp Op { get; }
        public bool BoolValue { get; }
        public int LeftOperandIndex { get; }
        public int RightOperandIndex { get; }
        public BattleConditionComparison Comparison { get; }
        public BattleConditionOperandValueKind ValueKind { get; }
        public int FirstChildInstructionIndex { get; }
        public int ChildCount { get; }

        public static BattleConditionInstruction Constant(bool value)
        {
            return new BattleConditionInstruction(
                BattleConditionInstructionOp.ConstantBool,
                value,
                leftOperandIndex: -1,
                rightOperandIndex: -1,
                BattleConditionComparison.Equal,
                BattleConditionOperandValueKind.Bool,
                firstChildInstructionIndex: -1,
                childCount: 0);
        }

        public static BattleConditionInstruction Compare(
            int leftOperandIndex,
            BattleConditionComparison comparison,
            int rightOperandIndex,
            BattleConditionOperandValueKind valueKind)
        {
            if (leftOperandIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(leftOperandIndex));
            }

            if (rightOperandIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rightOperandIndex));
            }

            return new BattleConditionInstruction(
                BattleConditionInstructionOp.Compare,
                boolValue: false,
                leftOperandIndex,
                rightOperandIndex,
                comparison,
                valueKind,
                firstChildInstructionIndex: -1,
                childCount: 0);
        }

        public static BattleConditionInstruction All(int firstChildInstructionIndex, int childCount)
        {
            ValidateChildSpan(firstChildInstructionIndex, childCount);
            return new BattleConditionInstruction(
                BattleConditionInstructionOp.All,
                boolValue: false,
                leftOperandIndex: -1,
                rightOperandIndex: -1,
                BattleConditionComparison.Equal,
                BattleConditionOperandValueKind.Bool,
                firstChildInstructionIndex,
                childCount);
        }

        public static BattleConditionInstruction Any(int firstChildInstructionIndex, int childCount)
        {
            ValidateChildSpan(firstChildInstructionIndex, childCount);
            return new BattleConditionInstruction(
                BattleConditionInstructionOp.Any,
                boolValue: false,
                leftOperandIndex: -1,
                rightOperandIndex: -1,
                BattleConditionComparison.Equal,
                BattleConditionOperandValueKind.Bool,
                firstChildInstructionIndex,
                childCount);
        }

        public static BattleConditionInstruction Not(int childInstructionIndex)
        {
            if (childInstructionIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(childInstructionIndex));
            }

            return new BattleConditionInstruction(
                BattleConditionInstructionOp.Not,
                boolValue: false,
                leftOperandIndex: -1,
                rightOperandIndex: -1,
                BattleConditionComparison.Equal,
                BattleConditionOperandValueKind.Bool,
                firstChildInstructionIndex: childInstructionIndex,
                childCount: 1);
        }

        private static void ValidateChildSpan(int firstChildInstructionIndex, int childCount)
        {
            if (firstChildInstructionIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(firstChildInstructionIndex));
            }

            if (childCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(childCount));
            }
        }

        private static BattleConditionInstructionOp ValidateOp(BattleConditionInstructionOp op)
        {
            switch (op)
            {
                case BattleConditionInstructionOp.ConstantBool:
                case BattleConditionInstructionOp.Compare:
                case BattleConditionInstructionOp.All:
                case BattleConditionInstructionOp.Any:
                case BattleConditionInstructionOp.Not:
                    return op;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op), op, "Unsupported battle condition instruction op.");
            }
        }

        private static BattleConditionComparison ValidateComparison(BattleConditionComparison comparison)
        {
            switch (comparison)
            {
                case BattleConditionComparison.Equal:
                case BattleConditionComparison.NotEqual:
                case BattleConditionComparison.Less:
                case BattleConditionComparison.LessOrEqual:
                case BattleConditionComparison.Greater:
                case BattleConditionComparison.GreaterOrEqual:
                    return comparison;
                default:
                    throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Unsupported battle condition comparison.");
            }
        }

        private static BattleConditionOperandValueKind ValidateValueKind(BattleConditionOperandValueKind valueKind)
        {
            switch (valueKind)
            {
                case BattleConditionOperandValueKind.Int:
                case BattleConditionOperandValueKind.Scalar:
                case BattleConditionOperandValueKind.Bool:
                case BattleConditionOperandValueKind.Identifier:
                    return valueKind;
                default:
                    throw new ArgumentOutOfRangeException(nameof(valueKind), valueKind, "Unsupported battle condition operand value kind.");
            }
        }
    }

    public enum BattleConditionOperandOp
    {
        LiteralInt,
        LiteralScalar,
        LiteralBool,
        LiteralIdentifier,
        HealthPercent,
        StatusCount,
        StatusStackCount,
        StatValue,
        DistanceBetween
    }

    public readonly struct BattleConditionOperandData
    {
        private BattleConditionOperandData(
            BattleConditionOperandOp op,
            BattleConditionOperandValueKind valueKind,
            BattleConditionSubject subject,
            BattleConditionSubject otherSubject,
            int intValue,
            BattleScalar scalarValue,
            bool boolValue,
            string identifierValue,
            int statusFilterIndex,
            BattleStatId stat)
        {
            Op = ValidateOp(op);
            ValueKind = ValidateValueKind(valueKind);
            Subject = ValidateSubject(subject);
            OtherSubject = ValidateSubject(otherSubject);
            IntValue = intValue;
            ScalarValue = scalarValue;
            BoolValue = boolValue;
            IdentifierValue = identifierValue;
            StatusFilterIndex = statusFilterIndex;
            Stat = BattleConditionOperandDefinition.ValidateStat(stat);
        }

        public BattleConditionOperandOp Op { get; }
        public BattleConditionOperandValueKind ValueKind { get; }
        public BattleConditionSubject Subject { get; }
        public BattleConditionSubject OtherSubject { get; }
        public int IntValue { get; }
        public BattleScalar ScalarValue { get; }
        public bool BoolValue { get; }
        public string IdentifierValue { get; }
        public int StatusFilterIndex { get; }
        public BattleStatId Stat { get; }

        public static BattleConditionOperandData LiteralInt(int value)
        {
            return new BattleConditionOperandData(
                BattleConditionOperandOp.LiteralInt,
                BattleConditionOperandValueKind.Int,
                BattleConditionSubject.Owner,
                BattleConditionSubject.Owner,
                intValue: value,
                scalarValue: default,
                boolValue: false,
                identifierValue: null,
                statusFilterIndex: -1,
                stat: BattleStatId.MaxHealth);
        }

        public static BattleConditionOperandData LiteralScalar(BattleScalar value)
        {
            return new BattleConditionOperandData(
                BattleConditionOperandOp.LiteralScalar,
                BattleConditionOperandValueKind.Scalar,
                BattleConditionSubject.Owner,
                BattleConditionSubject.Owner,
                intValue: 0,
                scalarValue: value,
                boolValue: false,
                identifierValue: null,
                statusFilterIndex: -1,
                stat: BattleStatId.MaxHealth);
        }

        public static BattleConditionOperandData LiteralBool(bool value)
        {
            return new BattleConditionOperandData(
                BattleConditionOperandOp.LiteralBool,
                BattleConditionOperandValueKind.Bool,
                BattleConditionSubject.Owner,
                BattleConditionSubject.Owner,
                intValue: 0,
                scalarValue: default,
                boolValue: value,
                identifierValue: null,
                statusFilterIndex: -1,
                stat: BattleStatId.MaxHealth);
        }

        public static BattleConditionOperandData LiteralIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Identifier literal is required.", nameof(value));
            }

            return new BattleConditionOperandData(
                BattleConditionOperandOp.LiteralIdentifier,
                BattleConditionOperandValueKind.Identifier,
                BattleConditionSubject.Owner,
                BattleConditionSubject.Owner,
                intValue: 0,
                scalarValue: default,
                boolValue: false,
                identifierValue: value,
                statusFilterIndex: -1,
                stat: BattleStatId.MaxHealth);
        }

        public static BattleConditionOperandData HealthPercent(BattleConditionSubject subject)
        {
            return new BattleConditionOperandData(
                BattleConditionOperandOp.HealthPercent,
                BattleConditionOperandValueKind.Scalar,
                subject,
                BattleConditionSubject.Owner,
                intValue: 0,
                scalarValue: default,
                boolValue: false,
                identifierValue: null,
                statusFilterIndex: -1,
                stat: BattleStatId.MaxHealth);
        }

        public static BattleConditionOperandData StatusCount(BattleConditionSubject subject, int statusFilterIndex)
        {
            if (statusFilterIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(statusFilterIndex));
            }

            return new BattleConditionOperandData(
                BattleConditionOperandOp.StatusCount,
                BattleConditionOperandValueKind.Int,
                subject,
                BattleConditionSubject.Owner,
                intValue: 0,
                scalarValue: default,
                boolValue: false,
                identifierValue: null,
                statusFilterIndex: statusFilterIndex,
                stat: BattleStatId.MaxHealth);
        }

        public static BattleConditionOperandData StatusStackCount(BattleConditionSubject subject, int statusFilterIndex)
        {
            if (statusFilterIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(statusFilterIndex));
            }

            return new BattleConditionOperandData(
                BattleConditionOperandOp.StatusStackCount,
                BattleConditionOperandValueKind.Int,
                subject,
                BattleConditionSubject.Owner,
                intValue: 0,
                scalarValue: default,
                boolValue: false,
                identifierValue: null,
                statusFilterIndex: statusFilterIndex,
                stat: BattleStatId.MaxHealth);
        }

        public static BattleConditionOperandData StatValue(BattleConditionSubject subject, BattleStatId stat)
        {
            return new BattleConditionOperandData(
                BattleConditionOperandOp.StatValue,
                BattleConditionOperandValueKind.Scalar,
                subject,
                BattleConditionSubject.Owner,
                intValue: 0,
                scalarValue: default,
                boolValue: false,
                identifierValue: null,
                statusFilterIndex: -1,
                stat: stat);
        }

        public static BattleConditionOperandData DistanceBetween(BattleConditionSubject subject, BattleConditionSubject otherSubject)
        {
            return new BattleConditionOperandData(
                BattleConditionOperandOp.DistanceBetween,
                BattleConditionOperandValueKind.Scalar,
                subject,
                otherSubject,
                intValue: 0,
                scalarValue: default,
                boolValue: false,
                identifierValue: null,
                statusFilterIndex: -1,
                stat: BattleStatId.MaxHealth);
        }

        private static BattleConditionOperandOp ValidateOp(BattleConditionOperandOp op)
        {
            switch (op)
            {
                case BattleConditionOperandOp.LiteralInt:
                case BattleConditionOperandOp.LiteralScalar:
                case BattleConditionOperandOp.LiteralBool:
                case BattleConditionOperandOp.LiteralIdentifier:
                case BattleConditionOperandOp.HealthPercent:
                case BattleConditionOperandOp.StatusCount:
                case BattleConditionOperandOp.StatusStackCount:
                case BattleConditionOperandOp.StatValue:
                case BattleConditionOperandOp.DistanceBetween:
                    return op;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op), op, "Unsupported battle condition operand op.");
            }
        }

        private static BattleConditionOperandValueKind ValidateValueKind(BattleConditionOperandValueKind valueKind)
        {
            switch (valueKind)
            {
                case BattleConditionOperandValueKind.Int:
                case BattleConditionOperandValueKind.Scalar:
                case BattleConditionOperandValueKind.Bool:
                case BattleConditionOperandValueKind.Identifier:
                    return valueKind;
                default:
                    throw new ArgumentOutOfRangeException(nameof(valueKind), valueKind, "Unsupported battle condition operand value kind.");
            }
        }

        private static BattleConditionSubject ValidateSubject(BattleConditionSubject subject)
        {
            switch (subject)
            {
                case BattleConditionSubject.Owner:
                case BattleConditionSubject.Source:
                case BattleConditionSubject.Target:
                    return subject;
                default:
                    throw new ArgumentOutOfRangeException(nameof(subject), subject, "Unsupported battle condition subject.");
            }
        }
    }

    public enum BattleStatusConditionFilterOp
    {
        Any,
        StatusId,
        Polarity
    }

    public readonly struct BattleStatusConditionFilterData
    {
        private BattleStatusConditionFilterData(BattleStatusConditionFilterOp op, string statusId, StatusPolarity polarity)
        {
            Op = ValidateOp(op);
            StatusId = statusId;
            Polarity = ValidatePolarity(polarity);
        }

        public BattleStatusConditionFilterOp Op { get; }
        public string StatusId { get; }
        public StatusPolarity Polarity { get; }

        public static BattleStatusConditionFilterData Any()
        {
            return new BattleStatusConditionFilterData(
                BattleStatusConditionFilterOp.Any,
                statusId: null,
                StatusPolarity.Neutral);
        }

        public static BattleStatusConditionFilterData ForStatusId(string statusId)
        {
            if (string.IsNullOrWhiteSpace(statusId))
            {
                throw new ArgumentException("Status filter id is required.", nameof(statusId));
            }

            return new BattleStatusConditionFilterData(
                BattleStatusConditionFilterOp.StatusId,
                statusId,
                StatusPolarity.Neutral);
        }

        public static BattleStatusConditionFilterData ForPolarity(StatusPolarity polarity)
        {
            return new BattleStatusConditionFilterData(
                BattleStatusConditionFilterOp.Polarity,
                statusId: null,
                polarity);
        }

        private static BattleStatusConditionFilterOp ValidateOp(BattleStatusConditionFilterOp op)
        {
            switch (op)
            {
                case BattleStatusConditionFilterOp.Any:
                case BattleStatusConditionFilterOp.StatusId:
                case BattleStatusConditionFilterOp.Polarity:
                    return op;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op), op, "Unsupported battle status condition filter op.");
            }
        }

        private static StatusPolarity ValidatePolarity(StatusPolarity polarity)
        {
            switch (polarity)
            {
                case StatusPolarity.Buff:
                case StatusPolarity.Debuff:
                case StatusPolarity.Neutral:
                    return polarity;
                default:
                    throw new ArgumentOutOfRangeException(nameof(polarity), polarity, "Unsupported status polarity.");
            }
        }
    }
}
