namespace Combat.Core.Battle
{
    internal readonly struct BattleTriggerContext
    {
        public BattleTriggerContext(
            BattleTriggerTiming timing,
            EntityId owner,
            EntityId source,
            EntityId target,
            int amount,
            BattleEffectContext effectContext,
            BattleEffectTriggerPolicy triggerPolicy)
        {
            Timing = timing;
            Owner = owner;
            Source = source;
            Target = target;
            Amount = amount;
            EffectContext = effectContext;
            TriggerPolicy = triggerPolicy;
        }

        public BattleTriggerTiming Timing { get; }
        public EntityId Owner { get; }
        public EntityId Source { get; }
        public EntityId Target { get; }
        public int Amount { get; }
        public BattleEffectContext EffectContext { get; }
        public BattleEffectTriggerPolicy TriggerPolicy { get; }

        public static BattleTriggerContext AfterDamageDealt(BattleDamageContext context)
        {
            return new BattleTriggerContext(
                BattleTriggerTiming.AfterDamageDealt,
                context.Source,
                context.Source,
                context.Target,
                context.ResolvedAmount,
                context.EffectContext,
                context.TriggerPolicy);
        }

        public static BattleTriggerContext AfterDamageTaken(BattleDamageContext context)
        {
            return new BattleTriggerContext(
                BattleTriggerTiming.AfterDamageTaken,
                context.Target,
                context.Source,
                context.Target,
                context.ResolvedAmount,
                context.EffectContext,
                context.TriggerPolicy);
        }

        public static BattleTriggerContext AfterEnemyKilled(
            EntityId killer,
            EntityId deadUnit,
            BattleEffectContext effectContext,
            BattleEffectTriggerPolicy triggerPolicy)
        {
            return new BattleTriggerContext(
                BattleTriggerTiming.AfterEnemyKilled,
                killer,
                killer,
                deadUnit,
                0,
                effectContext,
                triggerPolicy);
        }
    }
}
