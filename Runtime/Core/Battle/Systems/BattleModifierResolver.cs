using System;
using System.Collections.Generic;

namespace Combat.Core.Battle
{
    internal static class BattleModifierResolver
    {
        public static BattleScalar ResolveScalarStat(BattleScalar baseValue, IReadOnlyList<StatusInstance> ownerStatuses, BattleStatId stat)
        {
            if (ownerStatuses == null)
            {
                throw new ArgumentNullException(nameof(ownerStatuses));
            }

            BattleScalar result = Resolve(baseValue, ownerStatuses, BattleModifierTarget.Stat, stat, default);
            if (stat == BattleStatId.MoveSpeed && result < BattleScalar.Zero)
            {
                return BattleScalar.Zero;
            }

            if (stat == BattleStatId.MaxHealth && result < BattleScalar.One)
            {
                return BattleScalar.One;
            }

            return result;
        }

        public static int ResolveDamage(
            int baseDamage,
            IReadOnlyList<StatusInstance> sourceStatuses,
            IReadOnlyList<StatusInstance> targetStatuses,
            BattleEffectContext context)
        {
            if (sourceStatuses == null)
            {
                throw new ArgumentNullException(nameof(sourceStatuses));
            }

            if (targetStatuses == null)
            {
                throw new ArgumentNullException(nameof(targetStatuses));
            }

            if (baseDamage <= 0)
            {
                return 0;
            }

            BattleScalar damage = BattleScalar.FromInt(baseDamage);
            damage = Resolve(damage, sourceStatuses, BattleModifierTarget.Damage, default, BattleDamageModifierStat.DamageDealt);
            damage = Resolve(damage, targetStatuses, BattleModifierTarget.Damage, default, BattleDamageModifierStat.DamageTaken);
            return damage.ToIntRoundHalfUpSaturating();
        }

        private static BattleScalar Resolve(
            BattleScalar baseValue,
            IReadOnlyList<StatusInstance> statuses,
            BattleModifierTarget target,
            BattleStatId stat,
            BattleDamageModifierStat damageStat)
        {
            BattleScalar flat = BattleScalar.Zero;
            BattleScalar percentAdd = BattleScalar.Zero;
            BattleScalar overrideValue = BattleScalar.Zero;
            BattleScalar minClamp = BattleScalar.Zero;
            BattleScalar maxClamp = BattleScalar.Zero;
            var hasOverride = false;
            var hasMinClamp = false;
            var hasMaxClamp = false;

            for (var statusIndex = 0; statusIndex < statuses.Count; statusIndex++)
            {
                StatusInstance status = statuses[statusIndex];
                IReadOnlyList<BattleModifierInstance> modifiers = status.Modifiers;
                for (var modifierIndex = 0; modifierIndex < modifiers.Count; modifierIndex++)
                {
                    BattleModifierInstance modifier = modifiers[modifierIndex];
                    if (!Matches(modifier, target, stat, damageStat))
                    {
                        continue;
                    }

                    switch (modifier.Operation)
                    {
                        case BattleModifierOperation.Flat:
                            flat += modifier.Value * BattleScalar.FromInt(status.StackCount);
                            break;
                        case BattleModifierOperation.PercentAdd:
                            percentAdd += modifier.Value * BattleScalar.FromInt(status.StackCount);
                            break;
                        case BattleModifierOperation.Override:
                            if (hasOverride)
                            {
                                throw new InvalidOperationException("Multiple override battle modifiers are not supported.");
                            }

                            overrideValue = modifier.Value;
                            hasOverride = true;
                            break;
                        case BattleModifierOperation.MinClamp:
                            if (!hasMinClamp || modifier.Value > minClamp)
                            {
                                minClamp = modifier.Value;
                            }

                            hasMinClamp = true;
                            break;
                        case BattleModifierOperation.MaxClamp:
                            if (!hasMaxClamp || modifier.Value < maxClamp)
                            {
                                maxClamp = modifier.Value;
                            }

                            hasMaxClamp = true;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(modifier), modifier.Operation, "Unsupported battle modifier operation.");
                    }
                }
            }

            if (hasMinClamp && hasMaxClamp && minClamp > maxClamp)
            {
                throw new InvalidOperationException("Battle modifier min clamp cannot be greater than max clamp.");
            }

            BattleScalar result = (baseValue + flat) * (BattleScalar.One + percentAdd);
            if (hasOverride)
            {
                result = overrideValue;
            }

            if (hasMinClamp && result < minClamp)
            {
                result = minClamp;
            }

            if (hasMaxClamp && result > maxClamp)
            {
                result = maxClamp;
            }

            return result;
        }

        private static bool Matches(
            BattleModifierInstance modifier,
            BattleModifierTarget target,
            BattleStatId stat,
            BattleDamageModifierStat damageStat)
        {
            if (modifier.Target != target)
            {
                return false;
            }

            return target == BattleModifierTarget.Stat
                ? modifier.StatId == stat
                : modifier.DamageStat == damageStat;
        }
    }

}
