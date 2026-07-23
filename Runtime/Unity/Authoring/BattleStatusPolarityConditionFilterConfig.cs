using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    [Serializable]
    public sealed class BattleStatusPolarityConditionFilterConfig : BattleStatusConditionFilterConfig
    {
        [SerializeField] private StatusPolarity _polarity = StatusPolarity.Buff;

        public BattleStatusPolarityConditionFilterConfig()
        {
        }

        public BattleStatusPolarityConditionFilterConfig(StatusPolarity polarity)
        {
            _polarity = polarity;
        }

        public StatusPolarity Polarity => _polarity;

        public override BattleStatusConditionFilterDefinition BuildDefinition()
        {
            return BattleStatusConditionFilterDefinition.Polarity(_polarity);
        }

        public override void Validate(List<BattleConditionAuthoringValidationIssue> issues, string path)
        {
            base.Validate(issues, path);
            BattleConditionAuthoringRules.ValidatePolarity(issues, $"{path}.polarity", _polarity);
        }
    }
}
