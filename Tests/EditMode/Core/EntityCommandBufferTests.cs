using System;
using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class EntityCommandBufferTests
    {
        [Test]
        public void SpawnCombatantCommands_AreStoredInStableOrderUntilCleared()
        {
            var buffer = new EntityCommandBuffer();
            CombatantSpawnData first = TestSpawnData(new TeamId(1), new BattleVector2(0f, 0f));
            CombatantSpawnData second = TestSpawnData(new TeamId(2), new BattleVector2(1f, 0f));

            buffer.SpawnCombatant(new SpawnCombatantCommand(new UnitId(1), first));
            buffer.SpawnCombatant(new SpawnCombatantCommand(new UnitId(2), second));

            Assert.AreEqual(new UnitId(1), buffer.SpawnCombatantCommands[0].UnitId);
            Assert.AreEqual(new UnitId(2), buffer.SpawnCombatantCommands[1].UnitId);

            buffer.ClearSpawnCombatantCommands();

            Assert.AreEqual(0, buffer.SpawnCombatantCommands.Count);
        }

        [Test]
        public void QueueAction_UseBasicAbilityStoresActionCommand()
        {
            var buffer = new EntityCommandBuffer();
            var source = new EntityId(0, 1);
            var target = new EntityId(1, 1);

            buffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 0));

            Assert.AreEqual(1, buffer.ActionCommands.Count);
            BattleActionCommand command = buffer.ActionCommands[0];
            Assert.AreEqual(BattleActionType.UseAbility, command.Type);
            Assert.AreEqual(source, command.Source);
            Assert.AreEqual(target, command.Target);
            Assert.AreEqual(0, command.AbilityIndex);
            Assert.AreEqual(0, buffer.EffectCommands.Count);
        }

        [Test]
        public void QueueAction_UseAbilityStoresActionCommand()
        {
            var buffer = new EntityCommandBuffer();
            var source = new EntityId(0, 1);
            var target = new EntityId(1, 1);

            buffer.QueueAction(BattleActionCommand.UseAbility(source, target, abilityIndex: 2));

            Assert.AreEqual(1, buffer.ActionCommands.Count);
            BattleActionCommand command = buffer.ActionCommands[0];
            Assert.AreEqual(BattleActionType.UseAbility, command.Type);
            Assert.AreEqual(source, command.Source);
            Assert.AreEqual(target, command.Target);
            Assert.AreEqual(2, command.AbilityIndex);
            Assert.AreEqual(0, buffer.EffectCommands.Count);
        }

        [Test]
        public void ClearActionCommands_RemovesQueuedActions()
        {
            var buffer = new EntityCommandBuffer();

            buffer.QueueAction(BattleActionCommand.UseAbility(new EntityId(0, 1), new EntityId(1, 1), abilityIndex: 0));
            buffer.ClearActionCommands();

            Assert.AreEqual(0, buffer.ActionCommands.Count);
        }

        [Test]
        public void QueueEffect_DamageCommandStoresDamageEffect()
        {
            var buffer = new EntityCommandBuffer();
            var source = new EntityId(0, 1);
            var target = new EntityId(1, 1);

            buffer.QueueEffect(BattleEffectCommand.Damage(source, target, 7));

            Assert.AreEqual(1, buffer.EffectCommands.Count);
            BattleEffectCommand command = buffer.EffectCommands[0];
            Assert.AreEqual(BattleEffectType.Damage, command.Type);
            Assert.AreEqual(source, command.Source);
            Assert.AreEqual(target, command.Target);
            Assert.AreEqual(7, command.Amount);
        }

        [Test]
        public void BattleEffectContext_CopiesDamageTags()
        {
            string[] tags = { "fire", "dot" };

            var context = new BattleEffectContext(
                BattleEffectSourceKind.Ability,
                BattleEffectType.Damage,
                abilityId: "slash",
                statusId: null,
                projectileId: default,
                damageTags: tags);
            tags[0] = "ice";

            Assert.AreEqual(BattleEffectSourceKind.Ability, context.SourceKind);
            Assert.IsTrue(context.HasEffectType);
            Assert.AreEqual(BattleEffectType.Damage, context.EffectType);
            Assert.AreEqual("slash", context.AbilityId);
            Assert.AreEqual(2, context.DamageTags.Count);
            Assert.AreEqual("fire", context.DamageTags[0]);
            Assert.AreEqual("dot", context.DamageTags[1]);
        }

        [Test]
        public void BattleEffectContext_UnknownHasNoEffectType()
        {
            BattleEffectContext context = BattleEffectContext.Unknown();

            Assert.IsFalse(context.HasEffectType);
        }

        [Test]
        public void BattleEffectContext_ReusesEmptyDamageTags()
        {
            BattleEffectContext first = BattleEffectContext.Unknown(BattleEffectType.Damage);
            BattleEffectContext second = BattleEffectContext.Ability("slash", BattleEffectType.Damage);

            Assert.AreEqual(0, first.DamageTags.Count);
            Assert.AreSame(first.DamageTags, second.DamageTags);
        }

        [Test]
        public void QueueEffect_DamageCommandStoresExplicitContext()
        {
            var buffer = new EntityCommandBuffer();
            var source = new EntityId(0, 1);
            var target = new EntityId(1, 1);
            BattleEffectContext context = BattleEffectContext.Ability("slash", BattleEffectType.Damage);

            buffer.QueueEffect(BattleEffectCommand.Damage(source, target, 7, context));

            BattleEffectCommand command = buffer.EffectCommands[0];
            Assert.AreEqual(BattleEffectSourceKind.Ability, command.Context.SourceKind);
            Assert.AreEqual(BattleEffectType.Damage, command.Context.EffectType);
            Assert.AreEqual("slash", command.Context.AbilityId);
            Assert.AreEqual(BattleEffectTriggerPolicy.CanTriggerReactions, command.TriggerPolicy);
        }

        [Test]
        public void QueueEffect_HealStoresEffectAndContext()
        {
            var buffer = new EntityCommandBuffer();
            var source = new EntityId(0, 1);
            var target = new EntityId(1, 1);
            BattleEffectContext context = BattleEffectContext.Ability("mend", BattleEffectType.Heal);

            buffer.QueueEffect(BattleEffectCommand.Heal(source, target, 5, context));

            BattleEffectCommand command = buffer.EffectCommands[0];
            Assert.AreEqual(BattleEffectType.Heal, command.Type);
            Assert.AreEqual(source, command.Source);
            Assert.AreEqual(target, command.Target);
            Assert.AreEqual(5, command.Amount);
            Assert.AreEqual(BattleEffectTriggerPolicy.SuppressReactions, command.TriggerPolicy);
            Assert.AreEqual(BattleEffectType.Heal, command.Context.EffectType);
        }

        [Test]
        public void QueueEffect_AreaEffectStoresEffectAndTriggerPolicy()
        {
            var area = new AreaEffectData(
                BattleScalar.FromFloat(2f),
                AreaEffectTargetFilter.Enemies,
                new[] { BattleEffectData.Damage(3) });
            var source = new EntityId(0, 1);
            var target = new EntityId(1, 1);
            BattleEffectContext context = BattleEffectContext.Ability("nova", BattleEffectType.AreaEffect);

            BattleEffectCommand command = BattleEffectCommand.CreateAreaEffect(source, target, area, context, BattleEffectTriggerPolicy.SuppressReactions);

            Assert.AreEqual(BattleEffectType.AreaEffect, command.Type);
            Assert.AreEqual(BattleScalar.FromFloat(2f), command.AreaEffect.Radius);
            Assert.AreEqual(BattleEffectTriggerPolicy.SuppressReactions, command.TriggerPolicy);
        }

        [Test]
        public void BattleEffectCommand_DamageRejectsMismatchedContextKind()
        {
            var source = new EntityId(0, 1);
            var target = new EntityId(1, 1);
            BattleEffectContext context = BattleEffectContext.Ability("slash", BattleEffectType.ApplyStatus);

            Assert.Throws<ArgumentException>(() => BattleEffectCommand.Damage(source, target, 7, context));
        }

        [Test]
        public void BattleEffectCommand_DamageDefaultsToTriggerReactions()
        {
            var source = new EntityId(0, 1);
            var target = new EntityId(1, 1);

            BattleEffectCommand command = BattleEffectCommand.Damage(source, target, 5);

            Assert.AreEqual(BattleEffectTriggerPolicy.CanTriggerReactions, command.TriggerPolicy);
        }

        [Test]
        public void BattleEffectCommand_DamageCanSuppressReactions()
        {
            var source = new EntityId(0, 1);
            var target = new EntityId(1, 1);

            BattleEffectCommand command = BattleEffectCommand.Damage(source, target, 5, BattleEffectTriggerPolicy.SuppressReactions);

            Assert.AreEqual(BattleEffectTriggerPolicy.SuppressReactions, command.TriggerPolicy);
        }

        [Test]
        public void QueueEffect_SpawnProjectileEmitterStoresEffect()
        {
            var buffer = new EntityCommandBuffer();
            var source = new EntityId(1, 1);
            var target = new EntityId(2, 1);
            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, 0.1f, 2f, 5, new[] { BattleEffectDefinition.Damage(3) });
            var emitter = new ProjectileEmitterSpawnData(ProjectileEmitterAnchorMode.FollowSource, default, 2, 1, ProjectilePattern.Single(new BattleVector2(1f, 0f)), payload);

            buffer.QueueEffect(BattleEffectCommand.SpawnProjectileEmitter(source, target, emitter));

            Assert.AreEqual(1, buffer.EffectCommands.Count);
            Assert.AreEqual(BattleEffectType.SpawnProjectileEmitter, buffer.EffectCommands[0].Type);
            Assert.AreEqual(emitter.DurationTicks, buffer.EffectCommands[0].ProjectileEmitter.DurationTicks);
            Assert.IsFalse(buffer.EffectCommands[0].HasProjectileEmitterOrigin);
        }

        [Test]
        public void QueueEffect_SpawnProjectileEmitterAtStoresOrigin()
        {
            var buffer = new EntityCommandBuffer();
            var source = new EntityId(1, 1);
            var target = new EntityId(2, 1);
            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, 0.1f, 2f, 5, new[] { BattleEffectDefinition.Damage(3) });
            var emitter = new ProjectileEmitterSpawnData(ProjectileEmitterAnchorMode.FixedPosition, default, 2, 1, ProjectilePattern.Single(new BattleVector2(1f, 0f)), payload);

            buffer.QueueEffect(BattleEffectCommand.SpawnProjectileEmitterAt(source, target, emitter, new BattleVector2(3f, 4f)));

            Assert.AreEqual(1, buffer.EffectCommands.Count);
            Assert.AreEqual(BattleEffectType.SpawnProjectileEmitter, buffer.EffectCommands[0].Type);
            Assert.IsTrue(buffer.EffectCommands[0].HasProjectileEmitterOrigin);
            Assert.AreEqual(new BattleVector2(3f, 4f), buffer.EffectCommands[0].ProjectileEmitterOrigin);
        }

        [Test]
        public void QueueEffect_DamageWithNonPositiveAmountIsIgnored()
        {
            var buffer = new EntityCommandBuffer();
            var source = new EntityId(0, 1);
            var target = new EntityId(1, 1);

            buffer.QueueEffect(BattleEffectCommand.Damage(source, target, 0));
            buffer.QueueEffect(BattleEffectCommand.Damage(source, target, -1));

            Assert.AreEqual(0, buffer.EffectCommands.Count);
        }

        [Test]
        public void QueueEffect_ApplyStatusStoresStatusEffect()
        {
            var buffer = new EntityCommandBuffer();
            var source = new EntityId(0, 1);
            var target = new EntityId(1, 1);
            var status = new StatusApplicationData("burn", StatusPolarity.Debuff, durationTicks: 3, tickIntervalTicks: 1, periodicDamage: 2, modifiers: new BattleModifierData[0], triggers: new BattleTriggerData[0]);

            buffer.QueueEffect(BattleEffectCommand.ApplyStatus(source, target, status));

            Assert.AreEqual(1, buffer.EffectCommands.Count);
            BattleEffectCommand command = buffer.EffectCommands[0];
            Assert.AreEqual(BattleEffectType.ApplyStatus, command.Type);
            Assert.AreEqual(source, command.Source);
            Assert.AreEqual(target, command.Target);
            Assert.AreEqual("burn", command.Status.Id);
        }

        [Test]
        public void QueueEffect_ApplyStatusWithZeroPeriodicDamageIsStored()
        {
            var buffer = new EntityCommandBuffer();

            buffer.QueueEffect(BattleEffectCommand.ApplyStatus(
                new EntityId(0, 1),
                new EntityId(1, 1),
                new StatusApplicationData("mark", StatusPolarity.Neutral, 2, 1, 0, new BattleModifierData[0], new BattleTriggerData[0])));

            Assert.AreEqual(1, buffer.EffectCommands.Count);
        }

        [Test]
        public void ClearEffectCommands_RemovesQueuedEffects()
        {
            var buffer = new EntityCommandBuffer();

            buffer.QueueEffect(BattleEffectCommand.Damage(new EntityId(0, 1), new EntityId(1, 1), 3));
            buffer.ClearEffectCommands();

            Assert.AreEqual(0, buffer.EffectCommands.Count);
        }

        [Test]
        public void DrainEffectCommands_ReturnsSnapshotAndKeepsLaterQueuedEffects()
        {
            var buffer = new EntityCommandBuffer();
            var source = new EntityId(0, 1);
            var firstTarget = new EntityId(1, 1);
            var secondTarget = new EntityId(2, 1);

            buffer.QueueEffect(BattleEffectCommand.Damage(source, firstTarget, 3));
            BattleEffectCommand[] batch = buffer.DrainEffectCommands();
            buffer.QueueEffect(BattleEffectCommand.Damage(source, secondTarget, 5));

            Assert.AreEqual(1, batch.Length);
            Assert.AreEqual(firstTarget, batch[0].Target);
            Assert.AreEqual(1, buffer.EffectCommands.Count);
            Assert.AreEqual(secondTarget, buffer.EffectCommands[0].Target);
        }

        [Test]
        public void DrainReactionAndDeathCheckCommands_ReturnSnapshotsAndKeepLaterQueuedCommands()
        {
            var buffer = new EntityCommandBuffer();
            var source = new EntityId(0, 1);
            var firstTarget = new EntityId(1, 1);
            var secondTarget = new EntityId(2, 1);

            buffer.QueueReactionEffect(BattleEffectCommand.Damage(source, firstTarget, 3, BattleEffectTriggerPolicy.SuppressReactions));
            buffer.QueueDeathCheck(new DeathCheckCommand(firstTarget));
            BattleEffectCommand[] reactionBatch = buffer.DrainReactionEffectCommands();
            DeathCheckCommand[] deathBatch = buffer.DrainDeathCheckCommands();
            buffer.QueueReactionEffect(BattleEffectCommand.Damage(source, secondTarget, 5, BattleEffectTriggerPolicy.SuppressReactions));
            buffer.QueueDeathCheck(new DeathCheckCommand(secondTarget));

            Assert.AreEqual(1, reactionBatch.Length);
            Assert.AreEqual(firstTarget, reactionBatch[0].Target);
            Assert.AreEqual(1, deathBatch.Length);
            Assert.AreEqual(firstTarget, deathBatch[0].Entity);
            Assert.AreEqual(1, buffer.ReactionEffectCommands.Count);
            Assert.AreEqual(secondTarget, buffer.ReactionEffectCommands[0].Target);
            Assert.AreEqual(1, buffer.DeathCheckCommands.Count);
            Assert.AreEqual(secondTarget, buffer.DeathCheckCommands[0].Entity);
        }

        [Test]
        public void SpawnProjectileCommand_IsStoredAndCleared()
        {
            var buffer = new EntityCommandBuffer();
            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, 0.1f, 2f, 5, new[] { BattleEffectDefinition.Damage(3) });

            buffer.SpawnProjectile(new SpawnProjectileCommand(new EntityId(1, 1), new TeamId(1), new BattleVector2(0f, 0f), new BattleVector2(1f, 0f), payload, new BattleTick(4)));

            Assert.AreEqual(1, buffer.SpawnProjectileCommands.Count);
            buffer.ClearSpawnProjectileCommands();
            Assert.AreEqual(0, buffer.SpawnProjectileCommands.Count);
        }

        [Test]
        public void DeathCheckCommands_AreStoredUntilCleared()
        {
            var buffer = new EntityCommandBuffer();
            var target = new EntityId(1, 1);

            buffer.QueueDeathCheck(new DeathCheckCommand(target));

            Assert.AreEqual(1, buffer.DeathCheckCommands.Count);
            Assert.AreEqual(target, buffer.DeathCheckCommands[0].Entity);

            buffer.ClearDeathCheckCommands();

            Assert.AreEqual(0, buffer.DeathCheckCommands.Count);
        }

        [Test]
        public void DeathCheckCommands_StoreSourceContextAndTriggerPolicy()
        {
            var buffer = new EntityCommandBuffer();
            var source = new EntityId(0, 1);
            var target = new EntityId(1, 1);
            BattleEffectContext context = BattleEffectContext.Ability("execute", BattleEffectType.Damage);

            buffer.QueueDeathCheck(new DeathCheckCommand(
                target,
                source,
                context,
                BattleEffectTriggerPolicy.CanTriggerReactions));

            Assert.AreEqual(1, buffer.DeathCheckCommands.Count);
            Assert.AreEqual(target, buffer.DeathCheckCommands[0].Entity);
            Assert.AreEqual(source, buffer.DeathCheckCommands[0].Source);
            Assert.AreEqual(context, buffer.DeathCheckCommands[0].EffectContext);
            Assert.AreEqual(BattleEffectTriggerPolicy.CanTriggerReactions, buffer.DeathCheckCommands[0].TriggerPolicy);
        }

        [Test]
        public void BattleReactionEffectData_RejectsInvalidTarget()
        {
            BattleReactionTarget invalidTarget = (BattleReactionTarget)999;
            StatusApplicationData status = TestStatusApplicationData();

            Assert.Throws<ArgumentOutOfRangeException>(() => BattleReactionEffectData.Create(invalidTarget, BattleEffectData.Damage(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleReactionEffectData.Create(invalidTarget, BattleEffectData.ApplyStatus(status)));
        }

        [Test]
        public void BattleEffectData_ApplyStatusPreservesMaxStacks()
        {
            var status = new StatusApplicationData(
                "rage",
                StatusPolarity.Buff,
                durationTicks: 5,
                tickIntervalTicks: 5,
                periodicDamage: 0,
                modifiers: new BattleModifierData[0],
                triggers: new BattleTriggerData[0],
                maxStacks: 5);

            BattleEffectData effect = BattleEffectData.ApplyStatus(status);

            Assert.AreEqual(5, effect.Status.MaxStacks);
        }

        [Test]
        public void BattleTriggerData_RejectsInvalidTiming()
        {
            BattleTriggerTiming invalidTiming = (BattleTriggerTiming)999;

            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleTriggerData(
                invalidTiming,
                new[]
                {
                    BattleReactionEffectData.Create(BattleReactionTarget.Source, BattleEffectData.Damage(1))
                }));
        }

        [Test]
        public void BattleReactionEffectInstance_RejectsInvalidTarget()
        {
            BattleReactionTarget invalidTarget = (BattleReactionTarget)999;
            StatusApplicationData status = TestStatusApplicationData();

            Assert.Throws<ArgumentOutOfRangeException>(() => BattleReactionEffectInstance.Create(invalidTarget, BattleEffectData.Damage(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleReactionEffectInstance.Create(invalidTarget, BattleEffectData.ApplyStatus(status)));
        }

        [Test]
        public void BattleTriggerInstance_RejectsInvalidTiming()
        {
            BattleTriggerTiming invalidTiming = (BattleTriggerTiming)999;

            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleTriggerInstance(
                invalidTiming,
                new[]
                {
                    BattleReactionEffectInstance.Create(BattleReactionTarget.Source, BattleEffectData.Damage(1))
                }));
        }

        [Test]
        public void ReactionEffectAndDeathCheckCommands_AreStoredUntilCleared()
        {
            var buffer = new EntityCommandBuffer();
            var source = new EntityId(0, 1);
            var target = new EntityId(1, 1);

            buffer.QueueReactionEffect(BattleEffectCommand.Damage(source, target, 3, BattleEffectTriggerPolicy.SuppressReactions));
            buffer.QueueDeathCheck(new DeathCheckCommand(target));

            Assert.AreEqual(1, buffer.ReactionEffectCommands.Count);
            Assert.AreEqual(BattleEffectType.Damage, buffer.ReactionEffectCommands[0].Type);
            Assert.AreEqual(BattleEffectTriggerPolicy.SuppressReactions, buffer.ReactionEffectCommands[0].TriggerPolicy);
            Assert.AreEqual(1, buffer.DeathCheckCommands.Count);
            Assert.AreEqual(target, buffer.DeathCheckCommands[0].Entity);

            buffer.ClearReactionEffectCommands();
            buffer.ClearDeathCheckCommands();

            Assert.AreEqual(0, buffer.ReactionEffectCommands.Count);
            Assert.AreEqual(0, buffer.DeathCheckCommands.Count);
        }

        private static CombatantSpawnData TestSpawnData(TeamId teamId, BattleVector2 position)
        {
            return new CombatantSpawnData(
                teamId,
                "melee",
                position,
                maxHealth: 10,
                radius: BattleScalar.FromFloat(0.25f),
                moveSpeed: BattleScalar.One,
                basicAbility: TestCombatants.AbilitySpawn("basic-slash", 1f, 1, 1),
                abilities: new AbilitySpawnData[0]);
        }

        private static StatusApplicationData TestStatusApplicationData()
        {
            return new StatusApplicationData(
                "mark",
                StatusPolarity.Debuff,
                durationTicks: 2,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new BattleModifierData[0],
                triggers: new BattleTriggerData[0]);
        }
    }
}
