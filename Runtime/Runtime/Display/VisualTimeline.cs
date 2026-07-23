using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Runtime.Display
{
    public sealed class VisualTimeline
    {
        private const float DueEpsilonSeconds = 0.000001f;

        private readonly List<VisualTimelineEntry> _entries = new List<VisualTimelineEntry>();
        private readonly ReadOnlyCollection<VisualTimelineEntry> _readOnlyEntries;

        public VisualTimeline()
        {
            _readOnlyEntries = _entries.AsReadOnly();
        }

        public int Count => _entries.Count;
        public ReadOnlyCollection<VisualTimelineEntry> Entries => _readOnlyEntries;

        public void Enqueue(VisualTimelineEntry entry)
        {
            var insertIndex = _entries.Count;
            while (insertIndex > 0 && Compare(entry, _entries[insertIndex - 1]) < 0)
            {
                insertIndex--;
            }

            _entries.Insert(insertIndex, entry);
        }

        public void AdvanceTo(float timeSeconds, ICombatViewPort viewPort)
        {
            if (viewPort == null)
            {
                throw new ArgumentNullException(nameof(viewPort));
            }

            ValidateFinite(timeSeconds, nameof(timeSeconds));

            var dueCount = 0;
            while (dueCount < _entries.Count && _entries[dueCount].ScheduledTimeSeconds <= timeSeconds + DueEpsilonSeconds)
            {
                dueCount++;
            }

            if (dueCount <= 0)
            {
                return;
            }

            var dueEntries = _entries.GetRange(0, dueCount);
            _entries.RemoveRange(0, dueCount);

            DispatchEntries(dueEntries, viewPort);
        }

        public void Flush(ICombatViewPort viewPort)
        {
            if (viewPort == null)
            {
                throw new ArgumentNullException(nameof(viewPort));
            }

            if (_entries.Count <= 0)
            {
                return;
            }

            var entries = _entries.ToArray();
            _entries.Clear();
            DispatchEntries(entries, viewPort);
        }

        public void Clear()
        {
            _entries.Clear();
        }

        private static void DispatchEntries(IReadOnlyList<VisualTimelineEntry> entries, ICombatViewPort viewPort)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                VisualCommandApplier.Dispatch(viewPort, entries[i].Command);
            }
        }

        private static void ValidateFinite(float value, string paramName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Visual timeline time must be finite.");
            }
        }

        private static int Compare(VisualTimelineEntry left, VisualTimelineEntry right)
        {
            if (left.ScheduledTimeSeconds < right.ScheduledTimeSeconds)
            {
                return -1;
            }

            if (left.ScheduledTimeSeconds > right.ScheduledTimeSeconds)
            {
                return 1;
            }

            return left.Order.CompareTo(right.Order);
        }
    }
}
