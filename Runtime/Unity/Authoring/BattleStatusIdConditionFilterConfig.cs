using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    [Serializable]
    public sealed class BattleStatusIdConditionFilterConfig : BattleStatusConditionFilterConfig
    {
        [SerializeField] private StatusConfigAsset _status;

        public BattleStatusIdConditionFilterConfig()
        {
        }

        public BattleStatusIdConditionFilterConfig(StatusConfigAsset status)
        {
            _status = status;
        }

        public StatusConfigAsset Status => _status;
        public override StatusConfigAsset ReferencedStatus => _status;

        public override BattleStatusConditionFilterDefinition BuildDefinition()
        {
            if (_status == null)
            {
                throw new ArgumentException("Status condition filter is missing a status reference.", nameof(_status));
            }

            if (string.IsNullOrWhiteSpace(_status.Id))
            {
                throw new ArgumentException("Status condition filter status reference id is required.", nameof(_status));
            }

            return BattleStatusConditionFilterDefinition.StatusId(_status.Id);
        }

        public override void Validate(List<BattleConditionAuthoringValidationIssue> issues, string path)
        {
            base.Validate(issues, path);
            if (_status == null)
            {
                issues.Add(new BattleConditionAuthoringValidationIssue($"{path}.status", "is missing a status reference."));
            }
            else if (string.IsNullOrWhiteSpace(_status.Id))
            {
                issues.Add(new BattleConditionAuthoringValidationIssue($"{path}.status", "reference id is required."));
            }
        }
    }
}
