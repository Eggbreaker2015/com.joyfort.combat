using Combat.Core.Battle;
using Combat.Foundation.Events;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleProjectileEmitterSystemTests
    {
        private const int TestTicksPerSecond = 10;

        [Test]
        public void Run_BeforeActivationDoesNotSpawnProjectile()
        {
            var world = CreateWorldWithEmitter(new BattleTick(2), out _);

            RunEmitter(world, new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.SpawnProjectileCommands.Count);
        }

        [Test]
        public void Run_OnActivationSpawnsProjectileFromFollowSource()
        {
            var world = CreateWorldWithEmitter(new BattleTick(2), out EntityId source);

            RunEmitter(world, new BattleTick(2));

            Assert.AreEqual(1, world.CommandBuffer.SpawnProjectileCommands.Count);
            SpawnProjectileCommand command = world.CommandBuffer.SpawnProjectileCommands[0];
            Assert.AreEqual(source, command.Source);
            Assert.AreEqual(new BattleVector2(2.5f, 3f), command.Position);
            Assert.AreEqual(new BattleTick(3), command.ActivateOnTick);
        }

        [Test]
        public void Run_FireIntervalControlsRepeatedFire()
        {
            var world = CreateWorldWithEmitter(new BattleTick(2), out _);

            RunEmitter(world, new BattleTick(2));
            Assert.AreEqual(1, world.CommandBuffer.SpawnProjectileCommands.Count);
            world.CommandBuffer.ClearSpawnProjectileCommands();

            RunEmitter(world, new BattleTick(3));
            Assert.AreEqual(0, world.CommandBuffer.SpawnProjectileCommands.Count);

            RunEmitter(world, new BattleTick(4));
            Assert.AreEqual(1, world.CommandBuffer.SpawnProjectileCommands.Count);
        }

        [Test]
        public void Run_FollowSourceEmitterIsDestroyedWhenSourceDies()
        {
            var world = CreateWorldWithEmitter(new BattleTick(2), out EntityId source);
            world.SetComponent(source, new LifeStateComponent(LifeState.Dead));

            RunEmitter(world, new BattleTick(2));

            Assert.AreEqual(1, world.CommandBuffer.DestroyEntityCommands.Count);
        }

        [Test]
        public void Run_DurationThreeEmitterDestroysOnceOnTerminalActiveTick()
        {
            var world = CreateWorldWithEmitter(new BattleTick(2), out _);

            RunEmitter(world, new BattleTick(2));
            Assert.AreEqual(0, world.CommandBuffer.DestroyEntityCommands.Count);
            world.CommandBuffer.ClearSpawnProjectileCommands();

            RunEmitter(world, new BattleTick(3));
            Assert.AreEqual(0, world.CommandBuffer.DestroyEntityCommands.Count);

            RunEmitter(world, new BattleTick(4));

            Assert.AreEqual(1, world.CommandBuffer.DestroyEntityCommands.Count);
        }

        [Test]
        public void Run_FixedPositionEmitterDoesNotDriftAcrossRepeatedFire()
        {
            var world = CreateWorldWithEmitter(
                new BattleTick(2),
                out _,
                ProjectileEmitterAnchorMode.FixedPosition);

            RunEmitter(world, new BattleTick(2));
            Assert.AreEqual(new BattleVector2(2.5f, 3f), world.CommandBuffer.SpawnProjectileCommands[0].Position);
            world.CommandBuffer.ClearSpawnProjectileCommands();

            RunEmitter(world, new BattleTick(3));
            RunEmitter(world, new BattleTick(4));

            Assert.AreEqual(1, world.CommandBuffer.SpawnProjectileCommands.Count);
            Assert.AreEqual(new BattleVector2(2.5f, 3f), world.CommandBuffer.SpawnProjectileCommands[0].Position);
        }

        [Test]
        public void Run_CirclePatternQueuesRequestedProjectileCount()
        {
            var world = CreateWorldWithEmitter(
                new BattleTick(2),
                out _,
                ProjectileEmitterAnchorMode.FollowSource,
                ProjectilePattern.Circle(6));

            RunEmitter(world, new BattleTick(2));

            Assert.AreEqual(6, world.CommandBuffer.SpawnProjectileCommands.Count);
        }

        [Test]
        public void Run_TargetDirectionSinglePatternPointsProjectileAtCurrentTarget()
        {
            var world = new BattleWorld();
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                new UnitId(1),
                new CombatantSpawnData(new TeamId(1), "caster", new BattleVector2(2f, 3f), 10, BattleScalar.FromFloat(0.25f), BattleScalar.Zero, BasicAbility(), new AbilitySpawnData[0])));
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                new UnitId(2),
                new CombatantSpawnData(new TeamId(2), "target", new BattleVector2(-1f, 3f), 10, BattleScalar.FromFloat(0.25f), BattleScalar.Zero, BasicAbility(), new AbilitySpawnData[0])));
            world.FlushSpawnCombatantCommands(events, sequence, new BattleTick(0));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            world.TryFindEntity(new UnitId(2), out EntityId target);

            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, 0.1f, 2f, 5, new[] { BattleEffectDefinition.Damage(3) });
            var emitter = new ProjectileEmitterSpawnData(
                ProjectileEmitterAnchorMode.FollowSource,
                new BattleVector2(0.5f, 0f),
                durationTicks: 1,
                fireIntervalTicks: 1,
                ProjectilePattern.Single(new BattleVector2(1f, 0f), ProjectileDirectionMode.TargetDirection),
                payload);
            world.CommandBuffer.QueueEffect(BattleEffectCommand.SpawnProjectileEmitter(source, target, emitter));
            world.FlushEffectCommands(events, sequence, new BattleTick(0));

            RunEmitter(world, new BattleTick(1));

            Assert.AreEqual(1, world.CommandBuffer.SpawnProjectileCommands.Count);
            Assert.AreEqual(
                new BattleVector2(BattleScalar.FromInt(-2) / BattleScalar.FromInt(TestTicksPerSecond), BattleScalar.Zero),
                world.CommandBuffer.SpawnProjectileCommands[0].Velocity);
        }

        private static void RunEmitter(BattleWorld world, BattleTick tick)
        {
            ProjectileEmitterSystem.Run(world, TestTicksPerSecond, tick);
        }

        private static BattleWorld CreateWorldWithEmitter(
            BattleTick activateOnTick,
            out EntityId source,
            ProjectileEmitterAnchorMode anchorMode = ProjectileEmitterAnchorMode.FollowSource,
            ProjectilePattern? patternOverride = null)
        {
            var world = new BattleWorld();
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();
            world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(
                new UnitId(1),
                new CombatantSpawnData(new TeamId(1), "caster", new BattleVector2(2f, 3f), 10, BattleScalar.FromFloat(0.25f), BattleScalar.Zero, BasicAbility(), new AbilitySpawnData[0])));
            world.FlushSpawnCombatantCommands(events, sequence, new BattleTick(0));
            world.TryFindEntity(new UnitId(1), out source);

            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, 0.1f, 2f, 5, new[] { BattleEffectDefinition.Damage(3) });
            var emitter = new ProjectileEmitterSpawnData(
                anchorMode,
                new BattleVector2(0.5f, 0f),
                durationTicks: 3,
                fireIntervalTicks: 2,
                patternOverride ?? ProjectilePattern.Single(new BattleVector2(1f, 0f)),
                payload);
            world.CommandBuffer.QueueEffect(BattleEffectCommand.SpawnProjectileEmitter(source, default, emitter));
            world.FlushEffectCommands(events, sequence, new BattleTick(activateOnTick.Value - 1));
            return world;
        }

        private static AbilitySpawnData BasicAbility()
        {
            return TestCombatants.AbilitySpawn("basic-attack", 1f, 1, 1);
        }
    }
}
