namespace Combat.Runtime.Display
{
    public sealed class VisualTimelinePolicy
    {
        public static readonly VisualTimelinePolicy Default = new VisualTimelinePolicy(VisualTimelineSettings.Default);

        public VisualTimelinePolicy(VisualTimelineSettings settings)
        {
            Settings = settings;
        }

        public VisualTimelineSettings Settings { get; }

        public float GetDelaySeconds(VisualCommand command)
        {
            switch (command.Type)
            {
                case VisualCommandType.DestroyProjectile:
                    return Settings.ProjectileDestroyDelaySeconds;
                case VisualCommandType.DestroyUnit:
                    return Settings.UnitDestroyDelaySeconds;
                case VisualCommandType.ShowBattleResult:
                    return Settings.BattleResultDelaySeconds;
                default:
                    return 0f;
            }
        }
    }
}
