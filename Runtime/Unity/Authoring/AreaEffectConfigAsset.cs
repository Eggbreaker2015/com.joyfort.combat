using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    [CreateAssetMenu(menuName = "Combat/Area Effect Config", fileName = "AreaEffectConfig")]
    public sealed class AreaEffectConfigAsset : ScriptableObject
    {
        [SerializeField] private float _radius = 1f;
        [SerializeField] private AreaEffectTargetFilter _targetFilter = AreaEffectTargetFilter.Enemies;
        [SerializeField] private BattleEffectConfig[] _effects = Array.Empty<BattleEffectConfig>();

        public float Radius => _radius;
        public AreaEffectTargetFilter TargetFilter => _targetFilter;
        public IReadOnlyList<BattleEffectConfig> Effects => _effects ?? Array.Empty<BattleEffectConfig>();
    }
}
