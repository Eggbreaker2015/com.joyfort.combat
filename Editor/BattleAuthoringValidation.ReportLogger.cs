#if UNITY_EDITOR
using UnityEngine;

namespace Combat.Unity.Editor
{
    public static partial class BattleAuthoringValidator
    {
        public static void LogReport(BattleAuthoringValidationReport report)
        {
            if (report == null || report.Issues.Count <= 0)
            {
                Debug.Log("Battle authoring validation passed.");
                return;
            }

            for (var i = 0; i < report.Issues.Count; i++)
            {
                BattleAuthoringValidationIssue issue = report.Issues[i];
                string location = string.IsNullOrEmpty(issue.PropertyPath) ? issue.AssetPath : $"{issue.AssetPath}:{issue.PropertyPath}";
                string message = string.IsNullOrEmpty(location) ? issue.Message : $"{location} - {issue.Message}";
                if (issue.Severity == BattleAuthoringValidationSeverity.Error)
                {
                    Debug.LogError(message, issue.Asset);
                }
                else
                {
                    Debug.LogWarning(message, issue.Asset);
                }
            }
        }
    }
}
#endif
