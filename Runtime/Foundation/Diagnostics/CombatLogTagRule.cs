using System;

namespace Combat.Foundation.Diagnostics
{
    public readonly struct CombatLogTagRule
    {
        public CombatLogTagRule(string tag, CombatLogTagMatchMode matchMode, bool isVisible, CombatLogLevel minimumLevel)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                throw new ArgumentException("Combat log tag rule requires a tag.", nameof(tag));
            }

            Tag = tag.Trim();
            MatchMode = matchMode;
            IsVisible = isVisible;
            MinimumLevel = minimumLevel;
        }

        public string Tag { get; }
        public CombatLogTagMatchMode MatchMode { get; }
        public bool IsVisible { get; }
        public CombatLogLevel MinimumLevel { get; }

        public bool Matches(string tag)
        {
            string candidate = tag ?? string.Empty;
            switch (MatchMode)
            {
                case CombatLogTagMatchMode.Exact:
                    return string.Equals(candidate, Tag, StringComparison.Ordinal);
                case CombatLogTagMatchMode.Prefix:
                    return candidate.StartsWith(Tag, StringComparison.Ordinal);
                default:
                    return false;
            }
        }
    }
}
