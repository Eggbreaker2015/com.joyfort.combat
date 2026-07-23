using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    [Serializable]
    public sealed class BattleStatusCountConditionOperandConfig : BattleConditionOperandConfig
    {
        [SerializeField] private BattleConditionSubject _subject;
        [SerializeReference]
        [SerializeField] private BattleStatusConditionFilterConfig _statusFilter = new BattleAnyStatusConditionFilterConfig();

        public BattleStatusCountConditionOperandConfig()
        {
        }

        public BattleStatusCountConditionOperandConfig(BattleConditionSubject subject, BattleStatusConditionFilterConfig statusFilter)
        {
            _subject = subject;
            _statusFilter = statusFilter ?? new BattleAnyStatusConditionFilterConfig();
        }

        public BattleConditionSubject Subject => _subject;
        public BattleStatusConditionFilterConfig StatusFilter => _statusFilter;
        public override BattleConditionOperandValueKind ValueKind => BattleConditionOperandValueKind.Int;
        public override StatusConfigAsset ReferencedStatus => _statusFilter?.ReferencedStatus;

        public override BattleConditionOperandDefinition BuildDefinition()
        {
            return BattleConditionOperandDefinition.StatusCount(
                _subject,
                (_statusFilter ?? new BattleAnyStatusConditionFilterConfig()).BuildDefinition());
        }

        public override void Validate(List<BattleConditionAuthoringValidationIssue> issues, string path)
        {
            base.Validate(issues, path);
            BattleConditionAuthoringRules.ValidateSubject(issues, $"{path}.subject", _subject);
            if (_statusFilter == null)
            {
                issues.Add(new BattleConditionAuthoringValidationIssue($"{path}.statusFilter", "is required."));
            }
            else
            {
                _statusFilter.Validate(issues, $"{path}.statusFilter");
            }
        }
    }
}
