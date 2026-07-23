using System;
using System.Collections.Generic;

namespace Combat.Core.Battle
{
    internal readonly struct BattleConditionEvaluationContext
    {
        public BattleConditionEvaluationContext(
            BattleWorld world,
            BattleTick tick,
            EntityId owner,
            EntityId source,
            EntityId target,
            BattleEffectContext effectContext)
        {
            World = world;
            Tick = tick;
            Owner = owner;
            Source = source;
            Target = target;
            EffectContext = effectContext;
        }

        public BattleWorld World { get; }
        public BattleTick Tick { get; }
        public EntityId Owner { get; }
        public EntityId Source { get; }
        public EntityId Target { get; }
        public BattleEffectContext EffectContext { get; }

        public static BattleConditionEvaluationContext FromTrigger(BattleWorld world, BattleTick tick, BattleTriggerContext context)
        {
            return new BattleConditionEvaluationContext(
                world,
                tick,
                context.Owner,
                context.Source,
                context.Target,
                context.EffectContext);
        }
    }

    internal static class BattleConditionProgramEvaluator
    {
        public static bool Evaluate(BattleConditionProgram program, BattleConditionEvaluationContext context)
        {
            if (context.World == null)
            {
                throw new ArgumentNullException(nameof(context.World));
            }

            if (program == null || program.IsAlwaysTrue)
            {
                return true;
            }

            return EvaluateInstruction(program, program.RootInstructionIndex, context);
        }

        private static bool EvaluateInstruction(
            BattleConditionProgram program,
            int instructionIndex,
            BattleConditionEvaluationContext context)
        {
            BattleConditionInstruction instruction = program.Instructions[instructionIndex];
            switch (instruction.Op)
            {
                case BattleConditionInstructionOp.ConstantBool:
                    return instruction.BoolValue;
                case BattleConditionInstructionOp.Compare:
                    return EvaluateCompare(program, instruction, context);
                case BattleConditionInstructionOp.All:
                    return EvaluateAll(program, instruction, context);
                case BattleConditionInstructionOp.Any:
                    return EvaluateAny(program, instruction, context);
                case BattleConditionInstructionOp.Not:
                    return !EvaluateInstruction(program, instruction.FirstChildInstructionIndex, context);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(instruction),
                        instruction.Op,
                        "Unsupported battle condition instruction op.");
            }
        }

        private static bool EvaluateCompare(
            BattleConditionProgram program,
            BattleConditionInstruction instruction,
            BattleConditionEvaluationContext context)
        {
            return TryEvaluateOperand(program, instruction.LeftOperandIndex, context, out BattleConditionValue left)
                && TryEvaluateOperand(program, instruction.RightOperandIndex, context, out BattleConditionValue right)
                && left.ValueKind == right.ValueKind
                && Compare(left, instruction.Comparison, right);
        }

