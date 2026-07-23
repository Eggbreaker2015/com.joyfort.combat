using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    public enum ProjectileEmitterAnchorMode
    {
        FollowSource,
        FixedPosition
    }

    public enum ProjectilePatternType
    {
        Single,
        Circle
    }

    public enum ProjectileDirectionMode
    {
        FixedDirection,
        TargetDirection
    }

    public enum ProjectileBehavior
    {
        Linear
    }

    public enum ProjectileHitPolicyMode
    {
        DestroyOnFirstHit,
        Pierce
    }

    public readonly struct ProjectileHitPolicy
    {
        private ProjectileHitPolicy(
            ProjectileHitPolicyMode mode,
            int maxHitCount)
        {
            Mode = mode;
            MaxHitCount = maxHitCount;
        }

        public ProjectileHitPolicyMode Mode { get; }
        public int MaxHitCount { get; }

        public static ProjectileHitPolicy DestroyOnFirstHit =>
            new ProjectileHitPolicy(
                ProjectileHitPolicyMode.DestroyOnFirstHit,
                1);

        public static ProjectileHitPolicy Pierce(int maxHitCount)
        {
            if (maxHitCount < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHitCount));
            }

            return new ProjectileHitPolicy(
                ProjectileHitPolicyMode.Pierce,
                maxHitCount);
        }

        internal static ProjectileHitPolicy CopyValidated(
            ProjectileHitPolicy policy)
        {
            switch (policy.Mode)
            {
                case ProjectileHitPolicyMode.DestroyOnFirstHit:
                    if (policy.MaxHitCount != 1)
                    {
                        throw new ArgumentOutOfRangeException(nameof(policy));
                    }

                    return DestroyOnFirstHit;
                case ProjectileHitPolicyMode.Pierce:
                    return Pierce(policy.MaxHitCount);
                default:
                    throw new ArgumentOutOfRangeException(nameof(policy));
            }
        }
    }

    public readonly struct ProjectilePattern
    {
        private ProjectilePattern(ProjectilePatternType type, ProjectileDirectionMode directionMode, BattleVector2 direction, int projectileCount)
        {
            Type = type;
            DirectionMode = directionMode;
            Direction = direction;
            ProjectileCount = projectileCount;
        }

        public ProjectilePatternType Type { get; }
        public ProjectileDirectionMode DirectionMode { get; }
        public BattleVector2 Direction { get; }
        public int ProjectileCount { get; }

        public static ProjectilePattern Single(BattleVector2 direction)
        {
            return Single(direction, ProjectileDirectionMode.FixedDirection);
        }

        public static ProjectilePattern Single(BattleVector2 direction, ProjectileDirectionMode directionMode)
        {
            return new ProjectilePattern(ProjectilePatternType.Single, ValidateDirectionMode(directionMode), direction, 1);
        }

        public static ProjectilePattern Circle(int projectileCount)
        {
            if (projectileCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileCount));
            }

            return new ProjectilePattern(ProjectilePatternType.Circle, ProjectileDirectionMode.FixedDirection, new BattleVector2(1f, 0f), projectileCount);
        }

        internal static ProjectilePattern CopyValidated(ProjectilePattern pattern)
        {
            switch (pattern.Type)
            {
                case ProjectilePatternType.Single:
                    if (pattern.ProjectileCount != 1)
                    {
                        throw new ArgumentOutOfRangeException(nameof(pattern));
                    }

                    return Single(pattern.Direction, pattern.DirectionMode);
                case ProjectilePatternType.Circle:
                    return Circle(pattern.ProjectileCount);
                default:
                    throw new ArgumentOutOfRangeException(nameof(pattern));
            }
        }

        private static ProjectileDirectionMode ValidateDirectionMode(ProjectileDirectionMode directionMode)
        {
            switch (directionMode)
            {
                case ProjectileDirectionMode.FixedDirection:
                case ProjectileDirectionMode.TargetDirection:
                    return directionMode;
                default:
                    throw new ArgumentOutOfRangeException(nameof(directionMode));
            }
        }
    }

    public readonly struct ProjectilePayload
    {
        private readonly BattleEffectDefinition[] _impactEffects;
        private readonly ReadOnlyCollection<BattleEffectDefinition> _readOnlyImpactEffects;
        private readonly BattleEffectData[] _impactEffectData;
        private readonly ReadOnlyCollection<BattleEffectData> _readOnlyImpactEffectData;

        public ProjectilePayload(
            ProjectileBehavior behavior,
            ProjectileHitPolicy hitPolicy,
            float radius,
            float speed,
            int lifetimeTicks,
            IReadOnlyList<BattleEffectDefinition> impactEffects)
            : this(
                behavior,
                hitPolicy,
                BattleScalar.FromFloat(radius),
                BattleScalar.FromFloat(speed),
                lifetimeTicks,
                impactEffects)
        {
        }

        public ProjectilePayload(
            ProjectileBehavior behavior,
            ProjectileHitPolicy hitPolicy,
            BattleScalar radius,
            BattleScalar speed,
            int lifetimeTicks,
            IReadOnlyList<BattleEffectDefinition> impactEffects)
        {
            Behavior = behavior;
            HitPolicy = ProjectileHitPolicy.CopyValidated(hitPolicy);
            Radius = radius >= BattleScalar.Zero ? radius : throw new ArgumentOutOfRangeException(nameof(radius));
            Speed = speed >= BattleScalar.Zero ? speed : throw new ArgumentOutOfRangeException(nameof(speed));
            LifetimeTicks = lifetimeTicks > 0 ? lifetimeTicks : throw new ArgumentOutOfRangeException(nameof(lifetimeTicks));
            _impactEffects = CopyImpactEffects(impactEffects);
            _readOnlyImpactEffects = new ReadOnlyCollection<BattleEffectDefinition>(_impactEffects);
            _impactEffectData = CreateImpactEffectData(_impactEffects);
            _readOnlyImpactEffectData = new ReadOnlyCollection<BattleEffectData>(_impactEffectData);
        }

        public ProjectileBehavior Behavior { get; }
        public ProjectileHitPolicy HitPolicy { get; }
        public BattleScalar Radius { get; }
        public BattleScalar Speed { get; }
        public int LifetimeTicks { get; }
        public IReadOnlyList<BattleEffectDefinition> ImpactEffects => _readOnlyImpactEffects ?? EmptyImpactEffects;
        internal IReadOnlyList<BattleEffectData> ImpactEffectData => _readOnlyImpactEffectData ?? EmptyImpactEffectData;

        private static readonly ReadOnlyCollection<BattleEffectDefinition> EmptyImpactEffects = new ReadOnlyCollection<BattleEffectDefinition>(Array.Empty<BattleEffectDefinition>());
        private static readonly ReadOnlyCollection<BattleEffectData> EmptyImpactEffectData = new ReadOnlyCollection<BattleEffectData>(Array.Empty<BattleEffectData>());

        internal static ProjectilePayload CopyValidated(ProjectilePayload payload)
        {
            return new ProjectilePayload(
                payload.Behavior,
                payload.HitPolicy,
                payload.Radius,
                payload.Speed,
                payload.LifetimeTicks,
                payload.ImpactEffects);
        }

        private static BattleEffectDefinition[] CopyImpactEffects(IReadOnlyList<BattleEffectDefinition> impactEffects)
        {
            if (impactEffects == null)
            {
                throw new ArgumentNullException(nameof(impactEffects));
            }

            var copy = new BattleEffectDefinition[impactEffects.Count];
            for (var i = 0; i < impactEffects.Count; i++)
            {
                copy[i] = BattleEffectDefinition.CopyValidated(impactEffects[i]);
            }

            return copy;
        }

        private static BattleEffectData[] CreateImpactEffectData(IReadOnlyList<BattleEffectDefinition> impactEffects)
        {
            var copy = new BattleEffectData[impactEffects.Count];
            for (var i = 0; i < impactEffects.Count; i++)
            {
                copy[i] = BattleEffectRuntimeDataFactory.CreateEffectData(impactEffects[i]);
            }

            return copy;
        }
    }

    public readonly struct ProjectileEmitterSpawnData
    {
        public ProjectileEmitterSpawnData(
            ProjectileEmitterAnchorMode anchorMode,
            BattleVector2 anchorOffset,
            int durationTicks,
            int fireIntervalTicks,
            ProjectilePattern pattern,
            ProjectilePayload projectilePayload)
        {
            AnchorMode = anchorMode;
            AnchorOffset = anchorOffset;
            DurationTicks = durationTicks > 0 ? durationTicks : throw new ArgumentOutOfRangeException(nameof(durationTicks));
            FireIntervalTicks = fireIntervalTicks > 0 ? fireIntervalTicks : throw new ArgumentOutOfRangeException(nameof(fireIntervalTicks));
            Pattern = ProjectilePattern.CopyValidated(pattern);
            ProjectilePayload = ProjectilePayload.CopyValidated(projectilePayload);
        }

        public ProjectileEmitterAnchorMode AnchorMode { get; }
        public BattleVector2 AnchorOffset { get; }
        public int DurationTicks { get; }
        public int FireIntervalTicks { get; }
        public ProjectilePattern Pattern { get; }
        public ProjectilePayload ProjectilePayload { get; }
    }
}
