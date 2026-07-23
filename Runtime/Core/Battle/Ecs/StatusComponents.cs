using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    internal readonly struct BattleModifierInstance
    {
        private BattleModifierInstance(
            BattleModifierTarget target,
            BattleStatId stat,
            BattleDamageModifierStat damageStat,
            BattleModifierOperation operation,
            BattleScalar value)
        {
            Target = target;
            StatId = stat;
            DamageStat = damageStat;
            Operation = operation;
            Value = value;
        }

        public BattleModifierTarget Target { get; }
        public BattleStatId StatId { get; }
        public BattleDamageModifierStat DamageStat { get; }
        public BattleModifierOperation Operation { get; }
        public BattleScalar Value { get; }

        public static BattleModifierInstance Stat(BattleStatId stat, BattleModifierOperation operation, BattleScalar value)
        {
            return new BattleModifierInstance(BattleModifierTarget.Stat, ValidateStatModifierStat(stat), default, operation, value);
        }

        public static BattleModifierInstance Damage(BattleDamageModifierStat damageStat, BattleModifierOperation operation, BattleScalar value)
        {
            return new BattleModifierInstance(BattleModifierTarget.Damage, default, damageStat, operation, value);
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

    internal readonly struct BattleReactionEffectInstance
    {
        private BattleReactionEffectInstance(BattleReactionTarget target, BattleEffectData effect)
        {
            Target = target;
            Effect = effect;
        }

        public BattleReactionTarget Target { get; }
        public BattleEffectData Effect { get; }

        public static BattleReactionEffectInstance Create(BattleReactionTarget target, BattleEffectData effect)
        {
            return new BattleReactionEffectInstance(ValidateTarget(target), BattleEffectData.CopyValidated(effect));
        }

        private static BattleReactionTarget ValidateTarget(BattleReactionTarget target)
        {
            switch (target)
            {
                case BattleReactionTarget.Self:
                case BattleReactionTarget.Source:
                case BattleReactionTarget.Target:
                    return target;
                default:
                    throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported battle reaction target.");
            }
        }
    }

    internal readonly struct BattleTriggerInstance
    {
        private readonly BattleReactionEffectInstance[] _effects;
        private readonly ReadOnlyCollection<BattleReactionEffectInstance> _readOnlyEffects;

        public BattleTriggerInstance(BattleTriggerTiming timing, IReadOnlyList<BattleReactionEffectInstance> effects)
            : this(timing, BattleConditionProgram.AlwaysTrue, effects)
        {
        }

        public BattleTriggerInstance(BattleTriggerTiming timing, BattleConditionGroup conditions, IReadOnlyList<BattleReactionEffectInstance> effects)
            : this(timing, BattleConditionCompiler.Compile(conditions), effects)
        {
        }

        public BattleTriggerInstance(BattleTriggerTiming timing, BattleConditionProgram conditionProgram, IReadOnlyList<BattleReactionEffectInstance> effects)
        {
            Timing = ValidateTiming(timing);
            ConditionProgram = conditionProgram ?? BattleConditionProgram.AlwaysTrue;
            _effects = CopyEffects(effects);
            _readOnlyEffects = new ReadOnlyCollection<BattleReactionEffectInstance>(_effects);
        }

        public BattleTriggerTiming Timing { get; }
        public BattleConditionProgram ConditionProgram { get; }
        public IReadOnlyList<BattleReactionEffectInstance> Effects => _readOnlyEffects ?? EmptyEffects;

        private static readonly ReadOnlyCollection<BattleReactionEffectInstance> EmptyEffects = new ReadOnlyCollection<BattleReactionEffectInstance>(Array.Empty<BattleReactionEffectInstance>());

        private static BattleReactionEffectInstance[] CopyEffects(IReadOnlyList<BattleReactionEffectInstance> effects)
        {
            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            var copy = new BattleReactionEffectInstance[effects.Count];
            for (var i = 0; i < effects.Count; i++)
            {
                BattleReactionEffectInstance effect = effects[i];
                copy[i] = BattleReactionEffectInstance.Create(effect.Target, effect.Effect);
            }

            return copy;
        }

        private static BattleTriggerTiming ValidateTiming(BattleTriggerTiming timing)
        {
            switch (timing)
            {
                case BattleTriggerTiming.AfterDamageDealt:
                case BattleTriggerTiming.AfterDamageTaken:
                case BattleTriggerTiming.AfterEnemyKilled:
                    return timing;
                default:
                    throw new ArgumentOutOfRangeException(nameof(timing), timing, "Unsupported battle trigger timing.");
            }
        }
    }

    internal readonly struct StatusInstance
    {
        public StatusInstance(StatusRuntimeDefinition definition, EntityId source, int durationRemainingTicks, int ticksUntilNextPeriodicEffect)
            : this(definition, source, durationRemainingTicks, ticksUntilNextPeriodicEffect, stackCount: 1)
        {
        }

        public StatusInstance(StatusRuntimeDefinition definition, EntityId source, int durationRemainingTicks, int ticksUntilNextPeriodicEffect, int stackCount)
        {
            Definition = definition;
            Source = source.IsValid ? source : throw new ArgumentException("Status source must be valid.", nameof(source));
            DurationRemainingTicks = durationRemainingTicks > 0 ? durationRemainingTicks : throw new ArgumentOutOfRangeException(nameof(durationRemainingTicks));
            TicksUntilNextPeriodicEffect = ticksUntilNextPeriodicEffect > 0 ? ticksUntilNextPeriodicEffect : throw new ArgumentOutOfRangeException(nameof(ticksUntilNextPeriodicEffect));
            StackCount = stackCount > 0 && stackCount <= definition.MaxStacks ? stackCount : throw new ArgumentOutOfRangeException(nameof(stackCount));
        }

        public StatusInstance(
            string id,
            StatusPolarity polarity,
            EntityId source,
            int durationRemainingTicks,
            int tickIntervalTicks,
            int ticksUntilNextPeriodicEffect,
            int periodicDamage,
            IReadOnlyList<BattleModifierInstance> modifiers,
            IReadOnlyList<BattleTriggerInstance> triggers)
            : this(id, polarity, source, durationRemainingTicks, tickIntervalTicks, ticksUntilNextPeriodicEffect, periodicDamage, modifiers, triggers, stackCount: 1, maxStacks: 1)
        {
        }

        public StatusInstance(
            string id,
            StatusPolarity polarity,
            EntityId source,
            int durationRemainingTicks,
            int tickIntervalTicks,
            int ticksUntilNextPeriodicEffect,
            int periodicDamage,
            IReadOnlyList<BattleModifierInstance> modifiers,
            IReadOnlyList<BattleTriggerInstance> triggers,
            int stackCount,
            int maxStacks)
            : this(
                new StatusRuntimeDefinition(
                    id,
                    polarity,
                    tickIntervalTicks,
                    periodicDamage,
                    maxStacks,
                    StatusStackPolicy.RefreshDurationAndAddStack,
                    modifiers,
                    triggers),
                source,
                durationRemainingTicks,
                ticksUntilNextPeriodicEffect,
                stackCount)
        {
        }

        public StatusRuntimeDefinition Definition { get; }
        public string Id => Definition.Id;
        public StatusPolarity Polarity => Definition.Polarity;
        public EntityId Source { get; }
        public int DurationRemainingTicks { get; }
        public int TickIntervalTicks => Definition.TickIntervalTicks;
        public int TicksUntilNextPeriodicEffect { get; }
        public int PeriodicDamage => Definition.PeriodicDamage;
        public int StackCount { get; }
        public int MaxStacks => Definition.MaxStacks;
        public StatusStackPolicy StackPolicy => Definition.StackPolicy;
        public IReadOnlyList<BattleModifierInstance> Modifiers => Definition.Modifiers;
        public IReadOnlyList<BattleTriggerInstance> Triggers => Definition.Triggers;

        public StatusInstance WithTiming(int durationRemainingTicks, int ticksUntilNextPeriodicEffect)
        {
            return new StatusInstance(Definition, Source, durationRemainingTicks, ticksUntilNextPeriodicEffect, StackCount);
        }

        public StatusInstance WithStackCount(int stackCount)
        {
            return new StatusInstance(Definition, Source, DurationRemainingTicks, TicksUntilNextPeriodicEffect, stackCount);
        }
    }

    internal readonly struct StatusComponent
    {
        private readonly StatusInstance[] _statuses;
        private readonly ReadOnlyCollection<StatusInstance> _readOnlyStatuses;

        public StatusComponent(IReadOnlyList<StatusInstance> statuses)
        {
            if (statuses == null)
            {
                throw new ArgumentNullException(nameof(statuses));
            }

            _statuses = new StatusInstance[statuses.Count];
            for (var i = 0; i < statuses.Count; i++)
            {
                StatusInstance status = statuses[i];
                _statuses[i] = new StatusInstance(status.Definition, status.Source, status.DurationRemainingTicks, status.TicksUntilNextPeriodicEffect, status.StackCount);
            }

            _readOnlyStatuses = new ReadOnlyCollection<StatusInstance>(_statuses);
        }

        public IReadOnlyList<StatusInstance> Statuses => _readOnlyStatuses ?? EmptyStatuses;

        private static readonly ReadOnlyCollection<StatusInstance> EmptyStatuses = new ReadOnlyCollection<StatusInstance>(Array.Empty<StatusInstance>());

        public StatusComponent WithStatuses(IReadOnlyList<StatusInstance> statuses)
        {
            return new StatusComponent(statuses);
        }
    }
}
