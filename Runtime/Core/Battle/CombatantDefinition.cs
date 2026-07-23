using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    public sealed class TargetingBehaviorDefinition
    {
        private TargetingBehaviorDefinition()
        {
            LimitsAcquisitionRange = false;
            AcquisitionRange = BattleScalar.Zero;
            NoProgressTimeoutTicks = 0;
            MinimumProgressDistance = BattleScalar.Zero;
            RejectedTargetCooldownTicks = 0;
        }

        public TargetingBehaviorDefinition(
            BattleScalar acquisitionRange,
            int noProgressTimeoutTicks,
            BattleScalar minimumProgressDistance,
            int rejectedTargetCooldownTicks)
        {
            LimitsAcquisitionRange = true;
            AcquisitionRange = acquisitionRange > BattleScalar.Zero
                ? acquisitionRange
                : throw new ArgumentOutOfRangeException(nameof(acquisitionRange));
            NoProgressTimeoutTicks = noProgressTimeoutTicks > 0
                ? noProgressTimeoutTicks
                : throw new ArgumentOutOfRangeException(nameof(noProgressTimeoutTicks));
            MinimumProgressDistance = minimumProgressDistance > BattleScalar.Zero
                ? minimumProgressDistance
                : throw new ArgumentOutOfRangeException(nameof(minimumProgressDistance));
            RejectedTargetCooldownTicks = rejectedTargetCooldownTicks > 0
                ? rejectedTargetCooldownTicks
                : throw new ArgumentOutOfRangeException(nameof(rejectedTargetCooldownTicks));
        }

        public static TargetingBehaviorDefinition Unrestricted { get; } =
            new TargetingBehaviorDefinition();

        public bool LimitsAcquisitionRange { get; }
        public BattleScalar AcquisitionRange { get; }
        public int NoProgressTimeoutTicks { get; }
        public BattleScalar MinimumProgressDistance { get; }
        public int RejectedTargetCooldownTicks { get; }
    }

    public sealed class CombatantDefinition
    {
        private readonly AbilityDefinition[] _abilities;
        private readonly ReadOnlyCollection<AbilityDefinition> _readOnlyAbilities;

        public CombatantDefinition(string id, BattleScalar radius, BattleStatBlock stats, AbilityDefinition basicAbility, IReadOnlyList<AbilityDefinition> abilities)
            : this(id, radius, stats, basicAbility, abilities, null)
        {
        }

        public CombatantDefinition(string id, BattleScalar radius, BattleStatBlock stats, AbilityDefinition basicAbility, IReadOnlyList<AbilityDefinition> abilities, AiDefinition aiDefinition)
            : this(
                id,
                radius,
                stats,
                basicAbility,
                abilities,
                aiDefinition,
                TargetingBehaviorDefinition.Unrestricted)
        {
        }

        public CombatantDefinition(
            string id,
            BattleScalar radius,
            BattleStatBlock stats,
            AbilityDefinition basicAbility,
            IReadOnlyList<AbilityDefinition> abilities,
            AiDefinition aiDefinition,
            TargetingBehaviorDefinition targetingBehavior)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Combatant definition id is required.", nameof(id)) : id;
            Radius = radius >= BattleScalar.Zero ? radius : throw new ArgumentOutOfRangeException(nameof(radius));
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            BasicAbility = basicAbility ?? throw new ArgumentNullException(nameof(basicAbility));
            AiDefinition = aiDefinition;
            TargetingBehavior = targetingBehavior ??
                throw new ArgumentNullException(nameof(targetingBehavior));
            ValidateRequiredStats(Stats, Id);
            _abilities = CopyAbilities(BasicAbility, abilities);
            _readOnlyAbilities = new ReadOnlyCollection<AbilityDefinition>(_abilities);
        }

        public string Id { get; }
        public BattleScalar Radius { get; }
        public BattleStatBlock Stats { get; }
        public AbilityDefinition BasicAbility { get; }
        public AiDefinition AiDefinition { get; }
        public TargetingBehaviorDefinition TargetingBehavior { get; }
        public IReadOnlyList<AbilityDefinition> Abilities => _readOnlyAbilities;

        private static void ValidateRequiredStats(BattleStatBlock stats, string id)
        {
            string owner = $"Combatant '{id}'";
            int maxHealth = stats.RequireInt(BattleStatId.MaxHealth, owner);
            BattleScalar moveSpeed = stats.RequireScalar(BattleStatId.MoveSpeed, owner);

            if (maxHealth <= 0)
            {
                ThrowInvalidRequiredStat(nameof(stats), id, BattleStatId.MaxHealth, maxHealth, "greater than 0");
            }

            if (moveSpeed < BattleScalar.Zero)
            {
                ThrowInvalidRequiredStat(nameof(stats), id, BattleStatId.MoveSpeed, moveSpeed, "greater than or equal to 0");
            }
        }

        private static void ThrowInvalidRequiredStat(string parameterName, string id, BattleStatId stat, object value, string expected)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Combatant '{id}' stat {stat} must be {expected}; actual value was {value}.");
        }

        private static AbilityDefinition[] CopyAbilities(AbilityDefinition basicAbility, IReadOnlyList<AbilityDefinition> abilities)
        {
            if (abilities == null)
            {
                throw new ArgumentNullException(nameof(abilities));
            }

            var copy = new AbilityDefinition[abilities.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            ids.Add(basicAbility.Id);
            for (var i = 0; i < abilities.Count; i++)
            {
                AbilityDefinition ability = abilities[i] ?? throw new ArgumentNullException(nameof(abilities));
                if (!ids.Add(ability.Id))
                {
                    throw new ArgumentException($"Duplicate ability id: {ability.Id}.", nameof(abilities));
                }

                copy[i] = ability;
            }

            return copy;
        }
    }
}
