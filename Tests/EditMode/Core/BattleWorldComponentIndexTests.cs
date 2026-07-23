using Combat.Core.Battle;
using Combat.Foundation.Events;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleWorldComponentIndexTests
    {
        [Test]
        public void ProjectileComponents_SetUpdatesProjectileIdLookup()
        {
            BattleWorld world = CreateWorldWithProjectile(out EntityId projectileEntity);
            ProjectileComponent projectile = world.ProjectileComponents.Get(projectileEntity);
            var replacementId = new ProjectileId(99);

            world.ProjectileComponents.Set(projectileEntity, new ProjectileComponent(
                replacementId,
                projectile.Source,
                projectile.TeamId,
                projectile.Position,
                projectile.Velocity,
                projectile.Radius,
                projectile.LifetimeRemainingTicks,
                projectile.Behavior,
                projectile.HitPolicy,
                projectile.ImpactEffects,
                projectile.ActivateOnTick));

            Assert.IsFalse(world.TryFindProjectile(projectile.ProjectileId, out _));
            Assert.IsTrue(world.TryFindProjectile(replacementId, out EntityId indexedEntity));
            Assert.AreEqual(projectileEntity, indexedEntity);
        }

        [Test]
        public void ProjectileComponents_RemoveClearsProjectileIdLookup()
        {
            BattleWorld world = CreateWorldWithProjectile(out EntityId projectileEntity);
            ProjectileId projectileId = world.ProjectileComponents.Get(projectileEntity).ProjectileId;

            Assert.IsTrue(world.ProjectileComponents.Remove(projectileEntity));

            Assert.IsFalse(world.TryFindProjectile(projectileId, out _));
        }

        [Test]
        public void UnitComponents_SetUpdatesUnitIdLookup()
        {
            BattleWorld world = CreateWorldWithUnit(new UnitId(1), out EntityId unitEntity);
            UnitComponent unit = world.UnitComponents.Get(unitEntity);
            var replacementId = new UnitId(99);

            world.UnitComponents.Set(unitEntity, new UnitComponent(replacementId, unit.DefinitionId));

            Assert.IsFalse(world.TryFindEntity(unit.UnitId, out _));
            Assert.IsTrue(world.TryFindEntity(replacementId, out EntityId indexedEntity));
            Assert.AreEqual(unitEntity, indexedEntity);
        }

        [Test]
        public void UnitComponents_RemoveClearsUnitIdLookup()
        {
            BattleWorld world = CreateWorldWithUnit(new UnitId(1), out EntityId unitEntity);
            UnitId unitId = world.UnitComponents.Get(unitEntity).UnitId;

            Assert.IsTrue(world.UnitComponents.Remove(unitEntity));

            Assert.IsFalse(world.TryFindEntity(unitId, out _));
        }

        private static BattleWorld CreateWorldWithProjectile(out EntityId projectileEntity)
        {
            var world = new BattleWorld();
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();
            Spawn(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            var payload = new ProjectilePayload(
                ProjectileBehavior.Linear,
                ProjectileHitPolicy.DestroyOnFirstHit,
                radius: 0.5f,
                speed: 1f,
                lifetimeTicks: 3,
                impactEffects: new[] { BattleEffectDefinition.Damage(4) });

            world.CommandBuffer.SpawnProjectile(new SpawnProjectileCommand(
                source,
                new TeamId(1),
                new BattleVector2(0f, 0f),
                new BattleVector2(1f, 0f),
                payload,
                new BattleTick(1)));
            world.FlushSpawnProjectileCommands(events, sequence, new BattleTick(0));
            projectileEntity = world.ProjectileComponents.Entities[0];
            return world;
        }

        private static BattleWorld CreateWorldWithUnit(UnitId unitId, out EntityId unitEntity)
        {
            var world = new BattleWorld();
            Spawn(world, unitId, new TeamId(1), new BattleVector2(0f, 0f));
            world.TryFindEntity(unitId, out unitEntity);
            return world;
        }

        private static void Spawn(BattleWorld world, UnitId unitId, TeamId teamId, BattleVector2 position)
        {
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                unitId,
                new CombatantSpawnData(teamId, "unit", position, 10, BattleScalar.FromFloat(0.25f), BattleScalar.Zero, BasicAbility(), new AbilitySpawnData[0])));
            world.FlushSpawnCombatantCommands(new EventBuffer<BattleEvent>(), new EventSequence(), new BattleTick(0));
        }

        private static AbilitySpawnData BasicAbility()
        {
            return TestCombatants.AbilitySpawn("basic-attack", 1f, 1, 1);
        }
    }
}
