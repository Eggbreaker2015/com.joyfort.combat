using System;
using Combat.Core.Battle;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    [CreateAssetMenu(menuName = "Combat/Projectile Emitter Config", fileName = "ProjectileEmitterConfig")]
    public sealed class ProjectileEmitterConfigAsset : ScriptableObject
    {
        [SerializeField] private ProjectileEmitterAnchorMode _anchorMode;
        [SerializeField] private Vector2 _anchorOffset;
        [SerializeField] private float _durationSeconds = 1f;
        [SerializeField] private float _fireIntervalSeconds = 1f;
        [SerializeField] private ProjectilePatternConfig _pattern = new ProjectilePatternConfig(ProjectilePatternType.Single, Vector2.right, 1);
        [SerializeField] private ProjectileConfigAsset _projectile;

        public ProjectileEmitterAnchorMode AnchorMode => _anchorMode;
        public Vector2 AnchorOffset => _anchorOffset;
        public float DurationSeconds => _durationSeconds;
        public float FireIntervalSeconds => _fireIntervalSeconds;
        public ProjectilePatternConfig Pattern => _pattern;
        public ProjectileConfigAsset Projectile => _projectile;
    }

    [Serializable]
    public struct ProjectilePatternConfig
    {
        [SerializeField] private ProjectilePatternType _type;
        [SerializeField] private ProjectileDirectionMode _directionMode;
        [SerializeField] private Vector2 _direction;
        [SerializeField] private int _projectileCount;

        public ProjectilePatternConfig(ProjectilePatternType type, Vector2 direction, int projectileCount)
            : this(type, ProjectileDirectionMode.FixedDirection, direction, projectileCount)
        {
        }

        public ProjectilePatternConfig(ProjectilePatternType type, ProjectileDirectionMode directionMode, Vector2 direction, int projectileCount)
        {
            _type = type;
            _directionMode = directionMode;
            _direction = direction;
            _projectileCount = projectileCount;
        }

        public ProjectilePatternType Type => _type;
        public ProjectileDirectionMode DirectionMode => _directionMode;
        public Vector2 Direction => _direction;
        public int ProjectileCount => _projectileCount;
    }

    [Serializable]
    public struct BattleEffectConfig
    {
        [SerializeField] private BattleEffectType _type;
        [SerializeField] private int _amount;
        [SerializeField] private StatusConfigAsset _status;
        [SerializeField] private ProjectileEmitterConfigAsset _projectileEmitter;
        [SerializeField] private AreaEffectConfigAsset _areaEffect;

        public BattleEffectConfig(BattleEffectType type, int amount, StatusConfigAsset status)
            : this(type, amount, status, null)
        {
        }

        public BattleEffectConfig(
            BattleEffectType type,
            int amount,
            StatusConfigAsset status,
            ProjectileEmitterConfigAsset projectileEmitter)
            : this(type, amount, status, projectileEmitter, null)
        {
        }

        public BattleEffectConfig(
            BattleEffectType type,
            int amount,
            StatusConfigAsset status,
            ProjectileEmitterConfigAsset projectileEmitter,
            AreaEffectConfigAsset areaEffect)
        {
            _type = type;
            _amount = amount;
            _status = status;
            _projectileEmitter = projectileEmitter;
            _areaEffect = areaEffect;
        }

        public BattleEffectType Type => _type;
        public int Amount => _amount;
        public StatusConfigAsset Status => _status;
        public ProjectileEmitterConfigAsset ProjectileEmitter => _projectileEmitter;
        public AreaEffectConfigAsset AreaEffect => _areaEffect;

    }
}
