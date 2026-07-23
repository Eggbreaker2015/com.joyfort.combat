using System.Collections.Generic;
using Combat.Core.Spatial;

namespace Combat.Core.Battle
{
    internal interface IProjectileCollisionDetector
    {
        void CollectHits(ProjectileCollisionFrame frame, IList<ProjectileHit> hits);
    }

    internal readonly struct ProjectileCollisionFrame
    {
        public ProjectileCollisionFrame(IReadOnlyList<ProjectileCollisionSnapshot> projectiles, IReadOnlyList<ProjectileTargetSnapshot> targets)
        {
            Projectiles = projectiles;
            Targets = targets;
        }

        public IReadOnlyList<ProjectileCollisionSnapshot> Projectiles { get; }
        public IReadOnlyList<ProjectileTargetSnapshot> Targets { get; }
    }

    internal readonly struct ProjectileCollisionSnapshot
    {
        public ProjectileCollisionSnapshot(
            ProjectileId projectileId,
            EntityId entity,
            EntityId source,
            TeamId teamId,
            BattleVector2 startPosition,
            BattleVector2 endPosition,
            float radius)
            : this(
                projectileId,
                entity,
                source,
                teamId,
                startPosition,
                endPosition,
                BattleScalar.FromFloat(radius))
        {
        }

        public ProjectileCollisionSnapshot(
            ProjectileId projectileId,
            EntityId entity,
            EntityId source,
            TeamId teamId,
            BattleVector2 startPosition,
            BattleVector2 endPosition,
            BattleScalar radius)
        {
            ProjectileId = projectileId;
            Entity = entity;
            Source = source;
            TeamId = teamId;
            StartPosition = startPosition;
            EndPosition = endPosition;
            Radius = radius >= BattleScalar.Zero ? radius : throw new System.ArgumentOutOfRangeException(nameof(radius));
        }

        public ProjectileId ProjectileId { get; }
        public EntityId Entity { get; }
        public EntityId Source { get; }
        public TeamId TeamId { get; }
        public BattleVector2 StartPosition { get; }
        public BattleVector2 EndPosition { get; }
        public BattleScalar Radius { get; }
    }

    internal readonly struct ProjectileTargetSnapshot
    {
        public ProjectileTargetSnapshot(UnitId unitId, EntityId entity, TeamId teamId, BattleVector2 position, float radius)
            : this(unitId, entity, teamId, position, BattleScalar.FromFloat(radius))
        {
        }

        public ProjectileTargetSnapshot(UnitId unitId, EntityId entity, TeamId teamId, BattleVector2 position, BattleScalar radius)
        {
            UnitId = unitId;
            Entity = entity;
            TeamId = teamId;
            Position = position;
            Radius = radius >= BattleScalar.Zero ? radius : throw new System.ArgumentOutOfRangeException(nameof(radius));
        }

        public UnitId UnitId { get; }
        public EntityId Entity { get; }
        public TeamId TeamId { get; }
        public BattleVector2 Position { get; }
        public BattleScalar Radius { get; }
    }

    internal readonly struct ProjectileHit
    {
        public ProjectileHit(
            ProjectileId projectileId,
            EntityId projectileEntity,
            UnitId targetUnitId,
            EntityId targetEntity,
            BattleVector2 position,
            BattleScalar fraction)
        {
            ProjectileId = projectileId;
            ProjectileEntity = projectileEntity;
            TargetUnitId = targetUnitId;
            TargetEntity = targetEntity;
            Position = position;
            Fraction = fraction;
        }

        public ProjectileId ProjectileId { get; }
        public EntityId ProjectileEntity { get; }
        public UnitId TargetUnitId { get; }
        public EntityId TargetEntity { get; }
        public BattleVector2 Position { get; }
        public BattleScalar Fraction { get; }
    }

    internal readonly struct ProjectileHitContext
    {
        public ProjectileHitContext(
            ProjectileId projectileId,
            EntityId projectileEntity,
            EntityId source,
            UnitId sourceUnitId,
            EntityId targetEntity,
            UnitId targetUnitId,
            BattleVector2 position,
            BattleScalar fraction,
            bool destroysProjectile)
        {
            ProjectileId = projectileId;
            ProjectileEntity = projectileEntity;
            Source = source;
            SourceUnitId = sourceUnitId;
            TargetEntity = targetEntity;
            TargetUnitId = targetUnitId;
            Position = position;
            Fraction = fraction;
            DestroysProjectile = destroysProjectile;
        }

        public ProjectileId ProjectileId { get; }
        public EntityId ProjectileEntity { get; }
        public EntityId Source { get; }
        public UnitId SourceUnitId { get; }
        public EntityId TargetEntity { get; }
        public UnitId TargetUnitId { get; }
        public BattleVector2 Position { get; }
        public BattleScalar Fraction { get; }
        public bool DestroysProjectile { get; }

        public ProjectileHitContext WithDestroyDecision(
            bool destroysProjectile)
        {
            return new ProjectileHitContext(
                ProjectileId,
                ProjectileEntity,
                Source,
                SourceUnitId,
                TargetEntity,
                TargetUnitId,
                Position,
                Fraction,
                destroysProjectile);
        }
    }

    internal sealed class CircleProjectileCollisionDetector : IProjectileCollisionDetector
    {
        private SpatialProxy[] _targetProxies = new SpatialProxy[8];
        private readonly DeterministicUniformGrid _grid =
            new DeterministicUniformGrid(BattleScalar.FromInt(2));
        private readonly SpatialQueryWorkspace _workspace = new SpatialQueryWorkspace();

        public void CollectHits(ProjectileCollisionFrame frame, IList<ProjectileHit> hits)
        {
            EnsureTargetCapacity(frame.Targets.Count);
            for (var targetIndex = 0; targetIndex < frame.Targets.Count; targetIndex++)
            {
                ProjectileTargetSnapshot target = frame.Targets[targetIndex];
                _targetProxies[targetIndex] = new SpatialProxy(
                    new SpatialProxyId(target.UnitId.Value),
                    target.Position,
                    SpatialShape2D.Circle(target.Radius),
                    SpatialCollisionFilter.All,
                    targetIndex);
            }

            _grid.Build(_targetProxies, frame.Targets.Count);

            for (var projectileIndex = 0; projectileIndex < frame.Projectiles.Count; projectileIndex++)
            {
                ProjectileCollisionSnapshot projectile = frame.Projectiles[projectileIndex];
                int hitCount = _grid.SweepCircle(
                    projectile.StartPosition,
                    projectile.EndPosition - projectile.StartPosition,
                    projectile.Radius,
                    SpatialCollisionFilter.All,
                    _workspace);
                for (var hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    SpatialHit spatialHit = _workspace.GetHit(hitIndex);
                    ProjectileTargetSnapshot target = frame.Targets[spatialHit.PayloadIndex];
                    if (projectile.TeamId.Equals(target.TeamId))
                    {
                        continue;
                    }

                    hits.Add(new ProjectileHit(
                        projectile.ProjectileId,
                        projectile.Entity,
                        target.UnitId,
                        target.Entity,
                        spatialHit.Position,
                        spatialHit.Fraction));
                }
            }
        }

        private void EnsureTargetCapacity(int capacity)
        {
            if (_targetProxies.Length >= capacity)
            {
                return;
            }

            int nextCapacity = _targetProxies.Length;
            while (nextCapacity < capacity)
            {
                nextCapacity *= 2;
            }

            System.Array.Resize(ref _targetProxies, nextCapacity);
        }
    }
}
