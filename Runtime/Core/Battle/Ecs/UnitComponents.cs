namespace Combat.Core.Battle
{
    internal readonly struct UnitComponent
    {
        public UnitComponent(UnitId unitId, string definitionId)
        {
            UnitId = unitId;
            DefinitionId = definitionId;
        }

        public UnitId UnitId { get; }
        public string DefinitionId { get; }
    }

    internal readonly struct TeamComponent
    {
        public TeamComponent(TeamId teamId)
        {
            TeamId = teamId;
        }

        public TeamId TeamId { get; }
    }

    internal readonly struct PositionComponent
    {
        public PositionComponent(BattleVector2 position, float radius)
            : this(position, BattleScalar.FromFloat(radius))
        {
        }

        public PositionComponent(BattleVector2 position, BattleScalar radius)
        {
            Position = position;
            Radius = radius >= BattleScalar.Zero ? radius : throw new System.ArgumentOutOfRangeException(nameof(radius));
        }

        public BattleVector2 Position { get; }
        public BattleScalar Radius { get; }
    }

    internal readonly struct FacingComponent
    {
        private static BattleScalar DirectionEpsilon => BattleScalar.FromFloat(0.00001f);

        public FacingComponent(BattleVector2 direction)
        {
            Direction = direction.SqrMagnitudeScalar <= DirectionEpsilon ? BattleVector2.Right : direction.Normalized;
        }

        public BattleVector2 Direction { get; }
    }

    internal readonly struct HealthComponent
    {
        public HealthComponent(int current)
        {
            Current = current >= 0 ? current : throw new System.ArgumentOutOfRangeException(nameof(current));
        }

        public int Current { get; }
    }

    internal readonly struct BaseStatsComponent
    {
        public BaseStatsComponent(BattleStatBlock stats)
        {
            Stats = stats ?? throw new System.ArgumentNullException(nameof(stats));
        }

        public BattleStatBlock Stats { get; }
    }

    internal enum LifeState
    {
        Alive,
        Dead
    }

    internal readonly struct LifeStateComponent
    {
        public LifeStateComponent(LifeState state)
        {
            State = state;
        }

        public LifeState State { get; }
    }

    internal readonly struct TargetComponent
    {
        public TargetComponent(EntityId target)
        {
            Target = target;
        }

        public EntityId Target { get; }
    }

    internal readonly struct TargetingBehaviorComponent
    {
        public TargetingBehaviorComponent(TargetingBehaviorSpawnData data)
        {
            LimitsAcquisitionRange = data.LimitsAcquisitionRange;
            AcquisitionRange = data.AcquisitionRange;
            NoProgressTimeoutTicks = data.NoProgressTimeoutTicks;
            MinimumProgressDistance = data.MinimumProgressDistance;
            RejectedTargetCooldownTicks = data.RejectedTargetCooldownTicks;
        }

        public bool LimitsAcquisitionRange { get; }
        public BattleScalar AcquisitionRange { get; }
        public int NoProgressTimeoutTicks { get; }
        public BattleScalar MinimumProgressDistance { get; }
        public int RejectedTargetCooldownTicks { get; }
    }

    internal readonly struct TargetingStateComponent
    {
        public TargetingStateComponent(
            EntityId trackedTarget,
            BattleScalar progressBaseline,
            int noProgressTicks,
            EntityId rejectedTarget,
            int rejectedUntilTick,
            EntityId pendingAttacker)
        {
            TrackedTarget = trackedTarget;
            ProgressBaseline = progressBaseline;
            NoProgressTicks = noProgressTicks >= 0
                ? noProgressTicks
                : throw new System.ArgumentOutOfRangeException(nameof(noProgressTicks));
            RejectedTarget = rejectedTarget;
            RejectedUntilTick = rejectedUntilTick;
            PendingAttacker = pendingAttacker;
        }

        public EntityId TrackedTarget { get; }
        public BattleScalar ProgressBaseline { get; }
        public int NoProgressTicks { get; }
        public EntityId RejectedTarget { get; }
        public int RejectedUntilTick { get; }
        public EntityId PendingAttacker { get; }
    }
}
