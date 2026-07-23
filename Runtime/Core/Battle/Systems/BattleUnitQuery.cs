using System;
using System.Collections.Generic;

namespace Combat.Core.Battle
{
    internal enum BattleTargetTeamFilter
    {
        Allies,
        Enemies,
        AllUnits
    }

    internal readonly struct BattleUnitQueryResult
    {
        public BattleUnitQueryResult(
            EntityId entity,
            UnitId unitId,
            TeamId teamId,
            BattleVector2 position,
            BattleScalar radius,
            int currentHealth,
            int maxHealth)
        {
            Entity = entity;
            UnitId = unitId;
            TeamId = teamId;
            Position = position;
            Radius = radius;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
        }

        public EntityId Entity { get; }
        public UnitId UnitId { get; }
        public TeamId TeamId { get; }
        public BattleVector2 Position { get; }
        public BattleScalar Radius { get; }
        public int CurrentHealth { get; }
        public int MaxHealth { get; }
    }

    internal static class BattleUnitQuery
    {
        public static void CollectAliveUnits(
            BattleWorld world,
            List<BattleUnitQueryResult> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            IReadOnlyList<EntityId> entities = world.UnitComponents.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                if (TryCreateAliveUnit(world, entities[i], out BattleUnitQueryResult candidate))
                {
                    results.Add(candidate);
                }
            }

            results.Sort(CompareByUnitId);
        }

        public static void CollectAliveUnits(
            BattleWorld world,
            TeamId sourceTeamId,
            BattleTargetTeamFilter teamFilter,
            List<BattleUnitQueryResult> results)
        {
            CollectAliveUnits(world, sourceTeamId, teamFilter, hasRadius: false, default, default, results);
        }

        public static void CollectAliveUnitsInRadius(
            BattleWorld world,
            TeamId sourceTeamId,
            BattleTargetTeamFilter teamFilter,
            BattleVector2 center,
            BattleScalar radius,
            List<BattleUnitQueryResult> results)
        {
            if (radius < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            CollectAliveUnits(world, sourceTeamId, teamFilter, hasRadius: true, center, radius, results);
        }

        public static bool TrySelectNearest(
            BattleWorld world,
            BattleVector2 center,
            TeamId sourceTeamId,
            BattleTargetTeamFilter teamFilter,
            out BattleUnitQueryResult result)
        {
            return TrySelectNearest(
                world,
                center,
                sourceTeamId,
                teamFilter,
                excludedEntity: default,
                out result);
        }

        public static bool TrySelectNearest(
            BattleWorld world,
            BattleVector2 center,
            TeamId sourceTeamId,
            BattleTargetTeamFilter teamFilter,
            EntityId excludedEntity,
            out BattleUnitQueryResult result)
        {
            result = default;
            var hasSelected = false;
            BattleScalar selectedDistance = BattleScalar.Zero;
            IReadOnlyList<EntityId> entities = world.UnitComponents.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                EntityId entity = entities[i];
                if (entity.Equals(excludedEntity)
                    || !TryCreateCandidate(
                        world,
                        entity,
                        sourceTeamId,
                        teamFilter,
                        out BattleUnitQueryResult candidate))
                {
                    continue;
                }

                BattleScalar distance = BattleVector2.SqrDistanceScalar(center, candidate.Position);
                if (!hasSelected
                    || distance < selectedDistance
                    || distance == selectedDistance && candidate.UnitId.Value < result.UnitId.Value)
                {
                    hasSelected = true;
                    selectedDistance = distance;
                    result = candidate;
                }
            }

            return hasSelected;
        }

        public static bool TrySelectNearestInRadius(
            BattleWorld world,
            BattleVector2 center,
            BattleScalar radius,
            TeamId sourceTeamId,
            BattleTargetTeamFilter teamFilter,
            EntityId excludedEntity,
            out BattleUnitQueryResult result)
        {
            if (radius < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            result = default;
            var hasSelected = false;
            BattleScalar selectedDistance = BattleScalar.Zero;
            BattleScalar radiusSquared = radius * radius;
            IReadOnlyList<EntityId> entities = world.UnitComponents.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                EntityId entity = entities[i];
                if (entity.Equals(excludedEntity)
                    || !TryCreateCandidate(
                        world,
                        entity,
                        sourceTeamId,
                        teamFilter,
                        out BattleUnitQueryResult candidate))
                {
                    continue;
                }

                BattleScalar distance =
                    BattleVector2.SqrDistanceScalar(center, candidate.Position);
                if (distance > radiusSquared)
                {
                    continue;
                }

                if (!hasSelected
                    || distance < selectedDistance
                    || distance == selectedDistance &&
                    candidate.UnitId.Value < result.UnitId.Value)
                {
                    hasSelected = true;
                    selectedDistance = distance;
                    result = candidate;
                }
            }

            return hasSelected;
        }

