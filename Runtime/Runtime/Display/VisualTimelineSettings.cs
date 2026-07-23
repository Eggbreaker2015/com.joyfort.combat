using System;

namespace Combat.Runtime.Display
{
    public readonly struct VisualTimelineSettings
    {
        public const float DefaultProjectileDestroyDelaySeconds = 0.12f;
        public const float DefaultUnitDestroyDelaySeconds = 0.35f;
        public const float DefaultBattleResultDelaySeconds = 0.45f;

        public static readonly VisualTimelineSettings Default = new VisualTimelineSettings(
            DefaultProjectileDestroyDelaySeconds,
            DefaultUnitDestroyDelaySeconds,
            DefaultBattleResultDelaySeconds);

        public VisualTimelineSettings(
            float projectileDestroyDelaySeconds,
            float unitDestroyDelaySeconds,
            float battleResultDelaySeconds)
        {
            ProjectileDestroyDelaySeconds = ClampDelay(projectileDestroyDelaySeconds, nameof(projectileDestroyDelaySeconds));
            UnitDestroyDelaySeconds = ClampDelay(unitDestroyDelaySeconds, nameof(unitDestroyDelaySeconds));
            BattleResultDelaySeconds = ClampDelay(battleResultDelaySeconds, nameof(battleResultDelaySeconds));
        }

        public float ProjectileDestroyDelaySeconds { get; }
        public float UnitDestroyDelaySeconds { get; }
        public float BattleResultDelaySeconds { get; }

        private static float ClampDelay(float value, string paramName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Visual timeline delay must be finite.");
            }

            // Negative display delays intentionally collapse to immediate dispatch.
            return value > 0f ? value : 0f;
        }
    }
}
