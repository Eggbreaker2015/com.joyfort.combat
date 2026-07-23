using System;

namespace Combat.Core.Battle
{
    public enum BattleModifierTarget
    {
        Damage,
        Stat
    }

    public enum BattleDamageModifierStat
    {
        DamageDealt,
        DamageTaken
    }

    public enum BattleModifierOperation
    {
        Flat,
        PercentAdd,
        Override,
        MinClamp,
        MaxClamp
    }

    public sealed class BattleModifierDefinition
    {
        private BattleModifierDefinition(
            BattleModifierTarget target,
            BattleStatId stat,
            BattleDamageModifierStat damageStat,
            BattleModifierOperation operation,
            BattleScalar value)
        {
            Target = ValidateTarget(target);
            StatId = stat;
            DamageStat = damageStat;
            Operation = ValidateOperation(operation);
            Value = value;
        }

        public BattleModifierTarget Target { get; }
        public BattleStatId StatId { get; }
        public BattleDamageModifierStat DamageStat { get; }
        public BattleModifierOperation Operation { get; }
        public BattleScalar Value { get; }

        public static BattleModifierDefinition Stat(BattleStatId stat, BattleModifierOperation operation, BattleScalar value)
        {
            return new BattleModifierDefinition(BattleModifierTarget.Stat, ValidateStatModifierStat(stat), default, operation, value);
        }

        public static BattleModifierDefinition Damage(BattleDamageModifierStat damageStat, BattleModifierOperation operation, BattleScalar value)
        {
            return new BattleModifierDefinition(BattleModifierTarget.Damage, default, damageStat, operation, value);
        }

        private static BattleModifierTarget ValidateTarget(BattleModifierTarget target)
        {
            switch (target)
            {
                case BattleModifierTarget.Stat:
                case BattleModifierTarget.Damage:
                    return target;
                default:
                    throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported battle modifier target.");
            }
        }

        private static BattleModifierOperation ValidateOperation(BattleModifierOperation operation)
        {
            switch (operation)
            {
                case BattleModifierOperation.Flat:
                case BattleModifierOperation.PercentAdd:
                case BattleModifierOperation.Override:
                case BattleModifierOperation.MinClamp:
                case BattleModifierOperation.MaxClamp:
                    return operation;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported battle modifier operation.");
            }
        }

        private static BattleStatId ValidateStatModifierStat(BattleStatId stat)
        {
            switch (stat)
            {
                case BattleStatId.MaxHealth:
                case BattleStatId.MoveSpeed:
                    return stat;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unsupported battle stat modifier stat.");
            }
        }
    }
}
