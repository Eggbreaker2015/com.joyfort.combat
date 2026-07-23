using System;

namespace Combat.Unity.Authoring
{
    public readonly struct BattleConditionAuthoringValidationIssue
    {
        public BattleConditionAuthoringValidationIssue(string path, string message)
        {
            Path = string.IsNullOrWhiteSpace(path) ? throw new ArgumentException("Validation issue path is required.", nameof(path)) : path;
            Message = string.IsNullOrWhiteSpace(message) ? throw new ArgumentException("Validation issue message is required.", nameof(message)) : message;
        }

        public string Path { get; }
        public string Message { get; }
    }
}