        public static bool TrySelectLowestHealth(
            BattleWorld world,
            TeamId sourceTeamId,
            BattleTargetTeamFilter teamFilter,
            out BattleUnitQueryResult result)
        {
            result = default;
            var hasSelected = false;
            IReadOnlyList<EntityId> entities = world.UnitComponents.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                if (!TryCreateCandidate(world, entities[i], sourceTeamId, teamFilter, out BattleUnitQueryResult candidate))
                {
                    continue;
                }

                if (!hasSelected
                    || candidate.CurrentHealth < result.CurrentHealth
                    || candidate.CurrentHealth == result.CurrentHealth && candidate.UnitId.Value < result.UnitId.Value)
                {
                    hasSelected = true;
                    result = candidate;
                }
            }

            return hasSelected;
        }

        public static bool TrySelectLowestHealthInRadius(
            BattleWorld world,
            TeamId sourceTeamId,
            BattleTargetTeamFilter teamFilter,
            BattleVector2 center,
            BattleScalar radius,
            out BattleUnitQueryResult result)
        {
            if (radius < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            result = default;
            var hasSelected = false;
            BattleScalar radiusSqr = radius * radius;
            IReadOnlyList<EntityId> entities = world.UnitComponents.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                if (!TryCreateCandidate(world, entities[i], sourceTeamId, teamFilter, out BattleUnitQueryResult candidate)
                    || BattleVector2.SqrDistanceScalar(center, candidate.Position) > radiusSqr)
                {
                    continue;
                }

                if (!hasSelected
                    || candidate.CurrentHealth < result.CurrentHealth
                    || candidate.CurrentHealth == result.CurrentHealth && candidate.UnitId.Value < result.UnitId.Value)
                {
                    hasSelected = true;
                    result = candidate;
                }
            }

            return hasSelected;
        }

        public static bool TryGetAliveUnit(BattleWorld world, EntityId entity, out BattleUnitQueryResult result)
        {
            return TryCreateAliveUnit(world, entity, out result);
        }

        public static bool IsTeamAllowed(TeamId sourceTeamId, TeamId candidateTeamId, BattleTargetTeamFilter teamFilter)
        {
            switch (teamFilter)
            {
                case BattleTargetTeamFilter.Allies:
                    return candidateTeamId.Equals(sourceTeamId);
                case BattleTargetTeamFilter.Enemies:
                    return !candidateTeamId.Equals(sourceTeamId);
                case BattleTargetTeamFilter.AllUnits:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(teamFilter), teamFilter, "Unsupported battle target team filter.");
            }
        }

        public static BattleTargetTeamFilter FromAreaEffectTargetFilter(AreaEffectTargetFilter filter)
        {
            switch (filter)
            {
                case AreaEffectTargetFilter.Allies:
                    return BattleTargetTeamFilter.Allies;
                case AreaEffectTargetFilter.Enemies:
                    return BattleTargetTeamFilter.Enemies;
                case AreaEffectTargetFilter.AllUnits:
                    return BattleTargetTeamFilter.AllUnits;
                default:
                    throw new ArgumentOutOfRangeException(nameof(filter), filter, "Unsupported area effect target filter.");
            }
        }

        public static int CompareByUnitId(BattleUnitQueryResult left, BattleUnitQueryResult right)
        {
            return left.UnitId.Value.CompareTo(right.UnitId.Value);
        }

        private static void CollectAliveUnits(
            BattleWorld world,
            TeamId sourceTeamId,
            BattleTargetTeamFilter teamFilter,
            bool hasRadius,
            BattleVector2 center,
            BattleScalar radius,
            List<BattleUnitQueryResult> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            BattleScalar radiusSqr = radius * radius;
            IReadOnlyList<EntityId> entities = world.UnitComponents.Entities;
            for (var i = 0; i < entities.Count; i++)
            {
                if (!TryCreateCandidate(world, entities[i], sourceTeamId, teamFilter, out BattleUnitQueryResult candidate))
                {
                    continue;
                }

                if (hasRadius && BattleVector2.SqrDistanceScalar(center, candidate.Position) > radiusSqr)
                {
                    continue;
                }

                results.Add(candidate);
            }

            results.Sort(CompareByUnitId);
        }

        private static bool TryCreateCandidate(
            BattleWorld world,
            EntityId entity,
            TeamId sourceTeamId,
            BattleTargetTeamFilter teamFilter,
            out BattleUnitQueryResult candidate)
        {
            if (!TryCreateAliveUnit(world, entity, out candidate)
                || !IsTeamAllowed(sourceTeamId, candidate.TeamId, teamFilter))
            {
                candidate = default;
                return false;
            }

            return true;
        }

        private static bool TryCreateAliveUnit(BattleWorld world, EntityId entity, out BattleUnitQueryResult result)
        {
            if (!world.IsBattlefieldActiveUnit(entity)
                || !world.TryGetUnitId(entity, out UnitId unitId)
                || !world.TryGetTeamId(entity, out TeamId teamId)
                || !world.PositionComponents.TryGet(entity, out PositionComponent position)
                || !world.HealthComponents.TryGet(entity, out HealthComponent health)
                || !BattleStatResolver.TryResolveMaxHealth(world, entity, out int maxHealth))
            {
                result = default;
                return false;
            }

            result = new BattleUnitQueryResult(
                entity,
                unitId,
                teamId,
                position.Position,
                position.Radius,
                health.Current,
                maxHealth);
            return true;
        }
    }
}
