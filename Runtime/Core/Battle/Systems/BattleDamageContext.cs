namespace Combat.Core.Battle
{
    internal readonly struct BattleDamageContext
    {
        public BattleDamageContext(
            EntityId source,
            EntityId target,
            int baseAmount,
            int resolvedAmount,
            BattleEffectTriggerPolicy triggerPolicy,
            BattleEffectContext effectContext)
        {
            Source = source;
            Target = target;
            BaseAmount = baseAmount;
            ResolvedAmount = resolvedAmount;
            TriggerPolicy = triggerPolicy;
            EffectContext = effectContext;
        }

        public EntityId Source { get; }
        public EntityId Target { get; }
        public int BaseAmount { get; }
        public int ResolvedAmount { get; }
        public BattleEffectTriggerPolicy TriggerPolicy { get; }
        public BattleEffectContext EffectContext { get; }
    }
}
