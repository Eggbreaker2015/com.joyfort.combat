using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    [Serializable]
    public sealed class BattleLiteralPercentConditionOperandConfig : BattleConditionOperandConfig
    {
        [SerializeField] private float _percentValue;

        public BattleLiteralPercentConditionOperandConfig()
        {
        }

        public BattleLiteralPercentConditionOperandConfig(float percentValue)
        {
            _percentValue = percentValue;
        }

        public float PercentValue => _percentValue;
        public override BattleConditionOperandValueKind ValueKind => BattleConditionOperandValueKind.Scalar;

        public override BattleConditionOperandDefinition BuildDefinition()
        {
            return BattleConditionOperandDefinition.LiteralPercentBasisPoints(
                BattleConditionAuthoringRules.PercentToBasisPoints(_percentValue, nameof(_percentValue)));
        }

        public override void Validate(List<BattleConditionAuthoringValidationIssue> issues, string path)
        {
            base.Validate(issues, path);
            BattleConditionAuthoringRules.ValidatePercent(issues, $"{path}.percentValue", _percentValue);
        }
    }
}
