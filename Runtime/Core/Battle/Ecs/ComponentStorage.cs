using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    internal interface IComponentStorage
    {
        Type ComponentType { get; }
        bool Remove(EntityId entity);
    }

    internal interface IComponentIndex<T> where T : struct
    {
        void Add(EntityId entity, T component);
        void Set(EntityId entity, T previousComponent, T component);
        void Remove(EntityId entity, T component);
    }

    internal sealed class ComponentStorage<T> : IComponentStorage where T : struct
    {
        private readonly EntityRegistry _registry;
        private readonly IComponentIndex<T> _index;
        private readonly Dictionary<EntityId, T> _components = new Dictionary<EntityId, T>();
        private readonly List<EntityId> _entities = new List<EntityId>(64);
        private readonly ReadOnlyCollection<EntityId> _readOnlyEntities;

        public ComponentStorage(EntityRegistry registry, IComponentIndex<T> index = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _index = index;
            _readOnlyEntities = new ReadOnlyCollection<EntityId>(_entities);
        }

        public Type ComponentType => typeof(T);

        public IReadOnlyList<EntityId> Entities
        {
            get
            {
                PruneReleasedComponents();
                return _readOnlyEntities;
            }
        }

        public void Add(EntityId entity, T component)
        {
            EnsureAlive(entity);
            PruneReleasedComponents();
            if (_components.ContainsKey(entity))
            {
                throw new InvalidOperationException($"Entity already has component {typeof(T).Name}.");
            }

            _index?.Add(entity, component);
            _components.Add(entity, component);
            _entities.Add(entity);
        }

        public void Set(EntityId entity, T component)
        {
            EnsureAlive(entity);
            if (!_components.ContainsKey(entity))
            {
                Add(entity, component);
                return;
            }

            T previousComponent = _components[entity];
            _index?.Set(entity, previousComponent, component);
            _components[entity] = component;
        }

        public T Get(EntityId entity)
        {
            if (!TryGet(entity, out T component))
            {
                throw new InvalidOperationException($"Entity does not have live component {typeof(T).Name}.");
            }

            return component;
        }

        public bool TryGet(EntityId entity, out T component)
        {
            if (!_registry.IsAlive(entity))
            {
                Remove(entity);
                component = default;
                return false;
            }

            return _components.TryGetValue(entity, out component);
        }

        public bool Has(EntityId entity)
        {
            return TryGet(entity, out _);
        }

        public bool Remove(EntityId entity)
        {
            if (!_components.TryGetValue(entity, out T component))
            {
                return false;
            }

            _index?.Remove(entity, component);
            _components.Remove(entity);
            _entities.Remove(entity);
            return true;
        }

        private void EnsureAlive(EntityId entity)
        {
            if (!_registry.IsAlive(entity))
            {
                throw new InvalidOperationException($"Entity is not alive: {entity}.");
            }
        }

        private void PruneReleasedComponents()
        {
            for (int i = _entities.Count - 1; i >= 0; i--)
            {
                EntityId entity = _entities[i];
                if (_registry.IsAlive(entity))
                {
                    continue;
                }

                if (_components.TryGetValue(entity, out T component))
                {
                    _index?.Remove(entity, component);
                    _components.Remove(entity);
                }

                _entities.RemoveAt(i);
            }
        }
    }
}
