using System;

namespace Combat.Foundation.Diagnostics
{
    public static class CombatLog
    {
        private static CombatLogger s_shared = CombatLogger.Disabled;

        public static CombatLogger Shared => s_shared;

        public static void Configure(CombatLogger logger)
        {
            s_shared = logger ?? CombatLogger.Disabled;
        }

        public static void Reset()
        {
            s_shared = CombatLogger.Disabled;
        }

        public static bool ShouldLog(CombatLogLevel level, string tag)
        {
            return s_shared.ShouldLog(level, tag);
        }

        public static void Trace(string tag, Func<string> messageFactory)
        {
            s_shared.Trace(tag, messageFactory);
        }

        public static void Debug(string tag, Func<string> messageFactory)
        {
            s_shared.Debug(tag, messageFactory);
        }

        public static void Info(string tag, Func<string> messageFactory)
        {
            s_shared.Info(tag, messageFactory);
        }

        public static void Warning(string tag, Func<string> messageFactory)
        {
            s_shared.Warning(tag, messageFactory);
        }

        public static void Error(string tag, Func<string> messageFactory, Exception exception = null)
        {
            s_shared.Error(tag, messageFactory, exception);
        }
    }
}
