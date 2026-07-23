using System;
using System.Collections.Generic;
using Combat.Foundation.Events;

namespace Combat.Core.Battle
{
    internal sealed partial class BattleWorld
    {
        private static BattleScalar DirectionEpsilon => BattleScalar.FromFloat(0.00001f);

        private readonly EntityRegistry _entityRegistry = new EntityRegistry();
        private readonly Dictionary<Type, IComponentStorage> _componentStores = new Dictionary<Type, IComponentStorage>();
        private readonly List<IComponentStorage> _allComponentStores = new List<IComponentStorage>();
        private readonly UniqueComponentIndex<UnitComponent, UnitId> _unitIndex;
        private readonly UniqueComponentIndex<ProjectileComponent, ProjectileId> _projectileIndex;
        private int _nextProjectileIdValue = 1;

        public BattleWorld()
        {
            CommandBuffer = new EntityCommandBuffer();
            _unitIndex = new UniqueComponentIndex<UnitComponent, UnitId>(component => component.UnitId, nameof(UnitId));
            _projectileIndex = new UniqueComponentIndex<ProjectileComponent, ProjectileId>(component => component.ProjectileId, nameof(ProjectileId));
            UnitComponents = RegisterComponentStorage(_unitIndex);
            TeamComponents = RegisterComponentStorage<TeamComponent>();
            PositionComponents = RegisterComponentStorage<PositionComponent>();
            FacingComponents = RegisterComponentStorage<FacingComponent>();
            HealthComponents = RegisterComponentStorage<HealthComponent>();
            BaseStatsComponents = RegisterComponentStorage<BaseStatsComponent>();
            LifeStateComponents = RegisterComponentStorage<LifeStateComponent>();
            BrainComponents = RegisterComponentStorage<BrainComponent>();
            IntentComponents = RegisterComponentStorage<IntentComponent>();
            GarrisonedComponents = RegisterComponentStorage<GarrisonedComponent>();
            TargetComponents = RegisterComponentStorage<TargetComponent>();
            TargetingBehaviorComponents = RegisterComponentStorage<TargetingBehaviorComponent>();
            TargetingStateComponents = RegisterComponentStorage<TargetingStateComponent>();
            AbilityComponents = RegisterComponentStorage<AbilityComponent>();
            UnitActionComponents = RegisterComponentStorage<UnitActionComponent>();
            StatusComponents = RegisterComponentStorage<StatusComponent>();
            ProjectileEmitterComponents = RegisterComponentStorage<ProjectileEmitterComponent>();
            ProjectileComponents = RegisterComponentStorage(_projectileIndex);
        }

        public EntityCommandBuffer CommandBuffer { get; }
        public ComponentStorage<UnitComponent> UnitComponents { get; }
        public ComponentStorage<TeamComponent> TeamComponents { get; }
        public ComponentStorage<PositionComponent> PositionComponents { get; }
        public ComponentStorage<FacingComponent> FacingComponents { get; }
        public ComponentStorage<HealthComponent> HealthComponents { get; }
        public ComponentStorage<BaseStatsComponent> BaseStatsComponents { get; }
        public ComponentStorage<LifeStateComponent> LifeStateComponents { get; }
        public ComponentStorage<BrainComponent> BrainComponents { get; }
        public ComponentStorage<IntentComponent> IntentComponents { get; }
        public ComponentStorage<GarrisonedComponent> GarrisonedComponents { get; }
        public ComponentStorage<TargetComponent> TargetComponents { get; }
        public ComponentStorage<TargetingBehaviorComponent> TargetingBehaviorComponents { get; }
        public ComponentStorage<TargetingStateComponent> TargetingStateComponents { get; }
        public ComponentStorage<AbilityComponent> AbilityComponents { get; }
        public ComponentStorage<UnitActionComponent> UnitActionComponents { get; }
        public ComponentStorage<StatusComponent> StatusComponents { get; }
        public ComponentStorage<ProjectileEmitterComponent> ProjectileEmitterComponents { get; }
        public ComponentStorage<ProjectileComponent> ProjectileComponents { get; }

