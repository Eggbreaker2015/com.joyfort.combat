using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using Combat.Foundation.Events;
using Combat.Runtime.Display;
using NUnit.Framework;

namespace Combat.Tests.Runtime
{
    public sealed class VisualCommandDispatcherTests
    {
        [Test]
        public void Create_WithTypedNullCombatViewPortRejects()
        {
            Assert.Throws<ArgumentNullException>(() => new VisualCommandDispatcher((ICombatViewPort)null));
        }

        [Test]
        public void Create_WithTypedNullVisualCommandSinkRejects()
        {
            Assert.Throws<ArgumentNullException>(() => new VisualCommandDispatcher((IVisualCommandSink)null));
        }

        [Test]
        public void Dispatch_UnitSpawned_CreatesUnitFromEventData()
        {
            var viewport = new RecordingCombatViewPort();
            var dispatcher = new VisualCommandDispatcher(viewport);
            var events = new EventBuffer<BattleEvent>();
            events.Write(BattleEvent.UnitSpawned(
                sequence: 1,
                tick: new BattleTick(0),
                unitId: new UnitId(7),
                teamId: new TeamId(2),
                definitionId: "archer",
                position: new BattleVector2(3f, 4f),
                facing: new BattleVector2(0f, 1f)));

            dispatcher.Dispatch(events.AsStream());

            Assert.AreEqual(1, viewport.Commands.Count);
            Assert.AreEqual(VisualCommandType.CreateUnit, viewport.Commands[0].Type);
            Assert.AreEqual(new UnitId(7), viewport.Commands[0].UnitId);
            Assert.AreEqual(new TeamId(2), viewport.Commands[0].TeamId);
            Assert.AreEqual("archer", viewport.Commands[0].DefinitionId);
            Assert.AreEqual(new BattleVector2(3f, 4f), viewport.Commands[0].Position);
            Assert.AreEqual(new BattleVector2(0f, 1f), viewport.Commands[0].Facing);
        }

        [Test]
        public void Dispatch_UnitSpawnedStoresTypedPayload()
        {
            var sink = new RecordingVisualCommandSink();
            var dispatcher = new VisualCommandDispatcher(sink);
            var events = new EventBuffer<BattleEvent>();
            events.Write(BattleEvent.UnitSpawned(
                sequence: 1,
                tick: new BattleTick(0),
                unitId: new UnitId(7),
                teamId: new TeamId(2),
                definitionId: "archer",
                position: new BattleVector2(3f, 4f),
                facing: new BattleVector2(0f, 1f)));

            dispatcher.Dispatch(events.AsStream());

            UnitSpawnViewSnapshot payload = sink.Commands[0].GetPayload<UnitSpawnViewSnapshot>();
            Assert.AreEqual(new UnitId(7), payload.UnitId);
            Assert.AreEqual(new TeamId(2), payload.TeamId);
            Assert.AreEqual("archer", payload.DefinitionId);
            Assert.AreEqual(new BattleVector2(3f, 4f), payload.Position);
            Assert.AreEqual(new BattleVector2(0f, 1f), payload.Facing);
        }

        [Test]
        public void Dispatch_UnitFacingChanged_RecordsFaceUnitCommand()
        {
            var viewport = new RecordingCombatViewPort();
            var dispatcher = new VisualCommandDispatcher(viewport);
            var events = new EventBuffer<BattleEvent>();
            events.Write(BattleEvent.UnitFacingChanged(
                sequence: 1,
                tick: new BattleTick(3),
                unitId: new UnitId(7),
                teamId: new TeamId(2),
                facing: new BattleVector2(-1f, 0f)));

            dispatcher.Dispatch(events.AsStream());

            Assert.AreEqual(1, viewport.Commands.Count);
            Assert.AreEqual(VisualCommandType.FaceUnit, viewport.Commands[0].Type);
            Assert.AreEqual(new UnitId(7), viewport.Commands[0].UnitId);
            Assert.AreEqual(new BattleVector2(-1f, 0f), viewport.Commands[0].Facing);
        }

