using Combat.Foundation.Events;

namespace Combat.Core.Battle
{
    internal sealed partial class BattleWorld
    {
        public void FlushEffectCommands(EventBuffer<BattleEvent> events, EventSequence eventSequence, BattleTick tick)
        {
            BattleEffectResolver.FlushEffectCommands(this, events, eventSequence, tick);
        }

        internal bool CanResolveReactionUnit(EntityId entity)
        {
            return BattleEffectResolver.CanResolveReactionUnit(this, entity);
        }
    }
}
