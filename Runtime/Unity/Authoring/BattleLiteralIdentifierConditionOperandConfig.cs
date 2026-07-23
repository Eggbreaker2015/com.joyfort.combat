using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    [Serializable]
    public sealed class BattleLiteralIdentifierConditionOperandConfig : BattleConditionOperandConfig
    {
        [SerializeField] private string _identifierValue;

        public BattleLiteralIdentifierConditionOperandConfig()
        {
        }

        public BattleLiteralIdentifierConditionOperandConfig(string identifierValue)
        {
            _identifierValue = identifierValue;
        }

        public string IdentifierValue => _identifierValue;
        public override BattleConditionOperandValueKind ValueKind => BattleConditionOperandValueKind.Identifier;

        public override BattleConditionOperandDefinition BuildDefinition()
        {
            return BattleConditionOperandDefinition.LiteralIdentifier(_identifierValue);
        }

        public override void Validate(List<BattleConditionAuthoringValidationIssue> issues, string path)
        {
            base.Validate(issues, path);
            if (string.IsNullOrWhiteSpace(_identifierValue))
            {
                issues.Add(new BattleConditionAuthoringValidationIssue($"{path}.identifierValue", "is required."));
            }
        }
    }
}
