using System;
using Combat.Core.Battle;

namespace Combat.Unity.Authoring
{
    [Serializable]
    public sealed class BattleAnyStatusConditionFilterConfig : BattleStatusConditionFilterConfig
    {
        public override BattleStatusConditionFilterDefinition BuildDefinition()
        {
            return BattleStatusConditionFilterDefinition.Any();
        }
    }
}
