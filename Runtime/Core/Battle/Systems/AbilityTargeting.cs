namespace Combat.Core.Battle
{
    internal static class AbilityTargeting
    {
        public static bool TrySelectTarget(BattleWorld world, EntityId source, AbilityState ability, out EntityId target)
        {
            target = default;
            if (!world.IsAliveUnit(source)
                || !world.TryGetTeamId(source, out TeamId sourceTeamId)
                || !world.PositionComponents.TryGet(source, out PositionComponent sourcePosition))
            {
                return false;
            }

            switch (ability.TargetSelection)
            {
                case AbilityTargetSelection.CurrentEnemyTarget:
                    if (!world.TargetComponents.TryGet(source, out TargetComponent currentTarget)
                        || !TryCreateTargetCandidate(world, currentTarget.Target, sourceTeamId, BattleTargetTeamFilter.Enemies, out BattleUnitQueryResult enemyTarget))
                    {
                        return false;
                    }

                    target = enemyTarget.Entity;
                    return IsInRange(sourcePosition.Position, enemyTarget.Position, ability.Range);
                case AbilityTargetSelection.LowestHealthAlly:
                    if (!BattleUnitQuery.TrySelectLowestHealthInRadius(
                        world,
                        sourceTeamId,
                        BattleTargetTeamFilter.Allies,
                        sourcePosition.Position,
                        ability.Range,
                        out BattleUnitQueryResult allyTarget))
                    {
                        return false;
                    }

                    target = allyTarget.Entity;
                    return true;
                case AbilityTargetSelection.Self:
                    target = source;
                    return true;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(ability), ability.TargetSelection, "Unsupported ability target selection.");
            }
        }

        public static bool IsValidExplicitTarget(BattleWorld world, EntityId source, EntityId target, AbilityState ability)
        {
            if (!world.IsAliveUnit(source)
                || !world.TryGetTeamId(source, out TeamId sourceTeamId)
                || !world.PositionComponents.TryGet(source, out PositionComponent sourcePosition)
                || !BattleUnitQuery.TryGetAliveUnit(world, target, out BattleUnitQueryResult targetUnit)
                || !IsInRange(sourcePosition.Position, targetUnit.Position, ability.Range))
            {
                return false;
            }

            switch (ability.TargetSelection)
            {
                case AbilityTargetSelection.CurrentEnemyTarget:
                    return BattleUnitQuery.IsTeamAllowed(sourceTeamId, targetUnit.TeamId, BattleTargetTeamFilter.Enemies);
                case AbilityTargetSelection.LowestHealthAlly:
                    return BattleUnitQuery.IsTeamAllowed(sourceTeamId, targetUnit.TeamId, BattleTargetTeamFilter.Allies)
                        && BattleUnitQuery.TrySelectLowestHealthInRadius(
                            world,
                            sourceTeamId,
                            BattleTargetTeamFilter.Allies,
                            sourcePosition.Position,
                            ability.Range,
                            out BattleUnitQueryResult selected)
                        && selected.Entity.Equals(target);
                case AbilityTargetSelection.Self:
                    return target.Equals(source);
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(ability), ability.TargetSelection, "Unsupported ability target selection.");
            }
        }

        private static bool TryCreateTargetCandidate(
            BattleWorld world,
            EntityId target,
            TeamId sourceTeamId,
            BattleTargetTeamFilter teamFilter,
            out BattleUnitQueryResult result)
        {
            if (!BattleUnitQuery.TryGetAliveUnit(world, target, out result)
                || !BattleUnitQuery.IsTeamAllowed(sourceTeamId, result.TeamId, teamFilter))
            {
                result = default;
                return false;
            }

            return true;
        }

        private static bool IsInRange(BattleVector2 source, BattleVector2 target, BattleScalar range)
        {
            return BattleVector2.DistanceScalar(source, target) <= range;
        }
    }
}
