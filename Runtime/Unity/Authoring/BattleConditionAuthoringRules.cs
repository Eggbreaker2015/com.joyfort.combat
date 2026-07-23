using System;
using System.Collections.Generic;
using Combat.Core.Battle;

namespace Combat.Unity.Authoring
{
    internal static class BattleConditionAuthoringRules
    {
        public static int PercentToBasisPoints(float percent, string parameterName)
        {
            if (float.IsNaN(percent) || float.IsInfinity(percent))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Percent value must be finite.");
            }

            if (percent < 0f || percent > 100f)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Percent value must be between 0 and 100.");
            }

            return (int)Math.Round(percent * 100d, MidpointRounding.AwayFromZero);
        }

        public static void ValidatePercent(List<BattleConditionAuthoringValidationIssue> issues, string path, float percent)
        {
            if (float.IsNaN(percent) || float.IsInfinity(percent))
            {
                issues.Add(new BattleConditionAuthoringValidationIssue(path, "must be finite."));
            }
            else if (percent < 0f || percent > 100f)
            {
                issues.Add(new BattleConditionAuthoringValidationIssue(path, "must be between 0 and 100."));
            }
        }

        public static void ValidateFiniteScalar(List<BattleConditionAuthoringValidationIssue> issues, string path, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                issues.Add(new BattleConditionAuthoringValidationIssue(path, "must be finite."));
            }
        }

        public static void ValidateSubject(List<BattleConditionAuthoringValidationIssue> issues, string path, BattleConditionSubject subject)
        {
            switch (subject)
            {
                case BattleConditionSubject.Owner:
                case BattleConditionSubject.Source:
                case BattleConditionSubject.Target:
                    return;
                default:
                    issues.Add(new BattleConditionAuthoringValidationIssue(path, $"has unsupported BattleConditionSubject '{subject}'."));
                    return;
            }
        }

        public static void ValidateStat(List<BattleConditionAuthoringValidationIssue> issues, string path, BattleStatId stat)
        {
            switch (stat)
            {
                case BattleStatId.MaxHealth:
                case BattleStatId.MoveSpeed:
                    return;
                default:
                    issues.Add(new BattleConditionAuthoringValidationIssue(path, $"has unsupported BattleStatId '{stat}'."));
                    return;
            }
        }

        public static void ValidatePolarity(List<BattleConditionAuthoringValidationIssue> issues, string path, StatusPolarity polarity)
        {
            switch (polarity)
            {
                case StatusPolarity.Buff:
                case StatusPolarity.Debuff:
                case StatusPolarity.Neutral:
                    return;
                default:
                    issues.Add(new BattleConditionAuthoringValidationIssue(path, $"has unsupported StatusPolarity '{polarity}'."));
                    return;
            }
        }
    }
}
