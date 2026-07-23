using System;

namespace Combat.Foundation.Diagnostics
{
    public readonly struct CombatLogEntry
    {
        public CombatLogEntry(CombatLogLevel level, string tag, string message, Exception exception, DateTime timestampUtc)
        {
            Level = level;
            Tag = tag ?? string.Empty;
            Message = message ?? string.Empty;
            Exception = exception;
            TimestampUtc = timestampUtc;
        }

        public CombatLogLevel Level { get; }
        public string Tag { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public DateTime TimestampUtc { get; }
    }
}
