using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    internal readonly struct AbilityState
    {
        private readonly AbilityEffectFrameData[] _effectFrames;
        private readonly ReadOnlyCollection<AbilityEffectFrameData> _readOnlyEffectFrames;

        public AbilityState(
            string id,
            BattleScalar range,
            int cooldownTicks,
            int cooldownRemainingTicks,
            int windupTicks,
            int recoveryTicks,
            AbilityTargetSelection targetSelection,
            IReadOnlyList<AbilityEffectFrameData> effectFrames,
            BattleActionLocks actionLocks = AbilityDefinition.DefaultActionLocks)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Ability id is required.", nameof(id)) : id;
            Range = range >= BattleScalar.Zero ? range : throw new ArgumentOutOfRangeException(nameof(range));
            CooldownTicks = cooldownTicks >= 0 ? cooldownTicks : throw new ArgumentOutOfRangeException(nameof(cooldownTicks));
            CooldownRemainingTicks = cooldownRemainingTicks >= 0 ? cooldownRemainingTicks : throw new ArgumentOutOfRangeException(nameof(cooldownRemainingTicks));
            WindupTicks = windupTicks >= 0 ? windupTicks : throw new ArgumentOutOfRangeException(nameof(windupTicks));
            RecoveryTicks = recoveryTicks >= 0 ? recoveryTicks : throw new ArgumentOutOfRangeException(nameof(recoveryTicks));
            TargetSelection = AbilityDefinition.ValidateTargetSelection(targetSelection);
            ActionLocks = AbilityDefinition.ValidateActionLocks(actionLocks);
            _effectFrames = CopyEffectFrames(effectFrames);
            _readOnlyEffectFrames = new ReadOnlyCollection<AbilityEffectFrameData>(_effectFrames);
        }

        private AbilityState(
            string id,
            BattleScalar range,
            int cooldownTicks,
            int cooldownRemainingTicks,
            int windupTicks,
            int recoveryTicks,
            AbilityTargetSelection targetSelection,
            BattleActionLocks actionLocks,
            AbilityEffectFrameData[] effectFrames,
            ReadOnlyCollection<AbilityEffectFrameData> readOnlyEffectFrames)
        {
            Id = id;
            Range = range;
            CooldownTicks = cooldownTicks;
            CooldownRemainingTicks = cooldownRemainingTicks;
            WindupTicks = windupTicks;
            RecoveryTicks = recoveryTicks;
            TargetSelection = targetSelection;
            ActionLocks = actionLocks;
            _effectFrames = effectFrames ?? throw new ArgumentNullException(nameof(effectFrames));
            _readOnlyEffectFrames = readOnlyEffectFrames ?? new ReadOnlyCollection<AbilityEffectFrameData>(_effectFrames);
        }

        public string Id { get; }
        public BattleScalar Range { get; }
        public int CooldownTicks { get; }
        public int CooldownRemainingTicks { get; }
        public int WindupTicks { get; }
        public int RecoveryTicks { get; }
        public AbilityTargetSelection TargetSelection { get; }
        public BattleActionLocks ActionLocks { get; }
        public IReadOnlyList<AbilityEffectFrameData> EffectFrames => _readOnlyEffectFrames ?? EmptyEffectFrames;

        private static readonly ReadOnlyCollection<AbilityEffectFrameData> EmptyEffectFrames = new ReadOnlyCollection<AbilityEffectFrameData>(Array.Empty<AbilityEffectFrameData>());

        public AbilityState WithCooldownRemainingTicks(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            return new AbilityState(Id, Range, CooldownTicks, value, WindupTicks, RecoveryTicks, TargetSelection, ActionLocks, _effectFrames, _readOnlyEffectFrames);
        }

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

    internal readonly struct AbilityComponent
    {
        public const int BasicAbilityIndex = 0;
        public const int FirstSkillAbilityIndex = 1;

        private readonly AbilityState[] _abilities;
        private readonly ReadOnlyCollection<AbilityState> _readOnlyAbilities;

        public AbilityComponent(IReadOnlyList<AbilityState> abilities)
        {
            if (abilities == null)
            {
                throw new ArgumentNullException(nameof(abilities));
            }

            _abilities = new AbilityState[abilities.Count];
            for (var i = 0; i < abilities.Count; i++)
            {
                AbilityState ability = abilities[i];
                _abilities[i] = new AbilityState(
                    ability.Id,
                    ability.Range,
                    ability.CooldownTicks,
                    ability.CooldownRemainingTicks,
                    ability.WindupTicks,
                    ability.RecoveryTicks,
                    ability.TargetSelection,
                    ability.EffectFrames,
                    ability.ActionLocks);
            }

            _readOnlyAbilities = new ReadOnlyCollection<AbilityState>(_abilities);
        }

        private AbilityComponent(AbilityState[] abilities, ReadOnlyCollection<AbilityState> readOnlyAbilities)
        {
            _abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
            _readOnlyAbilities = readOnlyAbilities ?? new ReadOnlyCollection<AbilityState>(_abilities);
        }

        public IReadOnlyList<AbilityState> Abilities => _readOnlyAbilities ?? EmptyAbilities;

        private static readonly ReadOnlyCollection<AbilityState> EmptyAbilities = new ReadOnlyCollection<AbilityState>(Array.Empty<AbilityState>());

        public AbilityComponent WithAbilityCooldownRemainingTicks(int abilityIndex, int cooldownRemainingTicks)
        {
            IReadOnlyList<AbilityState> abilities = Abilities;
            if (abilityIndex < 0 || abilityIndex >= abilities.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(abilityIndex));
            }

            var copy = new AbilityState[abilities.Count];
            for (var i = 0; i < abilities.Count; i++)
            {
                copy[i] = abilities[i];
            }

            copy[abilityIndex] = copy[abilityIndex].WithCooldownRemainingTicks(cooldownRemainingTicks);
            return new AbilityComponent(copy, null);
        }

        public AbilityComponent TickCooldowns(out bool changed)
        {
            IReadOnlyList<AbilityState> abilities = Abilities;
            AbilityState[] copy = null;
            changed = false;
            for (var i = 0; i < abilities.Count; i++)
            {
                AbilityState ability = abilities[i];
                if (ability.CooldownRemainingTicks <= 0)
                {
                    continue;
                }

                if (copy == null)
                {
                    copy = new AbilityState[abilities.Count];
                    for (var copyIndex = 0; copyIndex < abilities.Count; copyIndex++)
                    {
                        copy[copyIndex] = abilities[copyIndex];
                    }
                }

                copy[i] = ability.WithCooldownRemainingTicks(ability.CooldownRemainingTicks - 1);
                changed = true;
            }

            return changed ? new AbilityComponent(copy, null) : this;
        }
    }
}
