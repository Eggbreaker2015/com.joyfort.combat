using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    internal readonly struct ProjectileEmitterComponent
    {
        public ProjectileEmitterComponent(
            EntityId source,
            EntityId target,
            TeamId teamId,
            ProjectileEmitterAnchorMode anchorMode,
            BattleVector2 anchorOffset,
            BattleVector2 lastResolvedPosition,
            int durationRemainingTicks,
            int fireIntervalTicks,
            int ticksUntilNextFire,
            ProjectilePattern pattern,
            ProjectilePayload projectilePayload,
            BattleTick activateOnTick)
            : this(
                source,
                target,
                teamId,
                anchorMode,
                anchorOffset,
                lastResolvedPosition,
                durationRemainingTicks,
                fireIntervalTicks,
                ticksUntilNextFire,
                pattern,
                projectilePayload,
                activateOnTick,
                copyPayload: true)
        {
        }

        private ProjectileEmitterComponent(
            EntityId source,
            EntityId target,
            TeamId teamId,
            ProjectileEmitterAnchorMode anchorMode,
            BattleVector2 anchorOffset,
            BattleVector2 lastResolvedPosition,
            int durationRemainingTicks,
            int fireIntervalTicks,
            int ticksUntilNextFire,
            ProjectilePattern pattern,
            ProjectilePayload projectilePayload,
            BattleTick activateOnTick,
            bool copyPayload)
        {
            Source = source;
            Target = target;
            TeamId = teamId;
            AnchorMode = anchorMode;
            AnchorOffset = anchorOffset;
            LastResolvedPosition = lastResolvedPosition;
            DurationRemainingTicks = durationRemainingTicks > 0 ? durationRemainingTicks : throw new ArgumentOutOfRangeException(nameof(durationRemainingTicks));
            FireIntervalTicks = fireIntervalTicks > 0 ? fireIntervalTicks : throw new ArgumentOutOfRangeException(nameof(fireIntervalTicks));
            TicksUntilNextFire = ticksUntilNextFire >= 0 ? ticksUntilNextFire : throw new ArgumentOutOfRangeException(nameof(ticksUntilNextFire));
            Pattern = ProjectilePattern.CopyValidated(pattern);
            ProjectilePayload = copyPayload ? ProjectilePayload.CopyValidated(projectilePayload) : projectilePayload;
            ActivateOnTick = activateOnTick;
        }

        public EntityId Source { get; }
        public EntityId Target { get; }
        public TeamId TeamId { get; }
        public ProjectileEmitterAnchorMode AnchorMode { get; }
        public BattleVector2 AnchorOffset { get; }
        public BattleVector2 LastResolvedPosition { get; }
        public int DurationRemainingTicks { get; }
        public int FireIntervalTicks { get; }
        public int TicksUntilNextFire { get; }
        public ProjectilePattern Pattern { get; }
        public ProjectilePayload ProjectilePayload { get; }
        public BattleTick ActivateOnTick { get; }

        public ProjectileEmitterComponent WithTiming(int durationRemainingTicks, int ticksUntilNextFire)
        {
            return new ProjectileEmitterComponent(Source, Target, TeamId, AnchorMode, AnchorOffset, LastResolvedPosition, durationRemainingTicks, FireIntervalTicks, ticksUntilNextFire, Pattern, ProjectilePayload, ActivateOnTick, copyPayload: false);
        }

        public ProjectileEmitterComponent WithLastResolvedPosition(BattleVector2 position)
        {
            return new ProjectileEmitterComponent(Source, Target, TeamId, AnchorMode, AnchorOffset, position, DurationRemainingTicks, FireIntervalTicks, TicksUntilNextFire, Pattern, ProjectilePayload, ActivateOnTick, copyPayload: false);
        }
    }

    internal readonly struct ProjectileComponent
    {
        private readonly BattleEffectData[] _impactEffects;
        private readonly ReadOnlyCollection<BattleEffectData> _readOnlyImpactEffects;
        private readonly UnitId[] _hitTargetIds;

        public ProjectileComponent(
            ProjectileId projectileId,
            EntityId source,
            TeamId teamId,
            BattleVector2 position,
            BattleVector2 velocity,
            float radius,
            int lifetimeRemainingTicks,
            ProjectileBehavior behavior,
            ProjectileHitPolicy hitPolicy,
            IReadOnlyList<BattleEffectDefinition> impactEffects,
            BattleTick activateOnTick)
            : this(
                projectileId,
                source,
                teamId,
                position,
                velocity,
                BattleScalar.FromFloat(radius),
                lifetimeRemainingTicks,
                behavior,
                hitPolicy,
                impactEffects,
                activateOnTick)
        {
        }

        public ProjectileComponent(
            ProjectileId projectileId,
            EntityId source,
            TeamId teamId,
            BattleVector2 position,
            BattleVector2 velocity,
            BattleScalar radius,
            int lifetimeRemainingTicks,
            ProjectileBehavior behavior,
            ProjectileHitPolicy hitPolicy,
            IReadOnlyList<BattleEffectDefinition> impactEffects,
            BattleTick activateOnTick)
            : this(
                projectileId,
                source,
                teamId,
                position,
                velocity,
                radius,
                lifetimeRemainingTicks,
                behavior,
                hitPolicy,
                CreateImpactEffectData(impactEffects),
                activateOnTick)
        {
        }

        public ProjectileComponent(
            ProjectileId projectileId,
            EntityId source,
            TeamId teamId,
            BattleVector2 position,
            BattleVector2 velocity,
            float radius,
            int lifetimeRemainingTicks,
            ProjectileBehavior behavior,
            ProjectileHitPolicy hitPolicy,
            IReadOnlyList<BattleEffectData> impactEffects,
            BattleTick activateOnTick)
            : this(
                projectileId,
                source,
                teamId,
                position,
                velocity,
                BattleScalar.FromFloat(radius),
                lifetimeRemainingTicks,
                behavior,
                hitPolicy,
                impactEffects,
                activateOnTick)
        {
        }

        public ProjectileComponent(
            ProjectileId projectileId,
            EntityId source,
            TeamId teamId,
            BattleVector2 position,
            BattleVector2 velocity,
            BattleScalar radius,
            int lifetimeRemainingTicks,
            ProjectileBehavior behavior,
            ProjectileHitPolicy hitPolicy,
            IReadOnlyList<BattleEffectData> impactEffects,
            BattleTick activateOnTick)
            : this(
                projectileId,
                source,
                teamId,
                position,
                velocity,
                radius,
                lifetimeRemainingTicks,
                behavior,
                hitPolicy,
                CopyImpactEffects(impactEffects),
                activateOnTick)
        {
        }

        private ProjectileComponent(
            ProjectileId projectileId,
            EntityId source,
            TeamId teamId,
            BattleVector2 position,
            BattleVector2 velocity,
            BattleScalar radius,
            int lifetimeRemainingTicks,
            ProjectileBehavior behavior,
            ProjectileHitPolicy hitPolicy,
            BattleEffectData[] impactEffects,
            BattleTick activateOnTick)
            : this(
                projectileId,
                source,
                teamId,
                position,
                velocity,
                radius,
                lifetimeRemainingTicks,
                behavior,
                hitPolicy,
                impactEffects,
                new ReadOnlyCollection<BattleEffectData>(impactEffects),
                Array.Empty<UnitId>(),
                activateOnTick)
        {
        }

        private ProjectileComponent(
            ProjectileId projectileId,
            EntityId source,
            TeamId teamId,
            BattleVector2 position,
            BattleVector2 velocity,
            BattleScalar radius,
            int lifetimeRemainingTicks,
            ProjectileBehavior behavior,
            ProjectileHitPolicy hitPolicy,
            BattleEffectData[] impactEffects,
            ReadOnlyCollection<BattleEffectData> readOnlyImpactEffects,
            UnitId[] hitTargetIds,
            BattleTick activateOnTick)
        {
            ProjectileId = projectileId;
            Source = source;
            TeamId = teamId;
            Position = position;
            Velocity = velocity;
            Radius = radius >= BattleScalar.Zero ? radius : throw new ArgumentOutOfRangeException(nameof(radius));
            LifetimeRemainingTicks = lifetimeRemainingTicks > 0 ? lifetimeRemainingTicks : throw new ArgumentOutOfRangeException(nameof(lifetimeRemainingTicks));
            Behavior = behavior;
            HitPolicy = ProjectileHitPolicy.CopyValidated(hitPolicy);
            _impactEffects = impactEffects ?? throw new ArgumentNullException(nameof(impactEffects));
            _readOnlyImpactEffects = readOnlyImpactEffects ?? throw new ArgumentNullException(nameof(readOnlyImpactEffects));
            _hitTargetIds = hitTargetIds ?? throw new ArgumentNullException(nameof(hitTargetIds));
            if (_hitTargetIds.Length > HitPolicy.MaxHitCount)
            {
                throw new ArgumentOutOfRangeException(nameof(hitTargetIds));
            }

            ActivateOnTick = activateOnTick;
        }

        public ProjectileId ProjectileId { get; }
        public EntityId Source { get; }
        public TeamId TeamId { get; }
        public BattleVector2 Position { get; }
        public BattleVector2 Velocity { get; }
        public BattleScalar Radius { get; }
        public int LifetimeRemainingTicks { get; }
        public ProjectileBehavior Behavior { get; }
        public ProjectileHitPolicy HitPolicy { get; }
        public int HitCount => _hitTargetIds?.Length ?? 0;
        public bool CanHitMoreTargets => HitCount < HitPolicy.MaxHitCount;
        public IReadOnlyList<BattleEffectData> ImpactEffects => _readOnlyImpactEffects ?? EmptyImpactEffects;
        public BattleTick ActivateOnTick { get; }

        private static readonly ReadOnlyCollection<BattleEffectData> EmptyImpactEffects = new ReadOnlyCollection<BattleEffectData>(Array.Empty<BattleEffectData>());

        public ProjectileComponent WithPositionAndLifetime(BattleVector2 position, int lifetimeRemainingTicks)
        {
            return new ProjectileComponent(ProjectileId, Source, TeamId, position, Velocity, Radius, lifetimeRemainingTicks, Behavior, HitPolicy, _impactEffects, _readOnlyImpactEffects, _hitTargetIds, ActivateOnTick);
        }

        public bool HasHitTarget(UnitId targetUnitId)
        {
            if (_hitTargetIds == null)
            {
                return false;
            }

            for (var i = 0; i < _hitTargetIds.Length; i++)
            {
                if (_hitTargetIds[i].Equals(targetUnitId))
                {
                    return true;
                }
            }

            return false;
        }

        public ProjectileComponent WithRegisteredHit(UnitId targetUnitId)
        {
            if (!CanHitMoreTargets || HasHitTarget(targetUnitId))
            {
                throw new InvalidOperationException(
                    $"Projectile {ProjectileId.Value} cannot register target {targetUnitId.Value}.");
            }

            int hitCount = HitCount;
            var hitTargetIds = new UnitId[hitCount + 1];
            if (hitCount > 0)
            {
                Array.Copy(_hitTargetIds, hitTargetIds, hitCount);
            }

            hitTargetIds[hitCount] = targetUnitId;
            return new ProjectileComponent(ProjectileId, Source, TeamId, Position, Velocity, Radius, LifetimeRemainingTicks, Behavior, HitPolicy, _impactEffects, _readOnlyImpactEffects, hitTargetIds, ActivateOnTick);
        }

        private static BattleEffectData[] CreateImpactEffectData(IReadOnlyList<BattleEffectDefinition> impactEffects)
        {
            if (impactEffects == null)
            {
                throw new ArgumentNullException(nameof(impactEffects));
            }

            var copy = new BattleEffectData[impactEffects.Count];
            for (var i = 0; i < impactEffects.Count; i++)
            {
                copy[i] = BattleEffectRuntimeDataFactory.CreateEffectData(impactEffects[i]);
            }

            return copy;
        }

        private static BattleEffectData[] CopyImpactEffects(IReadOnlyList<BattleEffectData> impactEffects)
        {
            if (impactEffects == null)
            {
                throw new ArgumentNullException(nameof(impactEffects));
            }

            var copy = new BattleEffectData[impactEffects.Count];
            for (var i = 0; i < impactEffects.Count; i++)
            {
                copy[i] = BattleEffectData.CopyValidated(impactEffects[i]);
            }

            return copy;
        }
    }
}
