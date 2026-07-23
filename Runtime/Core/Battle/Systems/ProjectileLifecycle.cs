using System.Collections.Generic;
using Combat.Foundation.Events;

namespace Combat.Core.Battle
{
    internal static class ProjectileLifecycle
    {
        public static bool HasPendingDestroy(BattleWorld world, EntityId entity)
        {
            IReadOnlyList<DestroyEntityCommand> destroyCommands =
                world.CommandBuffer.DestroyEntityCommands;
            for (var i = 0; i < destroyCommands.Count; i++)
            {
                if (destroyCommands[i].Entity.Equals(entity))
                {
                    return true;
                }
            }

            return false;
        }

        public static void QueueDestroy(
            BattleWorld world,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick,
            ProjectileId projectileId,
            EntityId projectileEntity,
            HashSet<EntityId> destroyedProjectiles)
        {
            if (!destroyedProjectiles.Add(projectileEntity)
                || HasPendingDestroy(world, projectileEntity))
            {
                return;
            }

            world.CommandBuffer.DestroyEntity(new DestroyEntityCommand(projectileEntity));
            events.Write(BattleEvent.ProjectileDestroyed(
                eventSequence.Next(),
                tick,
                projectileId));
        }
    }
}
