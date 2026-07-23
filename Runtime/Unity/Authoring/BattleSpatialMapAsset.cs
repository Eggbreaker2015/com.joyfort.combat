using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    public enum BattleSpatialShape
    {
        Circle,
        Aabb
    }

    [Serializable]
    public struct BattleSpatialEntry
    {
        [SerializeField] private int _stableId;
        [SerializeField] private BattleSpatialShape _shape;
        [SerializeField] private Vector2 _center;
        [SerializeField] private float _radius;
        [SerializeField] private Vector2 _size;
        [SerializeField] private uint _categoryBits;
        [SerializeField] private uint _maskBits;

        public BattleSpatialEntry(
            int stableId,
            BattleSpatialShape shape,
            Vector2 center,
            float radius,
            Vector2 size,
            uint categoryBits,
            uint maskBits)
        {
            _stableId = stableId;
            _shape = shape;
            _center = center;
            _radius = radius;
            _size = size;
            _categoryBits = categoryBits;
            _maskBits = maskBits;
        }

        public int StableId => _stableId;
        public BattleSpatialShape Shape => _shape;
        public Vector2 Center => _center;
        public float Radius => _radius;
        public Vector2 Size => _size;
        public uint CategoryBits => _categoryBits;
        public uint MaskBits => _maskBits;
    }

    [CreateAssetMenu(menuName = "Combat/Battle Spatial Map", fileName = "BattleSpatialMap")]
    public sealed class BattleSpatialMapAsset : ScriptableObject
    {
        [SerializeField] private BattleSpatialEntry[] _entries =
            Array.Empty<BattleSpatialEntry>();

        public IReadOnlyList<BattleSpatialEntry> Entries => _entries;
    }
}
