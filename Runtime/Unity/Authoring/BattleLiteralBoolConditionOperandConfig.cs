using System;
using Combat.Core.Battle;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    [Serializable]
    public sealed class BattleLiteralBoolConditionOperandConfig : BattleConditionOperandConfig
    {
        [SerializeField] private bool _value;

        public BattleLiteralBoolConditionOperandConfig()
        {
        }

        public BattleLiteralBoolConditionOperandConfig(bool value)
        {
            _value = value;
        }

        public bool Value => _value;
        public override BattleConditionOperandValueKind ValueKind => BattleConditionOperandValueKind.Bool;

        public override BattleConditionOperandDefinition BuildDefinition()
        {
            return BattleConditionOperandDefinition.LiteralBool(_value);
        }
    }
}
