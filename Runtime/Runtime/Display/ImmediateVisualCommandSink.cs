using System;

namespace Combat.Runtime.Display
{
    public sealed class ImmediateVisualCommandSink : IVisualCommandSink
    {
        private readonly ICombatViewPort _viewPort;

        public ImmediateVisualCommandSink(ICombatViewPort viewPort)
        {
            _viewPort = viewPort ?? throw new ArgumentNullException(nameof(viewPort));
        }

        public void Dispatch(VisualCommand command)
        {
            VisualCommandApplier.Dispatch(_viewPort, command);
        }
    }
}
