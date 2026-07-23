using System;
using Combat.Core.Battle;

namespace Combat.Core.Spatial
{
    internal static class SpatialQueries
    {
        public static int SweepCircle(
            BattleVector2 start,
            BattleVector2 delta,
            BattleScalar radius,
            SpatialCollisionFilter filter,
            SpatialProxy[] proxies,
            int proxyCount,
            SpatialQueryWorkspace workspace)
        {
            ValidateQuery(proxies, proxyCount, workspace);
            SpatialDomain.ValidatePosition(start, nameof(start));
            SpatialDomain.ValidateStep(delta, nameof(delta));
            SpatialDomain.ValidatePosition(start + delta, nameof(delta));
            SpatialDomain.ValidateShapeExtent(radius, nameof(radius));
            workspace.Reset(proxyCount);

            for (var i = 0; i < proxyCount; i++)
            {
                SpatialProxy proxy = proxies[i];
                if (!filter.Allows(proxy.Filter)
                    || proxy.Shape.Type != SpatialShapeType.Circle
                    || !SpatialGeometry.TrySweepCircleAgainstCircle(
                        start,
                        delta,
                        radius,
                        proxy.Position,
                        proxy.Shape.Radius,
                        out SpatialSweepHit sweep))
                {
                    continue;
                }

                workspace.Add(new SpatialHit(proxy.Id, proxy.PayloadIndex, sweep));
            }

            workspace.SortByFractionThenProxyId();
            return workspace.HitCount;
        }

        public static int OverlapCircle(
            BattleVector2 center,
            BattleScalar radius,
            SpatialCollisionFilter filter,
            SpatialProxy[] proxies,
            int proxyCount,
            SpatialQueryWorkspace workspace)
        {
            ValidateQuery(proxies, proxyCount, workspace);
            SpatialDomain.ValidatePosition(center, nameof(center));
            SpatialDomain.ValidateShapeExtent(radius, nameof(radius));
            workspace.Reset(proxyCount);

            for (var i = 0; i < proxyCount; i++)
            {
                SpatialProxy proxy = proxies[i];
                if (!filter.Allows(proxy.Filter))
                {
                    continue;
                }

                bool overlaps;
                switch (proxy.Shape.Type)
                {
                    case SpatialShapeType.Circle:
                        overlaps = SpatialGeometry.CirclesOverlap(
                            center,
                            radius,
                            proxy.Position,
                            proxy.Shape.Radius);
                        break;
                    case SpatialShapeType.Aabb:
                        overlaps = SpatialGeometry.CircleOverlapsAabb(
                            center,
                            radius,
                            new SpatialAabb(proxy.Position, proxy.Shape.HalfExtents));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(proxies),
                            proxy.Shape.Type,
                            "Unsupported spatial shape type.");
                }

                if (overlaps)
                {
                    workspace.Add(SpatialHit.QueryResult(proxy));
                }
            }

            workspace.SortByProxyId();
            return workspace.HitCount;
        }

        public static int QueryAabb(
            SpatialAabb bounds,
            SpatialCollisionFilter filter,
            SpatialProxy[] proxies,
            int proxyCount,
            SpatialQueryWorkspace workspace)
        {
            ValidateQuery(proxies, proxyCount, workspace);
            workspace.Reset(proxyCount);

            for (var i = 0; i < proxyCount; i++)
            {
                SpatialProxy proxy = proxies[i];
                if (filter.Allows(proxy.Filter)
                    && bounds.Overlaps(SpatialAabb.FromProxy(proxy)))
                {
                    workspace.Add(SpatialHit.QueryResult(proxy));
                }
            }

            workspace.SortByProxyId();
            return workspace.HitCount;
        }

        private static void ValidateQuery(
            SpatialProxy[] proxies,
            int proxyCount,
            SpatialQueryWorkspace workspace)
        {
            if (proxies == null)
            {
                throw new ArgumentNullException(nameof(proxies));
            }

            if (proxyCount < 0 || proxyCount > proxies.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(proxyCount));
            }

            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            for (var i = 0; i < proxyCount; i++)
            {
                if (proxies[i].Id.Value <= 0)
                {
                    throw new ArgumentException("Spatial proxies must have positive stable IDs.", nameof(proxies));
                }

                for (var otherIndex = i + 1; otherIndex < proxyCount; otherIndex++)
                {
                    if (proxies[i].Id == proxies[otherIndex].Id)
                    {
                        throw new ArgumentException(
                            "Duplicate spatial proxy ID: " + proxies[i].Id.Value + ".",
                            nameof(proxies));
                    }
                }
            }
        }
    }
}
