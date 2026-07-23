using System;
using System.Collections.Generic;

namespace Combat.Core.Battle
{
    internal sealed class UniqueComponentIndex<TComponent, TKey> : IComponentIndex<TComponent>
        where TComponent : struct
    {
        private readonly Func<TComponent, TKey> _getKey;
        private readonly string _keyName;
        private readonly Dictionary<TKey, EntityId> _entityByKey = new Dictionary<TKey, EntityId>();
        private readonly Dictionary<EntityId, TKey> _keyByEntity = new Dictionary<EntityId, TKey>();

        public UniqueComponentIndex(Func<TComponent, TKey> getKey, string keyName)
        {
            _getKey = getKey ?? throw new ArgumentNullException(nameof(getKey));
            _keyName = string.IsNullOrEmpty(keyName) ? typeof(TKey).Name : keyName;
        }

        public void Add(EntityId entity, TComponent component)
        {
            AddMapping(entity, _getKey(component));
        }

        public void Set(EntityId entity, TComponent previousComponent, TComponent component)
        {
            TKey previousKey = _getKey(previousComponent);
            TKey key = _getKey(component);
            ValidateMapping(entity, key, previousKey);
            RemoveMapping(entity, previousKey);
            AddMapping(entity, key);
        }

        public void Remove(EntityId entity, TComponent component)
        {
            RemoveMapping(entity, _getKey(component));
        }

        public bool TryFind(TKey key, EntityRegistry registry, out EntityId entity)
        {
            if (_entityByKey.TryGetValue(key, out entity) && registry.IsAlive(entity))
            {
                return true;
            }

            entity = default;
            return false;
        }

        public bool TryGetKey(EntityId entity, out TKey key)
        {
            return _keyByEntity.TryGetValue(entity, out key);
        }

        private void AddMapping(EntityId entity, TKey key)
        {
            ValidateMapping(entity, key);
            _entityByKey[key] = entity;
            _keyByEntity[entity] = key;
        }

        private void ValidateMapping(EntityId entity, TKey key, TKey allowedExistingKey = default)
        {
            if (_entityByKey.TryGetValue(key, out EntityId existingEntity)
                && !existingEntity.Equals(entity))
            {
                throw new InvalidOperationException($"{_keyName} already exists in BattleWorld: {key}.");
            }

            if (_keyByEntity.TryGetValue(entity, out TKey existingKey)
                && !EqualityComparer<TKey>.Default.Equals(existingKey, key)
                && !EqualityComparer<TKey>.Default.Equals(existingKey, allowedExistingKey))
            {
                throw new InvalidOperationException($"Entity already has {_keyName} mapping in BattleWorld: {entity}.");
            }
        }

        private void RemoveMapping(EntityId entity, TKey key)
        {
            if (_entityByKey.TryGetValue(key, out EntityId existingEntity)
                && existingEntity.Equals(entity))
            {
                _entityByKey.Remove(key);
            }

            if (_keyByEntity.TryGetValue(entity, out TKey existingKey)
                && EqualityComparer<TKey>.Default.Equals(existingKey, key))
            {
                _keyByEntity.Remove(entity);
            }
        }
    }
}
