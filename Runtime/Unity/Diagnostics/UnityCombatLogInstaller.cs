using Combat.Foundation.Diagnostics;
using UnityEngine;

namespace Combat.Unity.Diagnostics
{
    public sealed class UnityCombatLogInstaller : MonoBehaviour
    {
        [SerializeField] private CombatLogSettingsAsset _settings;

        private CombatLogger _previousLogger;

        private void Awake()
        {
            _previousLogger = CombatLog.Shared;
            UnityCombatLogFactory.Install(_settings, this);
        }

        private void OnDestroy()
        {
            CombatLog.Configure(_previousLogger);
            _previousLogger = null;
        }
    }
}
