using System.Collections.Generic;

namespace Combat.Core.Battle
{
    internal static class VictorySystem
    {
        public static bool TryGetWinningTeam(BattleWorld world, out TeamId winningTeam)
        {
            var hasLivingUnit = false;
            winningTeam = default;

            IReadOnlyList<EntityId> entities = world.UnitComponents.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                EntityId entity = entities[i];
                if (!world.IsAliveUnit(entity) || !world.TeamComponents.TryGet(entity, out TeamComponent team))
                {
                    continue;
                }

                if (!hasLivingUnit)
                {
                    hasLivingUnit = true;
                    winningTeam = team.TeamId;
                    continue;
                }

                if (!team.TeamId.Equals(winningTeam))
                {
                    return false;
                }
            }

            return hasLivingUnit;
        }
    }
}
