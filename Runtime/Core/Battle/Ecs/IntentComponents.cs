using System;

namespace Combat.Core.Battle
{
    internal enum BattleIntentType
    {
        Auto,
        Hold,
        MoveToPosition,
        FocusTarget,
        UseAbility
    }

    internal readonly struct BattleIntent
    {
        private BattleIntent(
            BattleIntentType type,
            EntityId source,
            EntityId target,
            BattleVector2 destination,
            int abilityIndex)
        {
            Type = ValidateType(type);
            Source = source;
            Target = target;
            Destination = destination;
            AbilityIndex = abilityIndex;
        }

        public BattleIntentType Type { get; }
        public EntityId Source { get; }
        public EntityId Target { get; }
        public BattleVector2 Destination { get; }
        public int AbilityIndex { get; }

        public static BattleIntent Auto(EntityId source)
        {
            return new BattleIntent(BattleIntentType.Auto, source, default, default, -1);
        }

        public static BattleIntent Hold(EntityId source)
        {
            return new BattleIntent(BattleIntentType.Hold, source, default, default, -1);
        }

        public static BattleIntent MoveToPosition(EntityId source, BattleVector2 destination)
        {
            return new BattleIntent(BattleIntentType.MoveToPosition, source, default, destination, -1);
        }

        public static BattleIntent FocusTarget(EntityId source, EntityId target)
        {
            return new BattleIntent(BattleIntentType.FocusTarget, source, target, default, -1);
        }

        public static BattleIntent UseAbility(EntityId source, int abilityIndex, EntityId target)
        {
            return new BattleIntent(BattleIntentType.UseAbility, source, target, default, abilityIndex);
        }

        private static BattleIntentType ValidateType(BattleIntentType type)
        {
            switch (type)
            {
                case BattleIntentType.Auto:
                case BattleIntentType.Hold:
                case BattleIntentType.MoveToPosition:
                case BattleIntentType.FocusTarget:
                case BattleIntentType.UseAbility:
                    return type;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported battle intent type.");
            }
        }
    }

    internal readonly struct IntentComponent
    {
        public IntentComponent(BattleIntent intent)
        {
            Intent = intent;
        }

        public BattleIntent Intent { get; }
    }

    internal readonly struct GarrisonedComponent
    {
    }
}
