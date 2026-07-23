using Combat.Core.Battle;
using Combat.Foundation.Events;
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleProjectileSystemTests
    {
        [Test]
        public void Run_BeforeActivationDoesNotMoveOrHit()
        {
            BattleWorld world = CreateWorldWithProjectile(new BattleTick(2), out _, out _);
            var events = new EventBuffer<BattleEvent>();

            ProjectileSystem.Run(world, new CircleProjectileCollisionDetector(), events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void Run_WithNoProjectilesDoesNotCollectCollisionHits()
        {
            var world = new BattleWorld();
            var events = new EventBuffer<BattleEvent>();

            ProjectileSystem.Run(world, new ThrowingProjectileCollisionDetector(), events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void Run_WithNoActiveProjectilesDoesNotCollectCollisionHits()
        {
            BattleWorld world = CreateWorldWithProjectile(new BattleTick(2), out _, out _);
            var events = new EventBuffer<BattleEvent>();

            ProjectileSystem.Run(world, new ThrowingProjectileCollisionDetector(), events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void Run_ActiveLinearProjectileMovesAndWritesMovedEvent()
        {
            BattleWorld world = CreateWorldWithProjectile(new BattleTick(1), out EntityId projectileEntity, out _);
            var events = new EventBuffer<BattleEvent>();

            ProjectileSystem.Run(world, new CircleProjectileCollisionDetector(), events, new EventSequence(), new BattleTick(1));

            ProjectileComponent projectile = world.ProjectileComponents.Get(projectileEntity);
            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(new BattleVector2(1f, 0f), projectile.Position);
            Assert.AreEqual(BattleEventType.ProjectileMoved, stream[0].Type);
            Assert.AreEqual(new ProjectileId(1), stream[0].ProjectileId);
        }

        [Test]
        public void Run_HitEnemyQueuesDamageAndDestroysProjectile()
        {
            BattleWorld world = CreateWorldWithProjectile(new BattleTick(1), out _, out _);
            var events = new EventBuffer<BattleEvent>();

            ProjectileSystem.Run(world, new CircleProjectileCollisionDetector(), events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(1, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(BattleEffectType.Damage, world.CommandBuffer.EffectCommands[0].Type);
            Assert.AreEqual(1, world.CommandBuffer.DestroyEntityCommands.Count);
            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(BattleEventType.ProjectileMoved, stream[0].Type);
            Assert.AreEqual(BattleEventType.ProjectileHit, stream[1].Type);
            Assert.AreEqual(new ProjectileId(1), stream[1].ProjectileId);
            Assert.AreEqual(new UnitId(1), stream[1].SourceUnitId);
            Assert.AreEqual(new UnitId(2), stream[1].TargetUnitId);
            Assert.AreEqual(new UnitId(2), stream[1].UnitId);
            Assert.AreEqual(new BattleVector2(0.25f, 0f), stream[1].Position);
            Assert.AreEqual(BattleEventType.ProjectileDestroyed, stream[2].Type);
        }

        [Test]
        public void Run_PiercingProjectileHitsInSweepOrderAndDestroysAtCapacity()
        {
            BattleWorld world = CreateProjectilePathWorld(
                ProjectileHitPolicy.Pierce(2),
                new BattleVector2(-2f, 0f),
                new BattleVector2(6f, 0f),
                out _,
                new BattleVector2(0f, 0f),
                new BattleVector2(2f, 0f),
                new BattleVector2(3.5f, 0f));
            var events = new EventBuffer<BattleEvent>();

            ProjectileSystem.Run(
                world,
                new CircleProjectileCollisionDetector(),
                events,
                new EventSequence(),
                new BattleTick(1));

            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(2, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(1, world.CommandBuffer.DestroyEntityCommands.Count);
            Assert.AreEqual(BattleEventType.ProjectileHit, stream[1].Type);
            Assert.AreEqual(new UnitId(2), stream[1].TargetUnitId);
            Assert.AreEqual(BattleEventType.ProjectileHit, stream[2].Type);
            Assert.AreEqual(new UnitId(3), stream[2].TargetUnitId);
            Assert.AreEqual(BattleEventType.ProjectileDestroyed, stream[3].Type);
        }

        [Test]
        public void Run_PiercingProjectileSkipsPriorTargetAndDestroysOnLaterDistinctHit()
        {
            BattleWorld world = CreateProjectilePathWorld(
                ProjectileHitPolicy.Pierce(2),
                BattleVector2.Zero,
                new BattleVector2(1f, 0f),
                out EntityId projectileEntity,
                new BattleVector2(1f, 0f),
                new BattleVector2(2f, 0f));
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();

            ProjectileSystem.Run(
                world,
                new CircleProjectileCollisionDetector(),
                events,
                sequence,
                new BattleTick(1));

            ProjectileComponent firstTickProjectile =
                world.ProjectileComponents.Get(projectileEntity);
            Assert.AreEqual(1, firstTickProjectile.HitCount);
            Assert.AreEqual(1, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.DestroyEntityCommands.Count);

            ProjectileSystem.Run(
                world,
                new CircleProjectileCollisionDetector(),
                events,
                sequence,
                new BattleTick(2));

            ProjectileComponent projectile =
                world.ProjectileComponents.Get(projectileEntity);
            Assert.AreEqual(2, projectile.HitCount);
            Assert.IsTrue(projectile.HasHitTarget(new UnitId(2)));
            Assert.IsTrue(projectile.HasHitTarget(new UnitId(3)));
            Assert.AreEqual(2, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(1, world.CommandBuffer.DestroyEntityCommands.Count);
            Assert.AreEqual(
                2,
                CountEvents(
                    events.AsStream(),
                    BattleEventType.ProjectileHit,
                    new ProjectileId(1)));
        }

        [Test]
        public void Run_HitEnemyQueuesHealAndAreaImpactEffects()
        {
            var area = new AreaEffectDefinition(BattleScalar.FromFloat(1f), AreaEffectTargetFilter.Enemies, new[] { BattleEffectDefinition.Damage(2) });
            BattleWorld world = CreateWorldWithProjectile(
                new BattleTick(1),
                out _,
                out EntityId target,
                impactEffects: new[]
                {
                    BattleEffectDefinition.Heal(1),
                    BattleEffectDefinition.AreaEffect(area)
                });
            var events = new EventBuffer<BattleEvent>();

            ProjectileSystem.Run(world, new CircleProjectileCollisionDetector(), events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(2, world.CommandBuffer.EffectCommands.Count);
            BattleEffectCommand healCommand = world.CommandBuffer.EffectCommands[0];
            BattleEffectCommand areaCommand = world.CommandBuffer.EffectCommands[1];
            Assert.AreEqual(BattleEffectType.Heal, healCommand.Type);
            Assert.AreEqual(target, healCommand.Target);
            Assert.AreEqual(BattleEffectType.AreaEffect, areaCommand.Type);
            Assert.AreEqual(target, areaCommand.Target);
            Assert.AreEqual(BattleScalar.FromFloat(1f), areaCommand.AreaEffect.Radius);
            Assert.AreEqual(AreaEffectTargetFilter.Enemies, areaCommand.AreaEffect.TargetFilter);
            Assert.AreEqual(1, areaCommand.AreaEffect.Effects.Count);
            Assert.AreEqual(BattleEffectType.Damage, areaCommand.AreaEffect.Effects[0].Type);
            Assert.AreEqual(2, areaCommand.AreaEffect.Effects[0].Amount);
            Assert.AreEqual(BattleEffectSourceKind.Projectile, areaCommand.Context.SourceKind);
            Assert.AreEqual(new ProjectileId(1), areaCommand.Context.ProjectileId);
            Assert.AreEqual(BattleEffectType.AreaEffect, areaCommand.Context.EffectType);
        }

        [Test]
        public void ProjectileHitEvent_StoresProjectileSourceTargetAndPosition()
        {
            BattleEvent battleEvent = BattleEvent.ProjectileHit(
                sequence: 7,
                tick: new BattleTick(3),
                projectileId: new ProjectileId(4),
                sourceUnitId: new UnitId(1),
                targetUnitId: new UnitId(2),
                position: new BattleVector2(5f, 6f));

            Assert.AreEqual(BattleEventType.ProjectileHit, battleEvent.Type);
            Assert.AreEqual(7, battleEvent.Sequence);
            Assert.AreEqual(new BattleTick(3), battleEvent.Tick);
            Assert.AreEqual(new ProjectileId(4), battleEvent.ProjectileId);
            Assert.AreEqual(new UnitId(1), battleEvent.SourceUnitId);
            Assert.AreEqual(new UnitId(2), battleEvent.TargetUnitId);
            Assert.AreEqual(new UnitId(2), battleEvent.UnitId);
            Assert.AreEqual(new BattleVector2(5f, 6f), battleEvent.Position);
        }

        [Test]
        public void Run_HitEnemyQueuesApplyStatusImpactEffect()
        {
            var burn = new StatusDefinition("burn", StatusPolarity.Debuff, 3, 1, 2, new BattleModifierDefinition[0], new BattleTriggerDefinition[0]);
            BattleWorld world = CreateWorldWithProjectile(
                new BattleTick(1),
                out _,
                out _,
                impactEffects: new[] { BattleEffectDefinition.ApplyStatus(burn) });
            var events = new EventBuffer<BattleEvent>();

            ProjectileSystem.Run(world, new CircleProjectileCollisionDetector(), events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(1, world.CommandBuffer.EffectCommands.Count);
            BattleEffectCommand effect = world.CommandBuffer.EffectCommands[0];
            Assert.AreEqual(BattleEffectType.ApplyStatus, effect.Type);
            Assert.AreEqual("burn", effect.Status.Id);
            Assert.AreEqual(1, world.CommandBuffer.DestroyEntityCommands.Count);
        }

        [Test]
        public void Run_HitEnemyQueuesSpawnProjectileEmitterImpactEffect()
        {
            var nestedPayload = new ProjectilePayload(
                ProjectileBehavior.Linear,
                ProjectileHitPolicy.DestroyOnFirstHit,
                radius: 0.1f,
                speed: 1f,
                lifetimeTicks: 2,
                impactEffects: new[] { BattleEffectDefinition.Damage(1) });
            var nestedEmitter = new ProjectileEmitterSpawnData(
                ProjectileEmitterAnchorMode.FixedPosition,
                default,
                durationTicks: 1,
                fireIntervalTicks: 1,
                ProjectilePattern.Circle(3),
                nestedPayload);
            BattleWorld world = CreateWorldWithProjectile(
                new BattleTick(1),
                out _,
                out _,
                impactEffects: new[] { BattleEffectDefinition.SpawnProjectileEmitter(nestedEmitter) });
            var events = new EventBuffer<BattleEvent>();

            ProjectileSystem.Run(world, new CircleProjectileCollisionDetector(), events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(1, world.CommandBuffer.EffectCommands.Count);
            BattleEffectCommand effect = world.CommandBuffer.EffectCommands[0];
            Assert.AreEqual(BattleEffectType.SpawnProjectileEmitter, effect.Type);
            Assert.AreEqual(ProjectileEmitterAnchorMode.FixedPosition, effect.ProjectileEmitter.AnchorMode);
            Assert.AreEqual(1, world.CommandBuffer.DestroyEntityCommands.Count);
        }

        [Test]
        public void Run_SpawnProjectileEmitterImpactUsesHitPositionAsFixedOrigin()
        {
            var nestedPayload = new ProjectilePayload(
                ProjectileBehavior.Linear,
                ProjectileHitPolicy.DestroyOnFirstHit,
                radius: 0.1f,
                speed: 1f,
                lifetimeTicks: 2,
                impactEffects: new[] { BattleEffectDefinition.Damage(1) });
            var nestedEmitter = new ProjectileEmitterSpawnData(
                ProjectileEmitterAnchorMode.FixedPosition,
                new BattleVector2(0.25f, 0.5f),
                durationTicks: 1,
                fireIntervalTicks: 1,
                ProjectilePattern.Single(new BattleVector2(1f, 0f)),
                nestedPayload);
            BattleWorld world = CreateWorldWithProjectile(
                new BattleTick(1),
                out _,
                out _,
                impactEffects: new[] { BattleEffectDefinition.SpawnProjectileEmitter(nestedEmitter) });
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();

            ProjectileSystem.Run(world, new CircleProjectileCollisionDetector(), events, sequence, new BattleTick(1));
            world.FlushEffectCommands(events, sequence, new BattleTick(1));
            ProjectileEmitterSystem.Run(world, 1, new BattleTick(2));

            Assert.AreEqual(1, world.CommandBuffer.SpawnProjectileCommands.Count);
            SpawnProjectileCommand command = world.CommandBuffer.SpawnProjectileCommands[0];
            Assert.AreEqual(new BattleVector2(0.5f, 0.5f), command.Position);
        }

        [Test]
        public void Run_FinalLifetimeProjectileCanHitBeforeExpiring()
        {
            BattleWorld world = CreateWorldWithProjectile(new BattleTick(1), out _, out _, lifetimeTicks: 1);
            var events = new EventBuffer<BattleEvent>();

            ProjectileSystem.Run(world, new CircleProjectileCollisionDetector(), events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(1, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(BattleEffectType.Damage, world.CommandBuffer.EffectCommands[0].Type);
            Assert.AreEqual(1, CountEvents(events.AsStream(), BattleEventType.ProjectileDestroyed, new ProjectileId(1)));
        }

        [Test]
        public void Run_ProjectileBeyondCullingBoundsQueuesDestroyAndDestroyedEvent()
        {
            var world = new BattleWorld();
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();
            Spawn(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            Spawn(world, new UnitId(2), new TeamId(2), new BattleVector2(100f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            var payload = new ProjectilePayload(
                ProjectileBehavior.Linear,
                ProjectileHitPolicy.DestroyOnFirstHit,
                radius: 0.1f,
                speed: 3f,
                lifetimeTicks: 10,
                impactEffects: new[] { BattleEffectDefinition.Damage(4) });
            world.CommandBuffer.SpawnProjectile(new SpawnProjectileCommand(source, new TeamId(1), BattleVector2.Zero, new BattleVector2(3f, 0f), payload, new BattleTick(1)));
            world.FlushSpawnProjectileCommands(events, sequence, new BattleTick(0));
            events.Clear();

            ProjectileSystem.Run(
                world,
                new CircleProjectileCollisionDetector(),
                events,
                sequence,
                new BattleTick(1),
                new ProjectileCullingBounds(BattleVector2.Zero, new BattleVector2(2f, 2f), padding: 0.5f));

            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(1, world.CommandBuffer.DestroyEntityCommands.Count);
            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(BattleEventType.ProjectileMoved, stream[0].Type);
            Assert.AreEqual(BattleEventType.ProjectileDestroyed, stream[1].Type);
            Assert.AreEqual(new ProjectileId(1), stream[1].ProjectileId);
        }

        [Test]
        public void Run_DoesNotDestroyProjectileTwiceBeforeStructuralApply()
        {
            BattleWorld world = CreateWorldWithProjectile(new BattleTick(1), out _, out _);
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();

            ProjectileSystem.Run(world, new ReverseProjectileCollisionDetector(), events, sequence, new BattleTick(1));
            ProjectileSystem.Run(world, new ReverseProjectileCollisionDetector(), events, sequence, new BattleTick(1));

            Assert.AreEqual(1, world.CommandBuffer.DestroyEntityCommands.Count);
            Assert.AreEqual(1, CountEvents(events.AsStream(), BattleEventType.ProjectileDestroyed, new ProjectileId(1)));
        }

        [Test]
        public void Run_MultipleProjectilesHittingSameTargetAreOrderedByProjectileId()
        {
            BattleWorld world = CreateWorldWithProjectiles(new BattleTick(1), projectileCount: 2);
            var events = new EventBuffer<BattleEvent>();

            ProjectileSystem.Run(world, new ReverseProjectileCollisionDetector(), events, new EventSequence(), new BattleTick(1));

            EventStream<BattleEvent> stream = events.AsStream();
            Assert.AreEqual(new ProjectileId(1), stream[2].ProjectileId);
            Assert.AreEqual(BattleEventType.ProjectileHit, stream[2].Type);
            Assert.AreEqual(new ProjectileId(1), stream[3].ProjectileId);
            Assert.AreEqual(BattleEventType.ProjectileDestroyed, stream[3].Type);
            Assert.AreEqual(new ProjectileId(2), stream[4].ProjectileId);
            Assert.AreEqual(BattleEventType.ProjectileHit, stream[4].Type);
            Assert.AreEqual(new ProjectileId(2), stream[5].ProjectileId);
            Assert.AreEqual(BattleEventType.ProjectileDestroyed, stream[5].Type);
        }

        [Test]
        public void Run_IgnoresDetectorHitAgainstSameTeamTarget()
        {
            BattleWorld world = CreateWorldWithProjectile(new BattleTick(1), out _, out _);
            Spawn(world, new UnitId(3), new TeamId(1), new BattleVector2(1f, 0f));
            world.TryFindEntity(new UnitId(3), out EntityId friendlyTarget);
            var events = new EventBuffer<BattleEvent>();

            ProjectileSystem.Run(world, new FixedProjectileCollisionDetector(new UnitId(3), friendlyTarget), events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.DestroyEntityCommands.Count);
            Assert.AreEqual(0, CountEvents(events.AsStream(), BattleEventType.ProjectileHit, new ProjectileId(1)));
            Assert.AreEqual(0, CountEvents(events.AsStream(), BattleEventType.ProjectileDestroyed, new ProjectileId(1)));
        }

        [Test]
        public void Run_IgnoresDetectorHitWhenTargetUnitIdDoesNotMatchTargetEntity()
        {
            BattleWorld world = CreateWorldWithProjectile(new BattleTick(1), out _, out _);
            Spawn(world, new UnitId(3), new TeamId(2), new BattleVector2(1f, 0f));
            world.TryFindEntity(new UnitId(3), out EntityId otherEnemyTarget);
            var events = new EventBuffer<BattleEvent>();

            ProjectileSystem.Run(world, new FixedProjectileCollisionDetector(new UnitId(2), otherEnemyTarget), events, new EventSequence(), new BattleTick(1));

            Assert.AreEqual(0, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(0, world.CommandBuffer.DestroyEntityCommands.Count);
            Assert.AreEqual(0, CountEvents(events.AsStream(), BattleEventType.ProjectileDestroyed, new ProjectileId(1)));
        }

        [Test]
        public void Run_InvalidDetectorHitDoesNotConsumeProjectileBeforeValidHit()
        {
            BattleWorld world = CreateWorldWithProjectile(new BattleTick(1), out _, out EntityId enemyTarget);
            world.TryFindEntity(new UnitId(1), out EntityId sourceTarget);
            var events = new EventBuffer<BattleEvent>();

            ProjectileSystem.Run(
                world,
                new OrderedProjectileCollisionDetector(
                    new[] { new UnitId(0), new UnitId(2) },
                    new[] { sourceTarget, enemyTarget }),
                events,
                new EventSequence(),
                new BattleTick(1));

            Assert.AreEqual(1, world.CommandBuffer.EffectCommands.Count);
            Assert.AreEqual(1, world.CommandBuffer.DestroyEntityCommands.Count);
            Assert.AreEqual(1, CountEvents(events.AsStream(), BattleEventType.ProjectileDestroyed, new ProjectileId(1)));
        }

        [Test]
        public void Run_ContinuousCollisionConsumesEarliestTargetInsteadOfLowestUnitId()
        {
            var world = new BattleWorld();
            var spawnEvents = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();
            Spawn(world, new UnitId(1), new TeamId(1), new BattleVector2(-2f, 0f));
            Spawn(world, new UnitId(2), new TeamId(2), new BattleVector2(2f, 0f));
            Spawn(world, new UnitId(3), new TeamId(2), BattleVector2.Zero);
            world.TryFindEntity(new UnitId(1), out EntityId source);
            var payload = new ProjectilePayload(
                ProjectileBehavior.Linear,
                ProjectileHitPolicy.DestroyOnFirstHit,
                radius: 0.5f,
                speed: 6f,
                lifetimeTicks: 3,
                impactEffects: new[] { BattleEffectDefinition.Damage(4) });
            world.CommandBuffer.SpawnProjectile(new SpawnProjectileCommand(
                source,
                new TeamId(1),
                new BattleVector2(-2f, 0f),
                new BattleVector2(6f, 0f),
                payload,
                new BattleTick(1)));
            world.FlushSpawnProjectileCommands(spawnEvents, sequence, new BattleTick(0));
            var events = new EventBuffer<BattleEvent>();

            ProjectileSystem.Run(
                world,
                new CircleProjectileCollisionDetector(),
                events,
                sequence,
                new BattleTick(1));

            BattleEvent hit = FindEvent(events.AsStream(), BattleEventType.ProjectileHit);
            Assert.That(hit.TargetUnitId, Is.EqualTo(new UnitId(3)));
            Assert.That(hit.Position.X, Is.EqualTo(-0.75f).Within(0.001f));
            Assert.That(hit.Position.Y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Run_V2ScaleWarmPathDoesNotAllocateAcrossTenTicks()
        {
            const int projectileCount = 512;
            const int unitCount = 128;
            var world = new BattleWorld();
            var spawnEvents = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();
            for (var i = 0; i < unitCount; i++)
            {
                Spawn(
                    world,
                    new UnitId(i + 1),
                    new TeamId(i == 0 ? 1 : 2),
                    new BattleVector2(500f + ((i % 16) * 2f), 500f + ((i / 16) * 2f)));
            }

            Assert.That(world.TryFindEntity(new UnitId(1), out EntityId source), Is.True);
            var payload = new ProjectilePayload(
                ProjectileBehavior.Linear,
                ProjectileHitPolicy.DestroyOnFirstHit,
                radius: 0.1f,
                speed: 1f,
                lifetimeTicks: 100,
                impactEffects: new[] { BattleEffectDefinition.Damage(1) });
            for (var i = 0; i < projectileCount; i++)
            {
                world.CommandBuffer.SpawnProjectile(new SpawnProjectileCommand(
                    source,
                    new TeamId(1),
                    new BattleVector2(-500f + ((i % 32) * 2f), -500f + ((i / 32) * 2f)),
                    BattleVector2.Right,
                    payload,
                    new BattleTick(1)));
            }

            world.FlushSpawnProjectileCommands(spawnEvents, sequence, new BattleTick(0));
            var events = new EventBuffer<BattleEvent>();
            var detector = new CircleProjectileCollisionDetector();
            var scratch = new ProjectileSystem.Scratch();
            for (var tickValue = 1; tickValue <= 2; tickValue++)
            {
                events.Clear();
                ProjectileSystem.Run(
                    world,
                    detector,
                    events,
                    sequence,
                    new BattleTick(tickValue),
                    scratch);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var tickValue = 3; tickValue <= 12; tickValue++)
            {
                events.Clear();
                ProjectileSystem.Run(
                    world,
                    detector,
                    events,
                    sequence,
                    new BattleTick(tickValue),
                    scratch);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(world.ProjectileComponents.Entities, Has.Count.EqualTo(projectileCount));
            Assert.That(events.Count, Is.EqualTo(projectileCount));
            Assert.That(allocatedBytes, Is.Zero);
        }

        private static BattleWorld CreateWorldWithProjectile(
            BattleTick activateOnTick,
            out EntityId projectileEntity,
            out EntityId target,
            int lifetimeTicks = 3,
            BattleEffectDefinition[] impactEffects = null)
        {
            BattleWorld world = CreateWorldWithProjectiles(activateOnTick, projectileCount: 1, lifetimeTicks, impactEffects);
            world.TryFindEntity(new UnitId(2), out target);
            projectileEntity = world.ProjectileComponents.Entities[0];
            return world;
        }

        private static BattleWorld CreateProjectilePathWorld(
            ProjectileHitPolicy hitPolicy,
            BattleVector2 startPosition,
            BattleVector2 velocity,
            out EntityId projectileEntity,
            params BattleVector2[] targetPositions)
        {
            var world = new BattleWorld();
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();
            Spawn(world, new UnitId(1), new TeamId(1), startPosition);
            for (var i = 0; i < targetPositions.Length; i++)
            {
                Spawn(
                    world,
                    new UnitId(i + 2),
                    new TeamId(2),
                    targetPositions[i]);
            }

            world.TryFindEntity(new UnitId(1), out EntityId source);
            var payload = new ProjectilePayload(
                ProjectileBehavior.Linear,
                hitPolicy,
                radius: 0.1f,
                speed: 6f,
                lifetimeTicks: 5,
                impactEffects: new[] { BattleEffectDefinition.Damage(1) });
            world.CommandBuffer.SpawnProjectile(new SpawnProjectileCommand(
                source,
                new TeamId(1),
                startPosition,
                velocity,
                payload,
                new BattleTick(1)));
            world.FlushSpawnProjectileCommands(events, sequence, new BattleTick(0));
            projectileEntity = world.ProjectileComponents.Entities[0];
            return world;
        }

        private static BattleWorld CreateWorldWithProjectiles(
            BattleTick activateOnTick,
            int projectileCount,
            int lifetimeTicks = 3,
            BattleEffectDefinition[] impactEffects = null)
        {
            var world = new BattleWorld();
            var events = new EventBuffer<BattleEvent>();
            var sequence = new EventSequence();
            Spawn(world, new UnitId(1), new TeamId(1), new BattleVector2(0f, 0f));
            Spawn(world, new UnitId(2), new TeamId(2), new BattleVector2(1f, 0f));
            world.TryFindEntity(new UnitId(1), out EntityId source);
            var payload = new ProjectilePayload(
                ProjectileBehavior.Linear,
                ProjectileHitPolicy.DestroyOnFirstHit,
                radius: 0.5f,
                speed: 1f,
                lifetimeTicks,
                impactEffects ?? new[] { BattleEffectDefinition.Damage(4) });
            for (var i = 0; i < projectileCount; i++)
            {
                world.CommandBuffer.SpawnProjectile(new SpawnProjectileCommand(source, new TeamId(1), new BattleVector2(0f, 0f), new BattleVector2(1f, 0f), payload, activateOnTick));
            }

            world.FlushSpawnProjectileCommands(events, sequence, new BattleTick(0));
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

        private static int CountEvents(EventStream<BattleEvent> stream, BattleEventType type, ProjectileId projectileId)
        {
            var count = 0;
            for (var i = 0; i < stream.Count; i++)
            {
                if (stream[i].Type == type && stream[i].ProjectileId == projectileId)
                {
                    count++;
                }
            }

            return count;
        }

        private static BattleEvent FindEvent(EventStream<BattleEvent> stream, BattleEventType type)
        {
            for (var i = 0; i < stream.Count; i++)
            {
                if (stream[i].Type == type)
                {
                    return stream[i];
                }
            }

            Assert.Fail($"Missing event: {type}.");
            return default;
        }

        private sealed class ReverseProjectileCollisionDetector : IProjectileCollisionDetector
        {
            public void CollectHits(ProjectileCollisionFrame frame, IList<ProjectileHit> hits)
            {
                for (var i = frame.Projectiles.Count - 1; i >= 0; i--)
                {
                    ProjectileCollisionSnapshot projectile = frame.Projectiles[i];
                    ProjectileTargetSnapshot target = FindEnemyTarget(frame, projectile.TeamId);
                    hits.Add(new ProjectileHit(
                        projectile.ProjectileId,
                        projectile.Entity,
                        target.UnitId,
                        target.Entity,
                        projectile.EndPosition,
                        BattleScalar.Zero));
                }
            }

            private static ProjectileTargetSnapshot FindEnemyTarget(ProjectileCollisionFrame frame, TeamId projectileTeamId)
            {
                for (var i = 0; i < frame.Targets.Count; i++)
                {
                    ProjectileTargetSnapshot target = frame.Targets[i];
                    if (!target.TeamId.Equals(projectileTeamId))
                    {
                        return target;
                    }
                }

                return default;
            }
        }

        private sealed class FixedProjectileCollisionDetector : IProjectileCollisionDetector
        {
            private readonly UnitId _targetUnitId;
            private readonly EntityId _targetEntity;

            public FixedProjectileCollisionDetector(UnitId targetUnitId, EntityId targetEntity)
            {
                _targetUnitId = targetUnitId;
                _targetEntity = targetEntity;
            }

            public void CollectHits(ProjectileCollisionFrame frame, IList<ProjectileHit> hits)
            {
                ProjectileCollisionSnapshot projectile = frame.Projectiles[0];
                hits.Add(new ProjectileHit(
                    projectile.ProjectileId,
                    projectile.Entity,
                    _targetUnitId,
                    _targetEntity,
                    projectile.EndPosition,
                    BattleScalar.Zero));
            }
        }

        private sealed class OrderedProjectileCollisionDetector : IProjectileCollisionDetector
        {
            private readonly UnitId[] _targetUnitIds;
            private readonly EntityId[] _targetEntities;

            public OrderedProjectileCollisionDetector(UnitId[] targetUnitIds, EntityId[] targetEntities)
            {
                _targetUnitIds = targetUnitIds;
                _targetEntities = targetEntities;
            }

            public void CollectHits(ProjectileCollisionFrame frame, IList<ProjectileHit> hits)
            {
                ProjectileCollisionSnapshot projectile = frame.Projectiles[0];
                for (var i = 0; i < _targetUnitIds.Length; i++)
                {
                    hits.Add(new ProjectileHit(
                        projectile.ProjectileId,
                        projectile.Entity,
                        _targetUnitIds[i],
                        _targetEntities[i],
                        projectile.EndPosition,
                        BattleScalar.Zero));
                }
            }
        }

        private sealed class ThrowingProjectileCollisionDetector : IProjectileCollisionDetector
        {
            public void CollectHits(ProjectileCollisionFrame frame, IList<ProjectileHit> hits)
            {
                Assert.Fail("Collision detector should not be invoked when there are no collectable projectiles.");
            }
        }
    }
}