        private static bool EvaluateAll(
            BattleConditionProgram program,
            BattleConditionInstruction instruction,
            BattleConditionEvaluationContext context)
        {
            int endIndex = instruction.FirstChildInstructionIndex + instruction.ChildCount;
            for (int childIndex = instruction.FirstChildInstructionIndex; childIndex < endIndex; childIndex++)
            {
                if (!EvaluateInstruction(program, childIndex, context))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool EvaluateAny(
            BattleConditionProgram program,
            BattleConditionInstruction instruction,
            BattleConditionEvaluationContext context)
        {
            int endIndex = instruction.FirstChildInstructionIndex + instruction.ChildCount;
            for (int childIndex = instruction.FirstChildInstructionIndex; childIndex < endIndex; childIndex++)
            {
                if (EvaluateInstruction(program, childIndex, context))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryEvaluateOperand(
            BattleConditionProgram program,
            int operandIndex,
            BattleConditionEvaluationContext context,
            out BattleConditionValue value)
        {
            value = default;
            BattleConditionOperandData operand = program.Operands[operandIndex];
            switch (operand.Op)
            {
                case BattleConditionOperandOp.LiteralInt:
                    value = BattleConditionValue.Int(operand.IntValue);
                    return true;
                case BattleConditionOperandOp.LiteralScalar:
                    value = BattleConditionValue.Scalar(operand.ScalarValue);
                    return true;
                case BattleConditionOperandOp.LiteralBool:
                    value = BattleConditionValue.Bool(operand.BoolValue);
                    return true;
                case BattleConditionOperandOp.LiteralIdentifier:
                    value = BattleConditionValue.Identifier(operand.IdentifierValue);
                    return true;
                case BattleConditionOperandOp.HealthPercent:
                    return TryEvaluateHealthPercent(context.World, ResolveSubject(operand.Subject, context), out value);
                case BattleConditionOperandOp.StatusCount:
                    value = BattleConditionValue.Int(CountStatuses(
                        context.World,
                        ResolveSubject(operand.Subject, context),
                        program.StatusFilters[operand.StatusFilterIndex]));
                    return true;
                case BattleConditionOperandOp.StatusStackCount:
                    value = BattleConditionValue.Int(CountStatusStacks(
                        context.World,
                        ResolveSubject(operand.Subject, context),
                        program.StatusFilters[operand.StatusFilterIndex]));
                    return true;
                case BattleConditionOperandOp.StatValue:
                    return TryEvaluateStatValue(
                        context.World,
                        ResolveSubject(operand.Subject, context),
                        operand.Stat,
                        out value);
                case BattleConditionOperandOp.DistanceBetween:
                    return TryEvaluateDistanceBetween(
                        context.World,
                        ResolveSubject(operand.Subject, context),
                        ResolveSubject(operand.OtherSubject, context),
                        out value);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(operand),
                        operand.Op,
                        "Unsupported battle condition operand op.");
            }
        }

        private static bool TryEvaluateHealthPercent(BattleWorld world, EntityId subject, out BattleConditionValue value)
        {
            value = default;
            if (!world.HealthComponents.TryGet(subject, out HealthComponent health)
                || !BattleStatResolver.TryResolveMaxHealth(world, subject, out int maxHealth))
            {
                return false;
            }

            value = BattleConditionValue.Scalar(BattleScalar.FromInt(health.Current) / BattleScalar.FromInt(maxHealth));
            return true;
        }

        private static bool TryEvaluateStatValue(BattleWorld world, EntityId subject, BattleStatId stat, out BattleConditionValue value)
        {
            value = default;
            switch (stat)
            {
                case BattleStatId.MaxHealth:
                    if (!BattleStatResolver.TryResolveMaxHealth(world, subject, out int maxHealth))
                    {
                        return false;
                    }

                    value = BattleConditionValue.Scalar(BattleScalar.FromInt(maxHealth));
                    return true;
                case BattleStatId.MoveSpeed:
                    if (!BattleStatResolver.TryResolveScalar(world, subject, stat, out BattleScalar statValue))
                    {
                        return false;
                    }

                    value = BattleConditionValue.Scalar(statValue);
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unsupported battle stat id.");
            }
        }

        private static bool TryEvaluateDistanceBetween(BattleWorld world, EntityId subject, EntityId otherSubject, out BattleConditionValue value)
        {
            value = default;
            if (!world.PositionComponents.TryGet(subject, out PositionComponent position)
                || !world.PositionComponents.TryGet(otherSubject, out PositionComponent otherPosition))
            {
                return false;
            }

            value = BattleConditionValue.Scalar(BattleVector2.DistanceScalar(position.Position, otherPosition.Position));
            return true;
        }

        private static int CountStatuses(BattleWorld world, EntityId subject, BattleStatusConditionFilterData filter)
        {
            if (!world.StatusComponents.TryGet(subject, out StatusComponent component))
            {
                return 0;
            }

            var count = 0;
            IReadOnlyList<StatusInstance> statuses = component.Statuses;
            for (var i = 0; i < statuses.Count; i++)
            {
                if (MatchesStatusFilter(statuses[i], filter))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountStatusStacks(BattleWorld world, EntityId subject, BattleStatusConditionFilterData filter)
        {
            if (!world.StatusComponents.TryGet(subject, out StatusComponent component))
            {
                return 0;
            }

            var count = 0;
            IReadOnlyList<StatusInstance> statuses = component.Statuses;
            for (var i = 0; i < statuses.Count; i++)
            {
                StatusInstance status = statuses[i];
                if (MatchesStatusFilter(status, filter))
                {
                    count += status.StackCount;
                }
            }

            return count;
        }

        private static bool MatchesStatusFilter(StatusInstance status, BattleStatusConditionFilterData filter)
        {
            switch (filter.Op)
            {
                case BattleStatusConditionFilterOp.Any:
                    return true;
                case BattleStatusConditionFilterOp.StatusId:
                    return StringComparer.Ordinal.Equals(status.Id, filter.StatusId);
                case BattleStatusConditionFilterOp.Polarity:
                    return status.Polarity == filter.Polarity;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(filter),
                        filter.Op,
                        "Unsupported battle status condition filter op.");
            }
        }

        private static EntityId ResolveSubject(BattleConditionSubject subject, BattleConditionEvaluationContext context)
        {
            switch (subject)
            {
                case BattleConditionSubject.Owner:
                    return context.Owner;
                case BattleConditionSubject.Source:
                    return context.Source;
                case BattleConditionSubject.Target:
                    return context.Target;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(subject),
                        subject,
                        "Unsupported battle condition subject.");
            }
        }

        private static bool Compare(BattleConditionValue left, BattleConditionComparison comparison, BattleConditionValue right)
        {
            switch (left.ValueKind)
            {
                case BattleConditionOperandValueKind.Int:
                    return CompareComparable(left.IntValue.CompareTo(right.IntValue), comparison);
                case BattleConditionOperandValueKind.Scalar:
                    return CompareComparable(left.ScalarValue.CompareTo(right.ScalarValue), comparison);
                case BattleConditionOperandValueKind.Bool:
                    return CompareEquality(left.BoolValue == right.BoolValue, comparison);
                case BattleConditionOperandValueKind.Identifier:
                    return CompareEquality(StringComparer.Ordinal.Equals(left.IdentifierValue, right.IdentifierValue), comparison);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(left),
                        left.ValueKind,
                        "Unsupported battle condition value kind.");
            }
        }

        private static bool CompareComparable(int comparisonResult, BattleConditionComparison comparison)
        {
            switch (comparison)
            {
                case BattleConditionComparison.Equal:
                    return comparisonResult == 0;
                case BattleConditionComparison.NotEqual:
                    return comparisonResult != 0;
                case BattleConditionComparison.Less:
                    return comparisonResult < 0;
                case BattleConditionComparison.LessOrEqual:
                    return comparisonResult <= 0;
                case BattleConditionComparison.Greater:
                    return comparisonResult > 0;
                case BattleConditionComparison.GreaterOrEqual:
                    return comparisonResult >= 0;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(comparison),
                        comparison,
                        "Unsupported battle condition comparison.");
            }
        }

        private static bool CompareEquality(bool isEqual, BattleConditionComparison comparison)
        {
            switch (comparison)
            {
                case BattleConditionComparison.Equal:
                    return isEqual;
                case BattleConditionComparison.NotEqual:
                    return !isEqual;
                default:
                    return false;
            }
        }

        private readonly struct BattleConditionValue
        {
            private BattleConditionValue(
                BattleConditionOperandValueKind valueKind,
                int intValue,
                BattleScalar scalarValue,
                bool boolValue,
                string identifierValue)
            {
                ValueKind = valueKind;
                IntValue = intValue;
                ScalarValue = scalarValue;
                BoolValue = boolValue;
                IdentifierValue = identifierValue;
            }

            public BattleConditionOperandValueKind ValueKind { get; }
            public int IntValue { get; }
            public BattleScalar ScalarValue { get; }
            public bool BoolValue { get; }
            public string IdentifierValue { get; }

            public static BattleConditionValue Int(int value)
            {
                return new BattleConditionValue(BattleConditionOperandValueKind.Int, value, default, default, null);
            }

            public static BattleConditionValue Scalar(BattleScalar value)
            {
                return new BattleConditionValue(BattleConditionOperandValueKind.Scalar, default, value, default, null);
            }

            public static BattleConditionValue Bool(bool value)
            {
                return new BattleConditionValue(BattleConditionOperandValueKind.Bool, default, default, value, null);
            }

            public static BattleConditionValue Identifier(string value)
            {
                return new BattleConditionValue(BattleConditionOperandValueKind.Identifier, default, default, default, value);
            }
        }
    }
}