        public BattlePerformanceWorldSnapshot CreatePerformanceSnapshot()
        {
            return new BattlePerformanceWorldSnapshot(
                UnitComponents.Entities.Count,
                ProjectileComponents.Entities.Count,
                ProjectileEmitterComponents.Entities.Count);
        }

        public bool TryFindEntity(UnitId unitId, out EntityId entity)
        {
            if (_unitIndex.TryFind(unitId, _entityRegistry, out entity))
            {
                return true;
            }

            entity = default;
            return false;
        }

        public bool TryFindProjectile(ProjectileId projectileId, out EntityId entity)
        {
            if (_projectileIndex.TryFind(projectileId, _entityRegistry, out entity))
            {
                return true;
            }

            entity = default;
            return false;
        }

        public bool IsEntityAlive(EntityId entity)
        {
            return _entityRegistry.IsAlive(entity);
        }

        public bool IsBattlefieldActiveUnit(EntityId entity)
        {
            return IsAliveUnit(entity) && !GarrisonedComponents.Has(entity);
        }

        public bool IsAliveUnit(EntityId entity)
        {
            return LifeStateComponents.TryGet(entity, out LifeStateComponent lifeState)
                && lifeState.State == LifeState.Alive
                && HealthComponents.TryGet(entity, out HealthComponent health)
                && health.Current > 0;
        }

        public bool TryGetUnitId(EntityId entity, out UnitId unitId)
        {
            if (_entityRegistry.IsAlive(entity) && _unitIndex.TryGetKey(entity, out unitId))
            {
                return true;
            }

            unitId = default;
            return false;
        }

        public bool TryGetTeamId(EntityId entity, out TeamId teamId)
        {
            if (TeamComponents.TryGet(entity, out TeamComponent team))
            {
                teamId = team.TeamId;
                return true;
            }

            teamId = default;
            return false;
        }

        public bool TryFaceUnitTowards(
            EntityId entity,
            EntityId target,
            EventBuffer<BattleEvent> events = null,
            EventSequence eventSequence = null,
            BattleTick tick = default)
        {
            if (!IsAliveUnit(entity)
                || !IsAliveUnit(target)
                || !PositionComponents.TryGet(entity, out PositionComponent position)
                || !PositionComponents.TryGet(target, out PositionComponent targetPosition))
            {
                return false;
            }

            return TrySetUnitFacing(entity, targetPosition.Position - position.Position, events, eventSequence, tick);
        }

        public bool TrySetUnitFacing(
            EntityId entity,
            BattleVector2 direction,
            EventBuffer<BattleEvent> events = null,
            EventSequence eventSequence = null,
            BattleTick tick = default)
        {
            if (!IsAliveUnit(entity) || direction.SqrMagnitudeScalar <= DirectionEpsilon)
            {
                return false;
            }

            var facing = new FacingComponent(direction);
            if (FacingComponents.TryGet(entity, out FacingComponent current)
                && BattleVector2.SqrDistanceScalar(current.Direction, facing.Direction) <= DirectionEpsilon)
            {
                return false;
            }

            FacingComponents.Set(entity, facing);
            if (events != null
                && eventSequence != null
                && TryGetUnitId(entity, out UnitId unitId)
                && TryGetTeamId(entity, out TeamId teamId))
            {
                events.Write(BattleEvent.UnitFacingChanged(eventSequence.Next(), tick, unitId, teamId, facing.Direction));
            }

            return true;
        }

        public bool TryGetUnitRuntimeSnapshot(UnitId unitId, BattleTick tick, out UnitRuntimeSnapshot snapshot)
        {
            return BattleSnapshotBuilder.TryGetUnitRuntimeSnapshot(this, unitId, tick, out snapshot);
        }

