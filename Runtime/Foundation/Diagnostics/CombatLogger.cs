using System;

namespace Combat.Foundation.Diagnostics
{
    public sealed class CombatLogger
    {
        public static readonly CombatLogger Disabled = new CombatLogger(CombatLogSettings.Disabled, NullCombatLogSink.Instance);

        private readonly ICombatLogFilter _filter;
        private readonly ICombatLogSink _sink;

        public CombatLogger(ICombatLogFilter filter, ICombatLogSink sink)
        {
            _filter = filter ?? CombatLogSettings.Disabled;
            _sink = sink ?? NullCombatLogSink.Instance;
        }

        public bool ShouldLog(CombatLogLevel level, string tag)
        {
            return _filter.ShouldLog(level, tag);
        }

        public void Trace(string tag, string message)
        {
            Log(CombatLogLevel.Trace, tag, message);
        }

        public void Trace(string tag, Func<string> messageFactory)
        {
            Log(CombatLogLevel.Trace, tag, messageFactory);
        }

        public void Debug(string tag, string message)
        {
            Log(CombatLogLevel.Debug, tag, message);
        }

        public void Debug(string tag, Func<string> messageFactory)
        {
            Log(CombatLogLevel.Debug, tag, messageFactory);
        }

        public void Info(string tag, string message)
        {
            Log(CombatLogLevel.Info, tag, message);
        }

        public void Info(string tag, Func<string> messageFactory)
        {
            Log(CombatLogLevel.Info, tag, messageFactory);
        }

        public void Warning(string tag, string message)
        {
            Log(CombatLogLevel.Warning, tag, message);
        }

        public void Warning(string tag, Func<string> messageFactory)
        {
            Log(CombatLogLevel.Warning, tag, messageFactory);
        }

        public void Error(string tag, string message, Exception exception = null)
        {
            Log(CombatLogLevel.Error, tag, message, exception);
        }

        public void Error(string tag, Func<string> messageFactory, Exception exception = null)
        {
            Log(CombatLogLevel.Error, tag, messageFactory, exception);
        }

        public void Log(CombatLogLevel level, string tag, string message, Exception exception = null)
        {
            if (!ShouldLog(level, tag))
            {
                return;
            }

            _sink.Write(new CombatLogEntry(level, tag, message, exception, DateTime.UtcNow));
        }

        public void Log(CombatLogLevel level, string tag, Func<string> messageFactory, Exception exception = null)
        {
            if (!ShouldLog(level, tag))
            {
                return;
            }

            string message = messageFactory != null ? messageFactory() : string.Empty;
            _sink.Write(new CombatLogEntry(level, tag, message, exception, DateTime.UtcNow));
        }

        private sealed class NullCombatLogSink : ICombatLogSink
        {
            public static readonly NullCombatLogSink Instance = new NullCombatLogSink();

            private NullCombatLogSink()
            {
            }

            public void Write(CombatLogEntry entry)
            {
            }
        }
    }
}
