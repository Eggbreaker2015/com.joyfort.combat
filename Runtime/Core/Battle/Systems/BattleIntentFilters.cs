namespace Combat.Core.Battle
{
    internal static class BattleIntentFilters
    {
        public static bool AllowsAutoBehavior(BattleWorld world, EntityId entity)
        {
            if (!world.IntentComponents.TryGet(entity, out IntentComponent intent))
            {
                return true;
            }

            switch (intent.Intent.Type)
            {
                case BattleIntentType.Auto:
                    return true;
                case BattleIntentType.FocusTarget:
                    return world.TargetComponents.TryGet(entity, out TargetComponent target)
                        && target.Target.Equals(intent.Intent.Target)
                        && IsValidFocusTarget(world, entity, intent.Intent.Target);
                case BattleIntentType.Hold:
                case BattleIntentType.MoveToPosition:
                case BattleIntentType.UseAbility:
                    return false;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(intent), intent.Intent.Type, "Unsupported battle intent type.");
            }
        }

        public static bool AllowsAutoTargetSelection(BattleWorld world, EntityId entity)
        {
            return !world.IntentComponents.TryGet(entity, out IntentComponent intent)
                || intent.Intent.Type == BattleIntentType.Auto;
        }

        public static bool TryGetMoveToPosition(BattleWorld world, EntityId entity, out BattleVector2 destination)
        {
            if (world.IntentComponents.TryGet(entity, out IntentComponent intent)
                && intent.Intent.Type == BattleIntentType.MoveToPosition)
            {
                destination = intent.Intent.Destination;
                return true;
            }

            destination = default;
            return false;
        }

        public static bool TryGetFocusTarget(BattleWorld world, EntityId entity, out EntityId target)
        {
            if (world.IntentComponents.TryGet(entity, out IntentComponent intent)
                && intent.Intent.Type == BattleIntentType.FocusTarget)
            {
                target = intent.Intent.Target;
                return true;
            }

            target = default;
            return false;
        }

        public static bool TryGetUseAbility(BattleWorld world, EntityId entity, out BattleIntent intent)
        {
            if (world.IntentComponents.TryGet(entity, out IntentComponent component)
                && component.Intent.Type == BattleIntentType.UseAbility)
            {
                intent = component.Intent;
                return true;
            }

            intent = default;
            return false;
        }

        public static bool IsValidFocusTarget(BattleWorld world, EntityId source, EntityId target)
        {
            return world.TryGetTeamId(source, out TeamId sourceTeamId)
                && BattleUnitQuery.TryGetAliveUnit(world, target, out BattleUnitQueryResult targetUnit)
                && BattleUnitQuery.IsTeamAllowed(sourceTeamId, targetUnit.TeamId, BattleTargetTeamFilter.Enemies);
        }
    }
}
