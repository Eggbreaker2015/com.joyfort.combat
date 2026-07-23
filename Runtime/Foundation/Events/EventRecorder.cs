using System.Collections.Generic;

namespace Combat.Foundation.Events
{
    public sealed class EventRecorder<TEvent>
    {
        private readonly List<TEvent> _events = new List<TEvent>(128);

        public int Count => _events.Count;

        public TEvent this[int index] => _events[index];

        public IReadOnlyList<TEvent> Events => _events;

        public void Record(EventStream<TEvent> stream)
        {
            var count = stream.Count;
            for (var i = 0; i < count; i++)
            {
                _events.Add(stream[i]);
            }
        }

        public void Clear()
        {
            _events.Clear();
        }
    }
}
