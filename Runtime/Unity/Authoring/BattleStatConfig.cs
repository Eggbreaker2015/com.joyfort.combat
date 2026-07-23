using System;
using Combat.Core.Battle;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    [Serializable]
    public struct BattleStatConfig
    {
        [SerializeField] private BattleStatId _stat;
        [SerializeField] private float _value;

        public BattleStatConfig(BattleStatId stat, float value)
        {
            _stat = stat;
            _value = value;
        }

        public BattleStatId Stat => _stat;
        public float Value => _value;
    }
}
