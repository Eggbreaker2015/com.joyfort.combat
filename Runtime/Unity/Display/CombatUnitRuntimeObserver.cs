using System;
using Combat.Core.Battle;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Combat.Unity.Display
{
    public sealed class CombatUnitRuntimeObserver : MonoBehaviour
    {
        private IBattleRuntimeSnapshotSource _snapshotSource;
        private UnitId _boundUnitId;

        [HideInInspector, SerializeField] private bool _isBound;
        [HideInInspector, SerializeField] private bool _hasSnapshot;
        [HideInInspector, SerializeField] private int _unitId;
        [HideInInspector, SerializeField] private int _tick;

        [HideInInspector, SerializeField] private string _definitionId = string.Empty;
        [HideInInspector, SerializeField] private int _teamId;
        [HideInInspector, SerializeField] private Vector2 _position;
        [HideInInspector, SerializeField] private Vector2 _facing;
        [HideInInspector, SerializeField] private float _radius;

        [HideInInspector, SerializeField] private int _currentHealth;
        [HideInInspector, SerializeField] private int _maxHealth;
        [HideInInspector, SerializeField] private string _lifeState = string.Empty;

        [HideInInspector, SerializeField] private bool _hasBrain;
        [HideInInspector, SerializeField] private string _brainDefinitionId = string.Empty;
        [HideInInspector, SerializeField] private string _brainKind = string.Empty;
        [HideInInspector, SerializeField] private string _brainState = string.Empty;
        [HideInInspector, SerializeField] private int _brainStateEnteredTick;

        [HideInInspector, SerializeField] private bool _hasTarget;
        [HideInInspector, SerializeField] private int _targetUnitId;

        [HideInInspector, SerializeField] private float _moveSpeed;
        [HideInInspector, SerializeField] private ObservedAbility[] _abilities = Array.Empty<ObservedAbility>();
        [HideInInspector, SerializeField] private ObservedStatus[] _statuses = Array.Empty<ObservedStatus>();

        public void Bind(UnitId unitId, IBattleRuntimeSnapshotSource snapshotSource)
        {
            _boundUnitId = unitId;
            _snapshotSource = snapshotSource;
            _isBound = true;
            _unitId = unitId.Value;
            ClearSnapshotFields();
            Refresh();
        }

        public void SetSnapshotSource(IBattleRuntimeSnapshotSource snapshotSource)
        {
            _snapshotSource = snapshotSource;
            Refresh();
        }

        public void ClearBinding()
        {
            _boundUnitId = default;
            _snapshotSource = null;
            _isBound = false;
            _unitId = 0;
            ClearSnapshotFields();
        }

#if UNITY_EDITOR
        private void LateUpdate()
        {
            if (Selection.activeGameObject == gameObject)
            {
                Refresh();
            }
        }
#endif

        private void Refresh()
        {
            if (!_isBound || _snapshotSource == null)
            {
                ClearSnapshotFields();
                return;
            }

            if (!_snapshotSource.TryGetUnitRuntimeSnapshot(_boundUnitId, out UnitRuntimeSnapshot snapshot))
            {
                _hasSnapshot = false;
                ClearSnapshotFields();
                return;
            }

            Apply(snapshot);
        }

        private void Apply(UnitRuntimeSnapshot snapshot)
        {
            _hasSnapshot = true;
            _tick = snapshot.Tick.Value;
            _unitId = snapshot.UnitId.Value;
            _definitionId = snapshot.DefinitionId;
            _teamId = snapshot.TeamId.Value;
            _position = new Vector2(snapshot.Position.X, snapshot.Position.Y);
            _facing = new Vector2(snapshot.Facing.X, snapshot.Facing.Y);
            _radius = snapshot.Radius;
            _currentHealth = snapshot.CurrentHealth;
            _maxHealth = snapshot.MaxHealth;
            _lifeState = snapshot.LifeState;
            _hasBrain = snapshot.HasBrain;
            _brainDefinitionId = snapshot.BrainDefinitionId;
            _brainKind = snapshot.BrainKind;
            _brainState = snapshot.BrainState;
            _brainStateEnteredTick = snapshot.BrainStateEnteredTick.Value;
            _hasTarget = snapshot.HasTarget;
            _targetUnitId = snapshot.HasTarget ? snapshot.TargetUnitId.Value : 0;
            _moveSpeed = snapshot.MoveSpeed;
            _abilities = CopyAbilities(snapshot);
            _statuses = CopyStatuses(snapshot);
        }

        private void ClearSnapshotFields()
        {
            _hasSnapshot = false;
            _tick = 0;
            _definitionId = string.Empty;
            _teamId = 0;
            _position = Vector2.zero;
            _facing = Vector2.zero;
            _radius = 0f;
            _currentHealth = 0;
            _maxHealth = 0;
            _lifeState = string.Empty;
            _hasBrain = false;
            _brainDefinitionId = string.Empty;
            _brainKind = string.Empty;
            _brainState = string.Empty;
            _brainStateEnteredTick = 0;
            _hasTarget = false;
            _targetUnitId = 0;
            _moveSpeed = 0f;
            _abilities = Array.Empty<ObservedAbility>();
            _statuses = Array.Empty<ObservedStatus>();
        }

        private static ObservedAbility[] CopyAbilities(UnitRuntimeSnapshot snapshot)
        {
            var abilities = new ObservedAbility[snapshot.Abilities.Count];
            for (var i = 0; i < snapshot.Abilities.Count; i++)
            {
                abilities[i] = new ObservedAbility(snapshot.Abilities[i]);
            }

            return abilities;
        }

        private static ObservedStatus[] CopyStatuses(UnitRuntimeSnapshot snapshot)
        {
            var statuses = new ObservedStatus[snapshot.Statuses.Count];
            for (var i = 0; i < snapshot.Statuses.Count; i++)
            {
                statuses[i] = new ObservedStatus(snapshot.Statuses[i]);
            }

            return statuses;
        }

        internal void RefreshForTests()
        {
            Refresh();
        }

        internal bool IsBoundForTests => _isBound;
        internal bool HasSnapshotForTests => _hasSnapshot;
        internal int UnitIdForTests => _unitId;
        internal string DefinitionIdForTests => _definitionId;
        internal Vector2 FacingForTests => _facing;
        internal int CurrentHealthForTests => _currentHealth;
        internal string LifeStateForTests => _lifeState;
        internal int AbilityCountForTests => _abilities.Length;
        internal int StatusCountForTests => _statuses.Length;

        [Serializable]
        private sealed class ObservedAbility
        {
            [HideInInspector, SerializeField] private int _slotIndex;
            [HideInInspector, SerializeField] private bool _isBasic;
            [HideInInspector, SerializeField] private string _id = string.Empty;
            [HideInInspector, SerializeField] private float _range;
            [HideInInspector, SerializeField] private int _damage;
            [HideInInspector, SerializeField] private int _cooldownTicks;
            [HideInInspector, SerializeField] private int _cooldownRemainingTicks;

            public ObservedAbility(AbilityRuntimeSnapshot snapshot)
            {
                _slotIndex = snapshot.SlotIndex;
                _isBasic = snapshot.IsBasic;
                _id = snapshot.Id;
                _range = snapshot.Range;
                _damage = snapshot.Damage;
                _cooldownTicks = snapshot.CooldownTicks;
                _cooldownRemainingTicks = snapshot.CooldownRemainingTicks;
            }
        }

        [Serializable]
        private sealed class ObservedStatus
        {
            [HideInInspector, SerializeField] private string _id = string.Empty;
            [HideInInspector, SerializeField] private StatusPolarity _polarity;
            [HideInInspector, SerializeField] private bool _hasSourceUnit;
            [HideInInspector, SerializeField] private int _sourceUnitId;
            [HideInInspector, SerializeField] private int _durationRemainingTicks;
            [HideInInspector, SerializeField] private int _tickIntervalTicks;
            [HideInInspector, SerializeField] private int _ticksUntilNextPeriodicEffect;
            [HideInInspector, SerializeField] private int _periodicDamage;
            [HideInInspector, SerializeField] private int _modifierCount;
            [HideInInspector, SerializeField] private int _triggerCount;
            [HideInInspector, SerializeField] private int _stackCount;
            [HideInInspector, SerializeField] private int _maxStacks;

            public ObservedStatus(StatusRuntimeSnapshot snapshot)
            {
                _id = snapshot.Id;
                _polarity = snapshot.Polarity;
                _hasSourceUnit = snapshot.HasSourceUnit;
                _sourceUnitId = snapshot.HasSourceUnit ? snapshot.SourceUnitId.Value : 0;
                _durationRemainingTicks = snapshot.DurationRemainingTicks;
                _tickIntervalTicks = snapshot.TickIntervalTicks;
                _ticksUntilNextPeriodicEffect = snapshot.TicksUntilNextPeriodicEffect;
                _periodicDamage = snapshot.PeriodicDamage;
                _modifierCount = snapshot.ModifierCount;
                _triggerCount = snapshot.TriggerCount;
                _stackCount = snapshot.StackCount;
                _maxStacks = snapshot.MaxStacks;
            }
        }
    }
}