        [Test]
        public void Dispatch_GarrisonTransitions_RecordUnitVisibilityCommands()
        {
            var sink = new RecordingVisualCommandSink();
            var dispatcher = new VisualCommandDispatcher(sink);
            var events = new EventBuffer<BattleEvent>();
            events.Write(BattleEvent.UnitGarrisoned(
                sequence: 1,
                tick: new BattleTick(3),
                unitId: new UnitId(7),
                teamId: new TeamId(2)));
            events.Write(BattleEvent.UnitDeployed(
                sequence: 2,
                tick: new BattleTick(4),
                unitId: new UnitId(7),
                teamId: new TeamId(2)));

            dispatcher.Dispatch(events.AsStream());

            Assert.That(sink.Commands.Count, Is.EqualTo(2));
            Assert.That(sink.Commands[0].Type, Is.EqualTo(VisualCommandType.SetUnitVisibility));
            UnitVisibilityViewSnapshot hidden = sink.Commands[0].GetPayload<UnitVisibilityViewSnapshot>();
            Assert.That(hidden.UnitId, Is.EqualTo(new UnitId(7)));
            Assert.IsFalse(hidden.IsVisible);
            Assert.That(sink.Commands[1].Type, Is.EqualTo(VisualCommandType.SetUnitVisibility));
            UnitVisibilityViewSnapshot visible = sink.Commands[1].GetPayload<UnitVisibilityViewSnapshot>();
            Assert.That(visible.UnitId, Is.EqualTo(new UnitId(7)));
            Assert.IsTrue(visible.IsVisible);
        }

        [Test]
        public void Dispatch_AbilityStartedRecordsPlayActionCommand()
        {
            var sink = new RecordingVisualCommandSink();
            var dispatcher = new VisualCommandDispatcher(sink);
            var events = new EventBuffer<BattleEvent>();
            events.Write(BattleEvent.AbilityStarted(
                sequence: 1,
                tick: new BattleTick(3),
                sourceUnitId: new UnitId(7),
                targetUnitId: new UnitId(8),
                abilityId: "firebolt",
                sourceKind: BattleEffectSourceKind.BasicAbility,
                actionLocks: BattleActionLocks.Movement | BattleActionLocks.StartAnotherAction));

            dispatcher.Dispatch(events.AsStream());

            Assert.AreEqual(1, sink.Commands.Count);
            VisualCommand command = sink.Commands[0];
            Assert.AreEqual(VisualCommandType.PlayAction, command.Type);
            Assert.AreEqual(new UnitId(7), command.UnitId);
            Assert.AreEqual(new UnitId(7), command.SourceUnitId);
            Assert.AreEqual(new UnitId(8), command.TargetUnitId);
            Assert.AreEqual("firebolt", command.AbilityId);
            Assert.AreEqual(BattleEffectSourceKind.BasicAbility, command.EffectSourceKind);
            Assert.AreEqual(BattleActionLocks.Movement | BattleActionLocks.StartAnotherAction, command.ActionLocks);
        }

        [Test]
        public void Dispatch_AbilityReleasedCreatesNoCommandAndAbilityEndedCreatesEndActionCommand()
        {
            var sink = new RecordingVisualCommandSink();
            var dispatcher = new VisualCommandDispatcher(sink);
            var events = new EventBuffer<BattleEvent>();
            events.Write(BattleEvent.AbilityReleased(
                sequence: 1,
                tick: new BattleTick(4),
                sourceUnitId: new UnitId(7),
                targetUnitId: new UnitId(8),
                abilityId: "firebolt",
                sourceKind: BattleEffectSourceKind.BasicAbility));
            events.Write(BattleEvent.AbilityEnded(
                sequence: 2,
                tick: new BattleTick(5),
                sourceUnitId: new UnitId(7),
                targetUnitId: new UnitId(8),
                abilityId: "firebolt",
                sourceKind: BattleEffectSourceKind.BasicAbility));

            dispatcher.Dispatch(events.AsStream());

            Assert.AreEqual(1, sink.Commands.Count);
            Assert.AreEqual(VisualCommandType.EndAction, sink.Commands[0].Type);
            Assert.AreEqual(new UnitId(7), sink.Commands[0].UnitId);
        }

