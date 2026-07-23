using UnityEngine;

namespace Combat.Unity.Display
{
    public readonly struct UnityCombatViewSmoothingSettings
    {
        public const float DefaultPositionDurationSeconds = 0.12f;
        public const float DefaultRotationDurationSeconds = 0.08f;

        public static readonly UnityCombatViewSmoothingSettings Default = new UnityCombatViewSmoothingSettings(
            DefaultPositionDurationSeconds,
            DefaultRotationDurationSeconds);

        public static readonly UnityCombatViewSmoothingSettings Immediate = new UnityCombatViewSmoothingSettings(0f, 0f);

        public UnityCombatViewSmoothingSettings(float positionDurationSeconds, float rotationDurationSeconds)
        {
            PositionDurationSeconds = Mathf.Max(0f, positionDurationSeconds);
            RotationDurationSeconds = Mathf.Max(0f, rotationDurationSeconds);
        }

        public float PositionDurationSeconds { get; }
        public float RotationDurationSeconds { get; }
    }
}
