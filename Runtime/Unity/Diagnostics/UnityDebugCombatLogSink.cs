using Combat.Foundation.Diagnostics;
using UnityEngine;

namespace Combat.Unity.Diagnostics
{
    public sealed class UnityDebugCombatLogSink : ICombatLogSink
    {
        private readonly Object _context;

        public UnityDebugCombatLogSink(Object context = null)
        {
            _context = context;
        }

        public void Write(CombatLogEntry entry)
        {
            string line = Format(entry);
            switch (entry.Level)
            {
                case CombatLogLevel.Warning:
                    Debug.LogWarning(line, _context);
                    break;
                case CombatLogLevel.Error:
                    Debug.LogError(line, _context);
                    if (entry.Exception != null)
                    {
                        Debug.LogException(entry.Exception, _context);
                    }

                    break;
                default:
                    Debug.Log(line, _context);
                    if (entry.Exception != null)
                    {
                        Debug.LogException(entry.Exception, _context);
                    }

                    break;
            }
        }

        private static string Format(CombatLogEntry entry)
        {
            string tag = string.IsNullOrEmpty(entry.Tag) ? "Untagged" : entry.Tag;
            return $"[Combat][{entry.Level}][{tag}] {entry.Message}";
        }
    }
}