        [Test]
        public void Dispatch_DamageAppliedRecordsDamageContext()
        {
            var viewport = new RecordingCombatViewPort();
            var dispatcher = new VisualCommandDispatcher(viewport);
            var events = new EventBuffer<BattleEvent>();
            var context = new BattleEffectContext(
                BattleEffectSourceKind.Projectile,
                BattleEffectType.Damage,
                abilityId: null,
                statusId: null,
                projectileId: new ProjectileId(9),
                damageTags: new[] { "fire" });
            events.Write(BattleEvent.DamageApplied(1, new BattleTick(3), new UnitId(1), new UnitId(2), 6, context));

            dispatcher.Dispatch(events.AsStream());

            Assert.AreEqual(1, viewport.Commands.Count);
            VisualCommand command = viewport.Commands[0];
            Assert.AreEqual(VisualCommandType.PlayHit, command.Type);
            Assert.AreEqual(new UnitId(1), command.UnitId);
            Assert.AreEqual(new UnitId(1), command.SourceUnitId);
            Assert.AreEqual(new UnitId(2), command.TargetUnitId);
            Assert.AreEqual(6, command.Amount);
            Assert.AreEqual(BattleEffectSourceKind.Projectile, command.EffectSourceKind);
            Assert.IsTrue(command.HasEffectType);
            Assert.AreEqual(BattleEffectType.Damage, command.EffectType);
            Assert.AreEqual(new ProjectileId(9), command.EffectProjectileId);
            Assert.AreEqual("fire", command.DamageTags[0]);
        }

        [Test]
        public void Dispatch_DamageAppliedStoresTypedDamagePayload()
        {
            var sink = new RecordingVisualCommandSink();
            var dispatcher = new VisualCommandDispatcher(sink);
            var events = new EventBuffer<BattleEvent>();
            var context = new BattleEffectContext(
                BattleEffectSourceKind.Projectile,
                BattleEffectType.Damage,
                abilityId: null,
                statusId: null,
                projectileId: new ProjectileId(9),
                damageTags: new[] { "fire" });
            events.Write(BattleEvent.DamageApplied(1, new BattleTick(3), new UnitId(1), new UnitId(2), 6, context));

            dispatcher.Dispatch(events.AsStream());

            DamageViewSnapshot payload = sink.Commands[0].GetPayload<DamageViewSnapshot>();
            Assert.AreEqual(new UnitId(1), payload.SourceUnitId);
            Assert.AreEqual(new UnitId(2), payload.TargetUnitId);
            Assert.AreEqual(6, payload.Amount);
            Assert.AreEqual(BattleEffectSourceKind.Projectile, payload.SourceKind);
            Assert.AreEqual(BattleEffectType.Damage, payload.EffectType);
            Assert.AreEqual(new ProjectileId(9), payload.ProjectileId);
            Assert.AreEqual("fire", payload.DamageTags[0]);
        }

        [Test]
        public void GetPayload_RejectsWrongPayloadType()
        {
            VisualCommand command = VisualCommand.MoveUnit(new UnitId(3), new BattleVector2(1f, 2f));

            Assert.Throws<InvalidOperationException>(() => command.GetPayload<DamageViewSnapshot>());
        }

        [Test]
        public void DefaultVisualCommand_IsInvalid()
        {
            VisualCommand command = default;

            Assert.IsFalse(command.IsValid);
        }