        public void FlushSpawnCombatantCommands(EventBuffer<BattleEvent> events, EventSequence eventSequence, BattleTick tick)
        {
            BattleSpawnResolver.FlushSpawnCombatantCommands(this, events, eventSequence, tick);
        }

        public void FlushSpawnProjectileCommands(EventBuffer<BattleEvent> events, EventSequence eventSequence, BattleTick tick)
        {
            BattleSpawnResolver.FlushSpawnProjectileCommands(this, events, eventSequence, tick);
        }

        public void FlushActionCommands(EventBuffer<BattleEvent> events, EventSequence eventSequence, BattleTick tick)
        {
            BattleActionResolver.FlushActionCommands(this, events, eventSequence, tick);
        }

        public void DestroyActiveProjectiles(EventBuffer<BattleEvent> events, EventSequence eventSequence, BattleTick tick)
        {
            IReadOnlyList<EntityId> projectileEntities = ProjectileComponents.Entities;
            for (var i = 0; i < projectileEntities.Count; i++)
            {
                EntityId entity = projectileEntities[i];
                if (HasPendingDestroy(entity) || !ProjectileComponents.TryGet(entity, out ProjectileComponent projectile))
                {
                    continue;
                }

                CommandBuffer.DestroyEntity(new DestroyEntityCommand(entity));
                events.Write(BattleEvent.ProjectileDestroyed(eventSequence.Next(), tick, projectile.ProjectileId));
            }
        }

        public void ApplyStructuralCommands()
        {
            IReadOnlyList<DestroyEntityCommand> destroyCommands = CommandBuffer.DestroyEntityCommands;
            for (var i = 0; i < destroyCommands.Count; i++)
            {
                DestroyEntityNow(destroyCommands[i].Entity);
            }

            IReadOnlyList<IEntityStructuralCommand> structuralCommands = CommandBuffer.StructuralCommands;
            for (var i = 0; i < structuralCommands.Count; i++)
            {
                IEntityStructuralCommand command = structuralCommands[i];
                if (IsEntityAlive(command.Entity))
                {
                    command.Apply(this);
                }
            }

            CommandBuffer.ClearStructuralCommands();
        }

        private bool HasPendingDestroy(EntityId entity)
        {
            IReadOnlyList<DestroyEntityCommand> destroyCommands = CommandBuffer.DestroyEntityCommands;
            for (var i = 0; i < destroyCommands.Count; i++)
            {
                if (destroyCommands[i].Entity.Equals(entity))
                {
                    return true;
                }
            }

            return false;
        }

        public void SetComponent<T>(EntityId entity, T component) where T : struct
        {
            GetComponentStorage<T>().Set(entity, component);
        }

        public void RemoveComponent<T>(EntityId entity) where T : struct
        {
            GetComponentStorage<T>().Remove(entity);
        }

        internal EntityId CreateEntity()
        {
            return _entityRegistry.CreateEntity();
        }

        internal ProjectileId AllocateProjectileId()
        {
            return new ProjectileId(_nextProjectileIdValue++);
        }

        private ComponentStorage<T> RegisterComponentStorage<T>(IComponentIndex<T> index = null) where T : struct
        {
            var storage = new ComponentStorage<T>(_entityRegistry, index);
            _componentStores.Add(typeof(T), storage);
            _allComponentStores.Add(storage);
            return storage;
        }

        private ComponentStorage<T> GetComponentStorage<T>() where T : struct
        {
            if (_componentStores.TryGetValue(typeof(T), out IComponentStorage storage)
                && storage is ComponentStorage<T> typedStorage)
            {
                return typedStorage;
            }

            throw new InvalidOperationException($"Unsupported component type {typeof(T).Name}.");
        }

        private void DestroyEntityNow(EntityId entity)
        {
            for (var i = 0; i < _allComponentStores.Count; i++)
            {
                _allComponentStores[i].Remove(entity);
            }

            _entityRegistry.ReleaseEntity(entity);
        }
    }
}
