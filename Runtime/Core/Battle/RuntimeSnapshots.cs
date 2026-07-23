using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    public interface IBattleRuntimeSnapshotSource
    {
        bool TryGetUnitRuntimeSnapshot(UnitId unitId, out UnitRuntimeSnapshot snapshot);
    }

    public readonly struct UnitRuntimeSnapshot
    {
        private readonly AbilityRuntimeSnapshot[] _abilities;
        private readonly ReadOnlyCollection<AbilityRuntimeSnapshot> _readOnlyAbilities;
        private readonly StatusRuntimeSnapshot[] _statuses;
        private readonly ReadOnlyCollection<StatusRuntimeSnapshot> _readOnlyStatuses;

        public UnitRuntimeSnapshot(
            BattleTick tick,
            UnitId unitId,
            string definitionId,
            TeamId teamId,
            BattleVector2 position,
            float radius,
            int currentHealth,
            int maxHealth,
            string lifeState,
            bool hasBrain,
            string brainDefinitionId,
            string brainKind,
            string brainState,
            BattleTick brainStateEnteredTick,
            bool hasTarget,
            UnitId targetUnitId,
            float moveSpeed,
            IReadOnlyList<AbilityRuntimeSnapshot> abilities,
            IReadOnlyList<StatusRuntimeSnapshot> statuses)
            : this(
                tick,
                unitId,
                definitionId,
                teamId,
                position,
                BattleVector2.Right,
                radius,
                currentHealth,
                maxHealth,
                lifeState,
                hasBrain,
                brainDefinitionId,
                brainKind,
                brainState,
                brainStateEnteredTick,
                hasTarget,
                targetUnitId,
                moveSpeed,
                abilities,
                statuses)
        {
        }

        public UnitRuntimeSnapshot(
            BattleTick tick,
            UnitId unitId,
            string definitionId,
            TeamId teamId,
            BattleVector2 position,
            BattleVector2 facing,
            float radius,
            int currentHealth,
            int maxHealth,
            string lifeState,
            bool hasBrain,
            string brainDefinitionId,
            string brainKind,
            string brainState,
            BattleTick brainStateEnteredTick,
            bool hasTarget,
            UnitId targetUnitId,
            float moveSpeed,
            IReadOnlyList<AbilityRuntimeSnapshot> abilities,
            IReadOnlyList<StatusRuntimeSnapshot> statuses)
        {
            Tick = tick;
            UnitId = unitId;
            DefinitionId = definitionId ?? string.Empty;
            TeamId = teamId;
            Position = position;
            Facing = new FacingComponent(facing).Direction;
            Radius = radius;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            LifeState = lifeState ?? string.Empty;
            HasBrain = hasBrain;
            BrainDefinitionId = brainDefinitionId ?? string.Empty;
            BrainKind = brainKind ?? string.Empty;
            BrainState = brainState ?? string.Empty;
            BrainStateEnteredTick = brainStateEnteredTick;
            HasTarget = hasTarget;
            TargetUnitId = targetUnitId;
            MoveSpeed = moveSpeed;
            _abilities = CopyList(abilities, nameof(abilities));
            _readOnlyAbilities = new ReadOnlyCollection<AbilityRuntimeSnapshot>(_abilities);
            _statuses = CopyList(statuses, nameof(statuses));
            _readOnlyStatuses = new ReadOnlyCollection<StatusRuntimeSnapshot>(_statuses);
        }

        public BattleTick Tick { get; }
        public UnitId UnitId { get; }
        public string DefinitionId { get; }
        public TeamId TeamId { get; }
        public BattleVector2 Position { get; }
        public BattleVector2 Facing { get; }
        public float Radius { get; }
        public int CurrentHealth { get; }
        public int MaxHealth { get; }
        public string LifeState { get; }
        public bool HasBrain { get; }
        public string BrainDefinitionId { get; }
        public string BrainKind { get; }
        public string BrainState { get; }
        public BattleTick BrainStateEnteredTick { get; }
        public bool HasTarget { get; }
        public UnitId TargetUnitId { get; }
        public float MoveSpeed { get; }
        public IReadOnlyList<AbilityRuntimeSnapshot> Abilities => _readOnlyAbilities ?? EmptyAbilities;
        public IReadOnlyList<StatusRuntimeSnapshot> Statuses => _readOnlyStatuses ?? EmptyStatuses;

        private static readonly ReadOnlyCollection<AbilityRuntimeSnapshot> EmptyAbilities = new ReadOnlyCollection<AbilityRuntimeSnapshot>(Array.Empty<AbilityRuntimeSnapshot>());
        private static readonly ReadOnlyCollection<StatusRuntimeSnapshot> EmptyStatuses = new ReadOnlyCollection<StatusRuntimeSnapshot>(Array.Empty<StatusRuntimeSnapshot>());

        private static T[] CopyList<T>(IReadOnlyList<T> values, string parameterName)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new T[values.Count];
            for (var i = 0; i < values.Count; i++)
            {
                copy[i] = values[i];
            }

            return copy;
        }
    }

    public readonly struct AbilityRuntimeSnapshot
    {
        public AbilityRuntimeSnapshot(int slotIndex, bool isBasic, string id, float range, int damage, int cooldownTicks, int cooldownRemainingTicks)
        {
            SlotIndex = slotIndex >= 0 ? slotIndex : throw new ArgumentOutOfRangeException(nameof(slotIndex));
            IsBasic = isBasic;
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Ability id is required.", nameof(id)) : id;
            Range = range >= 0f ? range : throw new ArgumentOutOfRangeException(nameof(range));
            Damage = damage >= 0 ? damage : throw new ArgumentOutOfRangeException(nameof(damage));
            CooldownTicks = cooldownTicks >= 0 ? cooldownTicks : throw new ArgumentOutOfRangeException(nameof(cooldownTicks));
            CooldownRemainingTicks = cooldownRemainingTicks >= 0 ? cooldownRemainingTicks : throw new ArgumentOutOfRangeException(nameof(cooldownRemainingTicks));
        }

        public int SlotIndex { get; }
        public bool IsBasic { get; }
        public string Id { get; }
        public float Range { get; }
        public int Damage { get; }
        public int CooldownTicks { get; }
        public int CooldownRemainingTicks { get; }
    }

    public readonly struct StatusRuntimeSnapshot
    {
        public StatusRuntimeSnapshot(
            string id,
            StatusPolarity polarity,
            bool hasSourceUnit,
            UnitId sourceUnitId,
            int durationRemainingTicks,
            int tickIntervalTicks,
            int ticksUntilNextPeriodicEffect,
            int periodicDamage,
            int modifierCount,
            int triggerCount)
            : this(
                id,
                polarity,
                hasSourceUnit,
                sourceUnitId,
                durationRemainingTicks,
                tickIntervalTicks,
                ticksUntilNextPeriodicEffect,
                periodicDamage,
                modifierCount,
                triggerCount,
                stackCount: 1,
                maxStacks: 1)
        {
        }

        public StatusRuntimeSnapshot(
            string id,
            StatusPolarity polarity,
            bool hasSourceUnit,
            UnitId sourceUnitId,
            int durationRemainingTicks,
            int tickIntervalTicks,
            int ticksUntilNextPeriodicEffect,
            int periodicDamage,
            int modifierCount,
            int triggerCount,
            int stackCount,
            int maxStacks)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Status id is required.", nameof(id)) : id;
            Polarity = polarity;
            HasSourceUnit = hasSourceUnit;
            SourceUnitId = sourceUnitId;
            DurationRemainingTicks = durationRemainingTicks > 0 ? durationRemainingTicks : throw new ArgumentOutOfRangeException(nameof(durationRemainingTicks));
            TickIntervalTicks = tickIntervalTicks > 0 ? tickIntervalTicks : throw new ArgumentOutOfRangeException(nameof(tickIntervalTicks));
            TicksUntilNextPeriodicEffect = ticksUntilNextPeriodicEffect > 0 ? ticksUntilNextPeriodicEffect : throw new ArgumentOutOfRangeException(nameof(ticksUntilNextPeriodicEffect));
            PeriodicDamage = periodicDamage >= 0 ? periodicDamage : throw new ArgumentOutOfRangeException(nameof(periodicDamage));
            ModifierCount = modifierCount >= 0 ? modifierCount : throw new ArgumentOutOfRangeException(nameof(modifierCount));
            TriggerCount = triggerCount >= 0 ? triggerCount : throw new ArgumentOutOfRangeException(nameof(triggerCount));
            MaxStacks = maxStacks > 0 ? maxStacks : throw new ArgumentOutOfRangeException(nameof(maxStacks));
            StackCount = stackCount > 0 && stackCount <= MaxStacks ? stackCount : throw new ArgumentOutOfRangeException(nameof(stackCount));
        }

        public string Id { get; }
        public StatusPolarity Polarity { get; }
        public bool HasSourceUnit { get; }
        public UnitId SourceUnitId { get; }
        public int DurationRemainingTicks { get; }
        public int TickIntervalTicks { get; }
        public int TicksUntilNextPeriodicEffect { get; }
        public int PeriodicDamage { get; }
        public int ModifierCount { get; }
        public int TriggerCount { get; }
        public int StackCount { get; }
        public int MaxStacks { get; }
    }
}