        [Test]
        public void DefaultVisualCommand_CompatibilityFieldsReturnDefaults()
        {
            VisualCommand command = default;

            Assert.AreEqual(default(UnitId), command.UnitId);
            Assert.AreEqual(default(ProjectileId), command.ProjectileId);
            Assert.AreEqual(default(UnitId), command.TargetUnitId);
            Assert.AreEqual(default(TeamId), command.TeamId);
            Assert.AreEqual(default(BattleVector2), command.Position);
            Assert.AreEqual(default(BattleVector2), command.Facing);
            Assert.AreEqual(0, command.Amount);
            Assert.AreEqual(default(TeamId), command.WinningTeamId);
            Assert.IsNull(command.DefinitionId);
            Assert.AreEqual(default(UnitId), command.SourceUnitId);
            Assert.IsNull(command.StatusId);
            Assert.AreEqual(default(StatusPolarity), command.StatusPolarity);
            Assert.AreEqual(BattleEffectSourceKind.Unknown, command.EffectSourceKind);
            Assert.IsFalse(command.HasEffectType);
            Assert.AreEqual(default(BattleEffectType), command.EffectType);
            Assert.IsNull(command.AbilityId);
            Assert.IsNull(command.EffectStatusId);
            Assert.AreEqual(default(ProjectileId), command.EffectProjectileId);
            Assert.AreEqual(BattleActionLocks.None, command.ActionLocks);
            Assert.AreEqual(0, command.DamageTags.Count);
        }

        [Test]
        public void DefaultVisualCommand_GetPayloadRejectsInvalidCommand()
        {
            VisualCommand command = default;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => command.GetPayload<UnitSpawnViewSnapshot>());
            StringAssert.Contains("invalid", exception.Message.ToLowerInvariant());
        }

