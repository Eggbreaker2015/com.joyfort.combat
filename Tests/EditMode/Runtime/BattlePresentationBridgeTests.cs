using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using Combat.Foundation.Events;
using Combat.Runtime.Display;
using Combat.Runtime.Runner;
using Combat.Tests.Core;
using NUnit.Framework;

namespace Combat.Tests.Runtime
{
    public sealed class BattlePresentationBridgeTests
    {
        [Test]
        public void Create_WithTypedNullDependenciesRejectsLikeDispatcher()
        {
            Assert.Throws<ArgumentNullException>(() => new BattlePresentationBridge((ICombatViewPort)null));
            Assert.Throws<ArgumentNullException>(() => new BattlePresentationBridge((IVisualCommandSink)null));
        }

        [Test]
        public void Consume_MapsInitialEventsInOrderWithoutAdvancingSimulation()
        {
            var sink = new RecordingVisualCommandSink();
            var bridge = new BattlePresentationBridge(sink);
            var battle = new BattleInstance(TestConfig());
            BattleTick before = battle.Simulation.CurrentTick;

            bridge.Consume(battle.InitialOutput.Events);

            Assert.AreEqual(before, battle.Simulation.CurrentTick);
            Assert.AreEqual(2, sink.Commands.Count);
            Assert.AreEqual(VisualCommandType.CreateUnit, sink.Commands[0].Type);
            Assert.AreEqual(new UnitId(1), sink.Commands[0].UnitId);
            Assert.AreEqual(VisualCommandType.CreateUnit, sink.Commands[1].Type);
            Assert.AreEqual(new UnitId(2), sink.Commands[1].UnitId);
        }

        [Test]
        public void Consume_DefaultAndEmptyStreamsDispatchNothing()
        {
            var sink = new RecordingVisualCommandSink();
            var bridge = new BattlePresentationBridge(sink);
            var empty = new EventBuffer<BattleEvent>();

            Assert.DoesNotThrow(() => bridge.Consume(default(EventStream<BattleEvent>)));
            Assert.DoesNotThrow(() => bridge.Consume(empty.AsStream()));
            Assert.AreEqual(0, sink.Commands.Count);
        }

        [Test]
        public void Consume_OwnedEventListAndSingleEventPreserveOrder()
        {
            var sink = new RecordingVisualCommandSink();
            var bridge = new BattlePresentationBridge(sink);
            var events = new[]
            {
                BattleEvent.UnitMoved(1, new BattleTick(2), new UnitId(7), new TeamId(1), new BattleVector2(1f, 2f)),
                BattleEvent.UnitMoved(2, new BattleTick(2), new UnitId(8), new TeamId(1), new BattleVector2(3f, 4f))
            };

            bridge.Consume((IReadOnlyList<BattleEvent>)events);
            bridge.Consume(BattleEvent.UnitMoved(
                3, new BattleTick(2), new UnitId(9), new TeamId(1), new BattleVector2(5f, 6f)));

            Assert.That(sink.Commands, Has.Count.EqualTo(3));
            Assert.That(sink.Commands[0].UnitId, Is.EqualTo(new UnitId(7)));
            Assert.That(sink.Commands[1].UnitId, Is.EqualTo(new UnitId(8)));
            Assert.That(sink.Commands[2].UnitId, Is.EqualTo(new UnitId(9)));
        }

        [Test]
        public void Consume_NullOwnedEventListIsRejected()
        {
            var bridge = new BattlePresentationBridge(new RecordingVisualCommandSink());

            Assert.Throws<ArgumentNullException>(() =>
                bridge.Consume((IReadOnlyList<BattleEvent>)null));
        }

        [Test]
        public void Consume_WithTimelineSinkPreservesSchedulingSemantics()
        {
            var viewport = new RecordingCombatViewPort();
            var runner = new VisualTimelineRunner(
                viewport,
                new VisualTimeline(),
                new VisualTimelinePolicy(new VisualTimelineSettings(
                    projectileDestroyDelaySeconds: 0.12f,
                    unitDestroyDelaySeconds: 0.35f,
                    battleResultDelaySeconds: 0.45f)));
            var bridge = new BattlePresentationBridge(runner);
            var events = new EventBuffer<BattleEvent>();
            events.Write(BattleEvent.ProjectileHit(
                1,
                new BattleTick(3),
                new ProjectileId(8),
                new UnitId(1),
                new UnitId(2),
                new BattleVector2(4f, 5f)));
            events.Write(BattleEvent.ProjectileDestroyed(
                2,
                new BattleTick(3),
                new ProjectileId(8)));

            bridge.Consume(events.AsStream());

            Assert.AreEqual(0, viewport.Commands.Count);
            Assert.AreEqual(2, runner.PendingCount);
            runner.Advance(0.01f);
            Assert.AreEqual(VisualCommandType.PlayProjectileHit, viewport.Commands[0].Type);
            runner.Advance(0.11f);
            Assert.AreEqual(VisualCommandType.DestroyProjectile, viewport.Commands[1].Type);
        }

        private static BattleConfig TestConfig()
        {
            CombatantDefinition attacker = TestCombatants.Create(
                "attacker",
                maxHealth: 10,
                moveSpeed: 0f,
                attackRange: 2f,
                attackDamage: 10,
                attackCooldownTicks: 2);
            CombatantDefinition defender = TestCombatants.Create(
                "defender",
                maxHealth: 10,
                moveSpeed: 0f,
                attackRange: 2f,
                attackDamage: 1,
                attackCooldownTicks: 2);
            return new BattleConfig(10, 100, new[]
            {
                new InitialCombatantSpawn(new TeamId(1), attacker, BattleVector2.Zero),
                new InitialCombatantSpawn(new TeamId(2), defender, new BattleVector2(1f, 0f))
            });
        }

        private sealed class RecordingVisualCommandSink : IVisualCommandSink
        {
            public readonly List<VisualCommand> Commands = new List<VisualCommand>();

            public void Dispatch(VisualCommand command)
            {
                Commands.Add(command);
            }
        }
    }
}
