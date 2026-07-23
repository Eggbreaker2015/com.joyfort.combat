using System;
using System.Collections.Generic;
using Combat.Foundation.Diagnostics;
using UnityEngine;

namespace Combat.Unity.Diagnostics
{
    [CreateAssetMenu(menuName = "Combat/Logging/Log Settings", fileName = "CombatLogSettings")]
    public sealed class CombatLogSettingsAsset : ScriptableObject
    {
        [SerializeField] private bool _isEnabled = true;
        [SerializeField] private bool _defaultVisible = true;
        [SerializeField] private CombatLogLevel _minimumLevel = CombatLogLevel.Info;
        [SerializeField] private CombatLogTagRuleConfig[] _tagRules = Array.Empty<CombatLogTagRuleConfig>();

        public CombatLogSettings BuildSettings()
        {
            return new CombatLogSettings(_isEnabled, _defaultVisible, _minimumLevel, BuildRules(_tagRules));
        }

        private static IReadOnlyList<CombatLogTagRule> BuildRules(IReadOnlyList<CombatLogTagRuleConfig> configs)
        {
            if (configs == null || configs.Count == 0)
            {
                return Array.Empty<CombatLogTagRule>();
            }

            var rules = new List<CombatLogTagRule>(configs.Count);
            for (var i = 0; i < configs.Count; i++)
            {
                CombatLogTagRuleConfig config = configs[i];
                if (config != null && config.TryBuildRule(out CombatLogTagRule rule))
                {
                    rules.Add(rule);
                }
            }

            return rules;
        }
    }

    [Serializable]
    public sealed class CombatLogTagRuleConfig
    {
        [SerializeField] private string _tag = CombatLogTags.View;
        [SerializeField] private CombatLogTagMatchMode _matchMode = CombatLogTagMatchMode.Exact;
        [SerializeField] private bool _isVisible = true;
        [SerializeField] private CombatLogLevel _minimumLevel = CombatLogLevel.Info;

        public bool TryBuildRule(out CombatLogTagRule rule)
        {
            if (string.IsNullOrWhiteSpace(_tag))
            {
                rule = default;
                return false;
            }

            rule = new CombatLogTagRule(_tag, _matchMode, _isVisible, _minimumLevel);
            return true;
        }
    }
}
