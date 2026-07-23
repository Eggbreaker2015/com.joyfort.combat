using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using Combat.Runtime.Display;
using Combat.Runtime.Runner;
using NUnit.Framework;

namespace Combat.Tests.Runtime
{
    public sealed class VisualTimelineTests
    {
        [Test]
        public void Runner_DelaysProjectileDestroyAfterProjectileHit()
        {
            var viewport = new RecordingCombatViewPort();
            var runner = new VisualTimelineRunner(
                viewport,
                new VisualTimeline(),
                new VisualTimelinePolicy(new VisualTimelineSettings(
                    projectileDestroyDelaySeconds: 0.12f,
                    unitDestroyDelaySeconds: 0.35f,
                    battleResultDelaySeconds: 0.45f)));

            runner.Dispatch(VisualCommand.PlayProjectileHit(new ProjectileHitViewSnapshot(
                new ProjectileId(3),
                new UnitId(1),
                new UnitId(2),
                new BattleVector2(4f, 5f))));
            runner.Dispatch(VisualCommand.DestroyProjectile(new ProjectileId(3)));

            runner.Advance(0.01f);

            AssertCommandTypes(viewport, VisualCommandType.PlayProjectileHit);
            Assert.AreEqual(new ProjectileId(3), viewport.Commands[0].ProjectileId);

            runner.Advance(0.10f);

            AssertCommandTypes(viewport, VisualCommandType.PlayProjectileHit);

            runner.Advance(0.01f);

            AssertCommandTypes(viewport, VisualCommandType.PlayProjectileHit, VisualCommandType.DestroyProjectile);
            Assert.AreEqual(new ProjectileId(3), viewport.Commands[1].ProjectileId);
        }

        [Test]
        public void Runner_DelaysUnitDestroyAfterHit()
        {
            var viewport = new RecordingCombatViewPort();
            var runner = new VisualTimelineRunner(
                viewport,
                new VisualTimeline(),
                new VisualTimelinePolicy(new VisualTimelineSettings(
                    projectileDestroyDelaySeconds: 0.12f,
                    unitDestroyDelaySeconds: 0.35f,
                    battleResultDelaySeconds: 0.45f)));

            runner.Dispatch(VisualCommand.PlayHit(new DamageViewSnapshot(
                new UnitId(1),
                new UnitId(2),
                8,
                BattleEffectSourceKind.BasicAbility,
                true,
                BattleEffectType.Damage,
                "basic-attack",
                null,
                default,
                Array.Empty<string>())));
            runner.Dispatch(VisualCommand.DestroyUnit(new UnitId(2)));

            runner.Advance(0.01f);

            AssertCommandTypes(viewport, VisualCommandType.PlayHit);
            Assert.AreEqual(new UnitId(2), viewport.Commands[0].TargetUnitId);

            runner.Advance(0.33f);

            AssertCommandTypes(viewport, VisualCommandType.PlayHit);

            runner.Advance(0.01f);

            AssertCommandTypes(viewport, VisualCommandType.PlayHit, VisualCommandType.DestroyUnit);
            Assert.AreEqual(new UnitId(2), viewport.Commands[1].UnitId);
        }

        [Test]
        public void Timeline_PreservesInsertionOrderForSameScheduledTime()
        {
            var viewport = new RecordingCombatViewPort();
            var timeline = new VisualTimeline();
            timeline.Enqueue(new VisualTimelineEntry(
                VisualCommand.MoveUnit(new UnitId(1), new BattleVector2(1f, 0f)),
                scheduledTimeSeconds: 0.1f,
                order: 2));
            timeline.Enqueue(new VisualTimelineEntry(
                VisualCommand.CreateUnit(new UnitSpawnViewSnapshot(
                    new UnitId(1),
                    new TeamId(1),
                    "fighter",
                    new BattleVector2(0f, 0f))),
                scheduledTimeSeconds: 0.1f,
                order: 1));
            timeline.Enqueue(new VisualTimelineEntry(
                VisualCommand.FaceUnit(new UnitId(1), BattleVector2.Right),
                scheduledTimeSeconds: 0.1f,
                order: 3));

            timeline.AdvanceTo(0.1f, viewport);

            AssertCommandTypes(
                viewport,
                VisualCommandType.CreateUnit,
                VisualCommandType.MoveUnit,
                VisualCommandType.FaceUnit);
        }

