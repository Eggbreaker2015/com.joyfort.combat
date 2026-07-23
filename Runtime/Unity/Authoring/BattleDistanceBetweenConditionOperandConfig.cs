using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    [Serializable]
    public sealed class BattleDistanceBetweenConditionOperandConfig : BattleConditionOperandConfig
    {
        [SerializeField] private BattleConditionSubject _subject;
        [SerializeField] private BattleConditionSubject _otherSubject = BattleConditionSubject.Target;

        public BattleDistanceBetweenConditionOperandConfig()
        {
        }

        public BattleDistanceBetweenConditionOperandConfig(BattleConditionSubject subject, BattleConditionSubject otherSubject)
        {
            _subject = subject;
            _otherSubject = otherSubject;
        }

        public BattleConditionSubject Subject => _subject;
        public BattleConditionSubject OtherSubject => _otherSubject;
        public override BattleConditionOperandValueKind ValueKind => BattleConditionOperandValueKind.Scalar;

        public override BattleConditionOperandDefinition BuildDefinition()
        {
            return BattleConditionOperandDefinition.DistanceBetween(_subject, _otherSubject);
        }

        public override void Validate(List<BattleConditionAuthoringValidationIssue> issues, string path)
        {
            base.Validate(issues, path);
            BattleConditionAuthoringRules.ValidateSubject(issues, $"{path}.subject", _subject);
            BattleConditionAuthoringRules.ValidateSubject(issues, $"{path}.otherSubject", _otherSubject);
        }
    }
}
