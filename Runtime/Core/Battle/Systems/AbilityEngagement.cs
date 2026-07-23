namespace Combat.Core.Battle
{
    internal static class AbilityEngagement
    {
        public static bool TryGetMovementRange(AbilityComponent abilities, out BattleScalar range)
        {
            for (var i = AbilityComponent.FirstSkillAbilityIndex; i < abilities.Abilities.Count; i++)
            {
                AbilityState ability = abilities.Abilities[i];
                if (ability.CooldownRemainingTicks <= 0)
                {
                    range = ability.Range;
                    return true;
                }
            }

            if (abilities.Abilities.Count > AbilityComponent.BasicAbilityIndex)
            {
                range = abilities.Abilities[AbilityComponent.BasicAbilityIndex].Range;
                return true;
            }

            range = BattleScalar.Zero;
            return false;
        }

        public static bool HasReadyAbilityInRange(AbilityComponent abilities, BattleScalar distance)
        {
            for (var i = AbilityComponent.FirstSkillAbilityIndex; i < abilities.Abilities.Count; i++)
            {
                if (CanUse(abilities.Abilities[i], distance))
                {
                    return true;
                }
            }

            return abilities.Abilities.Count > AbilityComponent.BasicAbilityIndex
                && CanUse(abilities.Abilities[AbilityComponent.BasicAbilityIndex], distance);
        }

        private static bool CanUse(AbilityState ability, BattleScalar distance)
        {
            return ability.CooldownRemainingTicks <= 0 && distance <= ability.Range;
        }
    }
}