        [Test]
        public void Runner_FlushDispatchesRemainingCommandsInTimelineOrder()
        {
            var viewport = new RecordingCombatViewPort();
            var runner = new VisualTimelineRunner(viewport);

            runner.Dispatch(VisualCommand.DestroyUnit(new UnitId(2)));
            runner.Dispatch(VisualCommand.ShowBattleResult(new TeamId(1)));

            Assert.AreEqual(0, viewport.Commands.Count);

            runner.Flush();

            AssertCommandTypes(viewport, VisualCommandType.DestroyUnit, VisualCommandType.ShowBattleResult);
            Assert.AreEqual(new UnitId(2), viewport.Commands[0].UnitId);
            Assert.AreEqual(new TeamId(1), viewport.Commands[1].WinningTeamId);
        }

        [Test]
        public void Runner_DispatchRejectsInvalidCommandAtEntry()
        {
            var runner = new VisualTimelineRunner(new NullCombatViewPort());

            var exception = Assert.Throws<InvalidOperationException>(() => runner.Dispatch(default));

            Assert.That(exception.Message, Does.Contain("invalid"));
            Assert.AreEqual(0, runner.PendingCount);
        }

        [Test]
        public void Runner_EnqueueListRejectsInvalidCommandWithoutAddingIt()
        {
            var runner = new VisualTimelineRunner(new NullCombatViewPort());
            var commands = new[]
            {
                VisualCommand.MoveUnit(new UnitId(1), new BattleVector2(2f, 0f)),
                default
            };

            var exception = Assert.Throws<InvalidOperationException>(() => runner.Enqueue(commands));

            Assert.That(exception.Message, Does.Contain("invalid"));
            Assert.AreEqual(1, runner.PendingCount);
        }

        [Test]
        public void Runner_AdvanceZeroPumpsDueCommandsWithoutMovingCurrentTime()
        {
            var viewport = new RecordingCombatViewPort();
            var runner = new VisualTimelineRunner(viewport);

            runner.Dispatch(VisualCommand.MoveUnit(new UnitId(1), new BattleVector2(2f, 0f)));

            runner.Advance(0f);

            Assert.AreEqual(0f, runner.CurrentTimeSeconds);
            AssertCommandTypes(viewport, VisualCommandType.MoveUnit);
            Assert.AreEqual(0, runner.PendingCount);
        }

        [Test]
        public void PresentationScheduler_PlayActionStopsPendingLocomotionBeforeForwardingAction()
        {
            var sink = new RecordingVisualCommandSink();
            var scheduler = new VisualPresentationScheduler(sink);
            var source = new UnitId(1);
            var target = new UnitId(2);

            scheduler.Dispatch(VisualCommand.MoveUnit(source, new BattleVector2(1f, 0f)));
            scheduler.Dispatch(VisualCommand.PlayAction(new ActionViewSnapshot(
                source,
                target,
                "basic-attack",
                BattleEffectSourceKind.BasicAbility),
                BattleActionLocks.Movement | BattleActionLocks.StartAnotherAction));

            AssertCommandTypes(
                sink.Commands,
                VisualCommandType.MoveUnit,
                VisualCommandType.StopUnitMovement,
                VisualCommandType.PlayAction);
            Assert.AreEqual(source, sink.Commands[1].UnitId);
            Assert.AreEqual(source, sink.Commands[2].SourceUnitId);
        }

        [Test]
        public void PresentationScheduler_PlayActionWithoutMovementLockDoesNotStopPendingLocomotion()
        {
            var sink = new RecordingVisualCommandSink();
            var scheduler = new VisualPresentationScheduler(sink);
            var source = new UnitId(1);
            var target = new UnitId(2);

            scheduler.Dispatch(VisualCommand.MoveUnit(source, new BattleVector2(1f, 0f)));
            scheduler.Dispatch(VisualCommand.PlayAction(new ActionViewSnapshot(
                source,
                target,
                "moving-cast",
                BattleEffectSourceKind.Ability),
                BattleActionLocks.StartAnotherAction));

            AssertCommandTypes(
                sink.Commands,
                VisualCommandType.MoveUnit,
                VisualCommandType.PlayAction);
            Assert.AreEqual(BattleActionLocks.StartAnotherAction, sink.Commands[1].ActionLocks);
        }

