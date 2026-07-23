using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    public enum BattleConditionMatchMode
    {
        All,
        Any
    }

    public enum BattleConditionSubject
    {
        Owner,
        Source,
        Target
    }

    public enum BattleConditionComparison
    {
        Equal,
        NotEqual,
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual
    }

    public enum BattleConditionOperandKind
    {
        LiteralInt,
        LiteralPercent,
        LiteralScalar,
        LiteralBool,
        LiteralIdentifier,
        HealthPercent,
        StatusCount,
        StatusStackCount,
        StatValue,
        DistanceBetween
    }

    public enum BattleConditionOperandValueKind
    {
        Int,
        Scalar,
        Bool,
        Identifier
    }

    public enum BattleStatusConditionFilterKind
    {
        Any,
        StatusId,
        Polarity
    }

    public sealed class BattleConditionDefinition
    {
        public BattleConditionDefinition(
            BattleConditionOperandDefinition left,
            BattleConditionComparison comparison,
            BattleConditionOperandDefinition right)
        {
            Left = BattleConditionOperandDefinition.CopyValidated(left);
            Comparison = ValidateComparison(comparison);
            Right = BattleConditionOperandDefinition.CopyValidated(right);
            ValidateOperandCompatibility(Left, Comparison, Right);
        }

        public BattleConditionOperandDefinition Left { get; }
        public BattleConditionComparison Comparison { get; }
        public BattleConditionOperandDefinition Right { get; }

        public static BattleConditionDefinition Compare(
            BattleConditionOperandDefinition left,
            BattleConditionComparison comparison,
            BattleConditionOperandDefinition right)
        {
            return new BattleConditionDefinition(left, comparison, right);
        }

        internal static BattleConditionDefinition CopyValidated(BattleConditionDefinition condition)
        {
            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition));
            }

            return new BattleConditionDefinition(condition.Left, condition.Comparison, condition.Right);
        }

        internal static BattleConditionComparison ValidateComparison(BattleConditionComparison comparison)
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

        private static void ValidateOperandCompatibility(
            BattleConditionOperandDefinition left,
            BattleConditionComparison comparison,
            BattleConditionOperandDefinition right)
        {
            if (left.ValueKind != right.ValueKind)
            {
                throw new ArgumentException("Condition operands must resolve to the same value kind.", nameof(right));
            }

            if ((left.ValueKind == BattleConditionOperandValueKind.Bool || left.ValueKind == BattleConditionOperandValueKind.Identifier)
                && comparison != BattleConditionComparison.Equal
                && comparison != BattleConditionComparison.NotEqual)
            {
                throw new ArgumentException("Boolean and identifier condition operands only support Equal and NotEqual comparisons.", nameof(comparison));
            }
        }
    }

    public abstract class BattleConditionOperandDefinition
    {
        protected BattleConditionOperandDefinition(BattleConditionOperandKind kind, BattleConditionOperandValueKind valueKind)
        {
            Kind = ValidateKind(kind);
            ValueKind = ValidateValueKind(valueKind);
        }

        public BattleConditionOperandKind Kind { get; }
        public BattleConditionOperandValueKind ValueKind { get; }

        public static BattleConditionOperandDefinition LiteralInt(int value)
        {
            return new BattleLiteralIntConditionOperandDefinition(value);
        }

        public static BattleConditionOperandDefinition LiteralPercentBasisPoints(int basisPoints)
        {
            return new BattleLiteralPercentConditionOperandDefinition(basisPoints);
        }

        public static BattleConditionOperandDefinition LiteralScalar(BattleScalar value)
        {
            return new BattleLiteralScalarConditionOperandDefinition(value);
        }

        public static BattleConditionOperandDefinition LiteralBool(bool value)
        {
            return new BattleLiteralBoolConditionOperandDefinition(value);
        }

        public static BattleConditionOperandDefinition LiteralIdentifier(string value)
        {
            return new BattleLiteralIdentifierConditionOperandDefinition(value);
        }

        public static BattleConditionOperandDefinition HealthPercent(BattleConditionSubject subject)
        {
            return new BattleHealthPercentConditionOperandDefinition(subject);
        }

        public static BattleConditionOperandDefinition StatusCount(BattleConditionSubject subject, BattleStatusConditionFilterDefinition filter)
        {
            return new BattleStatusCountConditionOperandDefinition(subject, filter);
        }

        public static BattleConditionOperandDefinition StatusStackCount(BattleConditionSubject subject, BattleStatusConditionFilterDefinition filter)
        {
            return new BattleStatusStackCountConditionOperandDefinition(subject, filter);
        }

        public static BattleConditionOperandDefinition StatValue(BattleConditionSubject subject, BattleStatId stat)
        {
            return new BattleStatValueConditionOperandDefinition(subject, stat);
        }

        public static BattleConditionOperandDefinition DistanceBetween(BattleConditionSubject subject, BattleConditionSubject otherSubject)
        {
            return new BattleDistanceBetweenConditionOperandDefinition(subject, otherSubject);
        }

        internal abstract BattleConditionOperandDefinition Copy();

        internal static BattleConditionOperandDefinition CopyValidated(BattleConditionOperandDefinition operand)
        {
            if (operand == null)
            {
                throw new ArgumentNullException(nameof(operand));
            }

            return operand.Copy();
        }

        internal static BattleConditionSubject ValidateSubject(BattleConditionSubject subject)
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

        internal static int ValidatePercentBasisPoints(int basisPoints)
        {
            if (basisPoints < 0 || basisPoints > 10000)
            {
                throw new ArgumentOutOfRangeException(nameof(basisPoints), basisPoints, "Percent literal must be between 0 and 10000 basis points.");
            }

            return basisPoints;
        }

        internal static BattleStatId ValidateStat(BattleStatId stat)
        {
            switch (stat)
            {
                case BattleStatId.MaxHealth:
                case BattleStatId.MoveSpeed:
                    return stat;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unsupported battle stat id.");
            }
        }

        private static BattleConditionOperandKind ValidateKind(BattleConditionOperandKind kind)
        {
            switch (kind)
            {
                case BattleConditionOperandKind.LiteralInt:
                case BattleConditionOperandKind.LiteralPercent:
                case BattleConditionOperandKind.LiteralScalar:
                case BattleConditionOperandKind.LiteralBool:
                case BattleConditionOperandKind.LiteralIdentifier:
                case BattleConditionOperandKind.HealthPercent:
                case BattleConditionOperandKind.StatusCount:
                case BattleConditionOperandKind.StatusStackCount:
                case BattleConditionOperandKind.StatValue:
                case BattleConditionOperandKind.DistanceBetween:
                    return kind;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported battle condition operand kind.");
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

    public sealed class BattleLiteralIntConditionOperandDefinition : BattleConditionOperandDefinition
    {
        public BattleLiteralIntConditionOperandDefinition(int value)
            : base(BattleConditionOperandKind.LiteralInt, BattleConditionOperandValueKind.Int)
        {
            Value = value;
        }

        public int Value { get; }

        internal override BattleConditionOperandDefinition Copy()
        {
            return new BattleLiteralIntConditionOperandDefinition(Value);
        }
    }

    public sealed class BattleLiteralPercentConditionOperandDefinition : BattleConditionOperandDefinition
    {
        public BattleLiteralPercentConditionOperandDefinition(int basisPoints)
            : base(BattleConditionOperandKind.LiteralPercent, BattleConditionOperandValueKind.Scalar)
        {
            BasisPoints = ValidatePercentBasisPoints(basisPoints);
        }

        public int BasisPoints { get; }

        internal override BattleConditionOperandDefinition Copy()
        {
            return new BattleLiteralPercentConditionOperandDefinition(BasisPoints);
        }
    }

    public sealed class BattleLiteralScalarConditionOperandDefinition : BattleConditionOperandDefinition
    {
        public BattleLiteralScalarConditionOperandDefinition(BattleScalar value)
            : base(BattleConditionOperandKind.LiteralScalar, BattleConditionOperandValueKind.Scalar)
        {
            Value = value;
        }

        public BattleScalar Value { get; }

        internal override BattleConditionOperandDefinition Copy()
        {
            return new BattleLiteralScalarConditionOperandDefinition(Value);
        }
    }

    public sealed class BattleLiteralBoolConditionOperandDefinition : BattleConditionOperandDefinition
    {
        public BattleLiteralBoolConditionOperandDefinition(bool value)
            : base(BattleConditionOperandKind.LiteralBool, BattleConditionOperandValueKind.Bool)
        {
            Value = value;
        }

        public bool Value { get; }

        internal override BattleConditionOperandDefinition Copy()
        {
            return new BattleLiteralBoolConditionOperandDefinition(Value);
        }
    }

    public sealed class BattleLiteralIdentifierConditionOperandDefinition : BattleConditionOperandDefinition
    {
        public BattleLiteralIdentifierConditionOperandDefinition(string value)
            : base(BattleConditionOperandKind.LiteralIdentifier, BattleConditionOperandValueKind.Identifier)
        {
            Value = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Identifier literal is required.", nameof(value)) : value;
        }

        public string Value { get; }

        internal override BattleConditionOperandDefinition Copy()
        {
            return new BattleLiteralIdentifierConditionOperandDefinition(Value);
        }
    }

    public sealed class BattleHealthPercentConditionOperandDefinition : BattleConditionOperandDefinition
    {
        public BattleHealthPercentConditionOperandDefinition(BattleConditionSubject subject)
            : base(BattleConditionOperandKind.HealthPercent, BattleConditionOperandValueKind.Scalar)
        {
            Subject = ValidateSubject(subject);
        }

        public BattleConditionSubject Subject { get; }

        internal override BattleConditionOperandDefinition Copy()
        {
            return new BattleHealthPercentConditionOperandDefinition(Subject);
        }
    }

    public sealed class BattleStatusCountConditionOperandDefinition : BattleConditionOperandDefinition
    {
        public BattleStatusCountConditionOperandDefinition(BattleConditionSubject subject, BattleStatusConditionFilterDefinition filter)
            : base(BattleConditionOperandKind.StatusCount, BattleConditionOperandValueKind.Int)
        {
            Subject = ValidateSubject(subject);
            Filter = BattleStatusConditionFilterDefinition.CopyValidated(filter);
        }

        public BattleConditionSubject Subject { get; }
        public BattleStatusConditionFilterDefinition Filter { get; }

        internal override BattleConditionOperandDefinition Copy()
        {
            return new BattleStatusCountConditionOperandDefinition(Subject, Filter);
        }
    }

    public sealed class BattleStatusStackCountConditionOperandDefinition : BattleConditionOperandDefinition
    {
        public BattleStatusStackCountConditionOperandDefinition(BattleConditionSubject subject, BattleStatusConditionFilterDefinition filter)
            : base(BattleConditionOperandKind.StatusStackCount, BattleConditionOperandValueKind.Int)
        {
            Subject = ValidateSubject(subject);
            Filter = BattleStatusConditionFilterDefinition.CopyValidated(filter);
        }

        public BattleConditionSubject Subject { get; }
        public BattleStatusConditionFilterDefinition Filter { get; }

        internal override BattleConditionOperandDefinition Copy()
        {
            return new BattleStatusStackCountConditionOperandDefinition(Subject, Filter);
        }
    }

    public sealed class BattleStatValueConditionOperandDefinition : BattleConditionOperandDefinition
    {
        public BattleStatValueConditionOperandDefinition(BattleConditionSubject subject, BattleStatId stat)
            : base(BattleConditionOperandKind.StatValue, BattleConditionOperandValueKind.Scalar)
        {
            Subject = ValidateSubject(subject);
            Stat = ValidateStat(stat);
        }

        public BattleConditionSubject Subject { get; }
        public BattleStatId Stat { get; }

        internal override BattleConditionOperandDefinition Copy()
        {
            return new BattleStatValueConditionOperandDefinition(Subject, Stat);
        }
    }

    public sealed class BattleDistanceBetweenConditionOperandDefinition : BattleConditionOperandDefinition
    {
        public BattleDistanceBetweenConditionOperandDefinition(BattleConditionSubject subject, BattleConditionSubject otherSubject)
            : base(BattleConditionOperandKind.DistanceBetween, BattleConditionOperandValueKind.Scalar)
        {
            Subject = ValidateSubject(subject);
            OtherSubject = ValidateSubject(otherSubject);
        }

        public BattleConditionSubject Subject { get; }
        public BattleConditionSubject OtherSubject { get; }

        internal override BattleConditionOperandDefinition Copy()
        {
            return new BattleDistanceBetweenConditionOperandDefinition(Subject, OtherSubject);
        }
    }

    public abstract class BattleStatusConditionFilterDefinition
    {
        protected BattleStatusConditionFilterDefinition(BattleStatusConditionFilterKind kind)
        {
            Kind = ValidateKind(kind);
        }

        public BattleStatusConditionFilterKind Kind { get; }

        public static BattleStatusConditionFilterDefinition Any()
        {
            return new BattleAnyStatusConditionFilterDefinition();
        }

        public static BattleStatusConditionFilterDefinition StatusId(string statusId)
        {
            return new BattleStatusIdConditionFilterDefinition(statusId);
        }

        public static BattleStatusConditionFilterDefinition Polarity(StatusPolarity polarity)
        {
            return new BattleStatusPolarityConditionFilterDefinition(polarity);
        }

        internal abstract BattleStatusConditionFilterDefinition Copy();

        internal static BattleStatusConditionFilterDefinition CopyValidated(BattleStatusConditionFilterDefinition filter)
        {
            if (filter == null)
            {
                return Any();
            }

            return filter.Copy();
        }

        internal static StatusPolarity ValidatePolarity(StatusPolarity polarity)
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

        private static BattleStatusConditionFilterKind ValidateKind(BattleStatusConditionFilterKind kind)
        {
            switch (kind)
            {
                case BattleStatusConditionFilterKind.Any:
                case BattleStatusConditionFilterKind.StatusId:
                case BattleStatusConditionFilterKind.Polarity:
                    return kind;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported status condition filter kind.");
            }
        }
    }

    public sealed class BattleAnyStatusConditionFilterDefinition : BattleStatusConditionFilterDefinition
    {
        public BattleAnyStatusConditionFilterDefinition()
            : base(BattleStatusConditionFilterKind.Any)
        {
        }

        internal override BattleStatusConditionFilterDefinition Copy()
        {
            return new BattleAnyStatusConditionFilterDefinition();
        }
    }

    public sealed class BattleStatusIdConditionFilterDefinition : BattleStatusConditionFilterDefinition
    {
        public BattleStatusIdConditionFilterDefinition(string statusId)
            : base(BattleStatusConditionFilterKind.StatusId)
        {
            Id = string.IsNullOrWhiteSpace(statusId) ? throw new ArgumentException("Status filter id is required.", nameof(statusId)) : statusId;
        }

        public string Id { get; }

        internal override BattleStatusConditionFilterDefinition Copy()
        {
            return new BattleStatusIdConditionFilterDefinition(Id);
        }
    }

    public sealed class BattleStatusPolarityConditionFilterDefinition : BattleStatusConditionFilterDefinition
    {
        public BattleStatusPolarityConditionFilterDefinition(StatusPolarity polarity)
            : base(BattleStatusConditionFilterKind.Polarity)
        {
            StatusPolarity = ValidatePolarity(polarity);
        }

        public StatusPolarity StatusPolarity { get; }

        internal override BattleStatusConditionFilterDefinition Copy()
        {
            return new BattleStatusPolarityConditionFilterDefinition(StatusPolarity);
        }
    }

    public sealed class BattleConditionGroup
    {
        private readonly BattleConditionDefinition[] _conditions;
        private readonly ReadOnlyCollection<BattleConditionDefinition> _readOnlyConditions;

        public BattleConditionGroup(BattleConditionMatchMode matchMode, IReadOnlyList<BattleConditionDefinition> conditions)
        {
            MatchMode = ValidateMatchMode(matchMode);
            _conditions = CopyConditions(conditions);
            _readOnlyConditions = new ReadOnlyCollection<BattleConditionDefinition>(_conditions);
        }

        public BattleConditionMatchMode MatchMode { get; }
        public IReadOnlyList<BattleConditionDefinition> Conditions => _readOnlyConditions;
        public bool HasConditions => _conditions.Length > 0;

        public static BattleConditionGroup Empty { get; } = new BattleConditionGroup(BattleConditionMatchMode.All, Array.Empty<BattleConditionDefinition>());

        internal static BattleConditionGroup CopyValidated(BattleConditionGroup group)
        {
            if (group == null)
            {
                return Empty;
            }

            return group.HasConditions
                ? new BattleConditionGroup(group.MatchMode, group.Conditions)
                : Empty;
        }

        internal static BattleConditionMatchMode ValidateMatchMode(BattleConditionMatchMode matchMode)
        {
            switch (matchMode)
            {
                case BattleConditionMatchMode.All:
                case BattleConditionMatchMode.Any:
                    return matchMode;
                default:
                    throw new ArgumentOutOfRangeException(nameof(matchMode), matchMode, "Unsupported battle condition match mode.");
            }
        }

        private static BattleConditionDefinition[] CopyConditions(IReadOnlyList<BattleConditionDefinition> conditions)
        {
            if (conditions == null)
            {
                throw new ArgumentNullException(nameof(conditions));
            }

            var copy = new BattleConditionDefinition[conditions.Count];
            for (var i = 0; i < conditions.Count; i++)
            {
                copy[i] = BattleConditionDefinition.CopyValidated(conditions[i]);
            }

            return copy;
        }
    }
}
