namespace Combat.Foundation.Events
{
    public sealed class EventSequence
    {
        private int _nextValue = 1;

        public int Next()
        {
            return _nextValue++;
        }
    }
}
