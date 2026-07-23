using System;
using System.Linq;
using System.Reflection;
using Combat.Core.Battle;
using Combat.Foundation.Diagnostics;
using Combat.Foundation.Events;
using Combat.Runtime.Runner;
using Combat.Tests.Core;
using NUnit.Framework;

namespace Combat.Tests.Runtime
{
    public sealed class BattleInstanceTests
    {
        [Test]
        public void Runtime_ExposesNarrowInitialPresentationCompositionToken()
        {
            Assert.That(typeof(BattleInstance).GetMethod("CreateForPresentation"), Is.Not.Null);
            Assert.That(typeof(BattleInstance).Assembly.GetType(
                "Combat.Runtime.Runner.BattleInitialPresentationComposition"), Is.Not.Null);
        }

        [Test]
        public void PresentationComposition_CommitsInitialDiagnosticsOnceAfterCallerConsumesFacts()
        {
            var sink = new CapturingCombatLogSink();
            var composition = BattleInstance.CreateForPresentation(
                TestConfig(),
                new CombatLogger(CombatLogSettings.ShowInfoAndAbove, sink));

            Assert.That(composition.InitialOutput.Events, Has.Count.EqualTo(2));
            Assert.That(sink.Messages, Is.Empty);
            BattleInstance instance = composition.CompletePresentation();

            Assert.That(instance, Is.Not.Null);
            Assert.That(sink.Messages, Has.Count.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => composition.CompletePresentation());
        }

        [Test]
        public void PresentationComposition_DoesNotRevealInstanceWhenDiagnosticsCommitFails()
        {
            var composition = BattleInstance.CreateForPresentation(
                TestConfig(),
                new CombatLogger(
                    CombatLogSettings.ShowInfoAndAbove,
                    new AlwaysThrowingCombatLogSink()));

            Assert.Throws<InvalidOperationException>(() => composition.CompletePresentation());
            Assert.Throws<InvalidOperationException>(() => composition.CompletePresentation());
        }

        [Test]
        public void Create_CapturesInitialSpawnEventsWithoutPresentationDependency()
        {
            var battle = new BattleInstance(TestConfig());

            Assert.AreEqual(new BattleTick(0), battle.InitialOutput.Tick);
            Assert.AreEqual(2, battle.InitialOutput.Events.Count);
            Assert.AreEqual(BattleEventType.UnitSpawned, battle.InitialOutput.Events[0].Type);
            Assert.AreEqual(BattleEventType.UnitSpawned, battle.InitialOutput.Events[1].Type);
            Assert.IsFalse(battle.InitialOutput.Result.IsFinished);
            Assert.IsFalse(battle.Result.IsFinished);
        }

        [Test]
        public void Create_WithNullLogger_UsesDisabledLogging()
        {
            var battle = new BattleInstance(TestConfig(), null);

            Assert.DoesNotThrow(() => battle.TickOnce(BattleInputFrame.Empty));
            Assert.AreEqual(new BattleTick(1), battle.Simulation.CurrentTick);
        }

        [Test]
        public void TickOnce_AdvancesExactlyOneTickAndReturnsCurrentEvents()
        {
            var battle = new BattleInstance(TestConfig());

            BattleStepOutput output = battle.TickOnce(BattleInputFrame.Empty);

            Assert.AreEqual(1, battle.Simulation.CurrentTick.Value);
            Assert.AreEqual(battle.Simulation.CurrentTick, output.Tick);
            Assert.AreEqual(battle.Simulation.Events.Count, output.Events.Count);
            for (var i = 0; i < output.Events.Count; i++)
            {
                Assert.AreEqual(battle.Simulation.Events[i], output.Events[i]);
            }

            Assert.AreEqual(battle.Result, output.Result);
        }

        [Test]
        public void ApplyStatus_ReturnsStatusEventWithoutDispatchingPresentation()
        {
            var battle = new BattleInstance(SingleUnitConfig());

            bool applied = battle.ApplyStatus(
                new UnitId(1),
                new UnitId(1),
                TestStatus(),
                out BattleStepOutput output);

            Assert.IsTrue(applied);
            Assert.AreEqual(new BattleTick(0), output.Tick);
            Assert.AreEqual(1, output.Events.Count);
            Assert.AreEqual(BattleEventType.StatusApplied, output.Events[0].Type);
            Assert.AreEqual("fortify", output.Events[0].StatusId);
            Assert.AreEqual(battle.Result, output.Result);
        }

        [Test]
        public void ApplyStatus_WhenTargetDoesNotExist_ReturnsEmptyCurrentOutput()
        {
            var battle = new BattleInstance(SingleUnitConfig());

            bool applied = battle.ApplyStatus(
                new UnitId(1),
                new UnitId(99),
                TestStatus(),
                out BattleStepOutput output);

            Assert.IsFalse(applied);
            Assert.AreEqual(battle.Simulation.CurrentTick, output.Tick);
            Assert.AreEqual(0, output.Events.Count);
            Assert.AreEqual(battle.Result, output.Result);
        }

        [Test]
        public void ApplyStatus_DirectCallerLogsCapturedOutputExactlyOnce()
        {
            var sink = new CapturingCombatLogSink();
            var logger = new CombatLogger(CombatLogSettings.ShowInfoAndAbove, sink);
            var battle = new BattleInstance(SingleUnitConfig(), logger);
            Assert.AreEqual(1, sink.Messages.Count);
            sink.Clear();

            Assert.IsTrue(battle.ApplyStatus(
                new UnitId(1),
                new UnitId(1),
                TestStatus(),
                out BattleStepOutput output));

            Assert.AreEqual(1, output.Events.Count);
            Assert.AreEqual(1, sink.Messages.Count);
            Assert.That(sink.Messages[0], Does.Contain("applied status"));
        }

