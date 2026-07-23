using System;
using System.Collections.Generic;

namespace Combat.Core.Battle
{
    internal sealed class EntityRegistry
    {
        private readonly List<int> _generations = new List<int>(64);
        private readonly List<bool> _alive = new List<bool>(64);
        private readonly Queue<int> _freeSlots = new Queue<int>();

        public EntityId CreateEntity()
        {
            if (_freeSlots.Count > 0)
            {
                int reusedIndex = _freeSlots.Dequeue();
                _alive[reusedIndex] = true;
                return new EntityId(reusedIndex, _generations[reusedIndex]);
            }

            int index = _generations.Count;
            _generations.Add(1);
            _alive.Add(true);
            return new EntityId(index, 1);
        }

        public bool IsAlive(EntityId entity)
        {
            return entity.IsValid
                && entity.Index >= 0
                && entity.Index < _generations.Count
                && _alive[entity.Index]
                && _generations[entity.Index] == entity.Generation;
        }

        public void ReleaseEntity(EntityId entity)
        {
            if (!IsAlive(entity))
            {
                return;
            }

            _alive[entity.Index] = false;
            _generations[entity.Index] = NextGeneration(_generations[entity.Index]);
            _freeSlots.Enqueue(entity.Index);
        }

        private static int NextGeneration(int current)
        {
            if (current == int.MaxValue)
            {
                throw new InvalidOperationException("Entity generation overflow.");
            }

            return current + 1;
        }
    }
}
