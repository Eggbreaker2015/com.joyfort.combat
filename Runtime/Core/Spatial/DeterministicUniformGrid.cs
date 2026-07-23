using System;
using System.Collections.Generic;
using Combat.Core.Battle;

namespace Combat.Core.Spatial
{
    internal sealed class DeterministicUniformGrid
    {
        private const int DefaultCapacity = 8;

        private readonly BattleScalar _cellSize;
        private SpatialProxy[] _proxies = new SpatialProxy[DefaultCapacity];
        private GridEntry[] _entries = new GridEntry[DefaultCapacity];
        private int _proxyCount;
        private BattleScalar _maximumHalfExtentX;
        private BattleScalar _maximumHalfExtentY;
        private bool _isBuilt;

        public DeterministicUniformGrid(BattleScalar cellSize)
        {
            if (cellSize <= BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            }

            SpatialDomain.ValidateShapeExtent(cellSize, nameof(cellSize));
            _cellSize = cellSize;
        }

        public int ProxyCount => _proxyCount;

        internal static long FloorToCell(long coordinateRaw, long cellSizeRaw)
        {
            if (cellSizeRaw <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSizeRaw));
            }

            long quotient = coordinateRaw / cellSizeRaw;
            long remainder = coordinateRaw % cellSizeRaw;
            return remainder != 0L && coordinateRaw < 0L ? quotient - 1L : quotient;
        }

        public void Build(SpatialProxy[] proxies, int proxyCount)
        {
            _isBuilt = false;
            _proxyCount = 0;
            if (proxies == null)
            {
                throw new ArgumentNullException(nameof(proxies));
            }

            if (proxyCount < 0 || proxyCount > proxies.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(proxyCount));
            }

            EnsureCapacity(proxyCount);
            Array.Copy(proxies, _proxies, proxyCount);
            Array.Sort(_proxies, 0, proxyCount, ProxyIdComparer.Instance);

            _maximumHalfExtentX = BattleScalar.Zero;
            _maximumHalfExtentY = BattleScalar.Zero;
            for (var i = 0; i < proxyCount; i++)
            {
                SpatialProxy proxy = _proxies[i];
                if (proxy.Id.Value <= 0)
                {
                    throw new ArgumentException(
                        "Spatial proxies must have positive stable IDs.",
                        nameof(proxies));
                }

                if (i > 0 && _proxies[i - 1].Id == proxy.Id)
                {
                    throw new ArgumentException(
                        "Duplicate spatial proxy ID: " + proxy.Id.Value + ".",
                        nameof(proxies));
                }

                BattleVector2 halfExtents = GetHalfExtents(proxy.Shape);
                if (halfExtents.XScalar > _maximumHalfExtentX)
                {
                    _maximumHalfExtentX = halfExtents.XScalar;
                }

                if (halfExtents.YScalar > _maximumHalfExtentY)
                {
                    _maximumHalfExtentY = halfExtents.YScalar;
                }

                _entries[i] = new GridEntry(
                    FloorToCell(proxy.Position.XRaw, _cellSize.RawValue),
                    FloorToCell(proxy.Position.YRaw, _cellSize.RawValue),
                    i,
                    proxy.Id);
            }

            Array.Sort(_entries, 0, proxyCount, GridEntryComparer.Instance);
            _proxyCount = proxyCount;
            _isBuilt = true;
        }

        public int SweepCircle(
            BattleVector2 start,
            BattleVector2 delta,
            BattleScalar radius,
            SpatialCollisionFilter filter,
            SpatialQueryWorkspace workspace)
        {
            EnsureReady(workspace);
            SpatialDomain.ValidatePosition(start, nameof(start));
            SpatialDomain.ValidateStep(delta, nameof(delta));
            BattleVector2 end = start + delta;
            SpatialDomain.ValidatePosition(end, nameof(delta));
            SpatialDomain.ValidateShapeExtent(radius, nameof(radius));

            BattleScalar expansionX = radius + _maximumHalfExtentX;
            BattleScalar expansionY = radius + _maximumHalfExtentY;
            CollectCandidates(
                Minimum(start.XScalar, end.XScalar) - expansionX,
                Maximum(start.XScalar, end.XScalar) + expansionX,
                Minimum(start.YScalar, end.YScalar) - expansionY,
                Maximum(start.YScalar, end.YScalar) + expansionY,
                workspace);

            for (var i = 0; i < workspace.CandidateCount; i++)
            {
                SpatialProxy proxy = _proxies[workspace.GetCandidate(i)];
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

        public int OverlapCircle(
            BattleVector2 center,
            BattleScalar radius,
            SpatialCollisionFilter filter,
            SpatialQueryWorkspace workspace)
        {
            EnsureReady(workspace);
            SpatialDomain.ValidatePosition(center, nameof(center));
            SpatialDomain.ValidateShapeExtent(radius, nameof(radius));

            CollectCandidates(
                center.XScalar - radius - _maximumHalfExtentX,
                center.XScalar + radius + _maximumHalfExtentX,
                center.YScalar - radius - _maximumHalfExtentY,
                center.YScalar + radius + _maximumHalfExtentY,
                workspace);

            for (var i = 0; i < workspace.CandidateCount; i++)
            {
                SpatialProxy proxy = _proxies[workspace.GetCandidate(i)];
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
                            nameof(proxy),
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

        public int QueryAabb(
            SpatialAabb bounds,
            SpatialCollisionFilter filter,
            SpatialQueryWorkspace workspace)
        {
            EnsureReady(workspace);
            CollectCandidates(
                bounds.Minimum.XScalar - _maximumHalfExtentX,
                bounds.Maximum.XScalar + _maximumHalfExtentX,
                bounds.Minimum.YScalar - _maximumHalfExtentY,
                bounds.Maximum.YScalar + _maximumHalfExtentY,
                workspace);

            for (var i = 0; i < workspace.CandidateCount; i++)
            {
                SpatialProxy proxy = _proxies[workspace.GetCandidate(i)];
                if (filter.Allows(proxy.Filter)
                    && bounds.Overlaps(SpatialAabb.FromProxy(proxy)))
                {
                    workspace.Add(SpatialHit.QueryResult(proxy));
                }
            }

            workspace.SortByProxyId();
            return workspace.HitCount;
        }

        private void CollectCandidates(
            BattleScalar minimumX,
            BattleScalar maximumX,
            BattleScalar minimumY,
            BattleScalar maximumY,
            SpatialQueryWorkspace workspace)
        {
            workspace.Reset(_proxyCount);
            if (_proxyCount == 0)
            {
                return;
            }

            long cellSizeRaw = _cellSize.RawValue;
            long minimumCellX = FloorToCell(minimumX.RawValue, cellSizeRaw);
            long maximumCellX = FloorToCell(maximumX.RawValue, cellSizeRaw);
            long minimumCellY = FloorToCell(minimumY.RawValue, cellSizeRaw);
            long maximumCellY = FloorToCell(maximumY.RawValue, cellSizeRaw);
            int startIndex = FindFirstCellX(minimumCellX);
            for (var i = startIndex; i < _proxyCount; i++)
            {
                GridEntry entry = _entries[i];
                if (entry.CellX > maximumCellX)
                {
                    break;
                }

                if (entry.CellY >= minimumCellY && entry.CellY <= maximumCellY)
                {
                    workspace.AddCandidate(entry.ProxyIndex);
                }
            }
        }

        private int FindFirstCellX(long minimumCellX)
        {
            int low = 0;
            int high = _proxyCount;
            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                if (_entries[middle].CellX < minimumCellX)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        private void EnsureReady(SpatialQueryWorkspace workspace)
        {
            if (!_isBuilt)
            {
                throw new InvalidOperationException(
                    "DeterministicUniformGrid must be built before querying.");
            }

            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }
        }

        private void EnsureCapacity(int capacity)
        {
            if (_proxies.Length >= capacity)
            {
                return;
            }

            int nextCapacity = _proxies.Length;
            while (nextCapacity < capacity)
            {
                nextCapacity *= 2;
            }

            Array.Resize(ref _proxies, nextCapacity);
            Array.Resize(ref _entries, nextCapacity);
        }

        private static BattleVector2 GetHalfExtents(SpatialShape2D shape)
        {
            switch (shape.Type)
            {
                case SpatialShapeType.Circle:
                    return new BattleVector2(shape.Radius, shape.Radius);
                case SpatialShapeType.Aabb:
                    return shape.HalfExtents;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(shape),
                        shape.Type,
                        "Unsupported spatial shape type.");
            }
        }

        private static BattleScalar Minimum(BattleScalar left, BattleScalar right)
        {
            return left <= right ? left : right;
        }

        private static BattleScalar Maximum(BattleScalar left, BattleScalar right)
        {
            return left >= right ? left : right;
        }

        private readonly struct GridEntry
        {
            public GridEntry(
                long cellX,
                long cellY,
                int proxyIndex,
                SpatialProxyId proxyId)
            {
                CellX = cellX;
                CellY = cellY;
                ProxyIndex = proxyIndex;
                ProxyId = proxyId;
            }

            public long CellX { get; }
            public long CellY { get; }
            public int ProxyIndex { get; }
            public SpatialProxyId ProxyId { get; }
        }

        private sealed class ProxyIdComparer : IComparer<SpatialProxy>
        {
            public static readonly ProxyIdComparer Instance = new ProxyIdComparer();

            public int Compare(SpatialProxy left, SpatialProxy right)
            {
                return left.Id.CompareTo(right.Id);
            }
        }

        private sealed class GridEntryComparer : IComparer<GridEntry>
        {
            public static readonly GridEntryComparer Instance = new GridEntryComparer();

            public int Compare(GridEntry left, GridEntry right)
            {
                int xComparison = left.CellX.CompareTo(right.CellX);
                if (xComparison != 0)
                {
                    return xComparison;
                }

                int yComparison = left.CellY.CompareTo(right.CellY);
                return yComparison != 0
                    ? yComparison
                    : left.ProxyId.CompareTo(right.ProxyId);
            }
        }
    }
}
