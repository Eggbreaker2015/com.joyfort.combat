using System;
using Combat.Core.Battle;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    [Serializable]
    public sealed class BattleLiteralIntConditionOperandConfig : BattleConditionOperandConfig
    {
        [SerializeField] private int _value;

        public BattleLiteralIntConditionOperandConfig()
        {
        }

        public BattleLiteralIntConditionOperandConfig(int value)
        {
            _value = value;
        }

        public int Value => _value;
        public override BattleConditionOperandValueKind ValueKind => BattleConditionOperandValueKind.Int;

        public override BattleConditionOperandDefinition BuildDefinition()
        {
            return BattleConditionOperandDefinition.LiteralInt(_value);
        }
    }
}
