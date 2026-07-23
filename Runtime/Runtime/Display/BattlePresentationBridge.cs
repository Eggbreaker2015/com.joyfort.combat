using System.Collections.Generic;
using Combat.Core.Battle;
using Combat.Foundation.Events;

namespace Combat.Runtime.Display
{
    /// <summary>
    /// Translates synchronous battle event output into presentation commands.
    /// This bridge owns no battle rule state and never advances a simulation.
    /// </summary>
    public sealed class BattlePresentationBridge
    {
        private readonly VisualCommandDispatcher _dispatcher;

        public BattlePresentationBridge(ICombatViewPort viewPort)
        {
            _dispatcher = new VisualCommandDispatcher(viewPort);
        }

        public BattlePresentationBridge(IVisualCommandSink commandSink)
        {
            _dispatcher = new VisualCommandDispatcher(commandSink);
        }

        public void Consume(EventStream<BattleEvent> events)
        {
            _dispatcher.Dispatch(events);
        }

        public void Consume(IReadOnlyList<BattleEvent> events)
        {
            _dispatcher.Dispatch(events);
        }

        public void Consume(BattleEvent battleEvent)
        {
            _dispatcher.Dispatch(battleEvent);
        }
    }
}
