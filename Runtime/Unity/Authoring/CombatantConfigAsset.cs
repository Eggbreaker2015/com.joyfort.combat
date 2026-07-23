using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    [CreateAssetMenu(menuName = "Combat/Combatant Config", fileName = "CombatantConfig")]
    public sealed class CombatantConfigAsset : ScriptableObject
    {
        [SerializeField] private float _radius = 0.25f;
        [SerializeField] private bool _targetingBehaviorEnabled;
        [SerializeField, Min(0f)] private float _targetAcquisitionRange = 4f;
        [SerializeField, Min(0f)] private float _noProgressTimeoutSeconds = 3f;
        [SerializeField, Min(0f)] private float _minimumProgressDistance = 0.1f;
        [SerializeField, Min(0f)] private float _rejectedTargetCooldownSeconds = 1f;
        [SerializeField] private BattleStatConfig[] _stats = Array.Empty<BattleStatConfig>();
        [SerializeField] private AbilityConfigAsset _basicAbility;
        [SerializeField] private AbilityConfigAsset[] _abilities = Array.Empty<AbilityConfigAsset>();

        public string Id => name;
        public float Radius => _radius;
        public bool TargetingBehaviorEnabled => _targetingBehaviorEnabled;
        public float TargetAcquisitionRange => _targetAcquisitionRange;
        public float NoProgressTimeoutSeconds => _noProgressTimeoutSeconds;
        public float MinimumProgressDistance => _minimumProgressDistance;
        public float RejectedTargetCooldownSeconds => _rejectedTargetCooldownSeconds;
        public IReadOnlyList<BattleStatConfig> Stats => _stats;
        public AbilityConfigAsset BasicAbility => _basicAbility;
        public IReadOnlyList<AbilityConfigAsset> Abilities => _abilities;
    }
}