        [Test]
        public void EventOutputs_AreSynchronousViewsOfSimulationBuffer()
        {
            var battle = new BattleInstance(TestConfig());
            EventStream<BattleEvent> initialEvents = battle.InitialOutput.Events;
            Assert.AreEqual(BattleEventType.UnitSpawned, initialEvents[0].Type);

            BattleStepOutput next = battle.TickOnce(BattleInputFrame.Empty);

            Assert.AreEqual(next.Events.Count, initialEvents.Count);
            for (var i = 0; i < next.Events.Count; i++)
            {
                Assert.AreEqual(next.Events[i], initialEvents[i]);
            }
        }

        [Test]
        public void TickOnce_RecordsBattleResultAndLogsCurrentEvents()
        {
            var sink = new CapturingCombatLogSink();
            var logger = new CombatLogger(CombatLogSettings.ShowInfoAndAbove, sink);
            var battle = new BattleInstance(TestConfig(), logger);

            Assert.That(sink.Messages, Has.Some.Contains("Tick 0").And.Contains("spawned"));
            sink.Clear();

            battle.TickOnce(BattleInputFrame.Empty);
            BattleStepOutput output = battle.TickOnce(BattleInputFrame.Empty);

            Assert.IsTrue(output.Result.IsFinished);
            Assert.AreEqual(new TeamId(1), output.Result.WinningTeamId);
            Assert.AreEqual(output.Result, battle.Result);
            Assert.That(sink.Messages, Has.Some.Contains("Tick 2").And.Contains("Battle ended").And.Contains("Team 1"));
        }

        [Test]
        public void TickOnce_WhenTerminalEventLoggingThrows_PersistsBattleResultBeforePropagating()
        {
            var logger = new CombatLogger(
                CombatLogSettings.ShowInfoAndAbove,
                new ThrowingOnBattleEndedLogSink());
            var battle = new BattleInstance(TestConfig(), logger);

            battle.TickOnce(BattleInputFrame.Empty);

            Assert.Throws<InvalidOperationException>(() =>
                battle.TickOnce(BattleInputFrame.Empty));
            Assert.IsTrue(battle.Simulation.IsFinished);
            Assert.IsTrue(battle.Result.IsFinished);
            Assert.AreEqual(new TeamId(1), battle.Result.WinningTeamId);
        }

        [Test]
        public void PublicSurface_DoesNotExposePresentationOrUnityTypes()
        {
            Type[] publicTypes =
            {
                typeof(BattleInstance),
                typeof(BattleStepOutput),
                typeof(BattleInitialPresentationComposition)
            };
            string[] forbiddenFragments =
            {
                "Combat.Runtime.Display",
                "VisualCommand",
                "ICombatViewPort",
                "UnityEngine"
            };

            string[] exposedTypeNames = publicTypes
                .SelectMany(GetExposedTypes)
                .Select(type => type.FullName ?? type.Name)
                .ToArray();

            Assert.That(exposedTypeNames, Has.None.Matches<string>(name =>
                forbiddenFragments.Any(fragment => name.Contains(fragment))));
        }

        private static Type[] GetExposedTypes(Type type)
        {
            return type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .SelectMany(member =>
                {
                    if (member is MethodInfo method)
                    {
                        return new[] { method.ReturnType }.Concat(method.GetParameters().Select(parameter => parameter.ParameterType));
                    }

                    if (member is PropertyInfo property)
                    {
                        return new[] { property.PropertyType };
                    }

                    if (member is FieldInfo field)
                    {
                        return new[] { field.FieldType };
                    }

                    return Array.Empty<Type>();
                })
                .ToArray();
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

        private static BattleConfig SingleUnitConfig()
        {
            CombatantDefinition unit = TestCombatants.Create(
                "unit",
                maxHealth: 10,
                moveSpeed: 0f,
                attackRange: 1f,
                attackDamage: 0,
                attackCooldownTicks: 2);
            return new BattleConfig(10, 100, new[]
            {
                new InitialCombatantSpawn(new TeamId(1), unit, BattleVector2.Zero)
            });
        }

        private static StatusDefinition TestStatus()
        {
            return new StatusDefinition(
                "fortify",
                StatusPolarity.Buff,
                durationTicks: 10,
                tickIntervalTicks: 10,
                periodicDamage: 0,
                modifiers: Array.Empty<BattleModifierDefinition>(),
                triggers: Array.Empty<BattleTriggerDefinition>());
        }

        private sealed class CapturingCombatLogSink : ICombatLogSink
        {
            private readonly System.Collections.Generic.List<string> _messages = new System.Collections.Generic.List<string>();

            public System.Collections.Generic.IReadOnlyList<string> Messages => _messages;

            public void Write(CombatLogEntry entry)
            {
                _messages.Add(entry.Message);
            }

            public void Clear()
            {
                _messages.Clear();
            }
        }

        private sealed class ThrowingOnBattleEndedLogSink : ICombatLogSink
        {
            public void Write(CombatLogEntry entry)
            {
                if (entry.Message.Contains("Battle ended"))
                {
                    throw new InvalidOperationException("Terminal log failure.");
                }
            }
        }

        private sealed class AlwaysThrowingCombatLogSink : ICombatLogSink
        {
            public void Write(CombatLogEntry entry)
            {
                throw new InvalidOperationException("Initial diagnostics failure.");
            }
        }
    }
}
