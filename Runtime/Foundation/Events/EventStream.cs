using System.Collections;
using System.Collections.Generic;

namespace Combat.Foundation.Events
{
    public readonly struct EventStream<TEvent> : IEnumerable<TEvent>
    {
        private readonly IReadOnlyList<TEvent> _events;

        public EventStream(IReadOnlyList<TEvent> events)
        {
            _events = events;
        }

        public int Count => _events?.Count ?? 0;

        public TEvent this[int index] => _events[index];

        public IEnumerator<TEvent> GetEnumerator()
        {
            return (_events ?? System.Array.Empty<TEvent>()).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
