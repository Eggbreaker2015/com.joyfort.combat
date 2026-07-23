using System.Collections.Generic;
using Combat.Foundation.Events;

namespace Combat.Core.Battle
{
    internal static class StatusSystem
    {
        public static void Run(BattleWorld world, EventBuffer<BattleEvent> events, EventSequence eventSequence, BattleTick tick)
        {
            IReadOnlyList<EntityId> entities = world.StatusComponents.Entities;
            var owners = new EntityId[entities.Count];
            for (var i = 0; i < entities.Count; i++)
            {
                owners[i] = entities[i];
            }

            for (var i = 0; i < owners.Length; i++)
            {
                EntityId owner = owners[i];
                if (!world.StatusComponents.Has(owner))
                {
                    continue;
                }

                if (!world.IsAliveUnit(owner))
                {
                    world.StatusComponents.Remove(owner);
                    continue;
                }

                StatusComponent component = world.StatusComponents.Get(owner);
                var nextStatuses = new List<StatusInstance>(component.Statuses.Count);
                for (var statusIndex = 0; statusIndex < component.Statuses.Count; statusIndex++)
                {
                    StatusInstance status = component.Statuses[statusIndex];
                    int nextDuration = status.DurationRemainingTicks - 1;
                    int nextPeriodicEffect = status.TicksUntilNextPeriodicEffect - 1;

                    if (nextPeriodicEffect == 0)
                    {
                        if (status.PeriodicDamage > 0 && world.IsAliveUnit(status.Source))
                        {
                            world.CommandBuffer.QueueEffect(BattleEffectCommand.Damage(
                                status.Source,
                                owner,
                                status.PeriodicDamage,
                                BattleEffectContext.Status(status.Id, BattleEffectType.Damage)));
                        }

                        nextPeriodicEffect = status.TickIntervalTicks;
                    }

                    if (nextDuration == 0)
                    {
                        if (world.TryGetUnitId(owner, out UnitId ownerUnitId))
                        {
                            events.Write(BattleEvent.StatusExpired(eventSequence.Next(), tick, ownerUnitId, status.Id, status.Polarity));
                        }

                        continue;
                    }

                    nextStatuses.Add(status.WithTiming(nextDuration, nextPeriodicEffect));
                }

                if (nextStatuses.Count == 0)
                {
                    world.StatusComponents.Remove(owner);
                    BattleStatResolver.ClampHealthToEffectiveMax(world, owner);
                    continue;
                }

                world.StatusComponents.Set(owner, component.WithStatuses(nextStatuses));
                BattleStatResolver.ClampHealthToEffectiveMax(world, owner);
            }
        }
    }
}
