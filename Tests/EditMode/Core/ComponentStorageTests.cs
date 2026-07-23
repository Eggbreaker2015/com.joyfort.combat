using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class ComponentStorageTests
    {
        [Test]
        public void AddTryGetSetAndRemove_ComponentForLiveEntity()
        {
            var registry = new EntityRegistry();
            var storage = new ComponentStorage<HealthComponent>(registry);
            EntityId entity = registry.CreateEntity();

            storage.Add(entity, new HealthComponent(7));

            Assert.IsTrue(storage.TryGet(entity, out HealthComponent health));
            Assert.AreEqual(7, health.Current);

            storage.Set(entity, new HealthComponent(5));
            Assert.AreEqual(5, storage.Get(entity).Current);

            Assert.IsTrue(storage.Remove(entity));
            Assert.IsFalse(storage.TryGet(entity, out _));
        }

        [Test]
        public void TryGet_ReturnsFalseForReleasedOldHandleAndReusedSlot()
        {
            var registry = new EntityRegistry();
            var storage = new ComponentStorage<TeamComponent>(registry);
            EntityId oldEntity = registry.CreateEntity();
            storage.Add(oldEntity, new TeamComponent(new TeamId(1)));

            registry.ReleaseEntity(oldEntity);
            EntityId newEntity = registry.CreateEntity();
            storage.Add(newEntity, new TeamComponent(new TeamId(2)));

            Assert.IsFalse(storage.TryGet(oldEntity, out _));
            Assert.IsTrue(storage.TryGet(newEntity, out TeamComponent team));
            Assert.AreEqual(new TeamId(2), team.TeamId);
        }

        [Test]
        public void Entities_ReturnsStableInsertionOrderForLiveComponents()
        {
            var registry = new EntityRegistry();
            var storage = new ComponentStorage<UnitComponent>(registry);
            EntityId first = registry.CreateEntity();
            EntityId second = registry.CreateEntity();

            storage.Add(first, new UnitComponent(new UnitId(1), "first"));
            storage.Add(second, new UnitComponent(new UnitId(2), "second"));

            Assert.AreEqual(first, storage.Entities[0]);
            Assert.AreEqual(second, storage.Entities[1]);
        }

        [Test]
        public void Entities_PrunesReleasedHandlesWhenSlotIsReused()
        {
            var registry = new EntityRegistry();
            var storage = new ComponentStorage<TeamComponent>(registry);
            EntityId oldEntity = registry.CreateEntity();
            storage.Add(oldEntity, new TeamComponent(new TeamId(1)));

            registry.ReleaseEntity(oldEntity);
            EntityId newEntity = registry.CreateEntity();
            storage.Add(newEntity, new TeamComponent(new TeamId(2)));

            Assert.AreEqual(1, storage.Entities.Count);
            Assert.AreEqual(newEntity, storage.Entities[0]);
            foreach (EntityId entity in storage.Entities)
            {
                Assert.DoesNotThrow(() => storage.Get(entity));
            }
        }
    }
}
