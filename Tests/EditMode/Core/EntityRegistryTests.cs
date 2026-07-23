using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class EntityRegistryTests
    {
        [Test]
        public void DefaultEntityId_IsInvalid()
        {
            var registry = new EntityRegistry();

            Assert.IsFalse(default(EntityId).IsValid);
            Assert.IsFalse(registry.IsAlive(default));
        }

        [Test]
        public void CreateEntity_ReturnsAliveEntityWithGeneration()
        {
            var registry = new EntityRegistry();

            EntityId entity = registry.CreateEntity();

            Assert.AreEqual(0, entity.Index);
            Assert.AreEqual(1, entity.Generation);
            Assert.IsTrue(entity.IsValid);
            Assert.IsTrue(registry.IsAlive(entity));
        }

        [Test]
        public void ReleaseEntity_InvalidatesOldHandle()
        {
            var registry = new EntityRegistry();
            EntityId entity = registry.CreateEntity();

            registry.ReleaseEntity(entity);

            Assert.IsFalse(registry.IsAlive(entity));
        }

        [Test]
        public void ReleasedSlotCanBeReusedWithoutRevivingOldHandle()
        {
            var registry = new EntityRegistry();
            EntityId oldEntity = registry.CreateEntity();

            registry.ReleaseEntity(oldEntity);
            EntityId newEntity = registry.CreateEntity();

            Assert.AreEqual(oldEntity.Index, newEntity.Index);
            Assert.AreNotEqual(oldEntity.Generation, newEntity.Generation);
            Assert.IsFalse(registry.IsAlive(oldEntity));
            Assert.IsTrue(registry.IsAlive(newEntity));
        }
    }
}
