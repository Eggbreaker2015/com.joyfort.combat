namespace Combat.Core.Battle
{
    internal static class UnitControlRules
    {
        public static bool CanMove(BattleWorld world, EntityId entity)
        {
            return world.IsAliveUnit(entity) && !HasLock(world, entity, BattleActionLocks.Movement);
        }

        public static bool CanTurn(BattleWorld world, EntityId entity)
        {
            return world.IsAliveUnit(entity) && !HasLock(world, entity, BattleActionLocks.Facing);
        }

        public static bool CanStartAction(BattleWorld world, EntityId entity)
        {
            return world.IsAliveUnit(entity) && !HasLock(world, entity, BattleActionLocks.StartAnotherAction);
        }

        private static bool HasLock(BattleWorld world, EntityId entity, BattleActionLocks flag)
        {
            return world.UnitActionComponents.TryGet(entity, out UnitActionComponent action)
                && action.IsActive
                && (action.Locks & flag) == flag;
        }
    }
}