        [Test]
        public void PresentationScheduler_DispatchRejectsInvalidCommandAtEntry()
        {
            var sink = new RecordingVisualCommandSink();
            var scheduler = new VisualPresentationScheduler(sink);

            var exception = Assert.Throws<InvalidOperationException>(() => scheduler.Dispatch(default));

            Assert.That(exception.Message, Does.Contain("invalid"));
            Assert.AreEqual(0, sink.Commands.Count);
            Assert.AreEqual(0, GetSchedulerChannelCount(scheduler));
        }

        [Test]
        public void PresentationScheduler_DiscardsLocomotionWhileActionMovementLockIsActive()
        {
            var sink = new RecordingVisualCommandSink();
            var scheduler = new VisualPresentationScheduler(sink);
            var source = new UnitId(1);
            var target = new UnitId(2);

            scheduler.Dispatch(VisualCommand.PlayAction(new ActionViewSnapshot(
                source,
                target,
                "basic-attack",
                BattleEffectSourceKind.BasicAbility),
                BattleActionLocks.Movement | BattleActionLocks.StartAnotherAction));
            scheduler.Dispatch(VisualCommand.MoveUnit(source, new BattleVector2(1f, 0f)));
            scheduler.Dispatch(VisualCommand.EndAction(source));
            scheduler.Dispatch(VisualCommand.MoveUnit(source, new BattleVector2(2f, 0f)));

            AssertCommandTypes(
                sink.Commands,
                VisualCommandType.StopUnitMovement,
                VisualCommandType.PlayAction,
                VisualCommandType.MoveUnit);
            Assert.AreEqual(new BattleVector2(2f, 0f), sink.Commands[2].Position);
        }

        [Test]
        public void Runner_ClampsNegativeDelaysToImmediateDispatch()
        {
            var viewport = new RecordingCombatViewPort();
            var runner = new VisualTimelineRunner(
                viewport,
                new VisualTimeline(),
                new VisualTimelinePolicy(new VisualTimelineSettings(
                    projectileDestroyDelaySeconds: -0.12f,
                    unitDestroyDelaySeconds: -0.35f,
                    battleResultDelaySeconds: -0.45f)));

            runner.Dispatch(VisualCommand.DestroyProjectile(new ProjectileId(7)));
            runner.Dispatch(VisualCommand.DestroyUnit(new UnitId(2)));
            runner.Dispatch(VisualCommand.ShowBattleResult(new TeamId(1)));

            runner.Advance(0f);

            AssertCommandTypes(
                viewport,
                VisualCommandType.DestroyProjectile,
                VisualCommandType.DestroyUnit,
                VisualCommandType.ShowBattleResult);
        }

