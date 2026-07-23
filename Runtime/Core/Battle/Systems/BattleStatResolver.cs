using System;
using System.Collections.Generic;

namespace Combat.Core.Battle
{
    internal static class BattleStatResolver
    {
        private static readonly StatusInstance[] EmptyStatuses = Array.Empty<StatusInstance>();

        public static BattleScalar ResolveScalar(BattleWorld world, EntityId entity, BattleStatId stat)
        {
            if (TryResolveScalar(world, entity, stat, out BattleScalar value))
            {
                return value;
            }

            throw new InvalidOperationException($"Cannot resolve stat {stat} for entity {entity}.");
        }

        public static bool TryResolveScalar(BattleWorld world, EntityId entity, BattleStatId stat, out BattleScalar value)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (!world.BaseStatsComponents.TryGet(entity, out BaseStatsComponent stats))
            {
                value = BattleScalar.Zero;
                return false;
            }

            BattleScalar baseValue = stats.Stats.RequireScalar(stat, $"Entity {entity}");
            value = BattleModifierResolver.ResolveScalarStat(baseValue, GetStatusesOrEmpty(world, entity), stat);
            return true;
        }

        public static int ResolveMaxHealth(BattleWorld world, EntityId entity)
        {
            if (TryResolveMaxHealth(world, entity, out int maxHealth))
            {
                return maxHealth;
            }

            throw new InvalidOperationException($"Cannot resolve max health for entity {entity}.");
        }

        public static bool TryResolveMaxHealth(BattleWorld world, EntityId entity, out int maxHealth)
        {
            if (!TryResolveScalar(world, entity, BattleStatId.MaxHealth, out BattleScalar value))
            {
                maxHealth = 0;
                return false;
            }

            maxHealth = value.ToIntRoundHalfUpSaturating();
            if (maxHealth < 1)
            {
                maxHealth = 1;
            }

            return true;
        }

        public static bool ClampHealthToEffectiveMax(BattleWorld world, EntityId entity)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (!world.HealthComponents.TryGet(entity, out HealthComponent health)
                || !TryResolveMaxHealth(world, entity, out int maxHealth)
                || health.Current <= maxHealth)
            {
                return false;
            }

            world.HealthComponents.Set(entity, new HealthComponent(maxHealth));
            return true;
        }

        private static IReadOnlyList<StatusInstance> GetStatusesOrEmpty(BattleWorld world, EntityId entity)
        {
            return world.StatusComponents.TryGet(entity, out StatusComponent statusComponent)
                ? statusComponent.Statuses
                : EmptyStatuses;
        }
    }
}
