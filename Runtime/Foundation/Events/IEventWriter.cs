namespace Combat.Foundation.Events
{
    public interface IEventWriter<in TEvent>
    {
        void Write(TEvent value);
    }
}
