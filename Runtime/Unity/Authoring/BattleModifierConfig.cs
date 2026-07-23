using System;
using Combat.Core.Battle;
using UnityEngine;
using UnityEngine.Serialization;

namespace Combat.Unity.Authoring
{
    [Serializable]
    public sealed class BattleModifierConfig
    {
        [SerializeField] private BattleModifierTarget _target = BattleModifierTarget.Damage;
        [SerializeField] private BattleStatId _statId;
        [FormerlySerializedAs("_stat")]
        [SerializeField] private BattleDamageModifierStat _damageStat;
        [SerializeField] private BattleModifierOperation _operation;
        [FormerlySerializedAs("_value")]
        [SerializeField] private float _value;

        public BattleModifierConfig(
            BattleModifierTarget target,
            BattleStatId statId,
            BattleDamageModifierStat damageStat,
            BattleModifierOperation operation,
            float value)
        {
            _target = target;
            _statId = statId;
            _damageStat = damageStat;
            _operation = operation;
            _value = value;
        }

        public BattleModifierTarget Target => _target;
        public BattleStatId StatId => _statId;
        public BattleDamageModifierStat DamageStat => _damageStat;
        public BattleModifierOperation Operation => _operation;
        public float Value => _value;
    }
}
