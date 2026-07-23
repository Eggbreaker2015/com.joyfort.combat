using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using Combat.Unity.Authoring;
using Combat.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Combat.Tests.Unity.Authoring
{
    public sealed class BattleAuthoringConverterTests
    {
        private const int AuthoringTestTicksPerSecond = 30;
        private readonly List<ScriptableObject> _assets = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _assets.Count; i++)
            {
                Object.DestroyImmediate(_assets[i]);
            }

            _assets.Clear();
        }

        [Test]
        public void BattleConditionAuthoring_UsesPolymorphicOperandAndFilterConfigs()
        {
            Type operandConfigType = typeof(BattleConditionOperandConfig);
            Assert.IsTrue(operandConfigType.IsClass);
            Assert.IsTrue(operandConfigType.IsAbstract);

            var leftField = typeof(BattleConditionConfig).GetField("_left", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rightField = typeof(BattleConditionConfig).GetField("_right", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(leftField);
            Assert.IsNotNull(rightField);
            Assert.IsTrue(Attribute.IsDefined(leftField, typeof(SerializeReference)));
            Assert.IsTrue(Attribute.IsDefined(rightField, typeof(SerializeReference)));

            AssertConcreteAuthoringTypeExists("Combat.Unity.Authoring.BattleLiteralIntConditionOperandConfig");
            AssertConcreteAuthoringTypeExists("Combat.Unity.Authoring.BattleStatusStackCountConditionOperandConfig");
            AssertConcreteAuthoringTypeExists("Combat.Unity.Authoring.BattleDistanceBetweenConditionOperandConfig");
            AssertConcreteAuthoringTypeExists("Combat.Unity.Authoring.BattleStatusIdConditionFilterConfig");
        }

        [Test]
        public void BuildBattleConfig_FromScenarioAssetCreatesCoreConfig()
        {
            StatusConfigAsset burn = CreateAsset<StatusConfigAsset>();
            SetStatus(burn, "burn", StatusPolarity.Debuff, durationTicks: 6, tickIntervalTicks: 2, periodicDamage: 3);

            AbilityConfigAsset basicSlash = CreateAsset<AbilityConfigAsset>();
            SetAbility(
                basicSlash,
                "basic-slash",
                range: 1.25f,
                damage: 2,
                cooldownTicks: 3,
                appliedStatuses: new[] { burn },
                projectileEmitters: Array.Empty<EmitterSpec>());

            AbilityConfigAsset firebolt = CreateAsset<AbilityConfigAsset>();
            SetAbility(
                firebolt,
                "firebolt",
                range: 4.5f,
                damage: 7,
                cooldownTicks: 12,
                appliedStatuses: new[] { burn },
                projectileEmitters: new[]
                {
                    new EmitterSpec(
                        ProjectileEmitterAnchorMode.FollowSource,
                        new Vector2(0.25f, 0.5f),
                        durationTicks: 9,
                        fireIntervalTicks: 3,
                        ProjectilePatternType.Single,
                        new Vector2(1f, 0f),
                        projectileCount: 1,
                        ProjectileBehavior.Linear,
                        radius: 0.2f,
                        speed: 5f,
                        lifetimeTicks: 30,
                        new[]
                        {
                            DamageImpact(4),
                            ApplyStatusImpact(burn)
                        })
                });

            CombatantConfigAsset mage = CreateAsset<CombatantConfigAsset>();
            SetCombatant(
                mage,
                "mage",
                radius: 0.5f,
                stats: RequiredStats(maxHealth: 25, moveSpeed: 2f),
                basicSlash,
                firebolt);
            SetTargetingBehavior(
                mage,
                acquisitionRange: 4f,
                noProgressTimeoutSeconds: 3f,
                minimumProgressDistance: 0.1f,
                rejectedTargetCooldownSeconds: 1f);

            BattleScenarioAsset scenario = CreateAsset<BattleScenarioAsset>();
            SetScenario(
                scenario,
                ticksPerSecond: AuthoringTestTicksPerSecond,
                maxTicks: 300,
                new SpawnSpec(1, mage, new Vector2(2.5f, -1f)),
                new SpawnSpec(2, mage, new Vector2(-3f, 4f)));
            SetLocalAvoidance(scenario, enabled: true);
            SetProjectileCulling(scenario, enabled: true, center: new Vector2(0f, 0f), size: new Vector2(18f, 10f), padding: 2.5f);

            BattleConfig config = BattleAuthoringConverter.BuildBattleConfig(scenario);

            Assert.AreEqual(AuthoringTestTicksPerSecond, config.TicksPerSecond);
            Assert.AreEqual(300, config.MaxTicks);
            Assert.IsTrue(config.LocalAvoidanceEnabled);
            Assert.IsTrue(config.ProjectileCullingBounds.IsEnabled);
            Assert.AreEqual(new BattleVector2(0f, 0f), config.ProjectileCullingBounds.Center);
            Assert.AreEqual(new BattleVector2(18f, 10f), config.ProjectileCullingBounds.Size);
            Assert.AreEqual(BattleScalar.FromFloat(2.5f), config.ProjectileCullingBounds.Padding);
            Assert.AreEqual(2, config.InitialSpawns.Count);
            Assert.AreEqual(new TeamId(1), config.InitialSpawns[0].TeamId);
            Assert.AreEqual(new BattleVector2(2.5f, -1f), config.InitialSpawns[0].Position);
            Assert.AreEqual(new TeamId(2), config.InitialSpawns[1].TeamId);
            Assert.AreEqual(new BattleVector2(-3f, 4f), config.InitialSpawns[1].Position);
            Assert.AreSame(config.InitialSpawns[0].Definition, config.InitialSpawns[1].Definition);

            CombatantDefinition definition = config.InitialSpawns[0].Definition;
            Assert.AreEqual("mage", definition.Id);
            Assert.AreEqual(BattleScalar.FromFloat(0.5f), definition.Radius);
            Assert.IsTrue(definition.TargetingBehavior.LimitsAcquisitionRange);
            Assert.AreEqual(BattleScalar.FromInt(4), definition.TargetingBehavior.AcquisitionRange);
            Assert.AreEqual(90, definition.TargetingBehavior.NoProgressTimeoutTicks);
            Assert.AreEqual(
                BattleScalar.FromFloat(0.1f),
                definition.TargetingBehavior.MinimumProgressDistance);
            Assert.AreEqual(30, definition.TargetingBehavior.RejectedTargetCooldownTicks);
            Assert.AreEqual(25, definition.Stats.RequireInt(BattleStatId.MaxHealth, definition.Id));
            Assert.AreEqual(2f, definition.Stats.RequireFloat(BattleStatId.MoveSpeed, definition.Id));
            Assert.AreEqual("basic-slash", definition.BasicAbility.Id);
            Assert.AreEqual(BattleScalar.FromFloat(1.25f), definition.BasicAbility.Range);
            Assert.AreEqual(2, AbilityEffects(definition.BasicAbility).Count);
            Assert.AreEqual(BattleEffectType.Damage, AbilityEffects(definition.BasicAbility)[0].Type);
            Assert.AreEqual(2, AbilityEffects(definition.BasicAbility)[0].Amount);
            Assert.AreEqual(3, definition.BasicAbility.CooldownTicks);
            Assert.AreEqual(BattleEffectType.ApplyStatus, AbilityEffects(definition.BasicAbility)[1].Type);
            Assert.AreEqual("burn", AbilityEffects(definition.BasicAbility)[1].Status.Id);

            Assert.AreEqual(1, definition.Abilities.Count);
            AbilityDefinition ability = definition.Abilities[0];
            Assert.AreEqual("firebolt", ability.Id);
            Assert.AreEqual(3, AbilityEffects(ability).Count);
            Assert.AreEqual(BattleEffectType.Damage, AbilityEffects(ability)[0].Type);
            Assert.AreEqual(7, AbilityEffects(ability)[0].Amount);
            Assert.AreEqual(BattleEffectType.ApplyStatus, AbilityEffects(ability)[1].Type);
            Assert.AreEqual("burn", AbilityEffects(ability)[1].Status.Id);
            Assert.AreEqual(BattleEffectType.SpawnProjectileEmitter, AbilityEffects(ability)[2].Type);
            ProjectileEmitterSpawnData emitter = AbilityEffects(ability)[2].ProjectileEmitter;
            Assert.AreEqual(ProjectilePatternType.Single, emitter.Pattern.Type);
            Assert.AreEqual(new BattleVector2(1f, 0f), emitter.Pattern.Direction);
            Assert.AreEqual(ProjectileDirectionMode.FixedDirection, emitter.Pattern.DirectionMode);
            Assert.AreEqual(2, emitter.ProjectilePayload.ImpactEffects.Count);
            Assert.AreEqual(BattleEffectType.Damage, emitter.ProjectilePayload.ImpactEffects[0].Type);
            Assert.AreEqual(4, emitter.ProjectilePayload.ImpactEffects[0].Amount);
            Assert.AreEqual(BattleEffectType.ApplyStatus, emitter.ProjectilePayload.ImpactEffects[1].Type);
            Assert.AreSame(AbilityEffects(ability)[1].Status, emitter.ProjectilePayload.ImpactEffects[1].Status);
        }

        [Test]
        public void BuildCombatantDefinition_UsesAssetNameAsDefinitionId()
        {
            AbilityConfigAsset slash = CreateAsset<AbilityConfigAsset>();
            SetAbility(
                slash,
                "slash",
                range: 1.25f,
                damage: 2,
                cooldownTicks: 3,
                appliedStatuses: Array.Empty<StatusConfigAsset>(),
                projectileEmitters: Array.Empty<EmitterSpec>());

            CombatantConfigAsset combatant = CreateAsset<CombatantConfigAsset>();
            SetCombatant(
                combatant,
                "legacy-melee",
                radius: 0.5f,
                stats: RequiredStats(maxHealth: 25, moveSpeed: 2f),
                slash);
            combatant.name = "DefaultMelee";

            CombatantDefinition definition = BattleAuthoringConverter.BuildCombatantDefinition(combatant);

            Assert.AreEqual("DefaultMelee", definition.Id);
        }

        [Test]
        public void BuildAbilityDefinition_PreservesActionLocks()
        {
            AbilityConfigAsset movingCast = CreateAsset<AbilityConfigAsset>();
            SetAbility(
                movingCast,
                "moving-cast",
                range: 3f,
                damage: 1,
                cooldownTicks: 4,
                appliedStatuses: Array.Empty<StatusConfigAsset>(),
                projectileEmitters: Array.Empty<EmitterSpec>());
            SetAbilityActionLocks(movingCast, BattleActionLocks.StartAnotherAction);

            AbilityDefinition definition = BattleAuthoringConverter.BuildAbilityDefinition(movingCast);

            Assert.AreEqual(BattleActionLocks.StartAnotherAction, definition.ActionLocks);
        }

        [Test]
        public void BuildBattleConfig_RejectsNullScenario()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => BattleAuthoringConverter.BuildBattleConfig(null));

            Assert.AreEqual("scenario", exception.ParamName);
        }

        [Test]
        public void BuildBattleConfig_PreservesProjectileTargetDirectionMode()
        {
            AbilityConfigAsset firebolt = CreateAsset<AbilityConfigAsset>();
            SetAbility(
                firebolt,
                "firebolt",
                range: 4.5f,
                damage: 0,
                cooldownTicks: 12,
                appliedStatuses: Array.Empty<StatusConfigAsset>(),
                projectileEmitters: new[]
                {
                    new EmitterSpec(
                        ProjectileEmitterAnchorMode.FollowSource,
                        Vector2.zero,
                        durationTicks: 1,
                        fireIntervalTicks: 1,
                        ProjectilePatternType.Single,
                        new Vector2(1f, 0f),
                        projectileCount: 1,
                        ProjectileBehavior.Linear,
                        radius: 0.2f,
                        speed: 5f,
                        lifetimeTicks: 30,
                        new[] { DamageImpact(4) },
                        directionMode: ProjectileDirectionMode.TargetDirection)
                });

            AbilityConfigAsset basicSlash = CreateAsset<AbilityConfigAsset>();
            SetAbility(
                basicSlash,
                "basic-slash",
                range: 1f,
                damage: 1,
                cooldownTicks: 3,
                appliedStatuses: Array.Empty<StatusConfigAsset>(),
                projectileEmitters: Array.Empty<EmitterSpec>());

            CombatantConfigAsset mage = CreateAsset<CombatantConfigAsset>();
            SetCombatant(
                mage,
                "mage",
                radius: 0.5f,
                stats: RequiredStats(maxHealth: 25, moveSpeed: 2f),
                basicSlash,
                firebolt);

            BattleScenarioAsset scenario = CreateAsset<BattleScenarioAsset>();
            SetScenario(
                scenario,
                ticksPerSecond: 20,
                maxTicks: 300,
                new SpawnSpec(1, mage, Vector2.zero),
                new SpawnSpec(2, mage, Vector2.right));

            BattleConfig config = BattleAuthoringConverter.BuildBattleConfig(scenario);

            ProjectilePattern pattern = AbilityEffects(config.InitialSpawns[0].Definition.Abilities[0])[0].ProjectileEmitter.Pattern;
            Assert.AreEqual(ProjectilePatternType.Single, pattern.Type);
            Assert.AreEqual(ProjectileDirectionMode.TargetDirection, pattern.DirectionMode);
        }

        [Test]
        public void BuildBattleConfig_ConvertsAuthoringSecondsToCoreTicks()
        {
            StatusConfigAsset burn = CreateAsset<StatusConfigAsset>();
            SetStatusSeconds(burn, "burn", StatusPolarity.Debuff, durationSeconds: 0.25f, tickIntervalSeconds: 0.1f, periodicDamage: 3);

            AbilityConfigAsset basicSlash = CreateAsset<AbilityConfigAsset>();
            SetAbilitySeconds(
                basicSlash,
                "basic-slash",
                range: 1.25f,
                damage: 2,
                cooldownSeconds: 0f,
                windupSeconds: 0f,
                recoverySeconds: 0f,
                appliedStatuses: Array.Empty<StatusConfigAsset>(),
                projectileEmitters: Array.Empty<EmitterSecondsSpec>());

            AbilityConfigAsset firebolt = CreateAsset<AbilityConfigAsset>();
            SetAbilitySeconds(
                firebolt,
                "firebolt",
                range: 4.5f,
                damage: 7,
                cooldownSeconds: 0.35f,
                windupSeconds: 0.2f,
                recoverySeconds: 0.3f,
                appliedStatuses: new[] { burn },
                projectileEmitters: new[]
                {
                    new EmitterSecondsSpec(
                        ProjectileEmitterAnchorMode.FollowSource,
                        new Vector2(0.25f, 0.5f),
                        durationSeconds: 0.15f,
                        fireIntervalSeconds: 0.05f,
                        ProjectilePatternType.Single,
                        new Vector2(1f, 0f),
                        projectileCount: 1,
                        ProjectileBehavior.Linear,
                        radius: 0.2f,
                        speed: 5f,
                        lifetimeSeconds: 1.2f,
                        new[]
                        {
                            DamageImpact(4),
                            ApplyStatusImpact(burn)
                        })
                });

            CombatantConfigAsset mage = CreateAsset<CombatantConfigAsset>();
            SetCombatant(
                mage,
                "mage",
                radius: 0.5f,
                stats: RequiredStats(maxHealth: 25, moveSpeed: 2f),
                basicSlash,
                firebolt);

            BattleScenarioAsset scenario = CreateAsset<BattleScenarioAsset>();
            SetScenarioSeconds(
                scenario,
                ticksPerSecond: 20,
                maxDurationSeconds: 2.5f,
                new SpawnSpec(1, mage, Vector2.zero),
                new SpawnSpec(2, mage, Vector2.right));

            BattleConfig config = BattleAuthoringConverter.BuildBattleConfig(scenario);

            Assert.AreEqual(20, config.TicksPerSecond);
            Assert.AreEqual(50, config.MaxTicks);
            CombatantDefinition definition = config.InitialSpawns[0].Definition;
            Assert.AreEqual(0, definition.BasicAbility.CooldownTicks);
            AbilityDefinition ability = definition.Abilities[0];
            Assert.AreEqual(7, ability.CooldownTicks);
            Assert.AreEqual(4, ability.WindupTicks);
            Assert.AreEqual(6, ability.RecoveryTicks);
            Assert.AreEqual(5, AbilityEffects(ability)[1].Status.DurationTicks);
            Assert.AreEqual(2, AbilityEffects(ability)[1].Status.TickIntervalTicks);
            Assert.AreEqual(3, AbilityEffects(ability)[2].ProjectileEmitter.DurationTicks);
            Assert.AreEqual(1, AbilityEffects(ability)[2].ProjectileEmitter.FireIntervalTicks);
            Assert.AreEqual(24, AbilityEffects(ability)[2].ProjectileEmitter.ProjectilePayload.LifetimeTicks);
        }

        [Test]
        public void BuildBattleConfig_WrapsInvalidScenarioTimingWithAuthoringContext()
        {
            AbilityConfigAsset slash = CreateAsset<AbilityConfigAsset>();
            SetAbility(slash, "slash", range: 1f, damage: 2, cooldownTicks: 3, appliedStatuses: Array.Empty<StatusConfigAsset>(), projectileEmitters: Array.Empty<EmitterSpec>());
            CombatantConfigAsset warrior = CreateAsset<CombatantConfigAsset>();
            SetCombatant(
                warrior,
                "warrior",
                radius: 0.25f,
                stats: RequiredStats(maxHealth: 10, moveSpeed: 1f),
                slash);
            BattleScenarioAsset scenario = CreateAsset<BattleScenarioAsset>();
            SetScenario(scenario, ticksPerSecond: 0, maxTicks: 60, new SpawnSpec(1, warrior, Vector2.zero));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => BattleAuthoringConverter.BuildBattleConfig(scenario));

            Assert.That(exception.Message, Does.Contain("Battle scenario"));
            Assert.IsInstanceOf<ArgumentOutOfRangeException>(exception.InnerException);
        }

        [Test]
        public void BuildBattleConfig_RejectsMissingRequiredCombatantStat()
        {
            AbilityConfigAsset slash = CreateAsset<AbilityConfigAsset>();
            SetAbility(slash, "slash", range: 1f, damage: 2, cooldownTicks: 3, appliedStatuses: Array.Empty<StatusConfigAsset>(), projectileEmitters: Array.Empty<EmitterSpec>());
            CombatantConfigAsset warrior = CreateAsset<CombatantConfigAsset>();
            SetCombatant(
                warrior,
                "warrior",
                radius: 0.25f,
                stats: new[]
                {
                    Stat(BattleStatId.MaxHealth, 10)
                },
                slash);
            BattleScenarioAsset scenario = ScenarioWith(warrior);

            ArgumentException exception = Assert.Throws<ArgumentException>(() => BattleAuthoringConverter.BuildBattleConfig(scenario));

            Assert.That(exception.Message, Does.Contain("warrior"));
            Assert.That(exception.Message, Does.Contain("MoveSpeed"));
        }

        [Test]
        public void BuildBattleConfig_RejectsMissingBasicAbility()
        {
            CombatantConfigAsset warrior = CreateAsset<CombatantConfigAsset>();
            SetCombatant(
                warrior,
                "warrior",
                radius: 0.25f,
                stats: RequiredStats(maxHealth: 10, moveSpeed: 1f),
                basicAbility: null);
            BattleScenarioAsset scenario = ScenarioWith(warrior);

            ArgumentException exception = Assert.Throws<ArgumentException>(() => BattleAuthoringConverter.BuildBattleConfig(scenario));

            Assert.That(exception.Message, Does.Contain("warrior"));
            Assert.That(exception.Message, Does.Contain("basic ability"));
        }

        [Test]
        public void BuildBattleConfig_WrapsOutOfRangeIntegerStatWithCombatantContext()
        {
            AbilityConfigAsset slash = CreateAsset<AbilityConfigAsset>();
            SetAbility(slash, "slash", range: 1f, damage: 2, cooldownTicks: 3, appliedStatuses: Array.Empty<StatusConfigAsset>(), projectileEmitters: Array.Empty<EmitterSpec>());
            CombatantConfigAsset warrior = CreateAsset<CombatantConfigAsset>();
            SetCombatant(
                warrior,
                "warrior",
                radius: 0.25f,
                stats: RequiredStats(maxHealth: 1e20f, moveSpeed: 1f),
                slash);
            BattleScenarioAsset scenario = ScenarioWith(warrior);

            ArgumentException exception = Assert.Throws<ArgumentException>(() => BattleAuthoringConverter.BuildBattleConfig(scenario));

            Assert.That(exception.Message, Does.Contain("warrior"));
            Assert.That(exception.Message, Does.Contain("MaxHealth"));
            Assert.IsNotNull(exception.InnerException);
        }

        [Test]
        public void BuildBattleConfig_RejectsDuplicateCombatantStat()
        {
            AbilityConfigAsset slash = CreateAsset<AbilityConfigAsset>();
            SetAbility(slash, "slash", range: 1f, damage: 2, cooldownTicks: 3, appliedStatuses: Array.Empty<StatusConfigAsset>(), projectileEmitters: Array.Empty<EmitterSpec>());
            CombatantConfigAsset warrior = CreateAsset<CombatantConfigAsset>();
            SetCombatant(
                warrior,
                "warrior",
                radius: 0.25f,
                stats: new[]
                {
                    Stat(BattleStatId.MaxHealth, 10),
                    Stat(BattleStatId.MaxHealth, 12),
                    Stat(BattleStatId.MoveSpeed, 1f)
                },
                slash);
            BattleScenarioAsset scenario = ScenarioWith(warrior);

            ArgumentException exception = Assert.Throws<ArgumentException>(() => BattleAuthoringConverter.BuildBattleConfig(scenario));

            Assert.That(exception.Message, Does.Contain("warrior"));
            Assert.That(exception.Message, Does.Contain("MaxHealth"));
            Assert.That(exception.Message, Does.Contain("duplicate"));
        }

        [Test]
        public void BuildBattleConfig_RejectsNullCombatantReference()
        {
            BattleScenarioAsset scenario = CreateAsset<BattleScenarioAsset>();
            SetScenario(scenario, ticksPerSecond: 30, maxTicks: 60, new SpawnSpec(1, null, Vector2.zero));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => BattleAuthoringConverter.BuildBattleConfig(scenario));

            Assert.That(exception.Message, Does.Contain("initial spawn 0"));
            Assert.That(exception.Message, Does.Contain("combatant"));
        }

        [Test]
        public void BuildBattleConfig_RejectsDuplicateAbilityIdOnCombatant()
        {
            AbilityConfigAsset firstSlash = CreateAsset<AbilityConfigAsset>();
            SetAbility(firstSlash, "slash", range: 1f, damage: 2, cooldownTicks: 3, appliedStatuses: Array.Empty<StatusConfigAsset>(), projectileEmitters: Array.Empty<EmitterSpec>());
            AbilityConfigAsset secondSlash = CreateAsset<AbilityConfigAsset>();
            SetAbility(secondSlash, "slash", range: 2f, damage: 3, cooldownTicks: 4, appliedStatuses: Array.Empty<StatusConfigAsset>(), projectileEmitters: Array.Empty<EmitterSpec>());
            CombatantConfigAsset warrior = CreateAsset<CombatantConfigAsset>();
            SetCombatant(
                warrior,
                "warrior",
                radius: 0.25f,
                stats: RequiredStats(maxHealth: 10, moveSpeed: 1f),
                firstSlash,
                secondSlash);
            BattleScenarioAsset scenario = ScenarioWith(warrior);

            ArgumentException exception = Assert.Throws<ArgumentException>(() => BattleAuthoringConverter.BuildBattleConfig(scenario));

            Assert.That(exception.Message, Does.Contain("warrior"));
            Assert.That(exception.Message, Does.Contain("slash"));
            Assert.That(exception.Message, Does.Contain("duplicate"));
        }

        [Test]
        public void BuildAbilityDefinition_BuildsSpawnProjectileEmitterImpactAuthoring()
        {
            ProjectileEmitterConfigAsset splitBurst = CreateAsset<ProjectileEmitterConfigAsset>();
            SetProjectileEmitterAsset(
                splitBurst,
                new EmitterSpec(
                    ProjectileEmitterAnchorMode.FixedPosition,
                    new Vector2(0.5f, -0.25f),
                    durationTicks: 4,
                    fireIntervalTicks: 2,
                    ProjectilePatternType.Circle,
                    Vector2.right,
                    projectileCount: 6,
                    ProjectileBehavior.Linear,
                    radius: 0.15f,
                    speed: 6f,
                    lifetimeTicks: 8,
                    new[]
                    {
                        DamageImpact(2)
                    }));

            AbilityConfigAsset splitShot = CreateAsset<AbilityConfigAsset>();
            SetAbility(
                splitShot,
                "split-shot",
                range: 4f,
                damage: 0,
                cooldownTicks: 5,
                appliedStatuses: Array.Empty<StatusConfigAsset>(),
                projectileEmitters: new[]
                {
                    new EmitterSpec(
                        ProjectileEmitterAnchorMode.FollowSource,
                        Vector2.zero,
                        durationTicks: 1,
                        fireIntervalTicks: 1,
                        ProjectilePatternType.Single,
                        Vector2.right,
                        projectileCount: 1,
                        ProjectileBehavior.Linear,
                        radius: 0.2f,
                        speed: 3f,
                        lifetimeTicks: 10,
                        new[]
                        {
                            SpawnEmitterImpact(splitBurst)
                        })
                });

            AbilityDefinition ability = BattleAuthoringConverter.BuildAbilityDefinition(splitShot);

            BattleEffectDefinition effect = AbilityEffects(ability)[0].ProjectileEmitter.ProjectilePayload.ImpactEffects[0];
            Assert.AreEqual(BattleEffectType.SpawnProjectileEmitter, effect.Type);
            ProjectileEmitterSpawnData nestedEmitter = effect.ProjectileEmitter;
            Assert.AreEqual(ProjectileEmitterAnchorMode.FixedPosition, nestedEmitter.AnchorMode);
            Assert.AreEqual(new BattleVector2(0.5f, -0.25f), nestedEmitter.AnchorOffset);
            Assert.AreEqual(4, nestedEmitter.DurationTicks);
            Assert.AreEqual(2, nestedEmitter.FireIntervalTicks);
            Assert.AreEqual(ProjectilePatternType.Circle, nestedEmitter.Pattern.Type);
            Assert.AreEqual(6, nestedEmitter.Pattern.ProjectileCount);
            Assert.AreEqual(ProjectileBehavior.Linear, nestedEmitter.ProjectilePayload.Behavior);
            Assert.AreEqual(BattleScalar.FromFloat(0.15f), nestedEmitter.ProjectilePayload.Radius);
            Assert.AreEqual(BattleScalar.FromFloat(6f), nestedEmitter.ProjectilePayload.Speed);
            Assert.AreEqual(8, nestedEmitter.ProjectilePayload.LifetimeTicks);
            Assert.AreEqual(1, nestedEmitter.ProjectilePayload.ImpactEffects.Count);
            Assert.AreEqual(BattleEffectType.Damage, nestedEmitter.ProjectilePayload.ImpactEffects[0].Type);
            Assert.AreEqual(2, nestedEmitter.ProjectilePayload.ImpactEffects[0].Amount);
        }

        [Test]
        public void BuildAbilityDefinition_BuildsTargetSelectionAuthoring()
        {
            AbilityConfigAsset mend = CreateAsset<AbilityConfigAsset>();
            SetAbilityEffects(
                mend,
                "mend",
                range: 5f,
                cooldownTicks: 3,
                new[] { DamageImpact(1) });
            Apply(mend, serialized =>
            {
                serialized.FindProperty("_targetSelection").enumValueIndex = (int)AbilityTargetSelection.LowestHealthAlly;
            });

            AbilityDefinition ability = BattleAuthoringConverter.BuildAbilityDefinition(mend);

            Assert.AreEqual(AbilityTargetSelection.LowestHealthAlly, ability.TargetSelection);
        }

        [Test]
        public void BuildAbilityDefinition_BuildsEffectFrameAuthoring()
        {
            StatusConfigAsset mark = CreateAsset<StatusConfigAsset>();
            SetStatus(mark, "mark", StatusPolarity.Debuff, durationTicks: 5, tickIntervalTicks: 1, periodicDamage: 0);

            AbilityConfigAsset combo = CreateAsset<AbilityConfigAsset>();
            SetAbilityEffectFrames(
                combo,
                "combo",
                range: 2f,
                cooldownTicks: 6,
                windupTicks: 2,
                recoveryTicks: 3,
                new[]
                {
                    new AbilityEffectFrameSpec(
                        "hit_02",
                        timeTicks: 4,
                        order: 1,
                        new[] { DamageImpact(7) }),
                    new AbilityEffectFrameSpec(
                        "hit_01",
                        timeTicks: 2,
                        order: 0,
                        new[] { DamageImpact(3), ApplyStatusImpact(mark) })
                });

            AbilityDefinition ability = BattleAuthoringConverter.BuildAbilityDefinition(combo);

            Assert.AreEqual("combo", ability.Id);
            Assert.AreEqual(2, ability.WindupTicks);
            Assert.AreEqual(3, ability.RecoveryTicks);
            Assert.AreEqual(2, ability.EffectFrames.Count);
            Assert.AreEqual("hit_01", ability.EffectFrames[0].FrameId);
            Assert.AreEqual(2, ability.EffectFrames[0].TickOffset);
            Assert.AreEqual(0, ability.EffectFrames[0].Order);
            Assert.AreEqual(2, ability.EffectFrames[0].Effects.Count);
            Assert.AreEqual(3, ability.EffectFrames[0].Effects[0].Amount);
            Assert.AreEqual("mark", ability.EffectFrames[0].Effects[1].Status.Id);
            Assert.AreEqual("hit_02", ability.EffectFrames[1].FrameId);
            Assert.AreEqual(4, ability.EffectFrames[1].TickOffset);
            Assert.AreEqual(1, ability.EffectFrames[1].Order);
            Assert.AreEqual(1, ability.EffectFrames[1].Effects.Count);
            Assert.AreEqual(7, ability.EffectFrames[1].Effects[0].Amount);
        }

        [Test]
        public void BuildAbilityDefinition_ConvertsStandaloneTimingWithDefaultTicksPerSecond()
        {
            AbilityConfigAsset slash = CreateAsset<AbilityConfigAsset>();
            SetAbilitySeconds(
                slash,
                "slash",
                range: 1f,
                damage: 2,
                cooldownSeconds: 0.1f,
                windupSeconds: 0.2f,
                recoverySeconds: 0.3f,
                appliedStatuses: Array.Empty<StatusConfigAsset>(),
                projectileEmitters: Array.Empty<EmitterSecondsSpec>());

            AbilityDefinition ability = BattleAuthoringConverter.BuildAbilityDefinition(slash);

            Assert.AreEqual(3, ability.CooldownTicks);
            Assert.AreEqual(6, ability.WindupTicks);
            Assert.AreEqual(9, ability.RecoveryTicks);
        }

        [TestCase(0.1f, 30, 3)]
        [TestCase(0.100001f, 30, 4)]
        [TestCase(0.000001f, 30, 1)]
        public void ConvertPositiveSecondsToTicks_UsesSharedCeilingPolicy(
            float seconds, int ticksPerSecond, int expectedTicks)
        {
            Assert.That(
                BattleAuthoringConverter.ConvertPositiveSecondsToTicks(
                    seconds, ticksPerSecond, "seconds"),
                Is.EqualTo(expectedTicks));
        }

        [Test]
        public void BuildCombatantDefinition_UsesExplicitTicksPerSecondForNestedAbilityTiming()
        {
            AbilityConfigAsset slash = CreateAsset<AbilityConfigAsset>();
            SetAbilitySeconds(
                slash, "slash", 1f, 2, 0.25f, 0f, 0f,
                Array.Empty<StatusConfigAsset>(), Array.Empty<EmitterSecondsSpec>());
            CombatantConfigAsset combatant = CreateAsset<CombatantConfigAsset>();
            SetCombatant(
                combatant, "unit", 0.25f, RequiredStats(10, 1f), slash);

            CombatantDefinition definition =
                BattleAuthoringConverter.BuildCombatantDefinition(combatant, 20);

            Assert.That(definition.BasicAbility.CooldownTicks, Is.EqualTo(5));
        }

        [Test]
        public void BuildAbilityDefinition_BuildsAreaEffectHealAuthoring()
        {
            AreaEffectConfigAsset area = CreateAsset<AreaEffectConfigAsset>();
            SetAreaEffectAsset(
                area,
                radius: 2.5f,
                AreaEffectTargetFilter.Allies,
                new[]
                {
                    HealImpact(4),
                    DamageImpact(1)
                });

            AbilityConfigAsset groupHeal = CreateAsset<AbilityConfigAsset>();
            SetAbilityEffects(
                groupHeal,
                "group-heal",
                range: 3f,
                cooldownTicks: 5,
                new[]
                {
                    AreaEffectImpact(area)
                });

            AbilityDefinition ability = BattleAuthoringConverter.BuildAbilityDefinition(groupHeal);

            Assert.AreEqual(1, AbilityEffects(ability).Count);
            Assert.AreEqual(BattleEffectType.AreaEffect, AbilityEffects(ability)[0].Type);
            AreaEffectDefinition areaDefinition = AbilityEffects(ability)[0].Area;
            Assert.AreEqual(BattleScalar.FromFloat(2.5f), areaDefinition.Radius);
            Assert.AreEqual(AreaEffectTargetFilter.Allies, areaDefinition.TargetFilter);
            Assert.AreEqual(2, areaDefinition.Effects.Count);
            Assert.AreEqual(BattleEffectType.Heal, areaDefinition.Effects[0].Type);
            Assert.AreEqual(4, areaDefinition.Effects[0].Amount);
            Assert.AreEqual(BattleEffectType.Damage, areaDefinition.Effects[1].Type);
            Assert.AreEqual(1, areaDefinition.Effects[1].Amount);
        }

        [Test]
        public void BuildAbilityDefinition_RejectsDirectHealWithoutExplicitTargetContext()
        {
            AbilityConfigAsset groupHeal = CreateAsset<AbilityConfigAsset>();
            SetAbilityEffects(
                groupHeal,
                "direct-heal",
                range: 3f,
                cooldownTicks: 5,
                new[]
                {
                    HealImpact(3)
                });

            ArgumentException exception = Assert.Throws<ArgumentException>(() => BattleAuthoringConverter.BuildAbilityDefinition(groupHeal));

            Assert.That(exception.Message, Does.Contain("Heal"));
            Assert.That(exception.Message, Does.Contain("explicit target context"));
            Assert.That(exception.Message, Does.Contain("AreaEffect"));
            Assert.That(exception.Message, Does.Contain("status reaction"));
        }

        [Test]
        public void BuildAbilityDefinition_AllowsDirectHealWithLowestHealthAllyTargetSelection()
        {
            AbilityConfigAsset mend = CreateAsset<AbilityConfigAsset>();
            SetAbilityEffects(
                mend,
                "direct-mend",
                range: 3f,
                cooldownTicks: 5,
                new[]
                {
                    HealImpact(3)
                });
            Apply(mend, serialized =>
            {
                serialized.FindProperty("_targetSelection").enumValueIndex = (int)AbilityTargetSelection.LowestHealthAlly;
            });

            AbilityDefinition ability = BattleAuthoringConverter.BuildAbilityDefinition(mend);

            Assert.AreEqual(AbilityTargetSelection.LowestHealthAlly, ability.TargetSelection);
            Assert.AreEqual(1, AbilityEffects(ability).Count);
            Assert.AreEqual(BattleEffectType.Heal, AbilityEffects(ability)[0].Type);
            Assert.AreEqual(3, AbilityEffects(ability)[0].Amount);
        }

        [Test]
        public void BuildAbilityDefinition_AllowsDirectHealWithSelfTargetSelection()
        {
            AbilityConfigAsset focus = CreateAsset<AbilityConfigAsset>();
            SetAbilityEffects(
                focus,
                "self-heal",
                range: 0f,
                cooldownTicks: 5,
                new[]
                {
                    HealImpact(3)
                });
            Apply(focus, serialized =>
            {
                serialized.FindProperty("_targetSelection").enumValueIndex = (int)AbilityTargetSelection.Self;
            });

            AbilityDefinition ability = BattleAuthoringConverter.BuildAbilityDefinition(focus);

            Assert.AreEqual(AbilityTargetSelection.Self, ability.TargetSelection);
            Assert.AreEqual(1, AbilityEffects(ability).Count);
            Assert.AreEqual(BattleEffectType.Heal, AbilityEffects(ability)[0].Type);
            Assert.AreEqual(3, AbilityEffects(ability)[0].Amount);
        }

        [Test]
        public void BuildAbilityDefinition_RejectsProjectileImpactHealWithoutExplicitTargetContext()
        {
            ProjectileEmitterConfigAsset emitter = CreateAsset<ProjectileEmitterConfigAsset>();
            SetProjectileEmitterAsset(
                emitter,
                new EmitterSpec(
                    ProjectileEmitterAnchorMode.FixedPosition,
                    Vector2.zero,
                    durationTicks: 1,
                    fireIntervalTicks: 1,
                    ProjectilePatternType.Single,
                    Vector2.right,
                    projectileCount: 1,
                    ProjectileBehavior.Linear,
                    radius: 0.1f,
                    speed: 3f,
                    lifetimeTicks: 8,
                    new[]
                    {
                        HealImpact(2)
                    }));
            AbilityConfigAsset ability = CreateAsset<AbilityConfigAsset>();
            SetAbilityEffects(
                ability,
                "heal-projectile",
                range: 3f,
                cooldownTicks: 5,
                new[]
                {
                    SpawnEmitterImpact(emitter)
                });

            ArgumentException exception = Assert.Throws<ArgumentException>(() => BattleAuthoringConverter.BuildAbilityDefinition(ability));

            Assert.That(exception.Message, Does.Contain("Heal"));
            Assert.That(exception.Message, Does.Contain("explicit target context"));
            Assert.That(exception.Message, Does.Contain("AreaEffect"));
            Assert.That(exception.Message, Does.Contain("status reaction"));
        }

        [Test]
        public void BuildAbilityDefinition_RejectsAreaChildProjectileImpactAreaEffectAuthoring()
        {
            AreaEffectConfigAsset impactArea = CreateAsset<AreaEffectConfigAsset>();
            SetAreaEffectAsset(
                impactArea,
                radius: 1f,
                AreaEffectTargetFilter.Enemies,
                new[]
                {
                    DamageImpact(1)
                });

            ProjectileEmitterConfigAsset areaChildEmitter = CreateAsset<ProjectileEmitterConfigAsset>();
            SetProjectileEmitterAsset(
                areaChildEmitter,
                new EmitterSpec(
                    ProjectileEmitterAnchorMode.FixedPosition,
                    Vector2.zero,
                    durationTicks: 1,
                    fireIntervalTicks: 1,
                    ProjectilePatternType.Single,
                    Vector2.right,
                    projectileCount: 1,
                    ProjectileBehavior.Linear,
                    radius: 0.1f,
                    speed: 3f,
                    lifetimeTicks: 8,
                    new[]
                    {
                        AreaEffectImpact(impactArea)
                    }));

            AreaEffectConfigAsset sourceArea = CreateAsset<AreaEffectConfigAsset>();
            SetAreaEffectAsset(
                sourceArea,
                radius: 2f,
                AreaEffectTargetFilter.Enemies,
                new[]
                {
                    SpawnEmitterImpact(areaChildEmitter)
                });

            AbilityConfigAsset ability = CreateAsset<AbilityConfigAsset>();
            SetAbilityEffects(
                ability,
                "area-child-projectile-area",
                range: 3f,
                cooldownTicks: 5,
                new[]
                {
                    AreaEffectImpact(sourceArea)
                });

            ArgumentException exception = Assert.Throws<ArgumentException>(() => BattleAuthoringConverter.BuildAbilityDefinition(ability));

            Assert.That(exception.Message, Does.Contain("Projectile impact effect 0"));
            Assert.That(exception.Message, Does.Contain("AreaEffect has a nested AreaEffect reference"));
        }

        [Test]
        public void BuildStatusDefinition_AllowsDirectHealReactionWithExplicitTargetContext()
        {
            StatusConfigAsset thorns = CreateAsset<StatusConfigAsset>();
            SetStatus(
                thorns,
                "healing-thorns",
                StatusPolarity.Buff,
                durationTicks: 5,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                triggers: new[]
                {
                    new TriggerSpec(
                        BattleTriggerTiming.AfterDamageTaken,
                        HealReaction(BattleReactionTarget.Self, 3))
                });

            StatusDefinition status = BattleAuthoringConverter.BuildStatusDefinition(thorns);

            Assert.AreEqual(StatusStackPolicy.RefreshDurationAndAddStack, status.StackPolicy);
            Assert.AreEqual(1, status.Triggers.Count);
            Assert.AreEqual(BattleEffectType.Heal, status.Triggers[0].Effects[0].Effect.Type);
            Assert.AreEqual(3, status.Triggers[0].Effects[0].Effect.Amount);
            Assert.AreEqual(BattleReactionTarget.Self, status.Triggers[0].Effects[0].Target);
        }

        [Test]
        public void BuildAbilityDefinition_RejectsAreaEffectWithoutAreaReference()
        {
            AbilityConfigAsset broken = CreateAsset<AbilityConfigAsset>();
            SetAbilityEffects(
                broken,
                "broken-area",
                range: 3f,
                cooldownTicks: 5,
                new[]
                {
                    AreaEffectImpact(null)
                });

            ArgumentException exception = Assert.Throws<ArgumentException>(() => BattleAuthoringConverter.BuildAbilityDefinition(broken));

            Assert.That(exception.Message, Does.Contain("AreaEffect"));
            Assert.That(exception.Message, Does.Contain("area"));
            Assert.That(exception.Message, Does.Contain("reference"));
        }

        [Test]
        public void BuildAbilityDefinition_RejectsRecursiveAreaEffectAuthoring()
        {
            AreaEffectConfigAsset recursiveArea = CreateAsset<AreaEffectConfigAsset>();
            SetAreaEffectAsset(
                recursiveArea,
                radius: 1f,
                AreaEffectTargetFilter.Enemies,
                new[]
                {
                    AreaEffectImpact(recursiveArea)
                });

            AbilityConfigAsset broken = CreateAsset<AbilityConfigAsset>();
            SetAbilityEffects(
                broken,
                "recursive-area",
                range: 3f,
                cooldownTicks: 5,
                new[]
                {
                    AreaEffectImpact(recursiveArea)
                });

            ArgumentException exception = Assert.Throws<ArgumentException>(() => BattleAuthoringConverter.BuildAbilityDefinition(broken));

            Assert.That(exception.Message, Does.Contain("AreaEffect"));
            Assert.That(exception.Message, Does.Match("recursive|nested AreaEffect"));
        }

        [Test]
        public void BuildAbilityDefinition_BuildsStatusTriggerReactionEffects()
        {
            StatusConfigAsset mark = CreateAsset<StatusConfigAsset>();
            SetStatus(mark, "mark", StatusPolarity.Debuff, durationTicks: 2, tickIntervalTicks: 1, periodicDamage: 0);
            StatusConfigAsset thorns = CreateAsset<StatusConfigAsset>();
            SetStatus(
                thorns,
                "thorns",
                StatusPolarity.Buff,
                durationTicks: 5,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                triggers: new[]
                {
                    new TriggerSpec(
                        BattleTriggerTiming.AfterDamageTaken,
                        DamageReaction(BattleReactionTarget.Source, 3),
                        ApplyStatusReaction(BattleReactionTarget.Target, mark))
                });
            AbilityConfigAsset counterStance = CreateAsset<AbilityConfigAsset>();
            SetAbility(counterStance, "counter-stance", range: 1f, damage: 0, cooldownTicks: 4, appliedStatuses: new[] { thorns, mark }, projectileEmitters: Array.Empty<EmitterSpec>());

            AbilityDefinition ability = BattleAuthoringConverter.BuildAbilityDefinition(counterStance);

            Assert.AreEqual(2, AbilityEffects(ability).Count);
            StatusDefinition status = AbilityEffects(ability)[0].Status;
            Assert.AreEqual("thorns", status.Id);
            Assert.AreEqual(1, status.Triggers.Count);
            BattleTriggerDefinition trigger = status.Triggers[0];
            Assert.AreEqual(BattleTriggerTiming.AfterDamageTaken, trigger.Timing);
            Assert.AreEqual(2, trigger.Effects.Count);
            Assert.AreEqual(BattleEffectType.Damage, trigger.Effects[0].Effect.Type);
            Assert.AreEqual(BattleReactionTarget.Source, trigger.Effects[0].Target);
            Assert.AreEqual(3, trigger.Effects[0].Effect.Amount);
            Assert.AreEqual(BattleEffectType.ApplyStatus, trigger.Effects[1].Effect.Type);
            Assert.AreEqual(BattleReactionTarget.Target, trigger.Effects[1].Target);
            Assert.AreSame(AbilityEffects(ability)[1].Status, trigger.Effects[1].Effect.Status);
        }

        [Test]
        public void BuildStatusDefinition_ConvertsStatusTriggerConditions()
        {
            StatusConfigAsset burn = CreateAsset<StatusConfigAsset>();
            SetStatus(burn, "burn", StatusPolarity.Debuff, durationTicks: 3, tickIntervalTicks: 1, periodicDamage: 1);
            StatusConfigAsset execute = CreateAsset<StatusConfigAsset>();
            SetStatus(
                execute,
                "execute",
                StatusPolarity.Buff,
                durationTicks: 5,
                tickIntervalTicks: 5,
                periodicDamage: 0,
                triggers: new[]
                {
                    new TriggerSpec(
                        BattleTriggerTiming.AfterDamageDealt,
                        BattleConditionMatchMode.All,
                        new[]
                        {
                            ConditionSpec.Compare(
                                OperandSpec.HealthPercent(BattleConditionSubject.Source),
                                BattleConditionComparison.LessOrEqual,
                                OperandSpec.LiteralPercent(20f)),
                            ConditionSpec.Compare(
                                OperandSpec.StatusCount(BattleConditionSubject.Target, StatusFilterSpec.StatusId(burn)),
                                BattleConditionComparison.GreaterOrEqual,
                                OperandSpec.LiteralInt(1)),
                            ConditionSpec.Compare(
                                OperandSpec.StatusCount(BattleConditionSubject.Target, StatusFilterSpec.PolarityFilter(StatusPolarity.Debuff)),
                                BattleConditionComparison.GreaterOrEqual,
                                OperandSpec.LiteralInt(1)),
                            ConditionSpec.Compare(
                                OperandSpec.StatusStackCount(BattleConditionSubject.Target, StatusFilterSpec.StatusId(burn)),
                                BattleConditionComparison.GreaterOrEqual,
                                OperandSpec.LiteralInt(2)),
                            ConditionSpec.Compare(
                                OperandSpec.StatValue(BattleConditionSubject.Source, BattleStatId.MoveSpeed),
                                BattleConditionComparison.GreaterOrEqual,
                                OperandSpec.LiteralScalar(1f)),
                            ConditionSpec.Compare(
                                OperandSpec.DistanceBetween(BattleConditionSubject.Source, BattleConditionSubject.Target),
                                BattleConditionComparison.LessOrEqual,
                                OperandSpec.LiteralScalar(5f))
                        },
                        DamageReaction(BattleReactionTarget.Target, 2))
                });

            StatusDefinition definition = BattleAuthoringConverter.BuildStatusDefinition(execute);

            BattleTriggerDefinition trigger = definition.Triggers[0];
            BattleConditionProgram program = trigger.ConditionProgram;
            Assert.IsNotNull(program);
            Assert.IsFalse(program.IsAlwaysTrue);
            IReadOnlyList<BattleConditionInstruction> instructions = program.Instructions;
            IReadOnlyList<BattleConditionOperandData> operands = program.Operands;
            IReadOnlyList<BattleStatusConditionFilterData> filters = program.StatusFilters;

            BattleConditionInstruction root = instructions[program.RootInstructionIndex];
            Assert.AreEqual(BattleConditionInstructionOp.All, root.Op);
            Assert.AreEqual(0, root.FirstChildInstructionIndex);
            Assert.AreEqual(6, root.ChildCount);
            Assert.AreEqual(3, filters.Count);

            BattleConditionInstruction healthCondition = instructions[root.FirstChildInstructionIndex];
            Assert.AreEqual(BattleConditionInstructionOp.Compare, healthCondition.Op);
            Assert.AreEqual(BattleConditionComparison.LessOrEqual, healthCondition.Comparison);
            BattleConditionOperandData healthOperand = operands[healthCondition.LeftOperandIndex];
            Assert.AreEqual(BattleConditionOperandOp.HealthPercent, healthOperand.Op);
            Assert.AreEqual(BattleConditionSubject.Source, healthOperand.Subject);
            BattleConditionOperandData percentOperand = operands[healthCondition.RightOperandIndex];
            Assert.AreEqual(BattleConditionOperandOp.LiteralScalar, percentOperand.Op);
            Assert.AreEqual(BattleScalar.FromInt(2000) / BattleScalar.FromInt(10000), percentOperand.ScalarValue);

            BattleConditionInstruction statusCondition = instructions[root.FirstChildInstructionIndex + 1];
            Assert.AreEqual(BattleConditionInstructionOp.Compare, statusCondition.Op);
            Assert.AreEqual(BattleConditionComparison.GreaterOrEqual, statusCondition.Comparison);
            BattleConditionOperandData statusCountOperand = operands[statusCondition.LeftOperandIndex];
            Assert.AreEqual(BattleConditionOperandOp.StatusCount, statusCountOperand.Op);
            Assert.AreEqual(BattleConditionSubject.Target, statusCountOperand.Subject);
            BattleStatusConditionFilterData statusIdFilter = filters[statusCountOperand.StatusFilterIndex];
            Assert.AreEqual(BattleStatusConditionFilterOp.StatusId, statusIdFilter.Op);
            Assert.AreEqual("burn", statusIdFilter.StatusId);
            BattleConditionOperandData statusLiteralOperand = operands[statusCondition.RightOperandIndex];
            Assert.AreEqual(BattleConditionOperandOp.LiteralInt, statusLiteralOperand.Op);
            Assert.AreEqual(1, statusLiteralOperand.IntValue);

            BattleConditionInstruction polarityCondition = instructions[root.FirstChildInstructionIndex + 2];
            Assert.AreEqual(BattleConditionInstructionOp.Compare, polarityCondition.Op);
            Assert.AreEqual(BattleConditionComparison.GreaterOrEqual, polarityCondition.Comparison);
            BattleConditionOperandData polarityCountOperand = operands[polarityCondition.LeftOperandIndex];
            Assert.AreEqual(BattleConditionOperandOp.StatusCount, polarityCountOperand.Op);
            Assert.AreEqual(BattleConditionSubject.Target, polarityCountOperand.Subject);
            BattleStatusConditionFilterData polarityFilter = filters[polarityCountOperand.StatusFilterIndex];
            Assert.AreEqual(BattleStatusConditionFilterOp.Polarity, polarityFilter.Op);
            Assert.AreEqual(StatusPolarity.Debuff, polarityFilter.Polarity);
            BattleConditionOperandData polarityLiteralOperand = operands[polarityCondition.RightOperandIndex];
            Assert.AreEqual(BattleConditionOperandOp.LiteralInt, polarityLiteralOperand.Op);
            Assert.AreEqual(1, polarityLiteralOperand.IntValue);

            BattleConditionInstruction stackCondition = instructions[root.FirstChildInstructionIndex + 3];
            BattleConditionOperandData stackOperand = operands[stackCondition.LeftOperandIndex];
            Assert.AreEqual(BattleConditionOperandOp.StatusStackCount, stackOperand.Op);
            Assert.AreEqual(BattleConditionSubject.Target, stackOperand.Subject);
            BattleStatusConditionFilterData stackFilter = filters[stackOperand.StatusFilterIndex];
            Assert.AreEqual(BattleStatusConditionFilterOp.StatusId, stackFilter.Op);
            Assert.AreEqual("burn", stackFilter.StatusId);

            BattleConditionInstruction statCondition = instructions[root.FirstChildInstructionIndex + 4];
            BattleConditionOperandData statOperand = operands[statCondition.LeftOperandIndex];
            Assert.AreEqual(BattleConditionOperandOp.StatValue, statOperand.Op);
            Assert.AreEqual(BattleConditionSubject.Source, statOperand.Subject);
            Assert.AreEqual(BattleStatId.MoveSpeed, statOperand.Stat);

            BattleConditionInstruction distanceCondition = instructions[root.FirstChildInstructionIndex + 5];
            BattleConditionOperandData distanceOperand = operands[distanceCondition.LeftOperandIndex];
            Assert.AreEqual(BattleConditionOperandOp.DistanceBetween, distanceOperand.Op);
            Assert.AreEqual(BattleConditionSubject.Source, distanceOperand.Subject);
            Assert.AreEqual(BattleConditionSubject.Target, distanceOperand.OtherSubject);
        }

        [Test]
        public void BuildStatusDefinition_ConvertsMaxStacksAndAfterEnemyKilledTrigger()
        {
            StatusConfigAsset buff = CreateAsset<StatusConfigAsset>();
            SetStatus(
                buff,
                "kill-stack",
                StatusPolarity.Buff,
                durationTicks: 5,
                tickIntervalTicks: 5,
                periodicDamage: 0,
                maxStacks: 5);
            StatusConfigAsset triggerStatus = CreateAsset<StatusConfigAsset>();
            SetStatus(
                triggerStatus,
                "kill-trigger",
                StatusPolarity.Buff,
                durationTicks: 60,
                tickIntervalTicks: 60,
                periodicDamage: 0,
                maxStacks: 1,
                triggers: new[]
                {
                    new TriggerSpec(
                        BattleTriggerTiming.AfterEnemyKilled,
                        ApplyStatusReaction(BattleReactionTarget.Self, buff))
                });

            StatusDefinition definition = BattleAuthoringConverter.BuildStatusDefinition(triggerStatus);

            Assert.AreEqual(1, definition.MaxStacks);
            Assert.AreEqual(1, definition.Triggers.Count);
            Assert.AreEqual(BattleTriggerTiming.AfterEnemyKilled, definition.Triggers[0].Timing);
            Assert.IsTrue(definition.Triggers[0].ConditionProgram.IsAlwaysTrue);
            Assert.AreEqual(1, definition.Triggers[0].Effects.Count);
            Assert.AreEqual(BattleReactionTarget.Self, definition.Triggers[0].Effects[0].Target);
            Assert.AreEqual(5, definition.Triggers[0].Effects[0].Effect.Status.MaxStacks);
        }

        [Test]
        public void BuildStatusDefinition_ConvertsStatModifier()
        {
            StatusConfigAsset haste = CreateAsset<StatusConfigAsset>();
            SetStatus(
                haste,
                "haste",
                StatusPolarity.Buff,
                durationTicks: 5,
                tickIntervalTicks: 5,
                periodicDamage: 0,
                modifiers: new[]
                {
                    new BattleModifierConfig(
                        BattleModifierTarget.Stat,
                        BattleStatId.MoveSpeed,
                        default,
                        BattleModifierOperation.PercentAdd,
                        -0.25f)
                });

            StatusDefinition definition = BattleAuthoringConverter.BuildStatusDefinition(haste);

            Assert.AreEqual(1, definition.Modifiers.Count);
            Assert.AreEqual(BattleModifierTarget.Stat, definition.Modifiers[0].Target);
            Assert.AreEqual(BattleStatId.MoveSpeed, definition.Modifiers[0].StatId);
            Assert.AreEqual(BattleModifierOperation.PercentAdd, definition.Modifiers[0].Operation);
            Assert.AreEqual(BattleScalar.FromFloat(-0.25f), definition.Modifiers[0].Value);
        }

        [Test]
        public void BuildStatusDefinition_ConvertsMaxHealthStatModifier()
        {
            StatusConfigAsset fortitude = CreateAsset<StatusConfigAsset>();
            SetStatus(
                fortitude,
                "max-health-modifier",
                StatusPolarity.Buff,
                durationTicks: 5,
                tickIntervalTicks: 5,
                periodicDamage: 0,
                modifiers: new[]
                {
                    new BattleModifierConfig(
                        BattleModifierTarget.Stat,
                        BattleStatId.MaxHealth,
                        default,
                        BattleModifierOperation.Flat,
                        1f)
                });

            StatusDefinition definition = BattleAuthoringConverter.BuildStatusDefinition(fortitude);

            Assert.AreEqual(1, definition.Modifiers.Count);
            Assert.AreEqual(BattleModifierTarget.Stat, definition.Modifiers[0].Target);
            Assert.AreEqual(BattleStatId.MaxHealth, definition.Modifiers[0].StatId);
            Assert.AreEqual(BattleModifierOperation.Flat, definition.Modifiers[0].Operation);
            Assert.AreEqual(BattleScalar.One, definition.Modifiers[0].Value);
        }

        [Test]
        public void BuildStatusDefinition_ConvertsDamageModifier()
        {
            StatusConfigAsset rage = CreateAsset<StatusConfigAsset>();
            SetStatus(
                rage,
                "rage",
                StatusPolarity.Buff,
                durationTicks: 5,
                tickIntervalTicks: 5,
                periodicDamage: 0,
                modifiers: new[]
                {
                    new BattleModifierConfig(
                        BattleModifierTarget.Damage,
                        default,
                        BattleDamageModifierStat.DamageDealt,
                        BattleModifierOperation.Flat,
                        1f)
                });

            StatusDefinition definition = BattleAuthoringConverter.BuildStatusDefinition(rage);

            Assert.AreEqual(1, definition.Modifiers.Count);
            Assert.AreEqual(BattleModifierTarget.Damage, definition.Modifiers[0].Target);
            Assert.AreEqual(BattleDamageModifierStat.DamageDealt, definition.Modifiers[0].DamageStat);
            Assert.AreEqual(BattleModifierOperation.Flat, definition.Modifiers[0].Operation);
            Assert.AreEqual(BattleScalar.FromInt(1), definition.Modifiers[0].Value);
        }

        [Test]
        public void BuildStatusDefinition_RejectsUnsupportedModifierTargetWithStatusContext()
        {
            StatusConfigAsset invalid = CreateAsset<StatusConfigAsset>();
            SetStatus(
                invalid,
                "invalid-target",
                StatusPolarity.Buff,
                durationTicks: 5,
                tickIntervalTicks: 5,
                periodicDamage: 0,
                modifiers: new[]
                {
                    new BattleModifierConfig(
                        BattleModifierTarget.Damage,
                        default,
                        BattleDamageModifierStat.DamageDealt,
                        BattleModifierOperation.Flat,
                        1f)
                });
            Apply(invalid, serialized =>
            {
                SerializedProperty modifier = serialized.FindProperty("_modifiers").GetArrayElementAtIndex(0);
                modifier.FindPropertyRelative("_target").intValue = 999;
            });

            ArgumentException exception = Assert.Throws<ArgumentException>(() => BattleAuthoringConverter.BuildStatusDefinition(invalid));

            StringAssert.Contains("Status 'invalid-target' modifier 0", exception.Message);
            StringAssert.Contains("Unsupported battle modifier target", exception.Message);
        }

        [Test]
        public void BuildStatusDefinition_RejectsInvalidStatModifierStatIdWithStatusContext()
        {
            StatusConfigAsset invalid = CreateAsset<StatusConfigAsset>();
            SetStatus(
                invalid,
                "invalid-stat-modifier",
                StatusPolarity.Buff,
                durationTicks: 5,
                tickIntervalTicks: 5,
                periodicDamage: 0,
                modifiers: new[]
                {
                    new BattleModifierConfig(
                        BattleModifierTarget.Stat,
                        BattleStatId.MoveSpeed,
                        default,
                        BattleModifierOperation.Flat,
                        1f)
                });
            Apply(invalid, serialized =>
            {
                SerializedProperty modifier = serialized.FindProperty("_modifiers").GetArrayElementAtIndex(0);
                modifier.FindPropertyRelative("_statId").intValue = 999;
            });

            ArgumentException exception = Assert.Throws<ArgumentException>(() => BattleAuthoringConverter.BuildStatusDefinition(invalid));

            StringAssert.Contains("Status 'invalid-stat-modifier' modifier 0", exception.Message);
            StringAssert.Contains("unsupported stat modifier id", exception.Message);
        }

        [Test]
        public void BuildStatusDefinition_RejectsInvalidDamageModifierStatWithStatusContext()
        {
            StatusConfigAsset invalid = CreateAsset<StatusConfigAsset>();
            SetStatus(
                invalid,
                "invalid-damage-modifier",
                StatusPolarity.Buff,
                durationTicks: 5,
                tickIntervalTicks: 5,
                periodicDamage: 0,
                modifiers: new[]
                {
                    new BattleModifierConfig(
                        BattleModifierTarget.Damage,
                        default,
                        BattleDamageModifierStat.DamageDealt,
                        BattleModifierOperation.Flat,
                        1f)
                });
            Apply(invalid, serialized =>
            {
                SerializedProperty modifier = serialized.FindProperty("_modifiers").GetArrayElementAtIndex(0);
                modifier.FindPropertyRelative("_damageStat").intValue = 999;
            });

            ArgumentException exception = Assert.Throws<ArgumentException>(() => BattleAuthoringConverter.BuildStatusDefinition(invalid));

            StringAssert.Contains("Status 'invalid-damage-modifier' modifier 0", exception.Message);
            StringAssert.Contains("Unsupported battle damage modifier stat", exception.Message);
        }

        [Test]
        public void BuildStatusDefinition_RejectsNullModifierWithStatusContext()
        {
            StatusConfigAsset invalid = CreateAsset<StatusConfigAsset>();
            SetStatus(
                invalid,
                "null-modifier",
                StatusPolarity.Buff,
                durationTicks: 5,
                tickIntervalTicks: 5,
                periodicDamage: 0);
            typeof(StatusConfigAsset)
                .GetField("_modifiers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(invalid, new BattleModifierConfig[] { null });

            ArgumentException exception = Assert.Throws<ArgumentException>(() => BattleAuthoringConverter.BuildStatusDefinition(invalid));

            StringAssert.Contains("Status 'null-modifier' modifier 0", exception.Message);
            StringAssert.Contains("is missing", exception.Message);
        }

        [Test]
        public void DemoKillFuryStance_AppliesKillTriggerThatStacksAttackBuff()
        {
            StatusConfigAsset attackStackAsset = CreateAsset<StatusConfigAsset>();
            StatusConfigAsset triggerAsset = CreateAsset<StatusConfigAsset>();
            AbilityConfigAsset stance = CreateAsset<AbilityConfigAsset>();
            InvokeInstallerConfigure("ConfigureKillAttackStack", attackStackAsset);
            InvokeInstallerConfigure("ConfigureKillFury", triggerAsset, attackStackAsset);
            InvokeInstallerConfigure("ConfigureKillFuryStance", stance, triggerAsset);

            AbilityDefinition ability = BattleAuthoringConverter.BuildAbilityDefinition(stance);

            Assert.AreEqual("kill-fury-stance", ability.Id);
            Assert.AreEqual(AbilityTargetSelection.Self, ability.TargetSelection);
            Assert.AreEqual(BattleScalar.Zero, ability.Range);
            Assert.AreEqual(30, ability.CooldownTicks);
            Assert.AreEqual(1, AbilityEffects(ability).Count);
            Assert.AreEqual(BattleEffectType.ApplyStatus, AbilityEffects(ability)[0].Type);
            StatusDefinition triggerStatus = AbilityEffects(ability)[0].Status;
            Assert.AreEqual("kill-fury", triggerStatus.Id);
            Assert.AreEqual(StatusPolarity.Buff, triggerStatus.Polarity);
            Assert.AreEqual(1, triggerStatus.Triggers.Count);
            Assert.AreEqual(BattleTriggerTiming.AfterEnemyKilled, triggerStatus.Triggers[0].Timing);
            Assert.AreEqual(BattleReactionTarget.Self, triggerStatus.Triggers[0].Effects[0].Target);

            StatusDefinition attackStack = triggerStatus.Triggers[0].Effects[0].Effect.Status;
            Assert.AreEqual("kill-attack-stack", attackStack.Id);
            Assert.AreEqual(StatusPolarity.Buff, attackStack.Polarity);
            Assert.AreEqual(150, attackStack.DurationTicks);
            Assert.AreEqual(5, attackStack.MaxStacks);
            Assert.AreEqual(1, attackStack.Modifiers.Count);
            Assert.AreEqual(BattleModifierTarget.Damage, attackStack.Modifiers[0].Target);
            Assert.AreEqual(BattleDamageModifierStat.DamageDealt, attackStack.Modifiers[0].DamageStat);
            Assert.AreEqual(BattleModifierOperation.Flat, attackStack.Modifiers[0].Operation);
            Assert.AreEqual(BattleScalar.FromInt(1), attackStack.Modifiers[0].Value);
        }

        private static void InvokeInstallerConfigure(string name, params object[] arguments)
        {
            System.Reflection.MethodInfo method = typeof(DemoScenarioInstaller).GetMethod(
                name,
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing DemoScenarioInstaller." + name);
            method.Invoke(null, arguments);
        }

        [Test]
        public void BuildStatusDefinition_RejectsApplyStatusReactionWithoutStatusReference()
        {
            StatusConfigAsset thorns = CreateAsset<StatusConfigAsset>();
            SetStatus(
                thorns,
                "thorns",
                StatusPolarity.Buff,
                durationTicks: 5,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                triggers: new[]
                {
                    new TriggerSpec(
                        BattleTriggerTiming.AfterDamageTaken,
                        ApplyStatusReaction(BattleReactionTarget.Source, null))
                });

            ArgumentException exception = Assert.Throws<ArgumentException>(() => BattleAuthoringConverter.BuildStatusDefinition(thorns));

            Assert.That(exception.Message, Does.Contain("thorns"));
            Assert.That(exception.Message, Does.Contain("trigger 0"));
            Assert.That(exception.Message, Does.Contain("reaction effect 0"));
            Assert.That(exception.Message, Does.Contain("ApplyStatus"));
            Assert.That(exception.Message, Does.Contain("status reference"));
        }

        [Test]
        public void BuildStatusDefinition_RejectsRecursiveStatusTriggerReference()
        {
            StatusConfigAsset loop = CreateAsset<StatusConfigAsset>();
            SetStatus(loop, "loop", StatusPolarity.Neutral, durationTicks: 2, tickIntervalTicks: 1, periodicDamage: 0);
            SetStatus(
                loop,
                "loop",
                StatusPolarity.Neutral,
                durationTicks: 2,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                triggers: new[]
                {
                    new TriggerSpec(
                        BattleTriggerTiming.AfterDamageTaken,
                        ApplyStatusReaction(BattleReactionTarget.Self, loop))
                });

            ArgumentException exception = Assert.Throws<ArgumentException>(() => BattleAuthoringConverter.BuildStatusDefinition(loop));

            Assert.That(exception.Message, Does.Contain("loop"));
            Assert.That(exception.Message, Does.Contain("recursive"));
        }

        [Test]
        public void BuildProjectileEmitterSpawnData_ConvertsStandaloneProjectileEmitterAsset()
        {
            ProjectileEmitterConfigAsset emitter = CreateAsset<ProjectileEmitterConfigAsset>();
            SetProjectileEmitterAsset(
                emitter,
                new EmitterSpec(
                    ProjectileEmitterAnchorMode.FixedPosition,
                    new Vector2(0.25f, 0.5f),
                    durationTicks: 3,
                    fireIntervalTicks: 1,
                    ProjectilePatternType.Circle,
                    Vector2.right,
                    projectileCount: 4,
                    ProjectileBehavior.Linear,
                    radius: 0.2f,
                    speed: 3f,
                    lifetimeTicks: 9,
                    new[] { DamageImpact(2) }));

            ProjectileEmitterSpawnData data = BattleAuthoringConverter.BuildProjectileEmitterSpawnData(emitter);

            Assert.AreEqual(ProjectileEmitterAnchorMode.FixedPosition, data.AnchorMode);
            Assert.AreEqual(new BattleVector2(0.25f, 0.5f), data.AnchorOffset);
            Assert.AreEqual(3, data.DurationTicks);
            Assert.AreEqual(1, data.FireIntervalTicks);
            Assert.AreEqual(ProjectilePatternType.Circle, data.Pattern.Type);
            Assert.AreEqual(4, data.Pattern.ProjectileCount);
            Assert.AreEqual(ProjectileBehavior.Linear, data.ProjectilePayload.Behavior);
            Assert.AreEqual(BattleScalar.FromFloat(0.2f), data.ProjectilePayload.Radius);
            Assert.AreEqual(BattleScalar.FromFloat(3f), data.ProjectilePayload.Speed);
            Assert.AreEqual(9, data.ProjectilePayload.LifetimeTicks);
            Assert.AreEqual(BattleEffectType.Damage, data.ProjectilePayload.ImpactEffects[0].Type);
            Assert.AreEqual(2, data.ProjectilePayload.ImpactEffects[0].Amount);
        }

        [Test]
        public void BuildProjectileEmitterSpawnData_RejectsInvalidAnchorMode()
        {
            ProjectileEmitterConfigAsset emitter = CreateValidStandaloneProjectileEmitterAsset();
            Apply(emitter, serialized =>
            {
                serialized.FindProperty("_anchorMode").intValue = 999;
            });

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => BattleAuthoringConverter.BuildProjectileEmitterSpawnData(emitter));

            Assert.That(exception.Message, Does.Contain("anchorMode"));
            Assert.That(exception.Message, Does.Contain("ProjectileEmitterAnchorMode"));
        }

        [Test]
        public void BuildProjectileEmitterSpawnData_RejectsInvalidProjectileBehavior()
        {
            ProjectileEmitterConfigAsset emitter = CreateValidStandaloneProjectileEmitterAsset();
            Apply(emitter.Projectile, serialized =>
            {
                serialized.FindProperty("_behavior").intValue = 999;
            });

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => BattleAuthoringConverter.BuildProjectileEmitterSpawnData(emitter));

            Assert.That(exception.Message, Does.Contain("projectile.behavior"));
            Assert.That(exception.Message, Does.Contain("ProjectileBehavior"));
        }

        [Test]
        public void BuildProjectileEmitterSpawnData_RejectsInvalidProjectileDirectionMode()
        {
            ProjectileEmitterConfigAsset emitter = CreateValidStandaloneProjectileEmitterAsset();
            Apply(emitter, serialized =>
            {
                serialized.FindProperty("_pattern").FindPropertyRelative("_directionMode").intValue = 999;
            });

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => BattleAuthoringConverter.BuildProjectileEmitterSpawnData(emitter));

            Assert.That(exception.Message, Does.Contain("pattern.directionMode"));
            Assert.That(exception.Message, Does.Contain("ProjectileDirectionMode"));
        }

        [Test]
        public void BuildProjectileEmitterSpawnData_RejectsInvalidProjectileDirectionModeOnCirclePattern()
        {
            ProjectileEmitterConfigAsset emitter = CreateValidStandaloneProjectileEmitterAsset();
            Apply(emitter, serialized =>
            {
                SerializedProperty patternProperty = serialized.FindProperty("_pattern");
                patternProperty.FindPropertyRelative("_type").enumValueIndex = (int)ProjectilePatternType.Circle;
                patternProperty.FindPropertyRelative("_directionMode").intValue = 999;
                patternProperty.FindPropertyRelative("_projectileCount").intValue = 4;
            });

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => BattleAuthoringConverter.BuildProjectileEmitterSpawnData(emitter));

            Assert.That(exception.Message, Does.Contain("pattern.directionMode"));
            Assert.That(exception.Message, Does.Contain("ProjectileDirectionMode"));
        }

        [Test]
        public void BuildProjectileEmitterSpawnData_RejectsInvalidProjectilePatternType()
        {
            ProjectileEmitterConfigAsset emitter = CreateValidStandaloneProjectileEmitterAsset();
            Apply(emitter, serialized =>
            {
                serialized.FindProperty("_pattern").FindPropertyRelative("_type").intValue = 999;
            });

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => BattleAuthoringConverter.BuildProjectileEmitterSpawnData(emitter));

            Assert.That(exception.Message, Does.Contain("pattern.type"));
            Assert.That(exception.Message, Does.Contain("ProjectilePatternType"));
        }

        [Test]
        public void BuildProjectileEmitterSpawnData_RejectsFixedSingleProjectileZeroDirection()
        {
            ProjectileEmitterConfigAsset emitter = CreateValidStandaloneProjectileEmitterAsset();
            Apply(emitter, serialized =>
            {
                SerializedProperty patternProperty = serialized.FindProperty("_pattern");
                patternProperty.FindPropertyRelative("_type").enumValueIndex = (int)ProjectilePatternType.Single;
                patternProperty.FindPropertyRelative("_directionMode").enumValueIndex = (int)ProjectileDirectionMode.FixedDirection;
                patternProperty.FindPropertyRelative("_direction").vector2Value = Vector2.zero;
            });

            ArgumentException exception = Assert.Catch<ArgumentException>(
                () => BattleAuthoringConverter.BuildProjectileEmitterSpawnData(emitter));

            Assert.That(exception.Message, Does.Match("direction|non-zero"));
        }

        [TestCase(0f)]
        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void BuildProjectileEmitterSpawnData_RejectsInvalidProjectileRadius(float radius)
        {
            ProjectileEmitterConfigAsset emitter = CreateValidStandaloneProjectileEmitterAsset();
            Apply(emitter.Projectile, serialized =>
            {
                serialized.FindProperty("_radius").floatValue = radius;
            });

            ArgumentException exception = Assert.Catch<ArgumentException>(
                () => BattleAuthoringConverter.BuildProjectileEmitterSpawnData(emitter));

            Assert.That(exception.Message, Does.Contain("radius"));
        }

        [TestCase(0f)]
        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void BuildProjectileEmitterSpawnData_RejectsInvalidProjectileSpeed(float speed)
        {
            ProjectileEmitterConfigAsset emitter = CreateValidStandaloneProjectileEmitterAsset();
            Apply(emitter.Projectile, serialized =>
            {
                serialized.FindProperty("_speed").floatValue = speed;
            });

            ArgumentException exception = Assert.Catch<ArgumentException>(
                () => BattleAuthoringConverter.BuildProjectileEmitterSpawnData(emitter));

            Assert.That(exception.Message, Does.Contain("speed"));
        }

        [Test]
        public void BuildProjectileEmitterSpawnData_UsesReferencedProjectileConfigAsset()
        {
            ProjectileConfigAsset projectile = CreateAsset<ProjectileConfigAsset>();
            Apply(projectile, serialized =>
            {
                serialized.FindProperty("_behavior").enumValueIndex = (int)ProjectileBehavior.Linear;
                serialized.FindProperty("_hitPolicyMode").enumValueIndex =
                    (int)ProjectileHitPolicyMode.Pierce;
                serialized.FindProperty("_maxHitCount").intValue = 3;
                serialized.FindProperty("_radius").floatValue = 0.35f;
                serialized.FindProperty("_speed").floatValue = 7f;
                serialized.FindProperty("_lifetimeSeconds").floatValue = 0.5f;
                SerializedProperty effects = serialized.FindProperty("_impactEffects");
                effects.arraySize = 1;
                SetBattleEffect(effects.GetArrayElementAtIndex(0), DamageImpact(6));
            });

            ProjectileEmitterConfigAsset emitter = CreateAsset<ProjectileEmitterConfigAsset>();
            Apply(emitter, serialized =>
            {
                serialized.FindProperty("_anchorMode").enumValueIndex = (int)ProjectileEmitterAnchorMode.FixedPosition;
                serialized.FindProperty("_durationSeconds").floatValue = 1f / AuthoringTestTicksPerSecond;
                serialized.FindProperty("_fireIntervalSeconds").floatValue = 1f / AuthoringTestTicksPerSecond;
                SerializedProperty pattern = serialized.FindProperty("_pattern");
                pattern.FindPropertyRelative("_type").enumValueIndex = (int)ProjectilePatternType.Single;
                pattern.FindPropertyRelative("_directionMode").enumValueIndex = (int)ProjectileDirectionMode.FixedDirection;
                pattern.FindPropertyRelative("_direction").vector2Value = Vector2.right;
                pattern.FindPropertyRelative("_projectileCount").intValue = 1;
                serialized.FindProperty("_projectile").objectReferenceValue = projectile;
            });

            ProjectileEmitterSpawnData data = BattleAuthoringConverter.BuildProjectileEmitterSpawnData(emitter);

            Assert.AreEqual(BattleScalar.FromFloat(0.35f), data.ProjectilePayload.Radius);
            Assert.AreEqual(BattleScalar.FromFloat(7f), data.ProjectilePayload.Speed);
            Assert.AreEqual(ProjectileHitPolicyMode.Pierce, data.ProjectilePayload.HitPolicy.Mode);
            Assert.AreEqual(3, data.ProjectilePayload.HitPolicy.MaxHitCount);
            Assert.AreEqual(15, data.ProjectilePayload.LifetimeTicks);
            Assert.AreEqual(6, data.ProjectilePayload.ImpactEffects[0].Amount);
        }

        [Test]
        public void BuildProjectileEmitterSpawnData_RejectsEmptyImpactEffects()
        {
            ProjectileEmitterConfigAsset emitter = CreateValidStandaloneProjectileEmitterAsset();
            Apply(emitter.Projectile, serialized =>
            {
                serialized.FindProperty("_impactEffects").arraySize = 0;
            });

            ArgumentException exception = Assert.Catch<ArgumentException>(
                () => BattleAuthoringConverter.BuildProjectileEmitterSpawnData(emitter));

            Assert.That(exception.Message, Does.Contain("impactEffects"));
        }

        [Test]
        public void BuildProjectileEmitterSpawnData_RejectsInvalidPierceHitCount()
        {
            ProjectileEmitterConfigAsset emitter = CreateValidStandaloneProjectileEmitterAsset();
            Apply(emitter.Projectile, serialized =>
            {
                serialized.FindProperty("_hitPolicyMode").enumValueIndex =
                    (int)ProjectileHitPolicyMode.Pierce;
                serialized.FindProperty("_maxHitCount").intValue = 1;
            });

            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    BattleAuthoringConverter.BuildProjectileEmitterSpawnData(emitter));

            Assert.That(exception.Message, Does.Contain("hitPolicy.maxHitCount"));
        }

        [Test]
        public void BuildProjectileEmitterSpawnData_RejectsInvalidHitPolicyMode()
        {
            ProjectileEmitterConfigAsset emitter = CreateValidStandaloneProjectileEmitterAsset();
            Apply(emitter.Projectile, serialized =>
                serialized.FindProperty("_hitPolicyMode").intValue = 999);

            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    BattleAuthoringConverter.BuildProjectileEmitterSpawnData(emitter));

            Assert.That(exception.Message, Does.Contain("hitPolicy.mode"));
            Assert.That(exception.Message, Does.Contain("ProjectileHitPolicyMode"));
        }

        [Test]
        public void BuildAreaEffectDefinition_ConvertsStandaloneAreaEffectAsset()
        {
            AreaEffectConfigAsset area = CreateAsset<AreaEffectConfigAsset>();
            SetAreaEffectAsset(
                area,
                radius: 1.75f,
                AreaEffectTargetFilter.Allies,
                new[] { HealImpact(3) });

            AreaEffectDefinition definition = BattleAuthoringConverter.BuildAreaEffectDefinition(area);

            Assert.AreEqual(BattleScalar.FromFloat(1.75f), definition.Radius);
            Assert.AreEqual(AreaEffectTargetFilter.Allies, definition.TargetFilter);
            Assert.AreEqual(BattleEffectType.Heal, definition.Effects[0].Type);
            Assert.AreEqual(3, definition.Effects[0].Amount);
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }

        private ProjectileEmitterConfigAsset CreateValidStandaloneProjectileEmitterAsset()
        {
            ProjectileEmitterConfigAsset emitter = CreateAsset<ProjectileEmitterConfigAsset>();
            SetProjectileEmitterAsset(
                emitter,
                new EmitterSpec(
                    ProjectileEmitterAnchorMode.FixedPosition,
                    Vector2.zero,
                    durationTicks: 3,
                    fireIntervalTicks: 1,
                    ProjectilePatternType.Single,
                    Vector2.right,
                    projectileCount: 1,
                    ProjectileBehavior.Linear,
                    radius: 0.2f,
                    speed: 3f,
                    lifetimeTicks: 9,
                    new[] { DamageImpact(2) }));
            return emitter;
        }

        private BattleScenarioAsset ScenarioWith(CombatantConfigAsset combatant)
        {
            BattleScenarioAsset scenario = CreateAsset<BattleScenarioAsset>();
            SetScenario(scenario, ticksPerSecond: 30, maxTicks: 60, new SpawnSpec(1, combatant, Vector2.zero));
            return scenario;
        }

        private static StatSpec[] RequiredStats(float maxHealth, float moveSpeed)
        {
            return new[]
            {
                Stat(BattleStatId.MaxHealth, maxHealth),
                Stat(BattleStatId.MoveSpeed, moveSpeed)
            };
        }

        private static StatSpec Stat(BattleStatId stat, float value)
        {
            return new StatSpec(stat, value);
        }

        private static void SetScenario(BattleScenarioAsset scenario, int ticksPerSecond, int maxTicks, params SpawnSpec[] spawns)
        {
            Apply(scenario, serialized =>
            {
                serialized.FindProperty("_ticksPerSecond").intValue = ticksPerSecond;
                serialized.FindProperty("_maxDurationSeconds").floatValue = ticksPerSecond > 0 ? TicksToSeconds(maxTicks, ticksPerSecond) : maxTicks;

                SerializedProperty spawnsProperty = serialized.FindProperty("_initialSpawns");
                spawnsProperty.arraySize = spawns.Length;
                for (var i = 0; i < spawns.Length; i++)
                {
                    SerializedProperty spawnProperty = spawnsProperty.GetArrayElementAtIndex(i);
                    spawnProperty.FindPropertyRelative("_teamId").intValue = spawns[i].TeamId;
                    spawnProperty.FindPropertyRelative("_combatant").objectReferenceValue = spawns[i].Combatant;
                    spawnProperty.FindPropertyRelative("_position").vector2Value = spawns[i].Position;
                }
            });
        }

        private static void SetScenarioSeconds(BattleScenarioAsset scenario, int ticksPerSecond, float maxDurationSeconds, params SpawnSpec[] spawns)
        {
            Apply(scenario, serialized =>
            {
                serialized.FindProperty("_ticksPerSecond").intValue = ticksPerSecond;
                serialized.FindProperty("_maxDurationSeconds").floatValue = maxDurationSeconds;

                SerializedProperty spawnsProperty = serialized.FindProperty("_initialSpawns");
                spawnsProperty.arraySize = spawns.Length;
                for (var i = 0; i < spawns.Length; i++)
                {
                    SerializedProperty spawnProperty = spawnsProperty.GetArrayElementAtIndex(i);
                    spawnProperty.FindPropertyRelative("_teamId").intValue = spawns[i].TeamId;
                    spawnProperty.FindPropertyRelative("_combatant").objectReferenceValue = spawns[i].Combatant;
                    spawnProperty.FindPropertyRelative("_position").vector2Value = spawns[i].Position;
                }
            });
        }

        private static void SetProjectileCulling(BattleScenarioAsset scenario, bool enabled, Vector2 center, Vector2 size, float padding)
        {
            Apply(scenario, serialized =>
            {
                serialized.FindProperty("_projectileCullingEnabled").boolValue = enabled;
                serialized.FindProperty("_projectileCullingCenter").vector2Value = center;
                serialized.FindProperty("_projectileCullingSize").vector2Value = size;
                serialized.FindProperty("_projectileCullingPadding").floatValue = padding;
            });
        }

        private static void SetLocalAvoidance(
            BattleScenarioAsset scenario,
            bool enabled)
        {
            Apply(scenario, serialized =>
            {
                serialized.FindProperty("_localAvoidanceEnabled").boolValue = enabled;
            });
        }

        private static void SetCombatant(CombatantConfigAsset combatant, string id, float radius, StatSpec[] stats, AbilityConfigAsset basicAbility, params AbilityConfigAsset[] abilities)
        {
            combatant.name = id;
            Apply(combatant, serialized =>
            {
                serialized.FindProperty("_radius").floatValue = radius;
                serialized.FindProperty("_basicAbility").objectReferenceValue = basicAbility;

                SerializedProperty statsProperty = serialized.FindProperty("_stats");
                statsProperty.arraySize = stats.Length;
                for (var i = 0; i < stats.Length; i++)
                {
                    SerializedProperty statProperty = statsProperty.GetArrayElementAtIndex(i);
                    statProperty.FindPropertyRelative("_stat").enumValueIndex = (int)stats[i].Stat;
                    statProperty.FindPropertyRelative("_value").floatValue = stats[i].Value;
                }

                SerializedProperty abilitiesProperty = serialized.FindProperty("_abilities");
                abilitiesProperty.arraySize = abilities.Length;
                for (var i = 0; i < abilities.Length; i++)
                {
                    abilitiesProperty.GetArrayElementAtIndex(i).objectReferenceValue = abilities[i];
                }
            });
        }

        private static void SetTargetingBehavior(
            CombatantConfigAsset combatant,
            float acquisitionRange,
            float noProgressTimeoutSeconds,
            float minimumProgressDistance,
            float rejectedTargetCooldownSeconds)
        {
            Apply(combatant, serialized =>
            {
                serialized.FindProperty("_targetingBehaviorEnabled").boolValue = true;
                serialized.FindProperty("_targetAcquisitionRange").floatValue = acquisitionRange;
                serialized.FindProperty("_noProgressTimeoutSeconds").floatValue =
                    noProgressTimeoutSeconds;
                serialized.FindProperty("_minimumProgressDistance").floatValue =
                    minimumProgressDistance;
                serialized.FindProperty("_rejectedTargetCooldownSeconds").floatValue =
                    rejectedTargetCooldownSeconds;
            });
        }

        private void SetAbility(
            AbilityConfigAsset ability,
            string id,
            float range,
            int damage,
            int cooldownTicks,
            StatusConfigAsset[] appliedStatuses,
            EmitterSpec[] projectileEmitters)
        {
            Apply(ability, serialized =>
            {
                serialized.FindProperty("_id").stringValue = id;
                serialized.FindProperty("_range").floatValue = range;
                serialized.FindProperty("_cooldownSeconds").floatValue = TicksToSeconds(cooldownTicks);

                SetAbilityReleaseFrame(serialized, BuildAbilityEffects(damage, appliedStatuses, projectileEmitters));
            });
        }

        private static void SetAbilityActionLocks(AbilityConfigAsset ability, BattleActionLocks actionLocks)
        {
            Apply(ability, serialized =>
            {
                serialized.FindProperty("_actionLocks").intValue = (int)actionLocks;
            });
        }

        private void SetAbilitySeconds(
            AbilityConfigAsset ability,
            string id,
            float range,
            int damage,
            float cooldownSeconds,
            float windupSeconds,
            float recoverySeconds,
            StatusConfigAsset[] appliedStatuses,
            EmitterSecondsSpec[] projectileEmitters)
        {
            Apply(ability, serialized =>
            {
                serialized.FindProperty("_id").stringValue = id;
                serialized.FindProperty("_range").floatValue = range;
                serialized.FindProperty("_cooldownSeconds").floatValue = cooldownSeconds;
                serialized.FindProperty("_windupSeconds").floatValue = windupSeconds;
                serialized.FindProperty("_recoverySeconds").floatValue = recoverySeconds;

                SetAbilityReleaseFrame(serialized, BuildAbilityEffects(damage, appliedStatuses, projectileEmitters), windupSeconds);
            });
        }

        private static void SetAbilityEffects(
            AbilityConfigAsset ability,
            string id,
            float range,
            int cooldownTicks,
            IReadOnlyList<BattleEffectConfig> effects)
        {
            Apply(ability, serialized =>
            {
                serialized.FindProperty("_id").stringValue = id;
                serialized.FindProperty("_range").floatValue = range;
                serialized.FindProperty("_cooldownSeconds").floatValue = TicksToSeconds(cooldownTicks);
                SetAbilityReleaseFrame(serialized, effects);
            });
        }

        private static void SetAbilityEffectFrames(
            AbilityConfigAsset ability,
            string id,
            float range,
            int cooldownTicks,
            int windupTicks,
            int recoveryTicks,
            IReadOnlyList<AbilityEffectFrameSpec> frames)
        {
            Apply(ability, serialized =>
            {
                serialized.FindProperty("_id").stringValue = id;
                serialized.FindProperty("_range").floatValue = range;
                serialized.FindProperty("_cooldownSeconds").floatValue = TicksToSeconds(cooldownTicks);
                serialized.FindProperty("_windupSeconds").floatValue = TicksToSeconds(windupTicks);
                serialized.FindProperty("_recoverySeconds").floatValue = TicksToSeconds(recoveryTicks);
                SerializedProperty framesProperty = serialized.FindProperty("_effectFrames");
                framesProperty.arraySize = frames.Count;
                for (var i = 0; i < frames.Count; i++)
                {
                    SetAbilityEffectFrame(framesProperty.GetArrayElementAtIndex(i), frames[i]);
                }
            });
        }

        private static void SetAbilityEffectFrame(SerializedProperty frameProperty, AbilityEffectFrameSpec frame)
        {
            frameProperty.FindPropertyRelative("_frameId").stringValue = frame.FrameId;
            frameProperty.FindPropertyRelative("_timeSeconds").floatValue = TicksToSeconds(frame.TimeTicks);
            frameProperty.FindPropertyRelative("_order").intValue = frame.Order;
            SetBattleEffects(frameProperty.FindPropertyRelative("_effects"), frame.Effects);
        }

        private static void SetAbilityReleaseFrame(SerializedObject serialized, IReadOnlyList<BattleEffectConfig> effects, float timeSeconds = 0f)
        {
            SerializedProperty framesProperty = serialized.FindProperty("_effectFrames");
            framesProperty.arraySize = 1;

            SerializedProperty frameProperty = framesProperty.GetArrayElementAtIndex(0);
            frameProperty.FindPropertyRelative("_frameId").stringValue = "release";
            frameProperty.FindPropertyRelative("_timeSeconds").floatValue = timeSeconds;
            frameProperty.FindPropertyRelative("_order").intValue = 0;
            SetBattleEffects(frameProperty.FindPropertyRelative("_effects"), effects);
        }

        private BattleEffectConfig[] BuildAbilityEffects(int damage, StatusConfigAsset[] appliedStatuses, EmitterSpec[] projectileEmitters)
        {
            var effects = new List<BattleEffectConfig>();
            if (damage > 0)
            {
                effects.Add(DamageImpact(damage));
            }

            for (var i = 0; i < appliedStatuses.Length; i++)
            {
                effects.Add(ApplyStatusImpact(appliedStatuses[i]));
            }

            for (var i = 0; i < projectileEmitters.Length; i++)
            {
                ProjectileEmitterConfigAsset emitterAsset = CreateAsset<ProjectileEmitterConfigAsset>();
                SetProjectileEmitterAsset(emitterAsset, projectileEmitters[i]);
                effects.Add(SpawnEmitterImpact(emitterAsset));
            }

            return effects.ToArray();
        }

        private static IReadOnlyList<BattleEffectDefinition> AbilityEffects(AbilityDefinition ability)
        {
            return ability.EffectFrames[0].Effects;
        }

        private BattleEffectConfig[] BuildAbilityEffects(int damage, StatusConfigAsset[] appliedStatuses, EmitterSecondsSpec[] projectileEmitters)
        {
            var effects = new List<BattleEffectConfig>();
            if (damage > 0)
            {
                effects.Add(DamageImpact(damage));
            }

            for (var i = 0; i < appliedStatuses.Length; i++)
            {
                effects.Add(ApplyStatusImpact(appliedStatuses[i]));
            }

            for (var i = 0; i < projectileEmitters.Length; i++)
            {
                ProjectileEmitterConfigAsset emitterAsset = CreateAsset<ProjectileEmitterConfigAsset>();
                SetProjectileEmitterAsset(emitterAsset, projectileEmitters[i]);
                effects.Add(SpawnEmitterImpact(emitterAsset));
            }

            return effects.ToArray();
        }

        private static void SetStatus(
            StatusConfigAsset status,
            string id,
            StatusPolarity polarity,
            int durationTicks,
            int tickIntervalTicks,
            int periodicDamage,
            int maxStacks = 1,
            TriggerSpec[] triggers = null,
            BattleModifierConfig[] modifiers = null)
        {
            Apply(status, serialized =>
            {
                serialized.FindProperty("_id").stringValue = id;
                serialized.FindProperty("_polarity").enumValueIndex = (int)polarity;
                serialized.FindProperty("_durationSeconds").floatValue = TicksToSeconds(durationTicks);
                serialized.FindProperty("_tickIntervalSeconds").floatValue = TicksToSeconds(tickIntervalTicks);
                serialized.FindProperty("_periodicDamage").intValue = periodicDamage;
                serialized.FindProperty("_maxStacks").intValue = maxStacks;
                SetModifiers(serialized.FindProperty("_modifiers"), modifiers);

                SerializedProperty triggersProperty = serialized.FindProperty("_triggers");
                triggersProperty.arraySize = triggers == null ? 0 : triggers.Length;
                for (var i = 0; i < triggersProperty.arraySize; i++)
                {
                    SetTrigger(triggersProperty.GetArrayElementAtIndex(i), triggers[i]);
                }
            });
        }

        private static void SetModifiers(SerializedProperty modifiersProperty, BattleModifierConfig[] modifiers)
        {
            modifiersProperty.arraySize = modifiers == null ? 0 : modifiers.Length;
            for (var i = 0; i < modifiersProperty.arraySize; i++)
            {
                BattleModifierConfig modifier = modifiers[i];
                SerializedProperty modifierProperty = modifiersProperty.GetArrayElementAtIndex(i);
                modifierProperty.FindPropertyRelative("_target").enumValueIndex = (int)modifier.Target;
                modifierProperty.FindPropertyRelative("_statId").enumValueIndex = (int)modifier.StatId;
                modifierProperty.FindPropertyRelative("_damageStat").enumValueIndex = (int)modifier.DamageStat;
                modifierProperty.FindPropertyRelative("_operation").enumValueIndex = (int)modifier.Operation;
                modifierProperty.FindPropertyRelative("_value").floatValue = modifier.Value;
            }
        }

        private static void SetStatusSeconds(
            StatusConfigAsset status,
            string id,
            StatusPolarity polarity,
            float durationSeconds,
            float tickIntervalSeconds,
            int periodicDamage,
            int maxStacks = 1,
            TriggerSpec[] triggers = null)
        {
            Apply(status, serialized =>
            {
                serialized.FindProperty("_id").stringValue = id;
                serialized.FindProperty("_polarity").enumValueIndex = (int)polarity;
                serialized.FindProperty("_durationSeconds").floatValue = durationSeconds;
                serialized.FindProperty("_tickIntervalSeconds").floatValue = tickIntervalSeconds;
                serialized.FindProperty("_periodicDamage").intValue = periodicDamage;
                serialized.FindProperty("_maxStacks").intValue = maxStacks;
                serialized.FindProperty("_modifiers").arraySize = 0;

                SerializedProperty triggersProperty = serialized.FindProperty("_triggers");
                triggersProperty.arraySize = triggers == null ? 0 : triggers.Length;
                for (var i = 0; i < triggersProperty.arraySize; i++)
                {
                    SetTrigger(triggersProperty.GetArrayElementAtIndex(i), triggers[i]);
                }
            });
        }

        private static void SetTrigger(SerializedProperty triggerProperty, TriggerSpec trigger)
        {
            triggerProperty.FindPropertyRelative("_timing").enumValueIndex = (int)trigger.Timing;
            triggerProperty.FindPropertyRelative("_conditionMatchMode").enumValueIndex = (int)trigger.ConditionMatchMode;
            SerializedProperty conditionsProperty = triggerProperty.FindPropertyRelative("_conditions");
            conditionsProperty.arraySize = trigger.Conditions.Length;
            for (var i = 0; i < trigger.Conditions.Length; i++)
            {
                SetCondition(conditionsProperty.GetArrayElementAtIndex(i), trigger.Conditions[i]);
            }

            SerializedProperty effectsProperty = triggerProperty.FindPropertyRelative("_effects");
            effectsProperty.arraySize = trigger.Effects.Length;
            for (var i = 0; i < trigger.Effects.Length; i++)
            {
                SetReactionEffect(effectsProperty.GetArrayElementAtIndex(i), trigger.Effects[i]);
            }
        }

        private static void SetCondition(SerializedProperty conditionProperty, ConditionSpec condition)
        {
            SetOperand(conditionProperty.FindPropertyRelative("_left"), condition.Left);
            conditionProperty.FindPropertyRelative("_comparison").enumValueIndex = (int)condition.Comparison;
            SetOperand(conditionProperty.FindPropertyRelative("_right"), condition.Right);
        }

        private static void SetOperand(SerializedProperty operandProperty, OperandSpec operand)
        {
            operandProperty.managedReferenceValue = operand.Config;
        }

        private static void SetStatusFilter(SerializedProperty filterProperty, StatusFilterSpec filter)
        {
            filterProperty.managedReferenceValue = filter.Config;
        }

        private static void SetReactionEffect(SerializedProperty effectProperty, StatusReactionEffectSpec effect)
        {
            effectProperty.FindPropertyRelative("_target").enumValueIndex = (int)effect.Target;
            SetBattleEffect(effectProperty.FindPropertyRelative("_effect"), effect.Effect);
        }

        private void SetProjectileEmitterAsset(ProjectileEmitterConfigAsset asset, EmitterSpec emitter)
        {
            ProjectileConfigAsset projectile = CreateAsset<ProjectileConfigAsset>();
            SetProjectileConfigAsset(
                projectile,
                emitter.Behavior,
                emitter.Radius,
                emitter.Speed,
                TicksToSeconds(emitter.LifetimeTicks),
                emitter.ImpactEffects);
            Apply(asset, serialized =>
            {
                SetEmitterConfig(
                    serialized,
                    emitter.AnchorMode,
                    emitter.AnchorOffset,
                    TicksToSeconds(emitter.DurationTicks),
                    TicksToSeconds(emitter.FireIntervalTicks),
                    emitter.PatternType,
                    emitter.DirectionMode,
                    emitter.Direction,
                    emitter.ProjectileCount,
                    projectile);
            });
        }

        private void SetProjectileEmitterAsset(ProjectileEmitterConfigAsset asset, EmitterSecondsSpec emitter)
        {
            ProjectileConfigAsset projectile = CreateAsset<ProjectileConfigAsset>();
            SetProjectileConfigAsset(
                projectile,
                emitter.Behavior,
                emitter.Radius,
                emitter.Speed,
                emitter.LifetimeSeconds,
                emitter.ImpactEffects);
            Apply(asset, serialized =>
            {
                SetEmitterConfig(
                    serialized,
                    emitter.AnchorMode,
                    emitter.AnchorOffset,
                    emitter.DurationSeconds,
                    emitter.FireIntervalSeconds,
                    emitter.PatternType,
                    emitter.DirectionMode,
                    emitter.Direction,
                    emitter.ProjectileCount,
                    projectile);
            });
        }

        private static void SetAreaEffectAsset(
            AreaEffectConfigAsset asset,
            float radius,
            AreaEffectTargetFilter filter,
            IReadOnlyList<BattleEffectConfig> effects)
        {
            Apply(asset, serialized =>
            {
                serialized.FindProperty("_radius").floatValue = radius;
                serialized.FindProperty("_targetFilter").enumValueIndex = (int)filter;
                SetBattleEffects(serialized.FindProperty("_effects"), effects);
            });
        }

        private static void SetEmitterConfig(
            SerializedObject serialized,
            ProjectileEmitterAnchorMode anchorMode,
            Vector2 anchorOffset,
            float durationSeconds,
            float fireIntervalSeconds,
            ProjectilePatternType patternType,
            ProjectileDirectionMode directionMode,
            Vector2 direction,
            int projectileCount,
            ProjectileConfigAsset projectile)
        {
            serialized.FindProperty("_anchorMode").enumValueIndex = (int)anchorMode;
            serialized.FindProperty("_anchorOffset").vector2Value = anchorOffset;
            serialized.FindProperty("_durationSeconds").floatValue = durationSeconds;
            serialized.FindProperty("_fireIntervalSeconds").floatValue = fireIntervalSeconds;

            SerializedProperty patternProperty = serialized.FindProperty("_pattern");
            patternProperty.FindPropertyRelative("_type").enumValueIndex = (int)patternType;
            patternProperty.FindPropertyRelative("_directionMode").enumValueIndex = (int)directionMode;
            patternProperty.FindPropertyRelative("_direction").vector2Value = direction;
            patternProperty.FindPropertyRelative("_projectileCount").intValue = projectileCount;
            serialized.FindProperty("_projectile").objectReferenceValue = projectile;
        }

        private static void SetProjectileConfigAsset(
            ProjectileConfigAsset projectile,
            ProjectileBehavior behavior,
            float radius,
            float speed,
            float lifetimeSeconds,
            IReadOnlyList<BattleEffectConfig> impactEffects)
        {
            Apply(projectile, serialized =>
            {
                serialized.FindProperty("_behavior").enumValueIndex = (int)behavior;
                serialized.FindProperty("_hitPolicyMode").enumValueIndex =
                    (int)ProjectileHitPolicyMode.DestroyOnFirstHit;
                serialized.FindProperty("_maxHitCount").intValue = 2;
                serialized.FindProperty("_radius").floatValue = radius;
                serialized.FindProperty("_speed").floatValue = speed;
                serialized.FindProperty("_lifetimeSeconds").floatValue = lifetimeSeconds;
                SetBattleEffects(serialized.FindProperty("_impactEffects"), impactEffects);
            });
        }

        private static void SetBattleEffects(SerializedProperty effectsProperty, IReadOnlyList<BattleEffectConfig> effects)
        {
            effectsProperty.arraySize = effects.Count;
            for (var i = 0; i < effects.Count; i++)
            {
                SetBattleEffect(effectsProperty.GetArrayElementAtIndex(i), effects[i]);
            }
        }

        private static void SetBattleEffect(SerializedProperty effectProperty, BattleEffectConfig effect)
        {
            effectProperty.FindPropertyRelative("_type").enumValueIndex = (int)effect.Type;
            effectProperty.FindPropertyRelative("_amount").intValue = effect.Amount;
            effectProperty.FindPropertyRelative("_status").objectReferenceValue = effect.Status;
            effectProperty.FindPropertyRelative("_projectileEmitter").objectReferenceValue = effect.ProjectileEmitter;
            effectProperty.FindPropertyRelative("_areaEffect").objectReferenceValue = effect.AreaEffect;
        }

        private static BattleEffectConfig DamageImpact(int amount)
        {
            return new BattleEffectConfig(BattleEffectType.Damage, amount, null);
        }

        private static BattleEffectConfig HealImpact(int amount)
        {
            return new BattleEffectConfig(BattleEffectType.Heal, amount, null);
        }

        private static BattleEffectConfig ApplyStatusImpact(StatusConfigAsset status)
        {
            return new BattleEffectConfig(BattleEffectType.ApplyStatus, 0, status);
        }

        private static StatusReactionEffectSpec DamageReaction(BattleReactionTarget target, int amount)
        {
            return new StatusReactionEffectSpec(target, DamageImpact(amount));
        }

        private static StatusReactionEffectSpec HealReaction(BattleReactionTarget target, int amount)
        {
            return new StatusReactionEffectSpec(target, HealImpact(amount));
        }

        private static StatusReactionEffectSpec ApplyStatusReaction(BattleReactionTarget target, StatusConfigAsset status)
        {
            return new StatusReactionEffectSpec(target, ApplyStatusImpact(status));
        }

        private static BattleEffectConfig SpawnEmitterImpact(ProjectileEmitterConfigAsset emitter)
        {
            return new BattleEffectConfig(BattleEffectType.SpawnProjectileEmitter, 0, null, emitter);
        }

        private static BattleEffectConfig AreaEffectImpact(AreaEffectConfigAsset area)
        {
            return new BattleEffectConfig(BattleEffectType.AreaEffect, 0, null, null, area);
        }

        private static float TicksToSeconds(int ticks)
        {
            return TicksToSeconds(ticks, AuthoringTestTicksPerSecond);
        }

        private static float TicksToSeconds(int ticks, int ticksPerSecond)
        {
            return ticks / (float)ticksPerSecond;
        }

        private static void Apply(Object target, Action<SerializedObject> edit)
        {
            var serialized = new SerializedObject(target);
            edit(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssertConcreteAuthoringTypeExists(string fullName)
        {
            Type type = typeof(StatusConfigAsset).Assembly.GetType(fullName);
            Assert.IsNotNull(type, fullName);
            Assert.IsFalse(type.IsAbstract, fullName);
        }

        private readonly struct SpawnSpec
        {
            public readonly int TeamId;
            public readonly CombatantConfigAsset Combatant;
            public readonly Vector2 Position;

            public SpawnSpec(int teamId, CombatantConfigAsset combatant, Vector2 position)
            {
                TeamId = teamId;
                Combatant = combatant;
                Position = position;
            }
        }

        private readonly struct StatSpec
        {
            public readonly BattleStatId Stat;
            public readonly float Value;

            public StatSpec(BattleStatId stat, float value)
            {
                Stat = stat;
                Value = value;
            }
        }

        private readonly struct EmitterSpec
        {
            public readonly ProjectileEmitterAnchorMode AnchorMode;
            public readonly Vector2 AnchorOffset;
            public readonly int DurationTicks;
            public readonly int FireIntervalTicks;
            public readonly ProjectilePatternType PatternType;
            public readonly ProjectileDirectionMode DirectionMode;
            public readonly Vector2 Direction;
            public readonly int ProjectileCount;
            public readonly ProjectileBehavior Behavior;
            public readonly float Radius;
            public readonly float Speed;
            public readonly int LifetimeTicks;
            public readonly BattleEffectConfig[] ImpactEffects;

            public EmitterSpec(
                ProjectileEmitterAnchorMode anchorMode,
                Vector2 anchorOffset,
                int durationTicks,
                int fireIntervalTicks,
                ProjectilePatternType patternType,
                Vector2 direction,
                int projectileCount,
                ProjectileBehavior behavior,
                float radius,
                float speed,
                int lifetimeTicks,
                BattleEffectConfig[] impactEffects,
                ProjectileDirectionMode directionMode = ProjectileDirectionMode.FixedDirection)
            {
                AnchorMode = anchorMode;
                AnchorOffset = anchorOffset;
                DurationTicks = durationTicks;
                FireIntervalTicks = fireIntervalTicks;
                PatternType = patternType;
                DirectionMode = directionMode;
                Direction = direction;
                ProjectileCount = projectileCount;
                Behavior = behavior;
                Radius = radius;
                Speed = speed;
                LifetimeTicks = lifetimeTicks;
                ImpactEffects = impactEffects;
            }

        }

        private readonly struct EmitterSecondsSpec
        {
            public readonly ProjectileEmitterAnchorMode AnchorMode;
            public readonly Vector2 AnchorOffset;
            public readonly float DurationSeconds;
            public readonly float FireIntervalSeconds;
            public readonly ProjectilePatternType PatternType;
            public readonly ProjectileDirectionMode DirectionMode;
            public readonly Vector2 Direction;
            public readonly int ProjectileCount;
            public readonly ProjectileBehavior Behavior;
            public readonly float Radius;
            public readonly float Speed;
            public readonly float LifetimeSeconds;
            public readonly BattleEffectConfig[] ImpactEffects;

            public EmitterSecondsSpec(
                ProjectileEmitterAnchorMode anchorMode,
                Vector2 anchorOffset,
                float durationSeconds,
                float fireIntervalSeconds,
                ProjectilePatternType patternType,
                Vector2 direction,
                int projectileCount,
                ProjectileBehavior behavior,
                float radius,
                float speed,
                float lifetimeSeconds,
                BattleEffectConfig[] impactEffects,
                ProjectileDirectionMode directionMode = ProjectileDirectionMode.FixedDirection)
            {
                AnchorMode = anchorMode;
                AnchorOffset = anchorOffset;
                DurationSeconds = durationSeconds;
                FireIntervalSeconds = fireIntervalSeconds;
                PatternType = patternType;
                DirectionMode = directionMode;
                Direction = direction;
                ProjectileCount = projectileCount;
                Behavior = behavior;
                Radius = radius;
                Speed = speed;
                LifetimeSeconds = lifetimeSeconds;
                ImpactEffects = impactEffects;
            }
        }

        private readonly struct TriggerSpec
        {
            public readonly BattleTriggerTiming Timing;
            public readonly BattleConditionMatchMode ConditionMatchMode;
            public readonly ConditionSpec[] Conditions;
            public readonly StatusReactionEffectSpec[] Effects;

            public TriggerSpec(BattleTriggerTiming timing, params StatusReactionEffectSpec[] effects)
                : this(timing, BattleConditionMatchMode.All, Array.Empty<ConditionSpec>(), effects)
            {
            }

            public TriggerSpec(
                BattleTriggerTiming timing,
                BattleConditionMatchMode conditionMatchMode,
                ConditionSpec[] conditions,
                params StatusReactionEffectSpec[] effects)
            {
                Timing = timing;
                ConditionMatchMode = conditionMatchMode;
                Conditions = conditions ?? Array.Empty<ConditionSpec>();
                Effects = effects;
            }
        }

        private readonly struct ConditionSpec
        {
            public readonly OperandSpec Left;
            public readonly BattleConditionComparison Comparison;
            public readonly OperandSpec Right;

            private ConditionSpec(OperandSpec left, BattleConditionComparison comparison, OperandSpec right)
            {
                Left = left;
                Comparison = comparison;
                Right = right;
            }

            public static ConditionSpec Compare(OperandSpec left, BattleConditionComparison comparison, OperandSpec right)
            {
                return new ConditionSpec(left, comparison, right);
            }
        }

        private readonly struct OperandSpec
        {
            public readonly BattleConditionOperandConfig Config;

            private OperandSpec(BattleConditionOperandConfig config)
            {
                Config = config;
            }

            public static OperandSpec LiteralInt(int value)
            {
                return new OperandSpec(new BattleLiteralIntConditionOperandConfig(value));
            }

            public static OperandSpec LiteralPercent(float value)
            {
                return new OperandSpec(new BattleLiteralPercentConditionOperandConfig(value));
            }

            public static OperandSpec LiteralScalar(float value)
            {
                return new OperandSpec(new BattleLiteralScalarConditionOperandConfig(value));
            }

            public static OperandSpec HealthPercent(BattleConditionSubject subject)
            {
                return new OperandSpec(new BattleHealthPercentConditionOperandConfig(subject));
            }

            public static OperandSpec StatusCount(BattleConditionSubject subject, StatusFilterSpec filter)
            {
                return new OperandSpec(new BattleStatusCountConditionOperandConfig(subject, filter.Config));
            }

            public static OperandSpec StatusStackCount(BattleConditionSubject subject, StatusFilterSpec filter)
            {
                return new OperandSpec(new BattleStatusStackCountConditionOperandConfig(subject, filter.Config));
            }

            public static OperandSpec StatValue(BattleConditionSubject subject, BattleStatId stat)
            {
                return new OperandSpec(new BattleStatValueConditionOperandConfig(subject, stat));
            }

            public static OperandSpec DistanceBetween(BattleConditionSubject subject, BattleConditionSubject otherSubject)
            {
                return new OperandSpec(new BattleDistanceBetweenConditionOperandConfig(subject, otherSubject));
            }
        }

        private readonly struct StatusFilterSpec
        {
            public readonly BattleStatusConditionFilterConfig Config;

            private StatusFilterSpec(BattleStatusConditionFilterConfig config)
            {
                Config = config;
            }

            public static StatusFilterSpec Any()
            {
                return new StatusFilterSpec(new BattleAnyStatusConditionFilterConfig());
            }

            public static StatusFilterSpec StatusId(StatusConfigAsset status)
            {
                return new StatusFilterSpec(new BattleStatusIdConditionFilterConfig(status));
            }

            public static StatusFilterSpec PolarityFilter(StatusPolarity polarity)
            {
                return new StatusFilterSpec(new BattleStatusPolarityConditionFilterConfig(polarity));
            }
        }

        private readonly struct AbilityEffectFrameSpec
        {
            public readonly string FrameId;
            public readonly int TimeTicks;
            public readonly int Order;
            public readonly BattleEffectConfig[] Effects;

            public AbilityEffectFrameSpec(string frameId, int timeTicks, int order, BattleEffectConfig[] effects)
            {
                FrameId = frameId;
                TimeTicks = timeTicks;
                Order = order;
                Effects = effects;
            }
        }

        private readonly struct StatusReactionEffectSpec
        {
            public readonly BattleReactionTarget Target;
            public readonly BattleEffectConfig Effect;

            public StatusReactionEffectSpec(BattleReactionTarget target, BattleEffectConfig effect)
            {
                Target = target;
                Effect = effect;
            }
        }
    }
}
