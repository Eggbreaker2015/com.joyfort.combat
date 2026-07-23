using System;

namespace Combat.Runtime.Display
{
    public readonly struct VisualTimelineEntry
    {
        public VisualTimelineEntry(VisualCommand command, float scheduledTimeSeconds, long order)
        {
            if (!command.IsValid)
            {
                throw new InvalidOperationException("Visual timeline entry command is invalid.");
            }

            if (float.IsNaN(scheduledTimeSeconds) || float.IsInfinity(scheduledTimeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(scheduledTimeSeconds), scheduledTimeSeconds, "Visual timeline scheduled time must be finite.");
            }

            Command = command;
            ScheduledTimeSeconds = scheduledTimeSeconds;
            Order = order;
        }

        public VisualCommand Command { get; }
        public float ScheduledTimeSeconds { get; }
        public long Order { get; }
    }
}
