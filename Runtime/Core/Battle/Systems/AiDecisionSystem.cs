using System.Collections.Generic;

namespace Combat.Core.Battle
{
    internal static class AiDecisionSystem
    {
        public static void Run(BattleWorld world, BattleTick tick)
        {
            IReadOnlyList<EntityId> entities = world.BrainComponents.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                EntityId entity = entities[i];
                if (!world.BrainComponents.TryGet(entity, out BrainComponent brain))
                {
                    continue;
                }

                BrainState nextState = ResolveState(world, entity);
                if (nextState != brain.State)
                {
                    world.BrainComponents.Set(entity, brain.WithState(nextState, tick));
                }
            }
        }

        private static BrainState ResolveState(BattleWorld world, EntityId entity)
        {
            if (!world.IsAliveUnit(entity))
            {
                return BrainState.Dead;
            }

            if (!world.IsBattlefieldActiveUnit(entity))
            {
                return BrainState.Idle;
            }

            if (!world.TargetComponents.TryGet(entity, out TargetComponent target)
                || !world.IsBattlefieldActiveUnit(target.Target)
                || !world.AbilityComponents.TryGet(entity, out AbilityComponent abilities)
                || !world.PositionComponents.TryGet(entity, out PositionComponent position)
                || !world.PositionComponents.TryGet(target.Target, out PositionComponent targetPosition))
            {
                return BrainState.Idle;
            }

            BattleScalar distance = BattleVector2.DistanceScalar(position.Position, targetPosition.Position);
            return AbilityEngagement.HasReadyAbilityInRange(abilities, distance) ? BrainState.Attack : BrainState.Chase;
        }
    }
}
