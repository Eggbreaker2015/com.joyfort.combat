using System;
using System.Collections.Generic;

namespace Combat.Core.Spatial
{
    internal sealed class SpatialQueryWorkspace
    {
        private const int DefaultCapacity = 8;
        private SpatialHit[] _hits = new SpatialHit[DefaultCapacity];
        private int[] _candidateIndices = new int[DefaultCapacity];

        public int HitCount { get; private set; }
        public int CandidateCount { get; private set; }

        public SpatialHit GetHit(int index)
        {
            if (index < 0 || index >= HitCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _hits[index];
        }

        internal void Reset(int capacity)
        {
            HitCount = 0;
            CandidateCount = 0;
            EnsureCapacity(capacity);
        }

        internal void AddCandidate(int proxyIndex)
        {
            EnsureCapacity(CandidateCount + 1);
            _candidateIndices[CandidateCount++] = proxyIndex;
        }

        internal int GetCandidate(int index)
        {
            if (index < 0 || index >= CandidateCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _candidateIndices[index];
        }

        internal void Add(SpatialHit hit)
        {
            EnsureCapacity(HitCount + 1);
            _hits[HitCount++] = hit;
        }

        internal void SortByProxyId()
        {
            Array.Sort(_hits, 0, HitCount, ProxyIdComparer.Instance);
        }

        internal void SortByFractionThenProxyId()
        {
            Array.Sort(_hits, 0, HitCount, FractionComparer.Instance);
        }

        private void EnsureCapacity(int capacity)
        {
            if (_hits.Length >= capacity)
            {
                return;
            }

            int nextCapacity = _hits.Length;
            while (nextCapacity < capacity)
            {
                nextCapacity *= 2;
            }

            Array.Resize(ref _hits, nextCapacity);
            Array.Resize(ref _candidateIndices, nextCapacity);
        }

        private sealed class ProxyIdComparer : IComparer<SpatialHit>
        {
            public static readonly ProxyIdComparer Instance = new ProxyIdComparer();

            public int Compare(SpatialHit left, SpatialHit right)
            {
                return left.ProxyId.CompareTo(right.ProxyId);
            }
        }

        private sealed class FractionComparer : IComparer<SpatialHit>
        {
            public static readonly FractionComparer Instance = new FractionComparer();

            public int Compare(SpatialHit left, SpatialHit right)
            {
                int fractionComparison = left.Fraction.CompareTo(right.Fraction);
                return fractionComparison != 0
                    ? fractionComparison
                    : left.ProxyId.CompareTo(right.ProxyId);
            }
        }
    }
}
