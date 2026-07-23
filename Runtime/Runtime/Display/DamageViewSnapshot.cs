using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Combat.Core.Battle;

namespace Combat.Runtime.Display
{
    public readonly struct DamageViewSnapshot
    {
        private static readonly ReadOnlyCollection<string> EmptyDamageTags = new ReadOnlyCollection<string>(Array.Empty<string>());

        private readonly string[] _damageTags;
        private readonly ReadOnlyCollection<string> _readOnlyDamageTags;

        public DamageViewSnapshot(
            UnitId sourceUnitId,
            UnitId targetUnitId,
            int amount,
            BattleEffectSourceKind sourceKind,
            bool hasEffectType,
            BattleEffectType effectType,
            string abilityId,
            string statusId,
            ProjectileId projectileId,
            IReadOnlyList<string> damageTags)
        {
            SourceUnitId = sourceUnitId;
            TargetUnitId = targetUnitId;
            Amount = amount;
            SourceKind = sourceKind;
            HasEffectType = hasEffectType;
            EffectType = hasEffectType ? effectType : default;
            AbilityId = abilityId;
            StatusId = statusId;
            ProjectileId = projectileId;
            _damageTags = CopyDamageTags(damageTags);
            _readOnlyDamageTags = _damageTags.Length == 0 ? EmptyDamageTags : new ReadOnlyCollection<string>(_damageTags);
        }

        public UnitId SourceUnitId { get; }
        public UnitId TargetUnitId { get; }
        public int Amount { get; }
        public BattleEffectSourceKind SourceKind { get; }
        public bool HasEffectType { get; }
        public BattleEffectType EffectType { get; }
        public string AbilityId { get; }
        public string StatusId { get; }
        public ProjectileId ProjectileId { get; }
        public IReadOnlyList<string> DamageTags => _readOnlyDamageTags ?? EmptyDamageTags;

        private static string[] CopyDamageTags(IReadOnlyList<string> damageTags)
        {
            if (damageTags == null || damageTags.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[damageTags.Count];
            for (var i = 0; i < damageTags.Count; i++)
            {
                copy[i] = damageTags[i] ?? string.Empty;
            }

            return copy;
        }
    }
}
