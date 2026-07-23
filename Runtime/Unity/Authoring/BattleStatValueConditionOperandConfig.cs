using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    [Serializable]
    public sealed class BattleStatValueConditionOperandConfig : BattleConditionOperandConfig
    {
        [SerializeField] private BattleConditionSubject _subject;
        [SerializeField] private BattleStatId _stat = BattleStatId.MaxHealth;

        public BattleStatValueConditionOperandConfig()
        {
        }

        public BattleStatValueConditionOperandConfig(BattleConditionSubject subject, BattleStatId stat)
        {
            _subject = subject;
            _stat = stat;
        }

        public BattleConditionSubject Subject => _subject;
        public BattleStatId Stat => _stat;
        public override BattleConditionOperandValueKind ValueKind => BattleConditionOperandValueKind.Scalar;

        public override BattleConditionOperandDefinition BuildDefinition()
        {
            return BattleConditionOperandDefinition.StatValue(_subject, _stat);
        }

        public override void Validate(List<BattleConditionAuthoringValidationIssue> issues, string path)
        {
            base.Validate(issues, path);
            BattleConditionAuthoringRules.ValidateSubject(issues, $"{path}.subject", _subject);
            BattleConditionAuthoringRules.ValidateStat(issues, $"{path}.stat", _stat);
        }
    }
}
