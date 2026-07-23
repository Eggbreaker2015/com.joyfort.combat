using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    [CreateAssetMenu(menuName = "Combat/Battle Scenario", fileName = "BattleScenario")]
    public sealed class BattleScenarioAsset : ScriptableObject
    {
        [SerializeField] private int _ticksPerSecond = 30;
        [SerializeField] private float _maxDurationSeconds = 60f;
        [Header("Movement")]
        [Tooltip("Enables deterministic RVO-like dynamic unit avoidance and overlap recovery.")]
        [SerializeField] private bool _localAvoidanceEnabled;
        [SerializeField] private bool _projectileCullingEnabled;
        [SerializeField] private Vector2 _projectileCullingCenter = Vector2.zero;
        [SerializeField] private Vector2 _projectileCullingSize = new Vector2(16f, 9f);
        [SerializeField] private float _projectileCullingPadding = 2f;
        [SerializeField] private bool _automaticVictoryEnabled = true;
        [SerializeField] private BattleSpatialMapAsset _spatialMap;
        [SerializeField] private SpawnEntry[] _initialSpawns = Array.Empty<SpawnEntry>();

        public int TicksPerSecond => _ticksPerSecond;
        public float MaxDurationSeconds => _maxDurationSeconds;
        public bool LocalAvoidanceEnabled => _localAvoidanceEnabled;
        public bool ProjectileCullingEnabled => _projectileCullingEnabled;
        public Vector2 ProjectileCullingCenter => _projectileCullingCenter;
        public Vector2 ProjectileCullingSize => _projectileCullingSize;
        public float ProjectileCullingPadding => _projectileCullingPadding;
        public bool AutomaticVictoryEnabled => _automaticVictoryEnabled;
        public BattleSpatialMapAsset SpatialMap => _spatialMap;
        public IReadOnlyList<SpawnEntry> InitialSpawns => _initialSpawns;
    }

    [Serializable]
    public struct SpawnEntry
    {
        [SerializeField] private int _teamId;
        [SerializeField] private CombatantConfigAsset _combatant;
        [SerializeField] private Vector2 _position;

        public SpawnEntry(int teamId, CombatantConfigAsset combatant, Vector2 position)
        {
            _teamId = teamId;
            _combatant = combatant;
            _position = position;
        }

        public int TeamId => _teamId;
        public CombatantConfigAsset Combatant => _combatant;
        public Vector2 Position => _position;
    }
}
