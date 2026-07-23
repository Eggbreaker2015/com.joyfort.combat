using System;
using System.Collections.Generic;
using Combat.Foundation.Events;

namespace Combat.Core.Battle
{
    internal static class ProjectileSystem
    {
        internal sealed class Scratch
        {
            private const int DefaultProjectileCapacity = 32;
            private const int DefaultTargetCapacity = 32;

            private readonly List<ProjectileCollisionSnapshot> _projectileSnapshots = new List<ProjectileCollisionSnapshot>(DefaultProjectileCapacity);
            private readonly List<ProjectileTargetSnapshot> _targetSnapshots = new List<ProjectileTargetSnapshot>(DefaultTargetCapacity);
            private readonly List<BattleUnitQueryResult> _unitSnapshots = new List<BattleUnitQueryResult>(DefaultTargetCapacity);
            private readonly List<ProjectileHit> _hits = new List<ProjectileHit>(DefaultProjectileCapacity);
            private readonly List<ProjectileHitContext> _resolvedHits = new List<ProjectileHitContext>(DefaultProjectileCapacity);
            private readonly List<ExpiredProjectile> _expiredProjectiles = new List<ExpiredProjectile>(DefaultProjectileCapacity);
            private readonly HashSet<EntityId> _destroyedProjectiles = new HashSet<EntityId>();
            private readonly HashSet<ProjectileId> _exhaustedProjectiles = new HashSet<ProjectileId>();

            internal List<ProjectileCollisionSnapshot> ProjectileSnapshots => _projectileSnapshots;
            internal List<ProjectileTargetSnapshot> TargetSnapshots => _targetSnapshots;
            internal List<BattleUnitQueryResult> UnitSnapshots => _unitSnapshots;
            internal List<ProjectileHit> Hits => _hits;
            internal List<ProjectileHitContext> ResolvedHits => _resolvedHits;
            internal List<ExpiredProjectile> ExpiredProjectiles => _expiredProjectiles;
            internal HashSet<EntityId> DestroyedProjectiles => _destroyedProjectiles;
            internal HashSet<ProjectileId> ExhaustedProjectiles => _exhaustedProjectiles;

            public void Clear()
            {
                ProjectileSnapshots.Clear();
                TargetSnapshots.Clear();
                UnitSnapshots.Clear();
                Hits.Clear();
                ResolvedHits.Clear();
                ExpiredProjectiles.Clear();
                DestroyedProjectiles.Clear();
                ExhaustedProjectiles.Clear();
            }

            public void EnsureCapacity(int projectileCapacity, int targetCapacity)
            {
                EnsureListCapacity(ProjectileSnapshots, projectileCapacity);
                EnsureListCapacity(TargetSnapshots, targetCapacity);
                EnsureListCapacity(UnitSnapshots, targetCapacity);
                EnsureListCapacity(Hits, projectileCapacity);
                EnsureListCapacity(ResolvedHits, projectileCapacity);
                EnsureListCapacity(ExpiredProjectiles, projectileCapacity);
            }

            private static void EnsureListCapacity<T>(List<T> list, int capacity)
            {
                if (list.Capacity < capacity)
                {
                    list.Capacity = capacity;
                }
            }
        }

        public static void Run(
            BattleWorld world,
            IProjectileCollisionDetector collisionDetector,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick,
            ProjectileCullingBounds cullingBounds = default)
        {
            if (world.ProjectileComponents.Entities.Count == 0)
            {
                return;
            }

            Run(world, collisionDetector, events, eventSequence, tick, new Scratch(), cullingBounds);
        }

