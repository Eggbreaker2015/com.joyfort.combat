using Combat.Core.Battle;
using Combat.Foundation.Events;

namespace Combat.Runtime.Runner
{
    /// <summary>
    /// Describes the rule output currently held by a battle simulation.
    /// Events are a synchronous view and may be cleared by the next operation.
    /// </summary>
    public readonly struct BattleStepOutput
    {
        public BattleStepOutput(
            BattleTick tick,
            EventStream<BattleEvent> events,
            BattleResult result)
        {
            Tick = tick;
            Events = events;
            Result = result;
        }

        public BattleTick Tick { get; }
        public EventStream<BattleEvent> Events { get; }
        public BattleResult Result { get; }
    }
}
