using System;
using NUnit.Framework;
using Combat.Core.Battle;

namespace Combat.Tests.Core
{
    public sealed class ProjectileRuntimeDataTests
    {
        [Test]
        public void ProjectileId_StoresValueAndSupportsEquality()
        {
            Assert.AreEqual(new ProjectileId(7), new ProjectileId(7));
            Assert.AreNotEqual(new ProjectileId(7), new ProjectileId(8));
            Assert.AreEqual(7, new ProjectileId(7).Value);
        }

        [Test]
        public void ProjectileHitPolicy_StoresValidatedFiniteHitCapacity()
        {
            ProjectileHitPolicy destroyOnFirstHit =
                ProjectileHitPolicy.DestroyOnFirstHit;
            ProjectileHitPolicy pierce = ProjectileHitPolicy.Pierce(3);

            Assert.AreEqual(
                ProjectileHitPolicyMode.DestroyOnFirstHit,
                destroyOnFirstHit.Mode);
            Assert.AreEqual(1, destroyOnFirstHit.MaxHitCount);
            Assert.AreEqual(ProjectileHitPolicyMode.Pierce, pierce.Mode);
            Assert.AreEqual(3, pierce.MaxHitCount);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ProjectileHitPolicy.Pierce(1));
        }

        [Test]
        public void ProjectilePayload_RejectsInvalidValues()
        {
            BattleEffectDefinition[] effects = { BattleEffectDefinition.Damage(2) };

            Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectilePayload(ProjectileBehavior.Linear, default, radius: 0.1f, speed: 1f, lifetimeTicks: 3, effects));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, radius: -0.1f, speed: 1f, lifetimeTicks: 3, effects));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, radius: 0.1f, speed: -0.1f, lifetimeTicks: 3, effects));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, radius: 0.1f, speed: 1f, lifetimeTicks: 0, effects));
            Assert.Throws<ArgumentNullException>(() => new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, radius: 0.1f, speed: 1f, lifetimeTicks: 3, impactEffects: null));
            Assert.Throws<ArgumentNullException>(() => new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, radius: 0.1f, speed: 1f, lifetimeTicks: 3, impactEffects: new BattleEffectDefinition[] { null }));
        }

        [Test]
        public void BattleEffectDefinition_StoresDamageEffect()
        {
            BattleEffectDefinition effect = BattleEffectDefinition.Damage(3);

            Assert.AreEqual(BattleEffectType.Damage, effect.Type);
            Assert.AreEqual(3, effect.Amount);
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleEffectDefinition.Damage(0));
        }

        [Test]
        public void BattleEffectDefinition_StoresApplyStatusEffect()
        {
            var status = new StatusDefinition("burn", StatusPolarity.Debuff, 3, 1, 2, new BattleModifierDefinition[0], new BattleTriggerDefinition[0]);

            BattleEffectDefinition effect = BattleEffectDefinition.ApplyStatus(status);

            Assert.AreEqual(BattleEffectType.ApplyStatus, effect.Type);
            Assert.AreSame(status, effect.Status);
            Assert.Throws<ArgumentNullException>(() => BattleEffectDefinition.ApplyStatus(null));
        }

        [Test]
        public void ProjectilePayload_StoresAndCopiesImpactEffects()
        {
            var burn = new StatusDefinition("burn", StatusPolarity.Debuff, 3, 1, 2, new BattleModifierDefinition[0], new BattleTriggerDefinition[0]);
            var effects = new[]
            {
                BattleEffectDefinition.Damage(3),
                BattleEffectDefinition.ApplyStatus(burn)
            };

            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, radius: 0.1f, speed: 1f, lifetimeTicks: 3, effects);
            effects[0] = BattleEffectDefinition.Damage(9);

            Assert.AreEqual(2, payload.ImpactEffects.Count);
            Assert.AreEqual(BattleEffectType.Damage, payload.ImpactEffects[0].Type);
            Assert.AreEqual(3, payload.ImpactEffects[0].Amount);
            Assert.AreEqual(BattleEffectType.ApplyStatus, payload.ImpactEffects[1].Type);
            Assert.IsFalse(payload.ImpactEffects is BattleEffectDefinition[]);
        }

        [Test]
        public void ProjectilePayload_StoresHealAndAreaEffectImpactEffects()
        {
            var area = new AreaEffectDefinition(
                BattleScalar.FromFloat(1.25f),
                AreaEffectTargetFilter.Enemies,
                new[] { BattleEffectDefinition.Damage(2), BattleEffectDefinition.Heal(1) });
            BattleEffectDefinition[] effects =
            {
                BattleEffectDefinition.Heal(3),
                BattleEffectDefinition.AreaEffect(area)
            };

            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, 0.1f, 1f, 3, effects);
            effects[0] = BattleEffectDefinition.Damage(9);

            Assert.AreEqual(2, payload.ImpactEffects.Count);
            Assert.AreEqual(BattleEffectType.Heal, payload.ImpactEffects[0].Type);
            Assert.AreEqual(3, payload.ImpactEffects[0].Amount);
            Assert.AreEqual(BattleEffectType.AreaEffect, payload.ImpactEffects[1].Type);
            Assert.AreEqual(2, payload.ImpactEffects[1].Area.Effects.Count);
            Assert.AreEqual(BattleEffectType.AreaEffect, payload.ImpactEffectData[1].Type);
            Assert.AreEqual(AreaEffectTargetFilter.Enemies, payload.ImpactEffectData[1].AreaEffect.TargetFilter);
            Assert.AreEqual(BattleEffectType.Heal, payload.ImpactEffectData[1].AreaEffect.Effects[1].Type);
        }

        [Test]
        public void ProjectileEmitterSpawnData_RejectsInvalidTiming()
        {
            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, 0.1f, 1f, 3, new[] { BattleEffectDefinition.Damage(2) });
            var pattern = ProjectilePattern.Single(new BattleVector2(1f, 0f));

            Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectileEmitterSpawnData(ProjectileEmitterAnchorMode.FollowSource, default, durationTicks: 0, fireIntervalTicks: 1, pattern, payload));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectileEmitterSpawnData(ProjectileEmitterAnchorMode.FollowSource, default, durationTicks: 1, fireIntervalTicks: 0, pattern, payload));
        }

        [Test]
        public void ProjectileEmitterSpawnData_RejectsDefaultNestedData()
        {
            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, 0.1f, 1f, 3, new[] { BattleEffectDefinition.Damage(2) });
            var pattern = ProjectilePattern.Single(new BattleVector2(1f, 0f));

            Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectileEmitterSpawnData(ProjectileEmitterAnchorMode.FollowSource, default, durationTicks: 1, fireIntervalTicks: 1, default, payload));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectileEmitterSpawnData(ProjectileEmitterAnchorMode.FollowSource, default, durationTicks: 1, fireIntervalTicks: 1, pattern, default));
        }

        [Test]
        public void ProjectilePattern_CircleRejectsInvalidCount()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ProjectilePattern.Circle(projectileCount: 0));
        }

        [Test]
        public void ProjectileEmitterComponent_RejectsDefaultNestedData()
        {
            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, 0.1f, 2f, 5, new[] { BattleEffectDefinition.Damage(3) });
            var pattern = ProjectilePattern.Circle(4);

            Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectileEmitterComponent(
                new EntityId(1, 1),
                default,
                new TeamId(1),
                ProjectileEmitterAnchorMode.FollowSource,
                default,
                default,
                durationRemainingTicks: 4,
                fireIntervalTicks: 2,
                ticksUntilNextFire: 0,
                default,
                payload,
                new BattleTick(9)));

            Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectileEmitterComponent(
                new EntityId(1, 1),
                default,
                new TeamId(1),
                ProjectileEmitterAnchorMode.FollowSource,
                default,
                default,
                durationRemainingTicks: 4,
                fireIntervalTicks: 2,
                ticksUntilNextFire: 0,
                pattern,
                default,
                new BattleTick(9)));
        }

        [Test]
        public void ProjectileEmitterComponent_StoresActivationTick()
        {
            var payload = new ProjectilePayload(ProjectileBehavior.Linear, ProjectileHitPolicy.DestroyOnFirstHit, 0.1f, 2f, 5, new[] { BattleEffectDefinition.Damage(3) });
            var component = new ProjectileEmitterComponent(
                new EntityId(1, 1),
                default,
                new TeamId(1),
                ProjectileEmitterAnchorMode.FollowSource,
                new BattleVector2(0.5f, 0f),
                new BattleVector2(2f, 3f),
                durationRemainingTicks: 4,
                fireIntervalTicks: 2,
                ticksUntilNextFire: 0,
                ProjectilePattern.Circle(4),
                payload,
                new BattleTick(9));

            Assert.AreEqual(new BattleTick(9), component.ActivateOnTick);
            Assert.AreEqual(4, component.DurationRemainingTicks);
            Assert.AreEqual(2, component.FireIntervalTicks);
            Assert.AreEqual(0, component.TicksUntilNextFire);
        }
    }
}
