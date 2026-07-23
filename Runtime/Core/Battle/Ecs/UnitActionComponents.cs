using System;

namespace Combat.Core.Battle
{
    [Flags]
    public enum BattleActionLocks
    {
        None = 0,
        Movement = 1 << 0,
        Facing = 1 << 1,
        StartAnotherAction = 1 << 2
    }

    internal enum UnitActionType
    {
        None,
        Ability
    }

    internal readonly struct UnitActionComponent
    {
        private UnitActionComponent(UnitActionType type, int abilityIndex, string abilityId, EntityId target, BattleTick startedTick, BattleTick releaseTick, BattleTick endTick, BattleActionLocks locks, int releasedFrameCount)
        {
            Type = type;
            AbilityIndex = abilityIndex;
            AbilityId = abilityId ?? string.Empty;
            Target = target;
            StartedTick = startedTick;
            ReleaseTick = releaseTick;
            EndTick = endTick;
            Locks = locks;
            ReleasedFrameCount = releasedFrameCount >= 0 ? releasedFrameCount : throw new ArgumentOutOfRangeException(nameof(releasedFrameCount));
        }

        public UnitActionType Type { get; }
        public int AbilityIndex { get; }
        public string AbilityId { get; }
        public EntityId Target { get; }
        public BattleTick StartedTick { get; }
        public BattleTick ReleaseTick { get; }
        public BattleTick EndTick { get; }
        public BattleActionLocks Locks { get; }
        public int ReleasedFrameCount { get; }
        public bool HasReleased => ReleasedFrameCount > 0;
        public static UnitActionComponent None => default;
        public bool IsActive => Type != UnitActionType.None;

        public static UnitActionComponent Ability(int abilityIndex, string abilityId, EntityId target, BattleTick startedTick, BattleTick releaseTick, BattleTick endTick, BattleActionLocks locks)
        {
            if (abilityIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(abilityIndex));
            }

            return new UnitActionComponent(UnitActionType.Ability, abilityIndex, abilityId, target, startedTick, releaseTick, endTick, locks, releasedFrameCount: 0);
        }

        public UnitActionComponent WithReleased()
        {
            return Type == UnitActionType.None
                ? this
                : new UnitActionComponent(Type, AbilityIndex, AbilityId, Target, StartedTick, ReleaseTick, EndTick, Locks, int.MaxValue);
        }

        public UnitActionComponent WithReleasedFrameCount(int releasedFrameCount)
        {
            return Type == UnitActionType.None
                ? this
                : new UnitActionComponent(Type, AbilityIndex, AbilityId, Target, StartedTick, ReleaseTick, EndTick, Locks, releasedFrameCount);
        }
    }
}
