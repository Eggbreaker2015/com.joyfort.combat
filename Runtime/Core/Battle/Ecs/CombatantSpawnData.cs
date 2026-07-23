using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    internal readonly struct BattleModifierData
    {
        private BattleModifierData(
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

        public static BattleModifierData Stat(BattleStatId stat, BattleModifierOperation operation, BattleScalar value)
        {
            return new BattleModifierData(BattleModifierTarget.Stat, ValidateStatModifierStat(stat), default, operation, value);
        }

        public static BattleModifierData Damage(BattleDamageModifierStat damageStat, BattleModifierOperation operation, BattleScalar value)
        {
            return new BattleModifierData(BattleModifierTarget.Damage, default, damageStat, operation, value);
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

    internal readonly struct AreaEffectData
    {
        private readonly BattleEffectData[] _effects;
        private readonly ReadOnlyCollection<BattleEffectData> _readOnlyEffects;

        public AreaEffectData(BattleScalar radius, AreaEffectTargetFilter targetFilter, IReadOnlyList<BattleEffectData> effects)
        {
            if (radius <= BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            Radius = radius;
            TargetFilter = ValidateTargetFilter(targetFilter);
            _effects = CopyEffects(effects);
            _readOnlyEffects = new ReadOnlyCollection<BattleEffectData>(_effects);
        }

        public BattleScalar Radius { get; }
        public AreaEffectTargetFilter TargetFilter { get; }
        public IReadOnlyList<BattleEffectData> Effects => _readOnlyEffects ?? EmptyEffects;

        private static readonly ReadOnlyCollection<BattleEffectData> EmptyEffects = new ReadOnlyCollection<BattleEffectData>(Array.Empty<BattleEffectData>());

        public static AreaEffectData CopyValidated(AreaEffectData areaEffect)
        {
            return new AreaEffectData(areaEffect.Radius, areaEffect.TargetFilter, areaEffect.Effects);
        }

        private static AreaEffectTargetFilter ValidateTargetFilter(AreaEffectTargetFilter targetFilter)
        {
            switch (targetFilter)
            {
                case AreaEffectTargetFilter.Allies:
                case AreaEffectTargetFilter.Enemies:
                case AreaEffectTargetFilter.AllUnits:
                    return targetFilter;
                default:
                    throw new ArgumentOutOfRangeException(nameof(targetFilter), targetFilter, "Unsupported area effect target filter.");
            }
        }

        private static BattleEffectData[] CopyEffects(IReadOnlyList<BattleEffectData> effects)
        {
            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            if (effects.Count == 0)
            {
                throw new ArgumentException("Area effect requires at least one child effect.", nameof(effects));
            }

            var copy = new BattleEffectData[effects.Count];
            for (var i = 0; i < effects.Count; i++)
            {
                BattleEffectData effect = effects[i];
                if (effect.Type == BattleEffectType.AreaEffect)
                {
                    throw new ArgumentException("Area effect cannot contain another AreaEffect child.", nameof(effects));
                }

                copy[i] = BattleEffectData.CopyValidated(effect);
            }

            return copy;
        }
    }

    internal readonly struct BattleEffectData
    {
        private BattleEffectData(BattleEffectType type, int amount, StatusApplicationData status, ProjectileEmitterSpawnData projectileEmitter, AreaEffectData areaEffect)
        {
            Type = type;
            Amount = amount;
            Status = status;
            ProjectileEmitter = projectileEmitter;
            AreaEffect = areaEffect;
        }

        public BattleEffectType Type { get; }
        public int Amount { get; }
        public StatusApplicationData Status { get; }
        public ProjectileEmitterSpawnData ProjectileEmitter { get; }
        public AreaEffectData AreaEffect { get; }

        public static BattleEffectData Damage(int amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            return new BattleEffectData(BattleEffectType.Damage, amount, default, default, default);
        }

        public static BattleEffectData Heal(int amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            return new BattleEffectData(BattleEffectType.Heal, amount, default, default, default);
        }

        public static BattleEffectData ApplyStatus(StatusApplicationData status)
        {
            if (string.IsNullOrWhiteSpace(status.Id))
            {
                throw new ArgumentException("Effect status is required.", nameof(status));
            }

            return new BattleEffectData(BattleEffectType.ApplyStatus, 0, CopyStatus(status), default, default);
        }

        public static BattleEffectData SpawnProjectileEmitter(ProjectileEmitterSpawnData projectileEmitter)
        {
            return new BattleEffectData(BattleEffectType.SpawnProjectileEmitter, 0, default, CopyProjectileEmitter(projectileEmitter), default);
        }

        public static BattleEffectData CreateAreaEffect(AreaEffectData areaEffect)
        {
            return new BattleEffectData(BattleEffectType.AreaEffect, 0, default, default, AreaEffectData.CopyValidated(areaEffect));
        }

        public static BattleEffectData CopyValidated(BattleEffectData effect)
        {
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
                    return CreateAreaEffect(effect.AreaEffect);
                default:
                    throw new ArgumentOutOfRangeException(nameof(effect), effect.Type, "Unsupported battle effect type.");
            }
        }

        private static StatusApplicationData CopyStatus(StatusApplicationData status)
        {
            return new StatusApplicationData(
                status.Id,
                status.Polarity,
                status.DurationTicks,
                status.TickIntervalTicks,
                status.PeriodicDamage,
                status.Modifiers,
                status.Triggers,
                status.MaxStacks,
                status.StackPolicy);
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

    internal readonly struct BattleReactionEffectData
    {
        private BattleReactionEffectData(BattleReactionTarget target, BattleEffectData effect)
        {
            Target = target;
            Effect = effect;
        }

        public BattleReactionTarget Target { get; }
        public BattleEffectData Effect { get; }

        public static BattleReactionEffectData Create(BattleReactionTarget target, BattleEffectData effect)
        {
            return new BattleReactionEffectData(ValidateTarget(target), BattleEffectData.CopyValidated(effect));
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

    internal readonly struct BattleTriggerData
    {
        private readonly BattleReactionEffectData[] _effects;
        private readonly ReadOnlyCollection<BattleReactionEffectData> _readOnlyEffects;

        public BattleTriggerData(BattleTriggerTiming timing, IReadOnlyList<BattleReactionEffectData> effects)
            : this(timing, BattleConditionProgram.AlwaysTrue, effects)
        {
        }

        public BattleTriggerData(BattleTriggerTiming timing, BattleConditionGroup conditions, IReadOnlyList<BattleReactionEffectData> effects)
            : this(timing, BattleConditionCompiler.Compile(conditions), effects)
        {
        }

        public BattleTriggerData(BattleTriggerTiming timing, BattleConditionProgram conditionProgram, IReadOnlyList<BattleReactionEffectData> effects)
        {
            Timing = ValidateTiming(timing);
            ConditionProgram = conditionProgram ?? BattleConditionProgram.AlwaysTrue;
            _effects = CopyEffects(effects);
            _readOnlyEffects = new ReadOnlyCollection<BattleReactionEffectData>(_effects);
        }

        public BattleTriggerTiming Timing { get; }
        public BattleConditionProgram ConditionProgram { get; }
        public IReadOnlyList<BattleReactionEffectData> Effects => _readOnlyEffects ?? EmptyEffects;

        private static readonly ReadOnlyCollection<BattleReactionEffectData> EmptyEffects = new ReadOnlyCollection<BattleReactionEffectData>(Array.Empty<BattleReactionEffectData>());

        private static BattleReactionEffectData[] CopyEffects(IReadOnlyList<BattleReactionEffectData> effects)
        {
            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            var copy = new BattleReactionEffectData[effects.Count];
            for (var i = 0; i < effects.Count; i++)
            {
                BattleReactionEffectData effect = effects[i];
                copy[i] = BattleReactionEffectData.Create(effect.Target, effect.Effect);
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

    internal readonly struct StatusApplicationData
    {
        private readonly BattleModifierData[] _modifiers;
        private readonly ReadOnlyCollection<BattleModifierData> _readOnlyModifiers;
        private readonly BattleTriggerData[] _triggers;
        private readonly ReadOnlyCollection<BattleTriggerData> _readOnlyTriggers;

        public StatusApplicationData(string id, StatusPolarity polarity, int durationTicks, int tickIntervalTicks, int periodicDamage, IReadOnlyList<BattleModifierData> modifiers, IReadOnlyList<BattleTriggerData> triggers)
            : this(id, polarity, durationTicks, tickIntervalTicks, periodicDamage, modifiers, triggers, maxStacks: 1, StatusStackPolicy.RefreshDurationAndAddStack)
        {
        }

        public StatusApplicationData(string id, StatusPolarity polarity, int durationTicks, int tickIntervalTicks, int periodicDamage, IReadOnlyList<BattleModifierData> modifiers, IReadOnlyList<BattleTriggerData> triggers, int maxStacks)
            : this(id, polarity, durationTicks, tickIntervalTicks, periodicDamage, modifiers, triggers, maxStacks, StatusStackPolicy.RefreshDurationAndAddStack)
        {
        }

        public StatusApplicationData(
            string id,
            StatusPolarity polarity,
            int durationTicks,
            int tickIntervalTicks,
            int periodicDamage,
            IReadOnlyList<BattleModifierData> modifiers,
            IReadOnlyList<BattleTriggerData> triggers,
            int maxStacks,
            StatusStackPolicy stackPolicy)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Status id is required.", nameof(id)) : id;
            Polarity = polarity;
            DurationTicks = durationTicks > 0 ? durationTicks : throw new ArgumentOutOfRangeException(nameof(durationTicks));
            TickIntervalTicks = tickIntervalTicks > 0 ? tickIntervalTicks : throw new ArgumentOutOfRangeException(nameof(tickIntervalTicks));
            PeriodicDamage = periodicDamage >= 0 ? periodicDamage : throw new ArgumentOutOfRangeException(nameof(periodicDamage));
            MaxStacks = maxStacks > 0 ? maxStacks : throw new ArgumentOutOfRangeException(nameof(maxStacks));
            StackPolicy = ValidateStackPolicy(stackPolicy);
            _modifiers = CopyModifiers(modifiers);
            _readOnlyModifiers = new ReadOnlyCollection<BattleModifierData>(_modifiers);
            _triggers = CopyTriggers(triggers);
            _readOnlyTriggers = new ReadOnlyCollection<BattleTriggerData>(_triggers);
        }

        public string Id { get; }
        public StatusPolarity Polarity { get; }
        public int DurationTicks { get; }
        public int TickIntervalTicks { get; }
        public int PeriodicDamage { get; }
        public int MaxStacks { get; }
        public StatusStackPolicy StackPolicy { get; }
        public IReadOnlyList<BattleModifierData> Modifiers => _readOnlyModifiers ?? EmptyModifiers;
        public IReadOnlyList<BattleTriggerData> Triggers => _readOnlyTriggers ?? EmptyTriggers;

        private static readonly ReadOnlyCollection<BattleModifierData> EmptyModifiers = new ReadOnlyCollection<BattleModifierData>(Array.Empty<BattleModifierData>());
        private static readonly ReadOnlyCollection<BattleTriggerData> EmptyTriggers = new ReadOnlyCollection<BattleTriggerData>(Array.Empty<BattleTriggerData>());

        private static StatusStackPolicy ValidateStackPolicy(StatusStackPolicy stackPolicy)
        {
            switch (stackPolicy)
            {
                case StatusStackPolicy.RefreshDurationAndAddStack:
                    return stackPolicy;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stackPolicy), stackPolicy, "Unsupported status stack policy.");
            }
        }

        private static BattleModifierData[] CopyModifiers(IReadOnlyList<BattleModifierData> modifiers)
        {
            if (modifiers == null)
            {
                throw new ArgumentNullException(nameof(modifiers));
            }

            var copy = new BattleModifierData[modifiers.Count];
            for (var i = 0; i < modifiers.Count; i++)
            {
                BattleModifierData modifier = modifiers[i];
                copy[i] = modifier.Target == BattleModifierTarget.Stat
                    ? BattleModifierData.Stat(modifier.StatId, modifier.Operation, modifier.Value)
                    : BattleModifierData.Damage(modifier.DamageStat, modifier.Operation, modifier.Value);
            }

            return copy;
        }

        private static BattleTriggerData[] CopyTriggers(IReadOnlyList<BattleTriggerData> triggers)
        {
            if (triggers == null)
            {
                throw new ArgumentNullException(nameof(triggers));
            }

            var copy = new BattleTriggerData[triggers.Count];
            for (var i = 0; i < triggers.Count; i++)
            {
                BattleTriggerData trigger = triggers[i];
                copy[i] = new BattleTriggerData(trigger.Timing, trigger.ConditionProgram, trigger.Effects);
            }

            return copy;
        }
    }

    internal readonly struct AbilityEffectFrameData
    {
        private readonly BattleEffectData[] _effects;
        private readonly ReadOnlyCollection<BattleEffectData> _readOnlyEffects;

        public AbilityEffectFrameData(string frameId, int tickOffset, int order, IReadOnlyList<BattleEffectData> effects)
        {
            FrameId = string.IsNullOrWhiteSpace(frameId) ? throw new ArgumentException("Ability effect frame id is required.", nameof(frameId)) : frameId;
            TickOffset = tickOffset >= 0 ? tickOffset : throw new ArgumentOutOfRangeException(nameof(tickOffset));
            Order = order >= 0 ? order : throw new ArgumentOutOfRangeException(nameof(order));
            _effects = CopyEffects(effects);
            _readOnlyEffects = new ReadOnlyCollection<BattleEffectData>(_effects);
        }

        public string FrameId { get; }
        public int TickOffset { get; }
        public int Order { get; }
        public IReadOnlyList<BattleEffectData> Effects => _readOnlyEffects ?? EmptyEffects;

        private static readonly ReadOnlyCollection<BattleEffectData> EmptyEffects = new ReadOnlyCollection<BattleEffectData>(Array.Empty<BattleEffectData>());

        private static BattleEffectData[] CopyEffects(IReadOnlyList<BattleEffectData> effects)
        {
            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            var copy = new BattleEffectData[effects.Count];
            for (var i = 0; i < effects.Count; i++)
            {
                copy[i] = BattleEffectData.CopyValidated(effects[i]);
            }

            return copy;
        }
    }

    internal readonly struct AbilitySpawnData
    {
        private readonly AbilityEffectFrameData[] _effectFrames;
        private readonly ReadOnlyCollection<AbilityEffectFrameData> _readOnlyEffectFrames;

        public AbilitySpawnData(
            string id,
            BattleScalar range,
            int cooldownTicks,
            int windupTicks,
            int recoveryTicks,
            AbilityTargetSelection targetSelection,
            IReadOnlyList<AbilityEffectFrameData> effectFrames,
            BattleActionLocks actionLocks = AbilityDefinition.DefaultActionLocks)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Ability id is required.", nameof(id)) : id;
            Range = range >= BattleScalar.Zero ? range : throw new ArgumentOutOfRangeException(nameof(range));
            CooldownTicks = cooldownTicks >= 0 ? cooldownTicks : throw new ArgumentOutOfRangeException(nameof(cooldownTicks));
            WindupTicks = windupTicks >= 0 ? windupTicks : throw new ArgumentOutOfRangeException(nameof(windupTicks));
            RecoveryTicks = recoveryTicks >= 0 ? recoveryTicks : throw new ArgumentOutOfRangeException(nameof(recoveryTicks));
            TargetSelection = AbilityDefinition.ValidateTargetSelection(targetSelection);
            ActionLocks = AbilityDefinition.ValidateActionLocks(actionLocks);
            _effectFrames = CopyEffectFrames(effectFrames);
            _readOnlyEffectFrames = new ReadOnlyCollection<AbilityEffectFrameData>(_effectFrames);
        }

        public string Id { get; }
        public BattleScalar Range { get; }
        public int CooldownTicks { get; }
        public int WindupTicks { get; }
        public int RecoveryTicks { get; }
        public AbilityTargetSelection TargetSelection { get; }
        public BattleActionLocks ActionLocks { get; }
        public IReadOnlyList<AbilityEffectFrameData> EffectFrames => _readOnlyEffectFrames ?? EmptyEffectFrames;

        private static readonly ReadOnlyCollection<AbilityEffectFrameData> EmptyEffectFrames = new ReadOnlyCollection<AbilityEffectFrameData>(Array.Empty<AbilityEffectFrameData>());

        private static AbilityEffectFrameData[] CopyEffectFrames(IReadOnlyList<AbilityEffectFrameData> frames)
        {
            return AbilityEffectFrameSequence.CopySorted(
                frames,
                frame => new AbilityEffectFrameData(frame.FrameId, frame.TickOffset, frame.Order, frame.Effects),
                frame => frame.TickOffset,
                frame => frame.Order,
                nameof(frames));
        }
    }

    internal readonly struct BrainSpawnData
    {
        public BrainSpawnData(string definitionId, AiBrainKind kind)
        {
            HasBrain = true;
            DefinitionId = string.IsNullOrWhiteSpace(definitionId) ? throw new ArgumentException("AI definition id is required.", nameof(definitionId)) : definitionId;
            Kind = ValidateKind(kind);
        }

        public bool HasBrain { get; }
        public string DefinitionId { get; }
        public AiBrainKind Kind { get; }
        public static BrainSpawnData None => default;

        private static AiBrainKind ValidateKind(AiBrainKind kind)
        {
            switch (kind)
            {
                case AiBrainKind.StateMachine:
                    return kind;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported AI brain kind.");
            }
        }
    }

    internal readonly struct TargetingBehaviorSpawnData
    {
        private TargetingBehaviorSpawnData(
            bool limitsAcquisitionRange,
            BattleScalar acquisitionRange,
            int noProgressTimeoutTicks,
            BattleScalar minimumProgressDistance,
            int rejectedTargetCooldownTicks)
        {
            LimitsAcquisitionRange = limitsAcquisitionRange;
            AcquisitionRange = acquisitionRange;
            NoProgressTimeoutTicks = noProgressTimeoutTicks;
            MinimumProgressDistance = minimumProgressDistance;
            RejectedTargetCooldownTicks = rejectedTargetCooldownTicks;
        }

        public bool LimitsAcquisitionRange { get; }
        public BattleScalar AcquisitionRange { get; }
        public int NoProgressTimeoutTicks { get; }
        public BattleScalar MinimumProgressDistance { get; }
        public int RejectedTargetCooldownTicks { get; }
        public static TargetingBehaviorSpawnData Unrestricted => default;

        public static TargetingBehaviorSpawnData Restricted(
            BattleScalar acquisitionRange,
            int noProgressTimeoutTicks,
            BattleScalar minimumProgressDistance,
            int rejectedTargetCooldownTicks)
        {
            if (acquisitionRange <= BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(acquisitionRange));
            }

            if (noProgressTimeoutTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(noProgressTimeoutTicks));
            }

            if (minimumProgressDistance <= BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumProgressDistance));
            }

            if (rejectedTargetCooldownTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rejectedTargetCooldownTicks));
            }

            return new TargetingBehaviorSpawnData(
                true,
                acquisitionRange,
                noProgressTimeoutTicks,
                minimumProgressDistance,
                rejectedTargetCooldownTicks);
        }
    }

    internal readonly struct CombatantSpawnData
    {
        private readonly AbilitySpawnData _basicAbility;
        private readonly AbilitySpawnData[] _abilities;
        private readonly ReadOnlyCollection<AbilitySpawnData> _readOnlyAbilities;

        public CombatantSpawnData(TeamId teamId, string definitionId, BattleVector2 position, int maxHealth, BattleScalar radius, BattleScalar moveSpeed, AbilitySpawnData basicAbility, IReadOnlyList<AbilitySpawnData> abilities)
            : this(teamId, definitionId, position, maxHealth, radius, moveSpeed, basicAbility, abilities, BrainSpawnData.None)
        {
        }

        public CombatantSpawnData(TeamId teamId, string definitionId, BattleVector2 position, int maxHealth, BattleScalar radius, BattleScalar moveSpeed, AbilitySpawnData basicAbility, IReadOnlyList<AbilitySpawnData> abilities, BrainSpawnData brain)
            : this(
                teamId,
                definitionId,
                position,
                maxHealth,
                radius,
                moveSpeed,
                basicAbility,
                abilities,
                brain,
                TargetingBehaviorSpawnData.Unrestricted)
        {
        }

        public CombatantSpawnData(
            TeamId teamId,
            string definitionId,
            BattleVector2 position,
            int maxHealth,
            BattleScalar radius,
            BattleScalar moveSpeed,
            AbilitySpawnData basicAbility,
            IReadOnlyList<AbilitySpawnData> abilities,
            BrainSpawnData brain,
            TargetingBehaviorSpawnData targetingBehavior)
            : this(
                teamId,
                definitionId,
                position,
                radius,
                CreateBaseStats(maxHealth, moveSpeed),
                basicAbility,
                abilities,
                brain,
                targetingBehavior)
        {
        }

        public CombatantSpawnData(TeamId teamId, string definitionId, BattleVector2 position, BattleScalar radius, BattleStatBlock baseStats, AbilitySpawnData basicAbility, IReadOnlyList<AbilitySpawnData> abilities)
            : this(teamId, definitionId, position, radius, baseStats, basicAbility, abilities, BrainSpawnData.None)
        {
        }

        public CombatantSpawnData(TeamId teamId, string definitionId, BattleVector2 position, BattleScalar radius, BattleStatBlock baseStats, AbilitySpawnData basicAbility, IReadOnlyList<AbilitySpawnData> abilities, BrainSpawnData brain)
            : this(
                teamId,
                definitionId,
                position,
                radius,
                baseStats,
                basicAbility,
                abilities,
                brain,
                TargetingBehaviorSpawnData.Unrestricted)
        {
        }

        public CombatantSpawnData(
            TeamId teamId,
            string definitionId,
            BattleVector2 position,
            BattleScalar radius,
            BattleStatBlock baseStats,
            AbilitySpawnData basicAbility,
            IReadOnlyList<AbilitySpawnData> abilities,
            BrainSpawnData brain,
            TargetingBehaviorSpawnData targetingBehavior)
        {
            TeamId = teamId;
            DefinitionId = string.IsNullOrWhiteSpace(definitionId) ? throw new ArgumentException("Combatant definition id is required.", nameof(definitionId)) : definitionId;
            Position = position;
            Radius = radius >= BattleScalar.Zero ? radius : throw new ArgumentOutOfRangeException(nameof(radius));
            BaseStats = baseStats ?? throw new ArgumentNullException(nameof(baseStats));
            ValidateRequiredStats(BaseStats, DefinitionId);
            _basicAbility = CopyAbility(basicAbility);
            Brain = brain;
            TargetingBehavior = targetingBehavior;
            _abilities = CopyAbilities(abilities);
            _readOnlyAbilities = new ReadOnlyCollection<AbilitySpawnData>(_abilities);
        }

        public TeamId TeamId { get; }
        public string DefinitionId { get; }
        public BattleVector2 Position { get; }
        public int MaxHealth => BaseStats.RequireInt(BattleStatId.MaxHealth, DefinitionId);
        public BattleScalar Radius { get; }
        public BattleScalar MoveSpeed => BaseStats.RequireScalar(BattleStatId.MoveSpeed, DefinitionId);
        public BattleStatBlock BaseStats { get; }
        public AbilitySpawnData BasicAbility => _basicAbility;
        public BrainSpawnData Brain { get; }
        public TargetingBehaviorSpawnData TargetingBehavior { get; }
        public IReadOnlyList<AbilitySpawnData> Abilities => _readOnlyAbilities ?? EmptyAbilities;

        private static readonly ReadOnlyCollection<AbilitySpawnData> EmptyAbilities = new ReadOnlyCollection<AbilitySpawnData>(Array.Empty<AbilitySpawnData>());

        private static BattleStatBlock CreateBaseStats(int maxHealth, BattleScalar moveSpeed)
        {
            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            }

            if (moveSpeed < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(moveSpeed));
            }

            return new BattleStatBlock(new[]
            {
                new BattleStatEntry(BattleStatId.MaxHealth, maxHealth),
                new BattleStatEntry(BattleStatId.MoveSpeed, moveSpeed.ToFloat())
            });
        }

        private static void ValidateRequiredStats(BattleStatBlock stats, string definitionId)
        {
            string owner = $"Combatant '{definitionId}' spawn data";
            int maxHealth = stats.RequireInt(BattleStatId.MaxHealth, owner);
            BattleScalar moveSpeed = stats.RequireScalar(BattleStatId.MoveSpeed, owner);
            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stats), $"Combatant '{definitionId}' spawn stat {BattleStatId.MaxHealth} must be greater than 0.");
            }

            if (moveSpeed < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(stats), $"Combatant '{definitionId}' spawn stat {BattleStatId.MoveSpeed} must be greater than or equal to 0.");
            }
        }

        private static AbilitySpawnData[] CopyAbilities(IReadOnlyList<AbilitySpawnData> abilities)
        {
            if (abilities == null)
            {
                throw new ArgumentNullException(nameof(abilities));
            }

            var copy = new AbilitySpawnData[abilities.Count];
            for (var i = 0; i < abilities.Count; i++)
            {
                AbilitySpawnData ability = abilities[i];
                copy[i] = CopyAbility(ability);
            }

            return copy;
        }

        private static AbilitySpawnData CopyAbility(AbilitySpawnData ability)
        {
            return new AbilitySpawnData(
                ability.Id,
                ability.Range,
                ability.CooldownTicks,
                ability.WindupTicks,
                ability.RecoveryTicks,
                ability.TargetSelection,
                ability.EffectFrames,
                ability.ActionLocks);
        }
    }
}
