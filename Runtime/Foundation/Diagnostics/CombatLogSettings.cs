using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Foundation.Diagnostics
{
    public sealed class CombatLogSettings : ICombatLogFilter
    {
        public static readonly CombatLogSettings ShowInfoAndAbove = new CombatLogSettings(
            isEnabled: true,
            defaultVisible: true,
            minimumLevel: CombatLogLevel.Info,
            rules: Array.Empty<CombatLogTagRule>());

        public static readonly CombatLogSettings HideAll = new CombatLogSettings(
            isEnabled: true,
            defaultVisible: false,
            minimumLevel: CombatLogLevel.Trace,
            rules: Array.Empty<CombatLogTagRule>());

        public static readonly CombatLogSettings Disabled = new CombatLogSettings(
            isEnabled: false,
            defaultVisible: false,
            minimumLevel: CombatLogLevel.Error,
            rules: Array.Empty<CombatLogTagRule>());

        private readonly CombatLogTagRule[] _rules;
        private readonly ReadOnlyCollection<CombatLogTagRule> _readOnlyRules;

        public CombatLogSettings(bool isEnabled, bool defaultVisible, CombatLogLevel minimumLevel, IReadOnlyList<CombatLogTagRule> rules)
        {
            IsEnabled = isEnabled;
            DefaultVisible = defaultVisible;
            MinimumLevel = minimumLevel;

            if (rules == null || rules.Count == 0)
            {
                _rules = Array.Empty<CombatLogTagRule>();
            }
            else
            {
                _rules = new CombatLogTagRule[rules.Count];
                for (var i = 0; i < rules.Count; i++)
                {
                    _rules[i] = rules[i];
                }
            }

            _readOnlyRules = new ReadOnlyCollection<CombatLogTagRule>(_rules);
        }

        public bool IsEnabled { get; }
        public bool DefaultVisible { get; }
        public CombatLogLevel MinimumLevel { get; }
        public IReadOnlyList<CombatLogTagRule> Rules => _readOnlyRules;

        public bool ShouldLog(CombatLogLevel level, string tag)
        {
            if (!IsEnabled || level < MinimumLevel)
            {
                return false;
            }

            if (TryFindBestRule(tag, out CombatLogTagRule rule))
            {
                return rule.IsVisible && level >= rule.MinimumLevel;
            }

            return DefaultVisible;
        }

        private bool TryFindBestRule(string tag, out CombatLogTagRule rule)
        {
            string candidate = tag ?? string.Empty;

            var hasExactRule = false;
            CombatLogTagRule exactRule = default;
            for (var i = 0; i < _rules.Length; i++)
            {
                CombatLogTagRule candidateRule = _rules[i];
                if (candidateRule.MatchMode == CombatLogTagMatchMode.Exact && candidateRule.Matches(candidate))
                {
                    exactRule = candidateRule;
                    hasExactRule = true;
                }
            }

            if (hasExactRule)
            {
                rule = exactRule;
                return true;
            }

            var bestPrefixLength = -1;
            CombatLogTagRule prefixRule = default;
            for (var i = 0; i < _rules.Length; i++)
            {
                CombatLogTagRule candidateRule = _rules[i];
                if (candidateRule.MatchMode != CombatLogTagMatchMode.Prefix || !candidateRule.Matches(candidate))
                {
                    continue;
                }

                if (candidateRule.Tag.Length >= bestPrefixLength)
                {
                    prefixRule = candidateRule;
                    bestPrefixLength = candidateRule.Tag.Length;
                }
            }

            if (bestPrefixLength >= 0)
            {
                rule = prefixRule;
                return true;
            }

            rule = default;
            return false;
        }
    }
}
