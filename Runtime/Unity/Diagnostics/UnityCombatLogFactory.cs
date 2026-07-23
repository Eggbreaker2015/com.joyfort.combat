using Combat.Foundation.Diagnostics;
using UnityEngine;

namespace Combat.Unity.Diagnostics
{
    public static class UnityCombatLogFactory
    {
        public static CombatLogger CreateDefault(Object context = null)
        {
            return Create(null, context);
        }

        public static CombatLogger Create(CombatLogSettingsAsset settingsAsset, Object context = null)
        {
            CombatLogSettings settings = settingsAsset != null
                ? settingsAsset.BuildSettings()
                : CombatLogSettings.ShowInfoAndAbove;

            return new CombatLogger(settings, new UnityDebugCombatLogSink(context));
        }

        public static void Install(CombatLogSettingsAsset settingsAsset, Object context = null)
        {
            CombatLog.Configure(Create(settingsAsset, context));
        }
    }
}
