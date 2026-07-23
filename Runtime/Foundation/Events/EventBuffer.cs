using System.Collections.Generic;

namespace Combat.Foundation.Events
{
    public sealed class EventBuffer<TEvent> : IEventWriter<TEvent>
    {
        private readonly List<TEvent> _events = new List<TEvent>(32);

        public int Count => _events.Count;

        public void Write(TEvent value)
        {
            _events.Add(value);
        }

        public EventStream<TEvent> AsStream()
        {
            return new EventStream<TEvent>(_events);
        }

        public void Clear()
        {
            _events.Clear();
        }
    }
}
