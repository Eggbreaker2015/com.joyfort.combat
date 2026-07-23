using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    public enum AreaEffectTargetFilter
    {
        Allies,
        Enemies,
        AllUnits
    }

    public sealed class AreaEffectDefinition
    {
        private readonly BattleEffectDefinition[] _effects;
        private readonly ReadOnlyCollection<BattleEffectDefinition> _readOnlyEffects;

        public AreaEffectDefinition(BattleScalar radius, AreaEffectTargetFilter targetFilter, IReadOnlyList<BattleEffectDefinition> effects)
        {
            if (radius <= BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            Radius = radius;
            TargetFilter = ValidateTargetFilter(targetFilter);
            _effects = CopyEffects(effects);
            _readOnlyEffects = new ReadOnlyCollection<BattleEffectDefinition>(_effects);
        }

        public BattleScalar Radius { get; }
        public AreaEffectTargetFilter TargetFilter { get; }
        public IReadOnlyList<BattleEffectDefinition> Effects => _readOnlyEffects;

        internal static AreaEffectDefinition CopyValidated(AreaEffectDefinition areaEffect)
        {
            if (areaEffect == null)
            {
                throw new ArgumentNullException(nameof(areaEffect));
            }

            return new AreaEffectDefinition(areaEffect.Radius, areaEffect.TargetFilter, areaEffect.Effects);
        }

        private static AreaEffectTargetFilter ValidateTargetFilter(AreaEffectTargetFilter targetFilter)
        {
            switch (targetFilter)
            {
                case AreaEffectTargetFilter.Allies:
                case AreaEffectTargetFilter.Enemies:
                case AreaEffectTargetFilter.AllUnits:
                    return targetFilter;
                default:
                    throw new ArgumentOutOfRangeException(nameof(targetFilter), targetFilter, "Unsupported area effect target filter.");
            }
        }

        private static BattleEffectDefinition[] CopyEffects(IReadOnlyList<BattleEffectDefinition> effects)
        {
            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            if (effects.Count == 0)
            {
                throw new ArgumentException("Area effect requires at least one child effect.", nameof(effects));
            }

            var copy = new BattleEffectDefinition[effects.Count];
            for (var i = 0; i < effects.Count; i++)
            {
                BattleEffectDefinition effect = effects[i] ?? throw new ArgumentNullException(nameof(effects));
                if (effect.Type == BattleEffectType.AreaEffect)
                {
                    throw new ArgumentException("Area effect cannot contain another AreaEffect child.", nameof(effects));
                }

                copy[i] = BattleEffectDefinition.CopyValidated(effect);
            }

            return copy;
        }
    }
}
