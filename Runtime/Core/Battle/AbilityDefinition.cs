using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    public enum AbilityTargetSelection
    {
        CurrentEnemyTarget,
        LowestHealthAlly,
        Self
    }

    public sealed class AbilityEffectFrameDefinition
    {
        private readonly BattleEffectDefinition[] _effects;
        private readonly ReadOnlyCollection<BattleEffectDefinition> _readOnlyEffects;

        public AbilityEffectFrameDefinition(string frameId, int tickOffset, int order, IReadOnlyList<BattleEffectDefinition> effects)
        {
            FrameId = string.IsNullOrWhiteSpace(frameId) ? throw new ArgumentException("Ability effect frame id is required.", nameof(frameId)) : frameId;
            TickOffset = tickOffset >= 0 ? tickOffset : throw new ArgumentOutOfRangeException(nameof(tickOffset));
            Order = order >= 0 ? order : throw new ArgumentOutOfRangeException(nameof(order));
            _effects = AbilityDefinition.CopyEffects(effects);
            _readOnlyEffects = new ReadOnlyCollection<BattleEffectDefinition>(_effects);
        }

        public string FrameId { get; }
        public int TickOffset { get; }
        public int Order { get; }
        public IReadOnlyList<BattleEffectDefinition> Effects => _readOnlyEffects ?? EmptyEffects;

        private static readonly ReadOnlyCollection<BattleEffectDefinition> EmptyEffects = new ReadOnlyCollection<BattleEffectDefinition>(Array.Empty<BattleEffectDefinition>());
    }

    public sealed class AbilityDefinition
    {
        public const BattleActionLocks DefaultActionLocks = BattleActionLocks.Movement | BattleActionLocks.StartAnotherAction;

        private readonly AbilityEffectFrameDefinition[] _effectFrames;
        private readonly ReadOnlyCollection<AbilityEffectFrameDefinition> _readOnlyEffectFrames;

        public AbilityDefinition(
            string id,
            BattleScalar range,
            int cooldownTicks,
            int windupTicks,
            int recoveryTicks,
            AbilityTargetSelection targetSelection,
            IReadOnlyList<AbilityEffectFrameDefinition> effectFrames,
            BattleActionLocks actionLocks = DefaultActionLocks)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Ability definition id is required.", nameof(id)) : id;
            Range = range >= BattleScalar.Zero ? range : throw new ArgumentOutOfRangeException(nameof(range));
            CooldownTicks = cooldownTicks >= 0 ? cooldownTicks : throw new ArgumentOutOfRangeException(nameof(cooldownTicks));
            WindupTicks = windupTicks >= 0 ? windupTicks : throw new ArgumentOutOfRangeException(nameof(windupTicks));
            RecoveryTicks = recoveryTicks >= 0 ? recoveryTicks : throw new ArgumentOutOfRangeException(nameof(recoveryTicks));
            TargetSelection = ValidateTargetSelection(targetSelection);
            ActionLocks = ValidateActionLocks(actionLocks);
            _effectFrames = CopyEffectFrames(effectFrames);
            _readOnlyEffectFrames = new ReadOnlyCollection<AbilityEffectFrameDefinition>(_effectFrames);
        }

        public string Id { get; }
        public BattleScalar Range { get; }
        public int CooldownTicks { get; }
        public int WindupTicks { get; }
        public int RecoveryTicks { get; }
        public AbilityTargetSelection TargetSelection { get; }
        public BattleActionLocks ActionLocks { get; }
        public IReadOnlyList<AbilityEffectFrameDefinition> EffectFrames => _readOnlyEffectFrames ?? EmptyEffectFrames;

        private static readonly ReadOnlyCollection<AbilityEffectFrameDefinition> EmptyEffectFrames = new ReadOnlyCollection<AbilityEffectFrameDefinition>(Array.Empty<AbilityEffectFrameDefinition>());

        internal static AbilityTargetSelection ValidateTargetSelection(AbilityTargetSelection targetSelection)
        {
            switch (targetSelection)
            {
                case AbilityTargetSelection.CurrentEnemyTarget:
                case AbilityTargetSelection.LowestHealthAlly:
                case AbilityTargetSelection.Self:
                    return targetSelection;
                default:
                    throw new ArgumentOutOfRangeException(nameof(targetSelection), targetSelection, "Unsupported ability target selection.");
            }
        }

        internal static BattleActionLocks ValidateActionLocks(BattleActionLocks actionLocks)
        {
            const BattleActionLocks supported = BattleActionLocks.Movement | BattleActionLocks.Facing | BattleActionLocks.StartAnotherAction;
            if ((actionLocks & ~supported) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionLocks), actionLocks, "Unsupported ability action locks.");
            }

            return actionLocks;
        }

        internal static BattleEffectDefinition[] CopyEffects(IReadOnlyList<BattleEffectDefinition> effects)
        {
            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            var copy = new BattleEffectDefinition[effects.Count];
            var statusIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < effects.Count; i++)
            {
                BattleEffectDefinition effect = BattleEffectDefinition.CopyValidated(effects[i]);
                if (effect.Type == BattleEffectType.ApplyStatus && !statusIds.Add(effect.Status.Id))
                {
                    throw new ArgumentException($"Duplicate applied status id: {effect.Status.Id}.", nameof(effects));
                }

                copy[i] = effect;
            }

            return copy;
        }

        private static AbilityEffectFrameDefinition[] CopyEffectFrames(IReadOnlyList<AbilityEffectFrameDefinition> frames)
        {
            return AbilityEffectFrameSequence.CopySorted(
                frames,
                frame =>
                {
                    if (frame == null)
                    {
                        throw new ArgumentException("Ability effect frame cannot be null.", nameof(frames));
                    }

                    return new AbilityEffectFrameDefinition(frame.FrameId, frame.TickOffset, frame.Order, frame.Effects);
                },
                frame => frame.TickOffset,
                frame => frame.Order,
                nameof(frames));
        }
    }

    internal static class AbilityEffectFrameSequence
    {
        public static TFrame[] CopySorted<TFrame>(
            IReadOnlyList<TFrame> frames,
            Func<TFrame, TFrame> copyFrame,
            Func<TFrame, int> tickOffset,
            Func<TFrame, int> order,
            string parameterName)
        {
            if (frames == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (frames.Count == 0)
            {
                throw new ArgumentException("Ability requires at least one effect frame.", parameterName);
            }

            var copy = new TFrame[frames.Count];
            for (var i = 0; i < frames.Count; i++)
            {
                TFrame frame = copyFrame(frames[i]);
                if (ReferenceEquals(frame, null))
                {
                    throw new ArgumentException("Ability effect frame cannot be null.", parameterName);
                }

                copy[i] = frame;
            }

            Sort(copy, tickOffset, order);
            return copy;
        }

        private static void Sort<TFrame>(TFrame[] frames, Func<TFrame, int> tickOffset, Func<TFrame, int> order)
        {
            for (var i = 1; i < frames.Length; i++)
            {
                TFrame frame = frames[i];
                var insert = i - 1;
                while (insert >= 0 && Compare(frames[insert], frame, tickOffset, order) > 0)
                {
                    frames[insert + 1] = frames[insert];
                    insert--;
                }

                frames[insert + 1] = frame;
            }
        }

        private static int Compare<TFrame>(TFrame left, TFrame right, Func<TFrame, int> tickOffset, Func<TFrame, int> order)
        {
            int tickComparison = tickOffset(left).CompareTo(tickOffset(right));
            return tickComparison != 0 ? tickComparison : order(left).CompareTo(order(right));
        }
    }
}