        [Test]
        public void ImmediateSink_RejectsDefaultVisualCommand()
        {
            var sink = new ImmediateVisualCommandSink(new RecordingCombatViewPort());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => sink.Dispatch(default));
            StringAssert.Contains("invalid", exception.Message.ToLowerInvariant());
        }

        [Test]
        public void EndAction_PreservesSourceUnitIdCompatibilityField()
        {
            VisualCommand command = VisualCommand.EndAction(new UnitId(7));

            Assert.AreEqual(new UnitId(7), command.SourceUnitId);
        }

        [Test]
        public void PlayHit_PreservesProjectileCompatibilityFields()
        {
            VisualCommand command = VisualCommand.PlayHit(new DamageViewSnapshot(
                new UnitId(1),
                new UnitId(2),
                6,
                BattleEffectSourceKind.Projectile,
                true,
                BattleEffectType.Damage,
                abilityId: null,
                statusId: null,
                projectileId: new ProjectileId(9),
                damageTags: Array.Empty<string>()));

            Assert.AreEqual(default(ProjectileId), command.ProjectileId);
            Assert.AreEqual(new ProjectileId(9), command.EffectProjectileId);
        }

        [Test]
        public void PlayHeal_PreservesProjectileCompatibilityFields()
        {
            VisualCommand command = VisualCommand.PlayHeal(new HealingViewSnapshot(
                new UnitId(1),
                new UnitId(2),
                6,
                BattleEffectSourceKind.Projectile,
                true,
                BattleEffectType.Heal,
                abilityId: null,
                statusId: null,
                projectileId: new ProjectileId(9)));

            Assert.AreEqual(default(ProjectileId), command.ProjectileId);
            Assert.AreEqual(new ProjectileId(9), command.EffectProjectileId);
        }

        [Test]
        public void Dispatch_HealingAppliedRecordsHealingContext()
        {
            var viewport = new RecordingCombatViewPort();
            var dispatcher = new VisualCommandDispatcher(viewport);
            var events = new EventBuffer<BattleEvent>();
            var context = BattleEffectContext.Ability("mend", BattleEffectType.Heal);
            events.Write(BattleEvent.HealingApplied(1, new BattleTick(3), new UnitId(1), new UnitId(2), 5, context));

            dispatcher.Dispatch(events.AsStream());

            Assert.AreEqual(1, viewport.Commands.Count);
            VisualCommand command = viewport.Commands[0];
            Assert.AreEqual(VisualCommandType.PlayHeal, command.Type);
            Assert.AreEqual(new UnitId(1), command.UnitId);
            Assert.AreEqual(new UnitId(1), command.SourceUnitId);
            Assert.AreEqual(new UnitId(2), command.TargetUnitId);
            Assert.AreEqual(5, command.Amount);
            Assert.AreEqual(BattleEffectSourceKind.Ability, command.EffectSourceKind);
            Assert.IsTrue(command.HasEffectType);
            Assert.AreEqual(BattleEffectType.Heal, command.EffectType);
            Assert.AreEqual("mend", command.AbilityId);
            Assert.AreEqual(0, command.DamageTags.Count);
        }

        [Test]
        public void Dispatch_HealingAppliedRecordsStatusAndProjectileContextWithoutDamageTags()
        {
            var viewport = new RecordingCombatViewPort();
            var dispatcher = new VisualCommandDispatcher(viewport);
            var events = new EventBuffer<BattleEvent>();
            var context = new BattleEffectContext(
                BattleEffectSourceKind.Projectile,
                BattleEffectType.Heal,
                abilityId: null,
                statusId: "regrowth",
                projectileId: new ProjectileId(9),
                damageTags: new[] { "ignored-for-heal" });
            events.Write(BattleEvent.HealingApplied(1, new BattleTick(4), new UnitId(3), new UnitId(4), 7, context));

            dispatcher.Dispatch(events.AsStream());

            Assert.AreEqual(1, viewport.Commands.Count);
            VisualCommand command = viewport.Commands[0];
            Assert.AreEqual(VisualCommandType.PlayHeal, command.Type);
            Assert.AreEqual(new UnitId(3), command.UnitId);
            Assert.AreEqual(new UnitId(3), command.SourceUnitId);
            Assert.AreEqual(new UnitId(4), command.TargetUnitId);
            Assert.AreEqual(7, command.Amount);
            Assert.AreEqual(BattleEffectSourceKind.Projectile, command.EffectSourceKind);
            Assert.IsTrue(command.HasEffectType);
            Assert.AreEqual(BattleEffectType.Heal, command.EffectType);
            Assert.AreEqual("regrowth", command.EffectStatusId);
            Assert.AreEqual(new ProjectileId(9), command.EffectProjectileId);
            Assert.AreEqual(0, command.DamageTags.Count);
        }

        [Test]
        public void Dispatch_StatusAppliedRecordsStatusAppliedCommand()
        {
            var viewport = new RecordingCombatViewPort();
            var dispatcher = new VisualCommandDispatcher(viewport);
            var events = new EventBuffer<BattleEvent>();
            events.Write(BattleEvent.StatusApplied(1, new BattleTick(1), new UnitId(7), new UnitId(8), "burn", StatusPolarity.Debuff));

            dispatcher.Dispatch(events.AsStream());

            Assert.AreEqual(1, viewport.Commands.Count);
            Assert.AreEqual(VisualCommandType.PlayStatusApplied, viewport.Commands[0].Type);
            Assert.AreEqual(new UnitId(8), viewport.Commands[0].UnitId);
            Assert.AreEqual(new UnitId(7), viewport.Commands[0].SourceUnitId);
            Assert.AreEqual("burn", viewport.Commands[0].StatusId);
            Assert.AreEqual(StatusPolarity.Debuff, viewport.Commands[0].StatusPolarity);
        }

        [Test]
        public void Dispatch_StatusExpiredRecordsStatusExpiredCommand()
        {
            var viewport = new RecordingCombatViewPort();
            var dispatcher = new VisualCommandDispatcher(viewport);
            var events = new EventBuffer<BattleEvent>();
            events.Write(BattleEvent.StatusExpired(1, new BattleTick(2), new UnitId(8), "burn", StatusPolarity.Debuff));

            dispatcher.Dispatch(events.AsStream());

            Assert.AreEqual(1, viewport.Commands.Count);
            Assert.AreEqual(VisualCommandType.PlayStatusExpired, viewport.Commands[0].Type);
            Assert.AreEqual(new UnitId(8), viewport.Commands[0].UnitId);
            Assert.AreEqual(default(UnitId), viewport.Commands[0].SourceUnitId);
            Assert.AreEqual("burn", viewport.Commands[0].StatusId);
            Assert.AreEqual(StatusPolarity.Debuff, viewport.Commands[0].StatusPolarity);
        }

        [Test]
        public void Dispatch_ProjectileEvents_RecordProjectileCommands()
        {
            var viewport = new RecordingCombatViewPort();
            var dispatcher = new VisualCommandDispatcher(viewport);
            var events = new EventBuffer<BattleEvent>();
            events.Write(BattleEvent.ProjectileSpawned(
                1,
                new BattleTick(1),
                new ProjectileId(3),
                new TeamId(1),
                new UnitId(7),
                new BattleVector2(1f, 2f)));
            events.Write(BattleEvent.ProjectileMoved(
                2,
                new BattleTick(1),
                new ProjectileId(3),
                new BattleVector2(2f, 2f)));
            events.Write(BattleEvent.ProjectileDestroyed(
                3,
                new BattleTick(1),
                new ProjectileId(3)));

            dispatcher.Dispatch(events.AsStream());

            Assert.AreEqual(3, viewport.Commands.Count);
            Assert.AreEqual(VisualCommandType.CreateProjectile, viewport.Commands[0].Type);
            Assert.AreEqual(new ProjectileId(3), viewport.Commands[0].ProjectileId);
            Assert.AreEqual(new BattleVector2(1f, 2f), viewport.Commands[0].Position);
            Assert.AreEqual(VisualCommandType.MoveProjectile, viewport.Commands[1].Type);
            Assert.AreEqual(VisualCommandType.DestroyProjectile, viewport.Commands[2].Type);
        }

        [Test]
        public void Dispatch_WithTimelineSinkQueuesCommandsUntilRunnerAdvances()
        {
            var viewport = new RecordingCombatViewPort();
            var runner = new VisualTimelineRunner(
                viewport,
                new VisualTimeline(),
                new VisualTimelinePolicy(new VisualTimelineSettings(
                    projectileDestroyDelaySeconds: 0.12f,
                    unitDestroyDelaySeconds: 0.35f,
                    battleResultDelaySeconds: 0.45f)));
            var dispatcher = new VisualCommandDispatcher(runner);
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

            dispatcher.Dispatch(events.AsStream());

            Assert.AreEqual(0, viewport.Commands.Count);
            Assert.AreEqual(2, runner.PendingCount);

            runner.Advance(0.01f);

            Assert.AreEqual(1, viewport.Commands.Count);
            Assert.AreEqual(VisualCommandType.PlayProjectileHit, viewport.Commands[0].Type);

            runner.Advance(0.11f);

            Assert.AreEqual(2, viewport.Commands.Count);
            Assert.AreEqual(VisualCommandType.DestroyProjectile, viewport.Commands[1].Type);
        }

        [Test]
        public void Dispatch_ProjectileHitRecordsProjectileHitCommand()
        {
            var viewport = new RecordingCombatViewPort();
            var dispatcher = new VisualCommandDispatcher(viewport);
            var events = new EventBuffer<BattleEvent>();
            events.Write(BattleEvent.ProjectileHit(
                1,
                new BattleTick(3),
                new ProjectileId(8),
                new UnitId(1),
                new UnitId(2),
                new BattleVector2(4f, 5f)));

            dispatcher.Dispatch(events.AsStream());

            Assert.AreEqual(1, viewport.Commands.Count);
            Assert.AreEqual(VisualCommandType.PlayProjectileHit, viewport.Commands[0].Type);
            Assert.AreEqual(new ProjectileId(8), viewport.Commands[0].ProjectileId);
            Assert.AreEqual(new UnitId(1), viewport.Commands[0].UnitId);
            Assert.AreEqual(new UnitId(2), viewport.Commands[0].TargetUnitId);
            Assert.AreEqual(new BattleVector2(4f, 5f), viewport.Commands[0].Position);
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
