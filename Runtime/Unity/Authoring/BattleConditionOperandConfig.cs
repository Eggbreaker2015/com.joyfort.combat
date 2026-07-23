using System;
using System.Collections.Generic;
using Combat.Core.Battle;

namespace Combat.Unity.Authoring
{
    [Serializable]
    public abstract class BattleConditionOperandConfig
    {
        public abstract BattleConditionOperandValueKind ValueKind { get; }

        public abstract BattleConditionOperandDefinition BuildDefinition();

        public virtual void Validate(List<BattleConditionAuthoringValidationIssue> issues, string path)
        {
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }
        }

        public virtual StatusConfigAsset ReferencedStatus => null;
    }
}
