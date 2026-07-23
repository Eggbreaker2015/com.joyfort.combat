namespace Combat.Runtime.Display
{
    public interface IVisualCommandSink
    {
        void Dispatch(VisualCommand command);
    }
}