        [Test]
        public void Runner_RejectsNegativeDelta()
        {
            var runner = new VisualTimelineRunner(new NullCombatViewPort());

            Assert.Throws<ArgumentOutOfRangeException>(() => runner.Advance(-0.01f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Runner_RejectsNonFiniteDelta(float deltaSeconds)
        {
            var runner = new VisualTimelineRunner(new NullCombatViewPort());

            Assert.Throws<ArgumentOutOfRangeException>(() => runner.Advance(deltaSeconds));
        }

        [Test]
        public void Settings_RejectNonFiniteDelays()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VisualTimelineSettings(float.NaN, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VisualTimelineSettings(0f, float.PositiveInfinity, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VisualTimelineSettings(0f, 0f, float.NegativeInfinity));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void TimelineEntry_RejectsNonFiniteScheduledTime(float scheduledTimeSeconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VisualTimelineEntry(
                VisualCommand.MoveUnit(new UnitId(1), new BattleVector2(1f, 0f)),
                scheduledTimeSeconds,
                order: 1));
        }

        [Test]
        public void Timeline_RejectsInvalidEntryCommandBeforeEnqueueSideEffects()
        {
            var timeline = new VisualTimeline();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => timeline.Enqueue(new VisualTimelineEntry(
                default,
                scheduledTimeSeconds: 0f,
                order: 1)));

            Assert.That(exception.Message, Does.Contain("invalid"));
            Assert.AreEqual(0, timeline.Count);
        }

        [Test]
        public void Timeline_AdvanceToLeavesReentrantCommandsPendingForNextPump()
        {
            var timeline = new VisualTimeline();
            timeline.Enqueue(new VisualTimelineEntry(
                VisualCommand.MoveUnit(new UnitId(1), new BattleVector2(1f, 0f)),
                scheduledTimeSeconds: 0f,
                order: 2));
            timeline.Enqueue(new VisualTimelineEntry(
                VisualCommand.FaceUnit(new UnitId(1), BattleVector2.Right),
                scheduledTimeSeconds: 0f,
                order: 3));
            var viewport = new ReentrantTimelineViewPort(timeline);

            timeline.AdvanceTo(0f, viewport);

            AssertCommandTypes(
                viewport.Commands,
                VisualCommandType.MoveUnit,
                VisualCommandType.FaceUnit);
            Assert.AreEqual(1, timeline.Count);

            timeline.AdvanceTo(0f, viewport);

            AssertCommandTypes(
                viewport.Commands,
                VisualCommandType.MoveUnit,
                VisualCommandType.FaceUnit,
                VisualCommandType.CreateUnit);
            Assert.AreEqual(0, timeline.Count);
        }

        [Test]
        public void Timeline_FlushLeavesReentrantCommandsPendingForNextFlush()
        {
            var timeline = new VisualTimeline();
            timeline.Enqueue(new VisualTimelineEntry(
                VisualCommand.MoveUnit(new UnitId(1), new BattleVector2(1f, 0f)),
                scheduledTimeSeconds: 0f,
                order: 2));
            timeline.Enqueue(new VisualTimelineEntry(
                VisualCommand.FaceUnit(new UnitId(1), BattleVector2.Right),
                scheduledTimeSeconds: 0f,
                order: 3));
            var viewport = new ReentrantTimelineViewPort(timeline);

            timeline.Flush(viewport);

            AssertCommandTypes(
                viewport.Commands,
                VisualCommandType.MoveUnit,
                VisualCommandType.FaceUnit);
            Assert.AreEqual(1, timeline.Count);

            timeline.Flush(viewport);

            AssertCommandTypes(
                viewport.Commands,
                VisualCommandType.MoveUnit,
                VisualCommandType.FaceUnit,
                VisualCommandType.CreateUnit);
            Assert.AreEqual(0, timeline.Count);
        }

        [Test]
        public void Timeline_RemovesDueCommandsBeforeDispatchWhenViewPortThrows()
        {
            var timeline = new VisualTimeline();
            timeline.Enqueue(new VisualTimelineEntry(
                VisualCommand.MoveUnit(new UnitId(1), new BattleVector2(1f, 0f)),
                scheduledTimeSeconds: 0f,
                order: 1));
            var viewport = new ThrowingCombatViewPort();

            Assert.Throws<InvalidOperationException>(() => timeline.AdvanceTo(0f, viewport));
            Assert.AreEqual(0, timeline.Count);
        }

        [Test]
        public void ImmediateSink_AppliesCommandsToRawViewPortSnapshots()
        {
            var viewport = new RawSpyCombatViewPort();
            var sink = new ImmediateVisualCommandSink(viewport);

            sink.Dispatch(VisualCommand.PlayHeal(new HealingViewSnapshot(
                new UnitId(1),
                new UnitId(2),
                6,
                BattleEffectSourceKind.Ability,
                true,
                BattleEffectType.Heal,
                "renew",
                "regen",
                new ProjectileId(9))));
            sink.Dispatch(VisualCommand.PlayStatusApplied(new StatusViewSnapshot(
                new UnitId(2),
                new UnitId(1),
                "burn",
                StatusPolarity.Debuff)));
            sink.Dispatch(VisualCommand.PlayStatusExpired(new StatusViewSnapshot(
                new UnitId(2),
                new UnitId(1),
                "burn",
                StatusPolarity.Debuff)));
            sink.Dispatch(VisualCommand.PlayProjectileHit(new ProjectileHitViewSnapshot(
                new ProjectileId(4),
                new UnitId(1),
                new UnitId(2),
                new BattleVector2(7f, 8f))));

            Assert.AreEqual(1, viewport.HealCount);
            Assert.AreEqual(new UnitId(1), viewport.HealSnapshot.SourceUnitId);
            Assert.AreEqual(new UnitId(2), viewport.HealSnapshot.TargetUnitId);
            Assert.AreEqual(6, viewport.HealSnapshot.Amount);
            Assert.AreEqual(BattleEffectSourceKind.Ability, viewport.HealSnapshot.SourceKind);
            Assert.IsTrue(viewport.HealSnapshot.HasEffectType);
            Assert.AreEqual(BattleEffectType.Heal, viewport.HealSnapshot.EffectType);
            Assert.AreEqual("renew", viewport.HealSnapshot.AbilityId);
            Assert.AreEqual("regen", viewport.HealSnapshot.StatusId);
            Assert.AreEqual(new ProjectileId(9), viewport.HealSnapshot.ProjectileId);

            Assert.AreEqual(1, viewport.StatusAppliedCount);
            Assert.AreEqual(new UnitId(2), viewport.StatusAppliedSnapshot.UnitId);
            Assert.AreEqual(new UnitId(1), viewport.StatusAppliedSnapshot.SourceUnitId);
            Assert.AreEqual("burn", viewport.StatusAppliedSnapshot.StatusId);
            Assert.AreEqual(StatusPolarity.Debuff, viewport.StatusAppliedSnapshot.Polarity);

            Assert.AreEqual(1, viewport.StatusExpiredCount);
            Assert.AreEqual(new UnitId(2), viewport.StatusExpiredSnapshot.UnitId);
            Assert.AreEqual(new UnitId(1), viewport.StatusExpiredSnapshot.SourceUnitId);
            Assert.AreEqual("burn", viewport.StatusExpiredSnapshot.StatusId);
            Assert.AreEqual(StatusPolarity.Debuff, viewport.StatusExpiredSnapshot.Polarity);

            Assert.AreEqual(1, viewport.ProjectileHitCount);
            Assert.AreEqual(new ProjectileId(4), viewport.ProjectileHitSnapshot.ProjectileId);
            Assert.AreEqual(new UnitId(1), viewport.ProjectileHitSnapshot.SourceUnitId);
            Assert.AreEqual(new UnitId(2), viewport.ProjectileHitSnapshot.TargetUnitId);
            Assert.AreEqual(new BattleVector2(7f, 8f), viewport.ProjectileHitSnapshot.Position);
        }

        private static void AssertCommandTypes(RecordingCombatViewPort viewport, params VisualCommandType[] expectedTypes)
        {
            AssertCommandTypes(viewport.Commands, expectedTypes);
        }

        private static void AssertCommandTypes(IReadOnlyList<VisualCommand> commands, params VisualCommandType[] expectedTypes)
        {
            Assert.AreEqual(expectedTypes.Length, commands.Count);
            for (var i = 0; i < expectedTypes.Length; i++)
            {
                Assert.AreEqual(expectedTypes[i], commands[i].Type);
            }
        }

        private static int GetSchedulerChannelCount(VisualPresentationScheduler scheduler)
        {
            var field = typeof(VisualPresentationScheduler).GetField(
                "_unitChannels",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var channels = (System.Collections.ICollection)field.GetValue(scheduler);
            return channels.Count;
        }

        private sealed class ReentrantTimelineViewPort : EmptyCombatViewPort
        {
            private readonly List<VisualCommand> _commands = new List<VisualCommand>();
            private readonly VisualTimeline _timeline;
            private bool _hasEnqueued;

            public ReentrantTimelineViewPort(VisualTimeline timeline)
            {
                _timeline = timeline;
            }

            public IReadOnlyList<VisualCommand> Commands => _commands;

            public override void CreateUnit(UnitSpawnViewSnapshot snapshot)
            {
                _commands.Add(VisualCommand.CreateUnit(snapshot));
            }

            public override void MoveUnit(UnitId unitId, BattleVector2 position)
            {
                _commands.Add(VisualCommand.MoveUnit(unitId, position));
                if (_hasEnqueued)
                {
                    return;
                }

                _hasEnqueued = true;
                _timeline.Enqueue(new VisualTimelineEntry(
                    VisualCommand.CreateUnit(new UnitSpawnViewSnapshot(
                        new UnitId(9),
                        new TeamId(1),
                        "reinforcement",
                        new BattleVector2(0f, 0f))),
                    scheduledTimeSeconds: 0f,
                    order: 1));
            }

            public override void FaceUnit(UnitId unitId, BattleVector2 facing)
            {
                _commands.Add(VisualCommand.FaceUnit(unitId, facing));
            }
        }

        private sealed class ThrowingCombatViewPort : EmptyCombatViewPort
        {
            public override void MoveUnit(UnitId unitId, BattleVector2 position)
            {
                throw new InvalidOperationException("Throwing viewport.");
            }
        }

        private sealed class RawSpyCombatViewPort : EmptyCombatViewPort
        {
            public int HealCount { get; private set; }
            public HealingViewSnapshot HealSnapshot { get; private set; }
            public int StatusAppliedCount { get; private set; }
            public StatusViewSnapshot StatusAppliedSnapshot { get; private set; }
            public int StatusExpiredCount { get; private set; }
            public StatusViewSnapshot StatusExpiredSnapshot { get; private set; }
            public int ProjectileHitCount { get; private set; }
            public ProjectileHitViewSnapshot ProjectileHitSnapshot { get; private set; }

            public override void PlayHeal(HealingViewSnapshot snapshot)
            {
                HealCount++;
                HealSnapshot = snapshot;
            }

            public override void PlayProjectileHit(ProjectileHitViewSnapshot snapshot)
            {
                ProjectileHitCount++;
                ProjectileHitSnapshot = snapshot;
            }

            public override void PlayStatusApplied(StatusViewSnapshot snapshot)
            {
                StatusAppliedCount++;
                StatusAppliedSnapshot = snapshot;
            }

            public override void PlayStatusExpired(StatusViewSnapshot snapshot)
            {
                StatusExpiredCount++;
                StatusExpiredSnapshot = snapshot;
            }
        }

        private sealed class RecordingVisualCommandSink : IVisualCommandSink
        {
            private readonly List<VisualCommand> _commands = new List<VisualCommand>();

            public IReadOnlyList<VisualCommand> Commands => _commands;

            public void Dispatch(VisualCommand command)
            {
                _commands.Add(command);
            }
        }

        private class EmptyCombatViewPort : ICombatViewPort
        {
            public virtual void CreateUnit(UnitSpawnViewSnapshot snapshot)
            {
            }

            public virtual void MoveUnit(UnitId unitId, BattleVector2 position)
            {
            }

            public virtual void StopUnitMovement(UnitId unitId)
            {
            }

            public virtual void FaceUnit(UnitId unitId, BattleVector2 facing)
            {
            }

            public virtual void SetUnitVisibility(UnitId unitId, bool isVisible)
            {
            }

            public virtual void PlayAction(ActionViewSnapshot snapshot)
            {
            }

            public virtual void PlayHit(DamageViewSnapshot snapshot)
            {
            }

            public virtual void PlayHeal(HealingViewSnapshot snapshot)
            {
            }

            public virtual void DestroyUnit(UnitId unitId)
            {
            }

            public virtual void CreateProjectile(ProjectileViewSnapshot snapshot)
            {
            }

            public virtual void MoveProjectile(ProjectileId projectileId, BattleVector2 position)
            {
            }

            public virtual void PlayProjectileHit(ProjectileHitViewSnapshot snapshot)
            {
            }

            public virtual void DestroyProjectile(ProjectileId projectileId)
            {
            }

            public virtual void PlayStatusApplied(StatusViewSnapshot snapshot)
            {
            }

            public virtual void PlayStatusExpired(StatusViewSnapshot snapshot)
            {
            }

            public virtual void ShowBattleResult(BattleResult result)
            {
            }
        }
    }
}
