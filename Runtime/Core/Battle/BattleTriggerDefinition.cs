using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    public enum BattleTriggerTiming
    {
        AfterDamageDealt,
        AfterDamageTaken,
        AfterEnemyKilled
    }

    public enum BattleReactionTarget
    {
        Self,
        Source,
        Target
    }

    public enum BattleEffectType
    {
        Damage,
        ApplyStatus,
        SpawnProjectileEmitter,
        Heal,
        AreaEffect
    }

    public sealed class BattleEffectDefinition
    {
        private BattleEffectDefinition(BattleEffectType type, int amount, StatusDefinition status, ProjectileEmitterSpawnData projectileEmitter, AreaEffectDefinition areaEffect)
        {
            Type = type;
            Amount = amount;
            Status = status;
            ProjectileEmitter = projectileEmitter;
            Area = areaEffect;
        }

        public BattleEffectType Type { get; }
        public int Amount { get; }
        public StatusDefinition Status { get; }
        public ProjectileEmitterSpawnData ProjectileEmitter { get; }
        public AreaEffectDefinition Area { get; }

        public static BattleEffectDefinition Damage(int amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            return new BattleEffectDefinition(BattleEffectType.Damage, amount, null, default, null);
        }

        public static BattleEffectDefinition Heal(int amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            return new BattleEffectDefinition(BattleEffectType.Heal, amount, null, default, null);
        }

        public static BattleEffectDefinition ApplyStatus(StatusDefinition status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            return new BattleEffectDefinition(BattleEffectType.ApplyStatus, 0, status, default, null);
        }

        public static BattleEffectDefinition SpawnProjectileEmitter(ProjectileEmitterSpawnData projectileEmitter)
        {
            ProjectileEmitterSpawnData copy = CopyProjectileEmitter(projectileEmitter);
            return new BattleEffectDefinition(BattleEffectType.SpawnProjectileEmitter, 0, null, copy, null);
        }

        public static BattleEffectDefinition AreaEffect(AreaEffectDefinition areaEffect)
        {
            AreaEffectDefinition copy = AreaEffectDefinition.CopyValidated(areaEffect);
            return new BattleEffectDefinition(BattleEffectType.AreaEffect, 0, null, default, copy);
        }

        internal static BattleEffectDefinition CopyValidated(BattleEffectDefinition effect)
        {
            if (effect == null)
            {
                throw new ArgumentNullException(nameof(effect));
            }

            switch (effect.Type)
            {
                case BattleEffectType.Damage:
                    return Damage(effect.Amount);
                case BattleEffectType.Heal:
                    return Heal(effect.Amount);
                case BattleEffectType.ApplyStatus:
                    return ApplyStatus(effect.Status);
                case BattleEffectType.SpawnProjectileEmitter:
                    return SpawnProjectileEmitter(effect.ProjectileEmitter);
                case BattleEffectType.AreaEffect:
                    return AreaEffect(effect.Area);
                default:
                    throw new ArgumentOutOfRangeException(nameof(effect), effect.Type, "Unsupported battle effect type.");
            }
        }

        private static ProjectileEmitterSpawnData CopyProjectileEmitter(ProjectileEmitterSpawnData projectileEmitter)
        {
            return new ProjectileEmitterSpawnData(
                projectileEmitter.AnchorMode,
                projectileEmitter.AnchorOffset,
                projectileEmitter.DurationTicks,
                projectileEmitter.FireIntervalTicks,
                projectileEmitter.Pattern,
                projectileEmitter.ProjectilePayload);
        }
    }

    public sealed class BattleReactionEffectDefinition
    {
        private BattleReactionEffectDefinition(BattleReactionTarget target, BattleEffectDefinition effect)
        {
            Target = target;
            Effect = effect;
        }

        public BattleReactionTarget Target { get; }
        public BattleEffectDefinition Effect { get; }

        public static BattleReactionEffectDefinition Create(BattleReactionTarget target, BattleEffectDefinition effect)
        {
            return new BattleReactionEffectDefinition(ValidateTarget(target), BattleEffectDefinition.CopyValidated(effect));
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

    public sealed class BattleTriggerDefinition
    {
        private readonly BattleReactionEffectDefinition[] _effects;
        private readonly ReadOnlyCollection<BattleReactionEffectDefinition> _readOnlyEffects;

        public BattleTriggerDefinition(BattleTriggerTiming timing, IReadOnlyList<BattleReactionEffectDefinition> effects)
            : this(timing, BattleConditionProgram.AlwaysTrue, effects)
        {
        }

        public BattleTriggerDefinition(BattleTriggerTiming timing, BattleConditionGroup conditions, IReadOnlyList<BattleReactionEffectDefinition> effects)
            : this(timing, BattleConditionCompiler.Compile(conditions), effects)
        {
        }

        public BattleTriggerDefinition(BattleTriggerTiming timing, BattleConditionProgram conditionProgram, IReadOnlyList<BattleReactionEffectDefinition> effects)
        {
            Timing = ValidateTiming(timing);
            ConditionProgram = conditionProgram ?? BattleConditionProgram.AlwaysTrue;
            _effects = CopyEffects(effects);
            _readOnlyEffects = new ReadOnlyCollection<BattleReactionEffectDefinition>(_effects);
        }

        public BattleTriggerTiming Timing { get; }
        public BattleConditionProgram ConditionProgram { get; }
        public IReadOnlyList<BattleReactionEffectDefinition> Effects => _readOnlyEffects;

        private static BattleReactionEffectDefinition[] CopyEffects(IReadOnlyList<BattleReactionEffectDefinition> effects)
        {
            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            var copy = new BattleReactionEffectDefinition[effects.Count];
            for (var i = 0; i < effects.Count; i++)
            {
                copy[i] = effects[i] ?? throw new ArgumentNullException(nameof(effects));
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
}
