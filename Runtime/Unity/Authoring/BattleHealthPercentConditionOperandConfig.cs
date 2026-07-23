using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    [Serializable]
    public sealed class BattleHealthPercentConditionOperandConfig : BattleConditionOperandConfig
    {
        [SerializeField] private BattleConditionSubject _subject;

        public BattleHealthPercentConditionOperandConfig()
        {
        }

        public BattleHealthPercentConditionOperandConfig(BattleConditionSubject subject)
        {
            _subject = subject;
        }

        public BattleConditionSubject Subject => _subject;
        public override BattleConditionOperandValueKind ValueKind => BattleConditionOperandValueKind.Scalar;

        public override BattleConditionOperandDefinition BuildDefinition()
        {
            return BattleConditionOperandDefinition.HealthPercent(_subject);
        }

        public override void Validate(List<BattleConditionAuthoringValidationIssue> issues, string path)
        {
            base.Validate(issues, path);
            BattleConditionAuthoringRules.ValidateSubject(issues, $"{path}.subject", _subject);
        }
    }
}