        public static void Run(
            BattleWorld world,
            IProjectileCollisionDetector collisionDetector,
            EventBuffer<BattleEvent> events,
            EventSequence eventSequence,
            BattleTick tick,
            Scratch scratch,
            ProjectileCullingBounds cullingBounds = default)
        {
            if (scratch == null)
            {
                throw new ArgumentNullException(nameof(scratch));
            }

            IReadOnlyList<EntityId> projectileEntities = world.ProjectileComponents.Entities;
            if (projectileEntities.Count == 0)
            {
                scratch.Clear();
                return;
            }

            scratch.Clear();
            scratch.EnsureCapacity(projectileEntities.Count, world.UnitComponents.Entities.Count);
            List<ProjectileCollisionSnapshot> projectileSnapshots = scratch.ProjectileSnapshots;
            List<ExpiredProjectile> expiredProjectiles = scratch.ExpiredProjectiles;
            HashSet<EntityId> destroyedProjectiles = scratch.DestroyedProjectiles;
            for (var i = 0; i < projectileEntities.Count; i++)
            {
                EntityId entity = projectileEntities[i];
                if (ProjectileLifecycle.HasPendingDestroy(world, entity)
                    || !world.ProjectileComponents.TryGet(entity, out ProjectileComponent projectile)
                    || tick.Value < projectile.ActivateOnTick.Value)
                {
                    continue;
                }

                BattleVector2 startPosition = projectile.Position;
                BattleVector2 nextPosition = ProjectileMotion.Advance(projectile);
                int nextLifetime = projectile.LifetimeRemainingTicks - 1;
                ProjectileComponent updated = projectile.WithPositionAndLifetime(nextPosition, nextLifetime > 1 ? nextLifetime : 1);
                world.ProjectileComponents.Set(entity, updated);
                events.Write(BattleEvent.ProjectileMoved(eventSequence.Next(), tick, projectile.ProjectileId, nextPosition));

                if (cullingBounds.ShouldCull(nextPosition))
                {
                    ProjectileLifecycle.QueueDestroy(
                        world,
                        events,
                        eventSequence,
                        tick,
                        projectile.ProjectileId,
                        entity,
                        destroyedProjectiles);
                    continue;
                }

                projectileSnapshots.Add(new ProjectileCollisionSnapshot(
                    projectile.ProjectileId,
                    entity,
                    projectile.Source,
                    projectile.TeamId,
                    startPosition,
                    nextPosition,
                    projectile.Radius));

                if (nextLifetime <= 0)
                {
                    expiredProjectiles.Add(new ExpiredProjectile(projectile.ProjectileId, entity));
                }
            }

            List<ProjectileHit> hits = scratch.Hits;
            if (projectileSnapshots.Count > 0)
            {
                List<ProjectileTargetSnapshot> targetSnapshots = scratch.TargetSnapshots;
                BuildTargetSnapshots(world, targetSnapshots, scratch.UnitSnapshots);
                if (targetSnapshots.Count > 0)
                {
                    collisionDetector.CollectHits(new ProjectileCollisionFrame(projectileSnapshots, targetSnapshots), hits);
                }
            }

            List<ProjectileHitContext> resolvedHits = scratch.ResolvedHits;
            ProjectileHitResolution.SelectValidHits(
                world,
                hits,
                scratch.ExhaustedProjectiles,
                resolvedHits);
            ProjectileHitResolution.Apply(
                world,
                resolvedHits,
                events,
                eventSequence,
                tick,
                destroyedProjectiles);

            for (var i = 0; i < expiredProjectiles.Count; i++)
            {
                ExpiredProjectile expired = expiredProjectiles[i];
                ProjectileLifecycle.QueueDestroy(
                    world,
                    events,
                    eventSequence,
                    tick,
                    expired.ProjectileId,
                    expired.Entity,
                    destroyedProjectiles);
            }
        }

        private static void BuildTargetSnapshots(BattleWorld world, List<ProjectileTargetSnapshot> targets, List<BattleUnitQueryResult> units)
        {
            BattleUnitQuery.CollectAliveUnits(world, units);
            for (var i = 0; i < units.Count; i++)
            {
                BattleUnitQueryResult unit = units[i];
                targets.Add(new ProjectileTargetSnapshot(unit.UnitId, unit.Entity, unit.TeamId, unit.Position, unit.Radius));
            }
        }


        internal readonly struct ExpiredProjectile
        {
            public ExpiredProjectile(ProjectileId projectileId, EntityId entity)
            {
                ProjectileId = projectileId;
                Entity = entity;
            }

            public ProjectileId ProjectileId { get; }
            public EntityId Entity { get; }
        }
    }
}
