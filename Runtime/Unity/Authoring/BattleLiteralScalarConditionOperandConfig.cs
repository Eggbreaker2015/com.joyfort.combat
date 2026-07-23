using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    [Serializable]
    public sealed class BattleLiteralScalarConditionOperandConfig : BattleConditionOperandConfig
    {
        [SerializeField] private float _scalarValue;

        public BattleLiteralScalarConditionOperandConfig()
        {
        }

        public BattleLiteralScalarConditionOperandConfig(float scalarValue)
        {
            _scalarValue = scalarValue;
        }

        public float ScalarValue => _scalarValue;
        public override BattleConditionOperandValueKind ValueKind => BattleConditionOperandValueKind.Scalar;

        public override BattleConditionOperandDefinition BuildDefinition()
        {
            return BattleConditionOperandDefinition.LiteralScalar(BattleScalar.FromFloat(_scalarValue));
        }

        public override void Validate(List<BattleConditionAuthoringValidationIssue> issues, string path)
        {
            base.Validate(issues, path);
            BattleConditionAuthoringRules.ValidateFiniteScalar(issues, $"{path}.scalarValue", _scalarValue);
        }
    }
}
