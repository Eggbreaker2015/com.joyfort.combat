using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    [Serializable]
    public sealed class AbilityEffectFrameConfig
    {
        [SerializeField] private string _frameId = "release";
        [SerializeField] private float _timeSeconds;
        [SerializeField] private int _order;
        [SerializeField] private BattleEffectConfig[] _effects = Array.Empty<BattleEffectConfig>();

        public AbilityEffectFrameConfig()
        {
        }

        public AbilityEffectFrameConfig(
            string frameId,
            float timeSeconds,
            int order,
            IReadOnlyList<BattleEffectConfig> effects)
        {
            _frameId = frameId;
            _timeSeconds = timeSeconds;
            _order = order;
            _effects = CopyEffects(effects);
        }

        public string FrameId => _frameId;
        public float TimeSeconds => _timeSeconds;
        public int Order => _order;
        public IReadOnlyList<BattleEffectConfig> Effects => _effects ?? Array.Empty<BattleEffectConfig>();

        private static BattleEffectConfig[] CopyEffects(IReadOnlyList<BattleEffectConfig> effects)
        {
            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            var copy = new BattleEffectConfig[effects.Count];
            for (var i = 0; i < effects.Count; i++)
            {
                copy[i] = effects[i];
            }

            return copy;
        }
    }

    [CreateAssetMenu(menuName = "Combat/Ability Config", fileName = "AbilityConfig")]
    public sealed class AbilityConfigAsset : ScriptableObject
    {
        [SerializeField] private string _id = "ability";
        [SerializeField] private float _range = 1f;
        [SerializeField] private float _cooldownSeconds = 1f;
        [SerializeField] private float _windupSeconds = 0f;
        [SerializeField] private float _recoverySeconds = 0f;
        [SerializeField] private AbilityTargetSelection _targetSelection = AbilityTargetSelection.CurrentEnemyTarget;
        [SerializeField] private BattleActionLocks _actionLocks = AbilityDefinition.DefaultActionLocks;
        [SerializeField] private AbilityEffectFrameConfig[] _effectFrames =
        {
            new AbilityEffectFrameConfig(
                "release",
                0f,
                0,
                new[]
                {
                    new BattleEffectConfig(BattleEffectType.Damage, 1, null, null)
                })
        };

        public string Id => _id;
        public float Range => _range;
        public float CooldownSeconds => _cooldownSeconds;
        public float WindupSeconds => _windupSeconds;
        public float RecoverySeconds => _recoverySeconds;
        public AbilityTargetSelection TargetSelection => _targetSelection;
        public BattleActionLocks ActionLocks => _actionLocks;
        public IReadOnlyList<AbilityEffectFrameConfig> EffectFrames => _effectFrames ?? Array.Empty<AbilityEffectFrameConfig>();
    }
}
