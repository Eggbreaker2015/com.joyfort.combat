using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    public enum BattleEffectSourceKind
    {
        Unknown,
        BasicAbility,
        Ability,
        Status,
        Projectile,
        Reaction
    }

    public readonly struct BattleEffectContext
    {
        private static readonly ReadOnlyCollection<string> EmptyDamageTags = new ReadOnlyCollection<string>(Array.Empty<string>());

        private readonly string[] _damageTags;
        private readonly ReadOnlyCollection<string> _readOnlyDamageTags;

        public BattleEffectContext(
            BattleEffectSourceKind sourceKind,
            BattleEffectType effectType,
            string abilityId,
            string statusId,
            ProjectileId projectileId,
            IReadOnlyList<string> damageTags)
            : this(sourceKind, true, effectType, abilityId, statusId, projectileId, damageTags)
        {
        }

        private BattleEffectContext(
            BattleEffectSourceKind sourceKind,
            bool hasEffectType,
            BattleEffectType effectType,
            string abilityId,
            string statusId,
            ProjectileId projectileId,
            IReadOnlyList<string> damageTags)
        {
            SourceKind = ValidateSourceKind(sourceKind);
            HasEffectType = hasEffectType;
            EffectType = hasEffectType ? ValidateEffectType(effectType) : default;
            AbilityId = abilityId;
            StatusId = statusId;
            ProjectileId = projectileId;
            _damageTags = CopyDamageTags(damageTags);
            _readOnlyDamageTags = _damageTags.Length == 0 ? EmptyDamageTags : new ReadOnlyCollection<string>(_damageTags);

            ValidateSourceFields(SourceKind, AbilityId, StatusId, ProjectileId);
        }

        public BattleEffectSourceKind SourceKind { get; }
        public bool HasEffectType { get; }
        public BattleEffectType EffectType { get; }
        public string AbilityId { get; }
        public string StatusId { get; }
        public ProjectileId ProjectileId { get; }
        public IReadOnlyList<string> DamageTags => _readOnlyDamageTags ?? EmptyDamageTags;

        public static BattleEffectContext Unknown()
        {
            return new BattleEffectContext(BattleEffectSourceKind.Unknown, false, default, null, null, default, Array.Empty<string>());
        }

        public static BattleEffectContext Unknown(BattleEffectType effectType)
        {
            return new BattleEffectContext(BattleEffectSourceKind.Unknown, effectType, null, null, default, Array.Empty<string>());
        }

        public static BattleEffectContext BasicAbility(string abilityId, BattleEffectType effectType)
        {
            return new BattleEffectContext(BattleEffectSourceKind.BasicAbility, effectType, abilityId, null, default, Array.Empty<string>());
        }

        public static BattleEffectContext Ability(string abilityId, BattleEffectType effectType)
        {
            return new BattleEffectContext(BattleEffectSourceKind.Ability, effectType, abilityId, null, default, Array.Empty<string>());
        }

        public static BattleEffectContext Status(string statusId, BattleEffectType effectType)
        {
            return new BattleEffectContext(BattleEffectSourceKind.Status, effectType, null, statusId, default, Array.Empty<string>());
        }

        public static BattleEffectContext Projectile(ProjectileId projectileId, BattleEffectType effectType)
        {
            return new BattleEffectContext(BattleEffectSourceKind.Projectile, effectType, null, null, projectileId, Array.Empty<string>());
        }

        public static BattleEffectContext Reaction(BattleEffectContext triggerContext, BattleEffectType effectType)
        {
            return new BattleEffectContext(BattleEffectSourceKind.Reaction, effectType, null, null, default, triggerContext.DamageTags);
        }

        public BattleEffectContext WithEffectType(BattleEffectType effectType)
        {
            return new BattleEffectContext(SourceKind, effectType, AbilityId, StatusId, ProjectileId, DamageTags);
        }

        private static BattleEffectSourceKind ValidateSourceKind(BattleEffectSourceKind sourceKind)
        {
            switch (sourceKind)
            {
                case BattleEffectSourceKind.Unknown:
                case BattleEffectSourceKind.BasicAbility:
                case BattleEffectSourceKind.Ability:
                case BattleEffectSourceKind.Status:
                case BattleEffectSourceKind.Projectile:
                case BattleEffectSourceKind.Reaction:
                    return sourceKind;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, "Unsupported battle effect source kind.");
            }
        }

        private static BattleEffectType ValidateEffectType(BattleEffectType effectType)
        {
            switch (effectType)
            {
                case BattleEffectType.Damage:
                case BattleEffectType.ApplyStatus:
                case BattleEffectType.SpawnProjectileEmitter:
                case BattleEffectType.Heal:
                case BattleEffectType.AreaEffect:
                    return effectType;
                default:
                    throw new ArgumentOutOfRangeException(nameof(effectType), effectType, "Unsupported battle effect type.");
            }
        }

        private static string[] CopyDamageTags(IReadOnlyList<string> damageTags)
        {
            if (damageTags == null || damageTags.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[damageTags.Count];
            for (var i = 0; i < damageTags.Count; i++)
            {
                string tag = damageTags[i];
                copy[i] = string.IsNullOrWhiteSpace(tag)
                    ? throw new ArgumentException("Damage tag is required.", nameof(damageTags))
                    : tag;
            }

            return copy;
        }

        private static void ValidateSourceFields(BattleEffectSourceKind sourceKind, string abilityId, string statusId, ProjectileId projectileId)
        {
            if ((sourceKind == BattleEffectSourceKind.BasicAbility || sourceKind == BattleEffectSourceKind.Ability)
                && string.IsNullOrWhiteSpace(abilityId))
            {
                throw new ArgumentException("Ability effect context requires an ability id.", nameof(abilityId));
            }

            if (sourceKind == BattleEffectSourceKind.Status && string.IsNullOrWhiteSpace(statusId))
            {
                throw new ArgumentException("Status effect context requires a status id.", nameof(statusId));
            }

            if (sourceKind == BattleEffectSourceKind.Projectile && projectileId.Value <= 0)
            {
                throw new ArgumentException("Projectile effect context requires a valid projectile id.", nameof(projectileId));
            }
        }
    }
}
