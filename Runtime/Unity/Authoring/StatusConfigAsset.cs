using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    [CreateAssetMenu(menuName = "Combat/Status Config", fileName = "StatusConfig")]
    public sealed class StatusConfigAsset : ScriptableObject
    {
        [SerializeField] private string _id = "status";
        [SerializeField] private StatusPolarity _polarity = StatusPolarity.Neutral;
        [SerializeField] private float _durationSeconds = 1f;
        [SerializeField] private float _tickIntervalSeconds = 1f;
        [SerializeField] private int _periodicDamage;
        [SerializeField] private int _maxStacks = 1;
        [SerializeField] private StatusStackPolicy _stackPolicy = StatusStackPolicy.RefreshDurationAndAddStack;
        [SerializeField] private BattleModifierConfig[] _modifiers = Array.Empty<BattleModifierConfig>();
        [SerializeField] private StatusTriggerConfig[] _triggers = Array.Empty<StatusTriggerConfig>();

        public string Id => _id;
        public StatusPolarity Polarity => _polarity;
        public float DurationSeconds => _durationSeconds;
        public float TickIntervalSeconds => _tickIntervalSeconds;
        public int PeriodicDamage => _periodicDamage;
        public int MaxStacks => _maxStacks;
        public StatusStackPolicy StackPolicy => _stackPolicy;
        public IReadOnlyList<BattleModifierConfig> Modifiers => _modifiers;
        public IReadOnlyList<StatusTriggerConfig> Triggers => _triggers;
    }

    [Serializable]
    public struct StatusTriggerConfig
    {
        [SerializeField] private BattleTriggerTiming _timing;
        [SerializeField] private BattleConditionMatchMode _conditionMatchMode;
        [SerializeField] private BattleConditionConfig[] _conditions;
        [SerializeField] private StatusReactionEffectConfig[] _effects;

        public StatusTriggerConfig(BattleTriggerTiming timing, IReadOnlyList<StatusReactionEffectConfig> effects)
            : this(timing, BattleConditionMatchMode.All, Array.Empty<BattleConditionConfig>(), effects)
        {
        }

        public StatusTriggerConfig(
            BattleTriggerTiming timing,
            BattleConditionMatchMode conditionMatchMode,
            IReadOnlyList<BattleConditionConfig> conditions,
            IReadOnlyList<StatusReactionEffectConfig> effects)
        {
            _timing = timing;
            _conditionMatchMode = conditionMatchMode;
            _conditions = CopyConditions(conditions);
            _effects = CopyEffects(effects);
        }

        public BattleTriggerTiming Timing => _timing;
        public BattleConditionMatchMode ConditionMatchMode => _conditionMatchMode;
        public IReadOnlyList<BattleConditionConfig> Conditions => _conditions ?? Array.Empty<BattleConditionConfig>();
        public IReadOnlyList<StatusReactionEffectConfig> Effects => _effects ?? Array.Empty<StatusReactionEffectConfig>();

        private static BattleConditionConfig[] CopyConditions(IReadOnlyList<BattleConditionConfig> conditions)
        {
            if (conditions == null)
            {
                throw new ArgumentNullException(nameof(conditions));
            }

            var copy = new BattleConditionConfig[conditions.Count];
            for (var i = 0; i < conditions.Count; i++)
            {
                copy[i] = conditions[i];
            }

            return copy;
        }

        private static StatusReactionEffectConfig[] CopyEffects(IReadOnlyList<StatusReactionEffectConfig> effects)
        {
            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            var copy = new StatusReactionEffectConfig[effects.Count];
            for (var i = 0; i < effects.Count; i++)
            {
                copy[i] = effects[i];
            }

            return copy;
        }
    }

    [Serializable]
    public struct BattleConditionConfig
    {
        [SerializeReference]
        [SerializeField] private BattleConditionOperandConfig _left;
        [SerializeField] private BattleConditionComparison _comparison;
        [SerializeReference]
        [SerializeField] private BattleConditionOperandConfig _right;

        public BattleConditionConfig(
            BattleConditionOperandConfig left,
            BattleConditionComparison comparison,
            BattleConditionOperandConfig right)
        {
            _left = left;
            _comparison = comparison;
            _right = right;
        }

        public BattleConditionOperandConfig Left => _left;
        public BattleConditionComparison Comparison => _comparison;
        public BattleConditionOperandConfig Right => _right;
    }

    [Serializable]
    public struct StatusReactionEffectConfig
    {
        [SerializeField] private BattleReactionTarget _target;
        [SerializeField] private BattleEffectConfig _effect;

        public StatusReactionEffectConfig(BattleReactionTarget target, BattleEffectConfig effect)
        {
            _target = target;
            _effect = effect;
        }

        public BattleReactionTarget Target => _target;
        public BattleEffectConfig Effect => _effect;
    }
}
