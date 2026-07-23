using System;
using Combat.Core.Battle;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    [CreateAssetMenu(menuName = "Combat/Projectile Config", fileName = "ProjectileConfig")]
    public sealed class ProjectileConfigAsset : ScriptableObject
    {
        [SerializeField] private ProjectileBehavior _behavior = ProjectileBehavior.Linear;
        [SerializeField] private ProjectileHitPolicyMode _hitPolicyMode =
            ProjectileHitPolicyMode.DestroyOnFirstHit;
        [SerializeField, Min(2)]
        private int _maxHitCount = 2;
        [SerializeField] private float _radius = 0.1f;
        [SerializeField] private float _speed = 1f;
        [SerializeField] private float _lifetimeSeconds = 1f;
        [SerializeField] private BattleEffectConfig[] _impactEffects =
            Array.Empty<BattleEffectConfig>();

        public ProjectileBehavior Behavior => _behavior;
        public ProjectileHitPolicyMode HitPolicyMode => _hitPolicyMode;
        public int MaxHitCount => _maxHitCount;
        public float Radius => _radius;
        public float Speed => _speed;
        public float LifetimeSeconds => _lifetimeSeconds;
        public BattleEffectConfig[] ImpactEffects =>
            _impactEffects ?? Array.Empty<BattleEffectConfig>();
    }
}
