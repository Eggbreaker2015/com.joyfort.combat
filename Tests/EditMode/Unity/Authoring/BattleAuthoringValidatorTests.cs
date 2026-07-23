using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Core.Battle;
using Combat.Unity.Authoring;
using Combat.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Combat.Tests.Unity.Authoring
{
    public sealed class BattleAuthoringValidatorTests
    {
        private const int AuthoringTestTicksPerSecond = 30;
        private readonly List<ScriptableObject> _assets = new List<ScriptableObject>();

        [Test]
        public void ValidateAssets_ReportsEmitterMissingProjectileReference()
        {
            ProjectileEmitterConfigAsset emitter = CreateAsset<ProjectileEmitterConfigAsset>("MissingProjectileEmitter");

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                Array.Empty<AbilityConfigAsset>(),
                Array.Empty<StatusConfigAsset>(),
                new[] { emitter });

            Assert.That(report.Issues.Any(issue =>
                issue.Asset == emitter
                && issue.PropertyPath == "projectileEmitter.projectile"
                && issue.Message.Contains("projectile reference")));
        }

        [Test]
        public void ValidateAssets_ReportsInvalidEnabledTargetingBehaviorValues()
        {
            CombatantConfigAsset combatant =
                CreateAsset<CombatantConfigAsset>("InvalidTargeting");
            SetCombatant(
                combatant,
                "invalid-targeting",
                radius: 0.25f,
                stats: RequiredStats(10, 1f),
                basicAbility: null);
            Apply(combatant, serialized =>
            {
                serialized.FindProperty("_targetingBehaviorEnabled").boolValue = true;
                serialized.FindProperty("_targetAcquisitionRange").floatValue = 0f;
                serialized.FindProperty("_noProgressTimeoutSeconds").floatValue = -1f;
                serialized.FindProperty("_minimumProgressDistance").floatValue =
                    float.PositiveInfinity;
                serialized.FindProperty("_rejectedTargetCooldownSeconds").floatValue = 0f;
            });

            BattleAuthoringValidationReport report =
                BattleAuthoringValidator.ValidateAssets(
                    Array.Empty<BattleScenarioAsset>(),
                    new[] { combatant },
                    Array.Empty<AbilityConfigAsset>(),
                    Array.Empty<StatusConfigAsset>());

            AssertIssuePropertyPath(report, "targetAcquisitionRange");
            AssertIssuePropertyPath(report, "noProgressTimeoutSeconds");
            AssertIssuePropertyPath(report, "minimumProgressDistance");
            AssertIssuePropertyPath(report, "rejectedTargetCooldownSeconds");
        }

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
        public void ValidateAssets_ReportsMissingReferencesDuplicateIdsAndRecursiveStatuses()
        {
            StatusConfigAsset loop = CreateAsset<StatusConfigAsset>("LoopStatus");
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
            StatusConfigAsset duplicateLoop = CreateAsset<StatusConfigAsset>("DuplicateLoopStatus");
            SetStatus(duplicateLoop, "loop", StatusPolarity.Debuff, durationTicks: 1, tickIntervalTicks: 1, periodicDamage: 0);

            AbilityConfigAsset brokenAbility = CreateAsset<AbilityConfigAsset>("BrokenAbility");
            SetAbility(
                brokenAbility,
                "broken",
                range: 3f,
                damage: 0,
                cooldownTicks: 3,
                appliedStatuses: new StatusConfigAsset[] { null },
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
                        radius: 0.1f,
                        speed: 2f,
                        lifetimeTicks: 5,
                        new[]
                        {
                            ApplyStatusImpact(null)
                        })
                });

            CombatantConfigAsset firstCombatant = CreateAsset<CombatantConfigAsset>("FirstCombatant");
            SetCombatant(
                firstCombatant,
                "duelist",
                radius: 0.25f,
                stats: new[]
                {
                    Stat(BattleStatId.MaxHealth, 10),
                    Stat(BattleStatId.MaxHealth, 12),
                    Stat(BattleStatId.MoveSpeed, 1f)
                },
                brokenAbility);
            CombatantConfigAsset secondCombatant = CreateAsset<CombatantConfigAsset>("SecondCombatant");
            SetCombatant(
                secondCombatant,
                "duelist",
                radius: 0.25f,
                stats: RequiredStats(10, 1f),
                brokenAbility);

            CombatantConfigAsset missingBasic = CreateAsset<CombatantConfigAsset>("MissingBasicCombatant");
            SetCombatant(
                missingBasic,
                "missing-basic",
                radius: 0.25f,
                stats: RequiredStats(10, 1f),
                basicAbility: null);

            BattleScenarioAsset scenario = CreateAsset<BattleScenarioAsset>("BrokenScenario");
            SetScenario(scenario, ticksPerSecond: 30, maxTicks: 60, new SpawnSpec(1, null, Vector2.zero));

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                new[] { scenario },
                new[] { firstCombatant, secondCombatant, missingBasic },
                new[] { brokenAbility },
                new[] { loop, duplicateLoop });

            Assert.IsTrue(report.HasErrors);
            AssertIssueContains(report, "BrokenScenario");
            AssertIssueContains(report, "initialSpawns[0].combatant");
            AssertIssueContains(report, "duplicate combatant id 'duelist'");
            AssertIssueContains(report, "duplicate status id 'loop'");
            AssertIssueContains(report, "stats[1]");
            AssertIssueContains(report, "basicAbility");
            AssertIssueContains(report, "effects[0].status");
            AssertIssueContains(report, "impactEffects[0].status");
            AssertIssueContains(report, "recursive status trigger reference");
        }

        [Test]
        public void ValidateAssets_AcceptsStatusReactionsAndSpawnProjectileEmitterImpacts()
        {
            StatusConfigAsset burn = CreateAsset<StatusConfigAsset>("BurnStatus");
            SetStatus(burn, "burn", StatusPolarity.Debuff, durationTicks: 6, tickIntervalTicks: 2, periodicDamage: 1);
            StatusConfigAsset mark = CreateAsset<StatusConfigAsset>("MarkStatus");
            SetStatus(mark, "mark", StatusPolarity.Debuff, durationTicks: 4, tickIntervalTicks: 1, periodicDamage: 0);
            StatusConfigAsset thorns = CreateAsset<StatusConfigAsset>("ThornsStatus");
            SetStatus(
                thorns,
                "thorns",
                StatusPolarity.Buff,
                durationTicks: 8,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                triggers: new[]
                {
                    new TriggerSpec(
                        BattleTriggerTiming.AfterDamageTaken,
                        DamageReaction(BattleReactionTarget.Source, 1),
                        ApplyStatusReaction(BattleReactionTarget.Source, mark))
                });

            ProjectileEmitterConfigAsset fireboltBurst = CreateAsset<ProjectileEmitterConfigAsset>("FireboltBurstEmitter");
            SetProjectileEmitterAsset(
                fireboltBurst,
                new EmitterSpec(
                    ProjectileEmitterAnchorMode.FixedPosition,
                    Vector2.zero,
                    durationTicks: 1,
                    fireIntervalTicks: 1,
                    ProjectilePatternType.Circle,
                    Vector2.right,
                    projectileCount: 6,
                    ProjectileBehavior.Linear,
                    radius: 0.1f,
                    speed: 3f,
                    lifetimeTicks: 10,
                    new[]
                    {
                        DamageImpact(1)
                    }));

            AbilityConfigAsset firebolt = CreateAsset<AbilityConfigAsset>("FireboltAbility");
            SetAbility(
                firebolt,
                "firebolt",
                range: 5f,
                damage: 0,
                cooldownTicks: 8,
                appliedStatuses: new[] { thorns },
                projectileEmitters: new[]
                {
                    new EmitterSpec(
                        ProjectileEmitterAnchorMode.FollowSource,
                        new Vector2(0.25f, 0f),
                        durationTicks: 1,
                        fireIntervalTicks: 1,
                        ProjectilePatternType.Single,
                        Vector2.right,
                        projectileCount: 1,
                        ProjectileBehavior.Linear,
                        radius: 0.15f,
                        speed: 5f,
                        lifetimeTicks: 24,
                        new[]
                        {
                            DamageImpact(3),
                            ApplyStatusImpact(burn),
                            SpawnEmitterImpact(fireboltBurst)
                        })
                });

            CombatantConfigAsset mage = CreateAsset<CombatantConfigAsset>("MageCombatant");
            SetCombatant(mage, "mage", radius: 0.35f, stats: RequiredStats(20, 2f), firebolt);

            BattleScenarioAsset scenario = CreateAsset<BattleScenarioAsset>("ValidScenario");
            SetScenario(
                scenario,
                ticksPerSecond: 30,
                maxTicks: 120,
                new SpawnSpec(1, mage, new Vector2(-1f, 0f)),
                new SpawnSpec(2, mage, new Vector2(1f, 0f)));

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                new[] { scenario },
                new[] { mage },
                new[] { firebolt },
                new[] { burn, mark, thorns });

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Issues.Select(issue => issue.Message)));
        }

        [Test]
        public void ValidateAssets_ReportsStatusWithInvalidMaxStacks()
        {
            StatusConfigAsset invalid = CreateAsset<StatusConfigAsset>("InvalidStackStatus");
            SetStatus(
                invalid,
                "invalid-stack",
                StatusPolarity.Buff,
                durationTicks: 5,
                tickIntervalTicks: 5,
                periodicDamage: 0,
                maxStacks: 0);

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                Array.Empty<AbilityConfigAsset>(),
                new[] { invalid },
                Array.Empty<ProjectileEmitterConfigAsset>(),
                Array.Empty<AreaEffectConfigAsset>());

            Assert.IsTrue(report.HasErrors);
            AssertIssueContains(report, "MaxStacks");
        }

        [Test]
        public void ValidateAssets_ReportsStatusModifierWithDuplicateOverride()
        {
            StatusConfigAsset status = CreateAsset<StatusConfigAsset>("InvalidOverrideStatus");
            SetStatus(
                status,
                "invalid-override",
                StatusPolarity.Buff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new[]
                {
                    ModifierSpec.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.Override, 1f),
                    ModifierSpec.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.Override, 2f)
                });

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                Array.Empty<AbilityConfigAsset>(),
                new[] { status },
                Array.Empty<ProjectileEmitterConfigAsset>(),
                Array.Empty<AreaEffectConfigAsset>());

            Assert.IsTrue(report.HasErrors);
            AssertIssueContains(report, "multiple Override modifiers");
            AssertIssueContains(report, "Stat:MoveSpeed");
        }

        [Test]
        public void ValidateAssets_AcceptsMaxHealthStatModifier()
        {
            StatusConfigAsset status = CreateAsset<StatusConfigAsset>("MaxHealthModifierStatus");
            SetStatus(
                status,
                "max-health-modifier",
                StatusPolarity.Buff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new[]
                {
                    ModifierSpec.Stat(BattleStatId.MaxHealth, BattleModifierOperation.Flat, 1f)
                });

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                Array.Empty<AbilityConfigAsset>(),
                new[] { status },
                Array.Empty<ProjectileEmitterConfigAsset>(),
                Array.Empty<AreaEffectConfigAsset>());

            Assert.IsFalse(report.HasErrors);
        }

        [Test]
        public void ValidateAssets_ReportsStatusModifierWithMinClampGreaterThanMaxClamp()
        {
            StatusConfigAsset status = CreateAsset<StatusConfigAsset>("InvalidClampStatus");
            SetStatus(
                status,
                "invalid-clamp",
                StatusPolarity.Buff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new[]
                {
                    ModifierSpec.Damage(BattleDamageModifierStat.DamageTaken, BattleModifierOperation.MinClamp, 5f),
                    ModifierSpec.Damage(BattleDamageModifierStat.DamageTaken, BattleModifierOperation.MaxClamp, 3f)
                });

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                Array.Empty<AbilityConfigAsset>(),
                new[] { status },
                Array.Empty<ProjectileEmitterConfigAsset>(),
                Array.Empty<AreaEffectConfigAsset>());

            Assert.IsTrue(report.HasErrors);
            AssertIssueContains(report, "MinClamp cannot be greater than MaxClamp");
            AssertIssueContains(report, "Damage:DamageTaken");
        }

        [Test]
        public void ValidateAssets_ReportsStatusModifierWithAggregatedClampConflict()
        {
            StatusConfigAsset status = CreateAsset<StatusConfigAsset>("InvalidAggregatedClampStatus");
            SetStatus(
                status,
                "invalid-aggregated-clamp",
                StatusPolarity.Buff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new[]
                {
                    ModifierSpec.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.MinClamp, 10f),
                    ModifierSpec.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.MinClamp, 1f),
                    ModifierSpec.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.MaxClamp, 20f),
                    ModifierSpec.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.MaxClamp, 5f)
                });

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                Array.Empty<AbilityConfigAsset>(),
                new[] { status },
                Array.Empty<ProjectileEmitterConfigAsset>(),
                Array.Empty<AreaEffectConfigAsset>());

            Assert.IsTrue(report.HasErrors);
            AssertIssueContains(report, "MinClamp cannot be greater than MaxClamp");
            AssertIssueContains(report, "Stat:MoveSpeed");
        }

        [Test]
        public void ValidateAssets_ReportsCrossStatusModifierDuplicateOverride()
        {
            StatusConfigAsset first = CreateAsset<StatusConfigAsset>("FirstOverrideStatus");
            SetStatus(
                first,
                "first-override",
                StatusPolarity.Buff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new[]
                {
                    ModifierSpec.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.Override, 1f)
                });
            StatusConfigAsset second = CreateAsset<StatusConfigAsset>("SecondOverrideStatus");
            SetStatus(
                second,
                "second-override",
                StatusPolarity.Buff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new[]
                {
                    ModifierSpec.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.Override, 2f)
                });

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                Array.Empty<AbilityConfigAsset>(),
                new[] { first, second },
                Array.Empty<ProjectileEmitterConfigAsset>(),
                Array.Empty<AreaEffectConfigAsset>());

            Assert.IsTrue(report.HasErrors);
            AssertIssueContains(report, "multiple Override modifiers");
            AssertIssueContains(report, "Stat:MoveSpeed");
            AssertIssueContains(report, "first-override");
            AssertIssueContains(report, "second-override");
        }

        [Test]
        public void ValidateAssets_ReportsCrossStatusModifierClampConflict()
        {
            StatusConfigAsset floor = CreateAsset<StatusConfigAsset>("FloorStatus");
            SetStatus(
                floor,
                "floor",
                StatusPolarity.Buff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new[]
                {
                    ModifierSpec.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.MinClamp, 10f)
                });
            StatusConfigAsset ceiling = CreateAsset<StatusConfigAsset>("CeilingStatus");
            SetStatus(
                ceiling,
                "ceiling",
                StatusPolarity.Buff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new[]
                {
                    ModifierSpec.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.MaxClamp, 5f)
                });

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                Array.Empty<AbilityConfigAsset>(),
                new[] { floor, ceiling },
                Array.Empty<ProjectileEmitterConfigAsset>(),
                Array.Empty<AreaEffectConfigAsset>());

            Assert.IsTrue(report.HasErrors);
            AssertIssueContains(report, "MinClamp cannot be greater than MaxClamp");
            AssertIssueContains(report, "Stat:MoveSpeed");
            AssertIssueContains(report, "floor");
            AssertIssueContains(report, "ceiling");
        }

        [Test]
        public void ValidateAssets_CrossStatusModifierKeysSeparateStatAndDamageTargets()
        {
            StatusConfigAsset statOverride = CreateAsset<StatusConfigAsset>("StatOverrideStatus");
            SetStatus(
                statOverride,
                "stat-override",
                StatusPolarity.Buff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new[]
                {
                    ModifierSpec.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.Override, 1f)
                });
            StatusConfigAsset damageOverride = CreateAsset<StatusConfigAsset>("DamageOverrideStatus");
            SetStatus(
                damageOverride,
                "damage-override",
                StatusPolarity.Buff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new[]
                {
                    ModifierSpec.Damage(BattleDamageModifierStat.DamageDealt, BattleModifierOperation.Override, 2f)
                });

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                Array.Empty<AbilityConfigAsset>(),
                new[] { statOverride, damageOverride },
                Array.Empty<ProjectileEmitterConfigAsset>(),
                Array.Empty<AreaEffectConfigAsset>());

            Assert.IsFalse(
                report.HasErrors,
                $"Expected separate stat and damage modifier keys across statuses to validate. Actual issues:\n{string.Join("\n", report.Issues.Select(issue => issue.Message))}");
        }

        [Test]
        public void ValidateAssets_StatusModifierKeysSeparateStatAndDamageTargets()
        {
            StatusConfigAsset status = CreateAsset<StatusConfigAsset>("SeparateModifierKeyStatus");
            SetStatus(
                status,
                "separate-modifier-key",
                StatusPolarity.Buff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new[]
                {
                    ModifierSpec.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.Override, 1f),
                    ModifierSpec.Damage(BattleDamageModifierStat.DamageDealt, BattleModifierOperation.Override, 2f)
                });

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                Array.Empty<AbilityConfigAsset>(),
                new[] { status },
                Array.Empty<ProjectileEmitterConfigAsset>(),
                Array.Empty<AreaEffectConfigAsset>());

            Assert.IsFalse(
                report.HasErrors,
                $"Expected separate stat and damage modifier keys to validate. Actual issues:\n{string.Join("\n", report.Issues.Select(issue => issue.Message))}");
        }

        [Test]
        public void ValidateAssets_ReportsStatusModifierInvalidEnumFields()
        {
            StatusConfigAsset status = CreateAsset<StatusConfigAsset>("InvalidModifierEnumStatus");
            SetStatus(
                status,
                "invalid-modifier-enum",
                StatusPolarity.Buff,
                durationTicks: 3,
                tickIntervalTicks: 1,
                periodicDamage: 0,
                modifiers: new[]
                {
                    ModifierSpec.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.Flat, 1f),
                    ModifierSpec.Damage(BattleDamageModifierStat.DamageDealt, BattleModifierOperation.Flat, 1f),
                    ModifierSpec.Stat(BattleStatId.MoveSpeed, BattleModifierOperation.Flat, 1f)
                });
            Apply(status, serialized =>
            {
                SerializedProperty modifiers = serialized.FindProperty("_modifiers");
                SerializedProperty invalidStat = modifiers.GetArrayElementAtIndex(0);
                invalidStat.FindPropertyRelative("_statId").intValue = 999;
                invalidStat.FindPropertyRelative("_operation").intValue = 998;

                SerializedProperty invalidDamage = modifiers.GetArrayElementAtIndex(1);
                invalidDamage.FindPropertyRelative("_damageStat").intValue = 997;

                SerializedProperty invalidTarget = modifiers.GetArrayElementAtIndex(2);
                invalidTarget.FindPropertyRelative("_target").intValue = 996;
            });

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                Array.Empty<AbilityConfigAsset>(),
                new[] { status },
                Array.Empty<ProjectileEmitterConfigAsset>(),
                Array.Empty<AreaEffectConfigAsset>());

            Assert.IsTrue(report.HasErrors);
            AssertIssuePropertyPathAndMessageContains(report, "modifiers[0].statId", "unsupported stat modifier id");
            AssertIssuePropertyPathAndMessageContains(report, "modifiers[0].operation", "unsupported BattleModifierOperation");
            AssertIssuePropertyPathAndMessageContains(report, "modifiers[1].damageStat", "unsupported BattleDamageModifierStat");
            AssertIssuePropertyPathAndMessageContains(report, "modifiers[2].target", "unsupported BattleModifierTarget");
        }

        [Test]
        public void ValidateAssets_ReportsInvalidStatusTriggerConditions()
        {
            StatusConfigAsset invalid = CreateAsset<StatusConfigAsset>("InvalidConditionStatus");
            SetStatus(
                invalid,
                "invalid-condition",
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
                                OperandSpec.LiteralPercent(-1f)),
                            ConditionSpec.Compare(
                                OperandSpec.StatusCount(BattleConditionSubject.Target, StatusFilterSpec.StatusId(null)),
                                BattleConditionComparison.GreaterOrEqual,
                                OperandSpec.LiteralInt(1)),
                            ConditionSpec.Compare(
                                OperandSpec.StatValue(BattleConditionSubject.Source, (BattleStatId)999),
                                BattleConditionComparison.GreaterOrEqual,
                                OperandSpec.LiteralScalar(1f)),
                            ConditionSpec.Compare(
                                OperandSpec.DistanceBetween(BattleConditionSubject.Source, (BattleConditionSubject)999),
                                BattleConditionComparison.LessOrEqual,
                                OperandSpec.LiteralScalar(5f))
                        },
                        DamageReaction(BattleReactionTarget.Target, 1))
                });

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                Array.Empty<AbilityConfigAsset>(),
                new[] { invalid },
                Array.Empty<ProjectileEmitterConfigAsset>(),
                Array.Empty<AreaEffectConfigAsset>());

            Assert.IsTrue(report.HasErrors);
            AssertIssueContains(report, "conditions[0].right.percentValue");
            AssertIssueContains(report, "between 0 and 100");
            AssertIssueContains(report, "conditions[1].left.statusFilter.status");
            AssertIssueContains(report, "status reference");
            AssertIssueContains(report, "conditions[2].left.stat");
            AssertIssueContains(report, "unsupported BattleStatId");
            AssertIssueContains(report, "conditions[3].left.otherSubject");
            AssertIssueContains(report, "unsupported BattleConditionSubject");
        }

        [Test]
        public void ValidateAssets_ReportsConditionValueKindMismatch()
        {
            StatusConfigAsset invalid = CreateAsset<StatusConfigAsset>("InvalidConditionCompileStatus");
            SetStatus(
                invalid,
                "invalid-condition-compile",
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
                                OperandSpec.LiteralPercent(20f),
                                BattleConditionComparison.Equal,
                                OperandSpec.LiteralInt(20))
                        },
                        DamageReaction(BattleReactionTarget.Target, 1))
                });

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                Array.Empty<AbilityConfigAsset>(),
                new[] { invalid },
                Array.Empty<ProjectileEmitterConfigAsset>(),
                Array.Empty<AreaEffectConfigAsset>());

            Assert.IsTrue(report.HasErrors);
            AssertIssueContains(report, "conditions[0]");
            AssertIssueContains(report, "same value kind");
        }

        [Test]
        public void ValidateAssets_ReportsConditionCompilerFailure()
        {
            StatusConfigAsset invalid = CreateAsset<StatusConfigAsset>("InvalidConditionCompileStatus");
            SetStatus(
                invalid,
                "invalid-condition-compile",
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
                                OperandSpec.HealthPercent(BattleConditionSubject.Target),
                                BattleConditionComparison.LessOrEqual,
                                OperandSpec.LiteralPercent(20f))
                        },
                        DamageReaction(BattleReactionTarget.Target, 1))
                });

            try
            {
                BattleAuthoringValidator.ConditionCompilerForTesting = _ => throw new ArgumentException("forced compiler failure");
                BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                    Array.Empty<BattleScenarioAsset>(),
                    Array.Empty<CombatantConfigAsset>(),
                    Array.Empty<AbilityConfigAsset>(),
                    new[] { invalid },
                    Array.Empty<ProjectileEmitterConfigAsset>(),
                    Array.Empty<AreaEffectConfigAsset>());

                Assert.IsTrue(report.HasErrors);
                AssertIssueContains(report, "conditions[0]");
                AssertIssueContains(report, "condition compile failed");
                AssertIssueContains(report, "forced compiler failure");
            }
            finally
            {
                BattleAuthoringValidator.ConditionCompilerForTesting = null;
            }
        }

        [Test]
        public void ValidateScenarioGraph_AcceptsHealAndAreaEffectAuthoring()
        {
            StatusConfigAsset burn = CreateAsset<StatusConfigAsset>("BurnStatus");
            SetStatus(burn, "burn", StatusPolarity.Debuff, durationTicks: 6, tickIntervalTicks: 2, periodicDamage: 1);
            StatusConfigAsset guardian = CreateAsset<StatusConfigAsset>("GuardianStatus");
            SetStatus(
                guardian,
                "guardian",
                StatusPolarity.Buff,
                durationTicks: 6,
                tickIntervalTicks: 2,
                periodicDamage: 0,
                triggers: new[]
                {
                    new TriggerSpec(
                        BattleTriggerTiming.AfterDamageTaken,
                        HealReaction(BattleReactionTarget.Self, 2))
                });

            ProjectileEmitterConfigAsset pulseEmitter = CreateAsset<ProjectileEmitterConfigAsset>("PulseEmitter");
            SetProjectileEmitterAsset(
                pulseEmitter,
                new EmitterSpec(
                    ProjectileEmitterAnchorMode.FixedPosition,
                    Vector2.zero,
                    durationTicks: 1,
                    fireIntervalTicks: 1,
                    ProjectilePatternType.Circle,
                    Vector2.right,
                    projectileCount: 4,
                    ProjectileBehavior.Linear,
                    radius: 0.1f,
                    speed: 3f,
                    lifetimeTicks: 8,
                    new[]
                    {
                        DamageImpact(1)
                    }));

            AreaEffectConfigAsset area = CreateAsset<AreaEffectConfigAsset>("GroupPulseArea");
            SetAreaEffectAsset(
                area,
                radius: 2.5f,
                AreaEffectTargetFilter.Allies,
                new[]
                {
                    HealImpact(4),
                    ApplyStatusImpact(burn),
                    ApplyStatusImpact(guardian),
                    SpawnEmitterImpact(pulseEmitter)
                });

            AbilityConfigAsset groupPulse = CreateAsset<AbilityConfigAsset>("GroupPulseAbility");
            SetAbilityEffects(
                groupPulse,
                "group-pulse",
                range: 4f,
                cooldownTicks: 8,
                new[]
                {
                    AreaEffectImpact(area)
                });

            CombatantConfigAsset cleric = CreateAsset<CombatantConfigAsset>("ClericCombatant");
            SetCombatant(cleric, "cleric", radius: 0.35f, stats: RequiredStats(20, 2f), groupPulse);

            BattleScenarioAsset scenario = CreateAsset<BattleScenarioAsset>("ValidAreaScenario");
            SetScenario(
                scenario,
                ticksPerSecond: 30,
                maxTicks: 120,
                new SpawnSpec(1, cleric, new Vector2(-1f, 0f)),
                new SpawnSpec(2, cleric, new Vector2(1f, 0f)));

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateScenarioGraph(scenario);

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Issues.Select(issue => issue.Message)));
        }

        [Test]
        public void ValidateScenarioGraph_AcceptsDirectHealWithLowestHealthAllyTargetSelection()
        {
            AbilityConfigAsset mend = CreateAsset<AbilityConfigAsset>("MendAbility");
            SetAbilityEffects(
                mend,
                "mend",
                range: 4f,
                cooldownTicks: 6,
                new[]
                {
                    HealImpact(4)
                });
            Apply(mend, serialized =>
            {
                serialized.FindProperty("_targetSelection").enumValueIndex = (int)AbilityTargetSelection.LowestHealthAlly;
            });

            CombatantConfigAsset cleric = CreateAsset<CombatantConfigAsset>("ClericCombatant");
            SetCombatant(cleric, "cleric", radius: 0.35f, stats: RequiredStats(20, 2f), mend);

            BattleScenarioAsset scenario = CreateAsset<BattleScenarioAsset>("ValidMendScenario");
            SetScenario(
                scenario,
                ticksPerSecond: 30,
                maxTicks: 120,
                new SpawnSpec(1, cleric, new Vector2(-1f, 0f)),
                new SpawnSpec(2, cleric, new Vector2(1f, 0f)));

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateScenarioGraph(scenario);

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Issues.Select(issue => issue.Message)));
        }

        [Test]
        public void ValidateScenarioGraph_AcceptsDirectHealWithSelfTargetSelection()
        {
            AbilityConfigAsset mend = CreateAsset<AbilityConfigAsset>("SelfMendAbility");
            SetAbilityEffects(
                mend,
                "self-mend",
                range: 0f,
                cooldownTicks: 6,
                new[]
                {
                    HealImpact(4)
                });
            Apply(mend, serialized =>
            {
                serialized.FindProperty("_targetSelection").enumValueIndex = (int)AbilityTargetSelection.Self;
            });

            CombatantConfigAsset cleric = CreateAsset<CombatantConfigAsset>("SelfMendCombatant");
            SetCombatant(cleric, "self-mender", radius: 0.35f, stats: RequiredStats(20, 2f), mend);

            BattleScenarioAsset scenario = CreateAsset<BattleScenarioAsset>("ValidSelfMendScenario");
            SetScenario(
                scenario,
                ticksPerSecond: 30,
                maxTicks: 120,
                new SpawnSpec(1, cleric, new Vector2(-1f, 0f)),
                new SpawnSpec(2, cleric, new Vector2(1f, 0f)));

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateScenarioGraph(scenario);

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Issues.Select(issue => issue.Message)));
        }

        [Test]
        public void ValidateAssets_ReportsInvalidHealAndAreaEffectAuthoring()
        {
            AreaEffectConfigAsset badRadiusArea = CreateAsset<AreaEffectConfigAsset>("BadRadiusArea");
            SetAreaEffectAsset(
                badRadiusArea,
                radius: 0f,
                AreaEffectTargetFilter.Enemies,
                new[]
                {
                    DamageImpact(1)
                });

            AreaEffectConfigAsset emptyArea = CreateAsset<AreaEffectConfigAsset>("EmptyArea");
            SetAreaEffectAsset(
                emptyArea,
                radius: 1f,
                AreaEffectTargetFilter.Enemies,
                Array.Empty<BattleEffectConfig>());

            AreaEffectConfigAsset recursiveArea = CreateAsset<AreaEffectConfigAsset>("RecursiveArea");
            SetAreaEffectAsset(
                recursiveArea,
                radius: 1f,
                AreaEffectTargetFilter.Enemies,
                new[]
                {
                    AreaEffectImpact(recursiveArea)
                });

            AbilityConfigAsset broken = CreateAsset<AbilityConfigAsset>("BrokenAreaAbility");
            SetAbilityEffects(
                broken,
                "broken-area",
                range: 3f,
                cooldownTicks: 5,
                new[]
                {
                    HealImpact(0),
                    AreaEffectImpact(null),
                    AreaEffectImpact(badRadiusArea)
                });

            ProjectileEmitterConfigAsset directHealEmitter = CreateAsset<ProjectileEmitterConfigAsset>("DirectHealEmitter");
            SetProjectileEmitterAsset(
                directHealEmitter,
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

            AbilityConfigAsset directHeal = CreateAsset<AbilityConfigAsset>("DirectHealAbility");
            SetAbilityEffects(
                directHeal,
                "direct-heal",
                range: 3f,
                cooldownTicks: 5,
                new[]
                {
                    HealImpact(3),
                    SpawnEmitterImpact(directHealEmitter)
                });

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                new[] { broken, directHeal },
                Array.Empty<StatusConfigAsset>(),
                new[] { directHealEmitter },
                new[] { badRadiusArea, emptyArea, recursiveArea });

            Assert.IsTrue(report.HasErrors);
            AssertIssueContains(report, "effects[0].amount");
            AssertIssueContains(report, "effects[1].areaEffect");
            AssertIssueContains(report, "radius");
            AssertIssueContains(report, "effects");
            AssertIssueContains(report, "nested AreaEffect");
            AssertIssueContains(report, "Heal requires explicit target context");
            AssertIssueContains(report, "projectile.impactEffects[0]");
        }

        [Test]
        public void ValidateAssets_ReportsAreaEffectNonFiniteRadiusAndInvalidTargetFilter()
        {
            AreaEffectConfigAsset nanRadiusArea = CreateAsset<AreaEffectConfigAsset>("NanRadiusArea");
            SetAreaEffectAssetRaw(
                nanRadiusArea,
                float.NaN,
                (int)AreaEffectTargetFilter.Enemies,
                new[]
                {
                    DamageImpact(1)
                });
            AreaEffectConfigAsset infinityRadiusArea = CreateAsset<AreaEffectConfigAsset>("InfinityRadiusArea");
            SetAreaEffectAssetRaw(
                infinityRadiusArea,
                float.PositiveInfinity,
                (int)AreaEffectTargetFilter.Allies,
                new[]
                {
                    DamageImpact(1)
                });
            AreaEffectConfigAsset invalidFilterArea = CreateAsset<AreaEffectConfigAsset>("InvalidFilterArea");
            SetAreaEffectAssetRaw(
                invalidFilterArea,
                1f,
                999,
                new[]
                {
                    DamageImpact(1)
                });

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                Array.Empty<AbilityConfigAsset>(),
                Array.Empty<StatusConfigAsset>(),
                Array.Empty<ProjectileEmitterConfigAsset>(),
                new[] { nanRadiusArea, infinityRadiusArea, invalidFilterArea });

            Assert.IsTrue(report.HasErrors);
            AssertIssueContains(report, "radius must be finite");
            AssertIssueContains(report, "targetFilter");
            AssertIssueContains(report, "unsupported AreaEffectTargetFilter");
        }

        [Test]
        public void ValidateScenarioGraph_CollectsStatusAndEmitterReferencesInsideAreaEffectChildren()
        {
            StatusConfigAsset loop = CreateAsset<StatusConfigAsset>("LoopStatus");
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

            ProjectileEmitterConfigAsset brokenEmitter = CreateAsset<ProjectileEmitterConfigAsset>("BrokenEmitter");
            SetProjectileEmitterAsset(
                brokenEmitter,
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
                        ApplyStatusImpact(null)
                    }));

            AreaEffectConfigAsset area = CreateAsset<AreaEffectConfigAsset>("CollectingArea");
            SetAreaEffectAsset(
                area,
                radius: 2f,
                AreaEffectTargetFilter.AllUnits,
                new[]
                {
                    ApplyStatusImpact(loop),
                    SpawnEmitterImpact(brokenEmitter)
                });

            AbilityConfigAsset pulse = CreateAsset<AbilityConfigAsset>("PulseAbility");
            SetAbilityEffects(
                pulse,
                "pulse",
                range: 4f,
                cooldownTicks: 8,
                new[]
                {
                    AreaEffectImpact(area)
                });

            CombatantConfigAsset caster = CreateAsset<CombatantConfigAsset>("CasterCombatant");
            SetCombatant(caster, "caster", radius: 0.35f, stats: RequiredStats(20, 2f), pulse);
            BattleScenarioAsset scenario = CreateAsset<BattleScenarioAsset>("CollectingScenario");
            SetScenario(
                scenario,
                ticksPerSecond: 30,
                maxTicks: 120,
                new SpawnSpec(1, caster, new Vector2(-1f, 0f)),
                new SpawnSpec(2, caster, new Vector2(1f, 0f)));

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateScenarioGraph(scenario);

            Assert.IsTrue(report.HasErrors);
            AssertIssueContains(report, "recursive status trigger reference");
            AssertIssueContains(report, "impactEffects[0].status");
        }

        [Test]
        public void ValidateAssets_RecursiveAreaGraphReportsErrorsWithoutThrowing()
        {
            AreaEffectConfigAsset recursiveArea = CreateAsset<AreaEffectConfigAsset>("RecursiveArea");
            SetAreaEffectAsset(
                recursiveArea,
                radius: 1f,
                AreaEffectTargetFilter.Enemies,
                new[]
                {
                    AreaEffectImpact(recursiveArea)
                });

            AbilityConfigAsset broken = CreateAsset<AbilityConfigAsset>("RecursiveAreaAbility");
            SetAbilityEffects(
                broken,
                "recursive-area",
                range: 3f,
                cooldownTicks: 5,
                new[]
                {
                    AreaEffectImpact(recursiveArea)
                });

            BattleAuthoringValidationReport report = null;
            Assert.DoesNotThrow(() =>
            {
                report = BattleAuthoringValidator.ValidateAssets(
                    Array.Empty<BattleScenarioAsset>(),
                    Array.Empty<CombatantConfigAsset>(),
                    new[] { broken },
                    Array.Empty<StatusConfigAsset>(),
                    Array.Empty<ProjectileEmitterConfigAsset>(),
                    new[] { recursiveArea });
            });

            Assert.IsNotNull(report);
            Assert.IsTrue(report.HasErrors);
            AssertIssueContains(report, "nested AreaEffect");
        }

        [Test]
        public void ValidateAssets_ReportsAreaEffectNestedThroughProjectileEmitterStructurally()
        {
            AreaEffectConfigAsset innerArea = CreateAsset<AreaEffectConfigAsset>("InnerArea");
            SetAreaEffectAsset(
                innerArea,
                radius: 1f,
                AreaEffectTargetFilter.Enemies,
                new[]
                {
                    DamageImpact(1)
                });

            ProjectileEmitterConfigAsset nestedEmitter = CreateAsset<ProjectileEmitterConfigAsset>("NestedAreaEmitter");
            SetProjectileEmitterAsset(
                nestedEmitter,
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
                        AreaEffectImpact(innerArea)
                    }));

            AreaEffectConfigAsset outerArea = CreateAsset<AreaEffectConfigAsset>("OuterArea");
            SetAreaEffectAsset(
                outerArea,
                radius: 2f,
                AreaEffectTargetFilter.Enemies,
                new[]
                {
                    SpawnEmitterImpact(nestedEmitter)
                });

            AbilityConfigAsset broken = CreateAsset<AbilityConfigAsset>("NestedEmitterAreaAbility");
            SetAbilityEffects(
                broken,
                "nested-emitter-area",
                range: 3f,
                cooldownTicks: 5,
                new[]
                {
                    AreaEffectImpact(outerArea)
                });

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                new[] { broken },
                Array.Empty<StatusConfigAsset>(),
                new[] { nestedEmitter },
                new[] { outerArea, innerArea });

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(
                report.Issues.Any(issue =>
                    issue.Asset == nestedEmitter.Projectile
                    && issue.PropertyPath.Contains("impactEffects[0].areaEffect")
                    && issue.Message.Contains("nested AreaEffect")),
                $"Expected structural nested AreaEffect issue on projectile impact path. Actual issues:\n{string.Join("\n", report.Issues.Select(issue => $"{issue.PropertyPath}: {issue.Message}"))}");
        }

        [Test]
        public void ValidateAssets_ReportsInvalidProjectileCullingBounds()
        {
            AbilityConfigAsset slash = CreateAsset<AbilityConfigAsset>("SlashAbility");
            SetAbility(slash, "slash", range: 1f, damage: 2, cooldownTicks: 3, appliedStatuses: Array.Empty<StatusConfigAsset>(), projectileEmitters: Array.Empty<EmitterSpec>());
            CombatantConfigAsset warrior = CreateAsset<CombatantConfigAsset>("WarriorCombatant");
            SetCombatant(warrior, "warrior", radius: 0.25f, stats: RequiredStats(10, 1f), slash);
            BattleScenarioAsset scenario = CreateAsset<BattleScenarioAsset>("InvalidCullingScenario");
            SetScenario(scenario, ticksPerSecond: 30, maxTicks: 60, new SpawnSpec(1, warrior, Vector2.zero));
            SetProjectileCulling(scenario, enabled: true, center: Vector2.zero, size: new Vector2(0f, 10f), padding: -1f);

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                new[] { scenario },
                new[] { warrior },
                new[] { slash },
                Array.Empty<StatusConfigAsset>());

            Assert.IsTrue(report.HasErrors);
            AssertIssueContains(report, "projectileCullingSize");
            AssertIssueContains(report, "projectileCullingPadding");
        }

        [Test]
        public void Validate_AbilityWithNegativeWindupReportsError()
        {
            AbilityConfigAsset slash = CreateAsset<AbilityConfigAsset>("NegativeWindupAbility");
            SetAbility(slash, "slash", range: 1f, damage: 2, cooldownTicks: 3, appliedStatuses: Array.Empty<StatusConfigAsset>(), projectileEmitters: Array.Empty<EmitterSpec>());
            Apply(slash, serialized =>
            {
                serialized.FindProperty("_windupSeconds").floatValue = -0.1f;
            });

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                new[] { slash },
                Array.Empty<StatusConfigAsset>());

            Assert.IsTrue(report.HasErrors);
            AssertIssueContains(report, "windupSeconds");
            AssertIssuePropertyPath(report, "windupSeconds");
        }

        [Test]
        public void Validate_AbilityWithNegativeRecoveryReportsError()
        {
            AbilityConfigAsset slash = CreateAsset<AbilityConfigAsset>("NegativeRecoveryAbility");
            SetAbility(slash, "slash", range: 1f, damage: 2, cooldownTicks: 3, appliedStatuses: Array.Empty<StatusConfigAsset>(), projectileEmitters: Array.Empty<EmitterSpec>());
            Apply(slash, serialized =>
            {
                serialized.FindProperty("_recoverySeconds").floatValue = -0.1f;
            });

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                new[] { slash },
                Array.Empty<StatusConfigAsset>());

            Assert.IsTrue(report.HasErrors);
            AssertIssueContains(report, "recoverySeconds");
            AssertIssuePropertyPath(report, "recoverySeconds");
        }

        [Test]
        public void Validate_AbilityWithInvalidEffectFrameReportsErrors()
        {
            AbilityConfigAsset broken = CreateAsset<AbilityConfigAsset>("InvalidEffectFrameAbility");
            SetAbilityEffectFrames(
                broken,
                "broken-frame",
                range: 1f,
                cooldownTicks: 3,
                new[]
                {
                    new AbilityEffectFrameSpec(
                        " ",
                        timeSeconds: -0.1f,
                        order: -1,
                        Array.Empty<BattleEffectConfig>())
                });

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                new[] { broken },
                Array.Empty<StatusConfigAsset>());

            Assert.IsTrue(report.HasErrors);
            AssertIssuePropertyPath(report, "effectFrames[0].frameId");
            AssertIssuePropertyPath(report, "effectFrames[0].timeSeconds");
            AssertIssuePropertyPath(report, "effectFrames[0].order");
            AssertIssuePropertyPath(report, "effectFrames[0].effects");
        }

        [Test]
        public void ValidateAssets_ReportsStandaloneProjectileEmitterTimingPatternAndPayloadIssues()
        {
            ProjectileEmitterConfigAsset brokenEmitter = CreateAsset<ProjectileEmitterConfigAsset>("BrokenStandaloneEmitter");
            SetProjectileEmitterAsset(
                brokenEmitter,
                new EmitterSpec(
                    ProjectileEmitterAnchorMode.FixedPosition,
                    Vector2.zero,
                    durationTicks: 0,
                    fireIntervalTicks: 0,
                    ProjectilePatternType.Circle,
                    Vector2.zero,
                    projectileCount: 0,
                    ProjectileBehavior.Linear,
                    radius: 0f,
                    speed: 0f,
                    lifetimeTicks: 0,
                    Array.Empty<BattleEffectConfig>()));

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                Array.Empty<AbilityConfigAsset>(),
                Array.Empty<StatusConfigAsset>(),
                new[] { brokenEmitter },
                Array.Empty<AreaEffectConfigAsset>());

            Assert.IsTrue(report.HasErrors);
            AssertIssueContains(report, "durationSeconds");
            AssertIssueContains(report, "fireIntervalSeconds");
            AssertIssueContains(report, "projectileCount");
            AssertIssueContains(report, "radius");
            AssertIssueContains(report, "speed");
            AssertIssueContains(report, "lifetimeSeconds");
            AssertIssueContains(report, "impactEffects");
        }

        [Test]
        public void ValidateAssets_ReportsStandaloneProjectileEmitterInvalidEnumFields()
        {
            ProjectileEmitterConfigAsset brokenEmitter = CreateAsset<ProjectileEmitterConfigAsset>("BrokenStandaloneEmitterEnums");
            SetProjectileEmitterAsset(
                brokenEmitter,
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
            Apply(brokenEmitter, serialized =>
            {
                serialized.FindProperty("_anchorMode").intValue = 999;
                serialized.FindProperty("_pattern").FindPropertyRelative("_type").intValue = 999;
                serialized.FindProperty("_pattern").FindPropertyRelative("_directionMode").intValue = 999;
            });
            Apply(brokenEmitter.Projectile, serialized =>
            {
                serialized.FindProperty("_behavior").intValue = 999;
                serialized.FindProperty("_hitPolicyMode").intValue = 999;
            });

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                Array.Empty<AbilityConfigAsset>(),
                Array.Empty<StatusConfigAsset>(),
                new[] { brokenEmitter },
                Array.Empty<AreaEffectConfigAsset>());

            Assert.IsTrue(report.HasErrors);
            AssertIssuePropertyPath(report, "projectileEmitter.anchorMode");
            AssertIssuePropertyPath(report, "projectileEmitter.pattern.type");
            AssertIssuePropertyPath(report, "projectileEmitter.pattern.directionMode");
            AssertIssuePropertyPath(report, "projectile.behavior");
            AssertIssuePropertyPath(report, "projectile.hitPolicy.mode");
            AssertIssuePropertyPathAndMessageContains(
                report,
                "projectileEmitter.anchorMode",
                "unsupported ProjectileEmitterAnchorMode");
            AssertIssuePropertyPathAndMessageContains(
                report,
                "projectileEmitter.pattern.type",
                "unsupported ProjectilePatternType");
            AssertIssuePropertyPathAndMessageContains(
                report,
                "projectileEmitter.pattern.directionMode",
                "unsupported ProjectileDirectionMode");
            AssertIssuePropertyPathAndMessageContains(
                report,
                "projectile.behavior",
                "unsupported ProjectileBehavior");
            AssertIssuePropertyPathAndMessageContains(
                report,
                "projectile.hitPolicy.mode",
                "unsupported ProjectileHitPolicyMode");
        }

        [Test]
        public void ValidateAssets_RejectsPiercingProjectileWithTooFewHits()
        {
            ProjectileEmitterConfigAsset emitter =
                CreateAsset<ProjectileEmitterConfigAsset>("InvalidPierceEmitter");
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
            Apply(emitter.Projectile, serialized =>
            {
                serialized.FindProperty("_hitPolicyMode").enumValueIndex =
                    (int)ProjectileHitPolicyMode.Pierce;
                serialized.FindProperty("_maxHitCount").intValue = 1;
            });

            BattleAuthoringValidationReport report =
                BattleAuthoringValidator.ValidateAssets(
                    Array.Empty<BattleScenarioAsset>(),
                    Array.Empty<CombatantConfigAsset>(),
                    Array.Empty<AbilityConfigAsset>(),
                    Array.Empty<StatusConfigAsset>(),
                    new[] { emitter },
                    Array.Empty<AreaEffectConfigAsset>());

            AssertIssuePropertyPath(
                report,
                "projectile.hitPolicy.maxHitCount");
        }

        [Test]
        public void ValidateAssets_StatusRecursionSkipsNullTriggerCollectionsWithoutThrowing()
        {
            StatusConfigAsset broken = CreateAsset<StatusConfigAsset>("NullTriggerStatus");
            Apply(broken, serialized =>
            {
                serialized.FindProperty("_id").stringValue = "null-trigger-status";
                serialized.FindProperty("_polarity").enumValueIndex = (int)StatusPolarity.Buff;
                serialized.FindProperty("_durationSeconds").floatValue = 1f;
                serialized.FindProperty("_tickIntervalSeconds").floatValue = 1f;
                serialized.FindProperty("_periodicDamage").intValue = 0;
                serialized.FindProperty("_maxStacks").intValue = 1;
                serialized.FindProperty("_modifiers").arraySize = 0;
                serialized.FindProperty("_triggers").isExpanded = false;
            });
            typeof(StatusConfigAsset)
                .GetField("_triggers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(broken, null);

            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateAssets(
                Array.Empty<BattleScenarioAsset>(),
                Array.Empty<CombatantConfigAsset>(),
                Array.Empty<AbilityConfigAsset>(),
                new[] { broken },
                Array.Empty<ProjectileEmitterConfigAsset>(),
                Array.Empty<AreaEffectConfigAsset>());

            Assert.IsTrue(report.HasErrors);
            AssertIssueContains(report, "trigger");
        }

        private T CreateAsset<T>(string name) where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            _assets.Add(asset);
            return asset;
        }

        private static void AssertIssueContains(BattleAuthoringValidationReport report, string expected)
        {
            Assert.IsTrue(
                report.Issues.Any(issue => issue.Message.Contains(expected)),
                $"Expected validation issue containing '{expected}'. Actual issues:\n{string.Join("\n", report.Issues.Select(issue => issue.Message))}");
        }

        private static void AssertIssuePropertyPath(BattleAuthoringValidationReport report, string expected)
        {
            Assert.IsTrue(
                report.Issues.Any(issue => issue.PropertyPath == expected),
                $"Expected validation issue with PropertyPath '{expected}'. Actual issues:\n{string.Join("\n", report.Issues.Select(issue => $"{issue.PropertyPath}: {issue.Message}"))}");
        }

        private static void AssertIssuePropertyPathAndMessageContains(BattleAuthoringValidationReport report, string expectedPath, string expectedMessage)
        {
            Assert.IsTrue(
                report.Issues.Any(issue => issue.PropertyPath == expectedPath && issue.Message.Contains(expectedMessage)),
                $"Expected validation issue with PropertyPath '{expectedPath}' and message containing '{expectedMessage}'. Actual issues:\n{string.Join("\n", report.Issues.Select(issue => $"{issue.PropertyPath}: {issue.Message}"))}");
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
            IReadOnlyList<AbilityEffectFrameSpec> frames)
        {
            Apply(ability, serialized =>
            {
                serialized.FindProperty("_id").stringValue = id;
                serialized.FindProperty("_range").floatValue = range;
                serialized.FindProperty("_cooldownSeconds").floatValue = TicksToSeconds(cooldownTicks);
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
            frameProperty.FindPropertyRelative("_timeSeconds").floatValue = frame.TimeSeconds;
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
                ProjectileEmitterConfigAsset emitterAsset = CreateAsset<ProjectileEmitterConfigAsset>("AbilityEmitter");
                SetProjectileEmitterAsset(emitterAsset, projectileEmitters[i]);
                effects.Add(SpawnEmitterImpact(emitterAsset));
            }

            return effects.ToArray();
        }

        private void SetProjectileEmitterAsset(ProjectileEmitterConfigAsset asset, EmitterSpec emitter)
        {
            ProjectileConfigAsset projectile = CreateAsset<ProjectileConfigAsset>("Projectile");
            Apply(projectile, serialized =>
            {
                serialized.FindProperty("_behavior").enumValueIndex = (int)emitter.Behavior;
                serialized.FindProperty("_hitPolicyMode").enumValueIndex =
                    (int)ProjectileHitPolicyMode.DestroyOnFirstHit;
                serialized.FindProperty("_maxHitCount").intValue = 2;
                serialized.FindProperty("_radius").floatValue = emitter.Radius;
                serialized.FindProperty("_speed").floatValue = emitter.Speed;
                serialized.FindProperty("_lifetimeSeconds").floatValue = TicksToSeconds(emitter.LifetimeTicks);
                SetBattleEffects(serialized.FindProperty("_impactEffects"), emitter.ImpactEffects);
            });
            Apply(asset, serialized =>
            {
                serialized.FindProperty("_anchorMode").enumValueIndex = (int)emitter.AnchorMode;
                serialized.FindProperty("_anchorOffset").vector2Value = emitter.AnchorOffset;
                serialized.FindProperty("_durationSeconds").floatValue = TicksToSeconds(emitter.DurationTicks);
                serialized.FindProperty("_fireIntervalSeconds").floatValue = TicksToSeconds(emitter.FireIntervalTicks);

                SerializedProperty pattern = serialized.FindProperty("_pattern");
                pattern.FindPropertyRelative("_type").enumValueIndex = (int)emitter.PatternType;
                pattern.FindPropertyRelative("_directionMode").enumValueIndex = (int)ProjectileDirectionMode.FixedDirection;
                pattern.FindPropertyRelative("_direction").vector2Value = emitter.Direction;
                pattern.FindPropertyRelative("_projectileCount").intValue = emitter.ProjectileCount;
                serialized.FindProperty("_projectile").objectReferenceValue = projectile;
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

        private static void SetAreaEffectAssetRaw(
            AreaEffectConfigAsset asset,
            float radius,
            int targetFilter,
            IReadOnlyList<BattleEffectConfig> effects)
        {
            Apply(asset, serialized =>
            {
                serialized.FindProperty("_radius").floatValue = radius;
                serialized.FindProperty("_targetFilter").intValue = targetFilter;
                SetBattleEffects(serialized.FindProperty("_effects"), effects);
            });
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
            ModifierSpec[] modifiers = null)
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

        private static void SetModifiers(SerializedProperty modifiersProperty, ModifierSpec[] modifiers)
        {
            modifiersProperty.arraySize = modifiers == null ? 0 : modifiers.Length;
            for (var i = 0; i < modifiersProperty.arraySize; i++)
            {
                ModifierSpec modifier = modifiers[i];
                SerializedProperty modifierProperty = modifiersProperty.GetArrayElementAtIndex(i);
                modifierProperty.FindPropertyRelative("_target").enumValueIndex = (int)modifier.Target;
                modifierProperty.FindPropertyRelative("_statId").enumValueIndex = (int)modifier.StatId;
                modifierProperty.FindPropertyRelative("_damageStat").enumValueIndex = (int)modifier.DamageStat;
                modifierProperty.FindPropertyRelative("_operation").enumValueIndex = (int)modifier.Operation;
                modifierProperty.FindPropertyRelative("_value").floatValue = modifier.Value;
            }
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

        private static BattleEffectConfig SpawnEmitterImpact(ProjectileEmitterConfigAsset emitter)
        {
            return new BattleEffectConfig(BattleEffectType.SpawnProjectileEmitter, 0, null, emitter);
        }

        private static BattleEffectConfig AreaEffectImpact(AreaEffectConfigAsset area)
        {
            return new BattleEffectConfig(BattleEffectType.AreaEffect, 0, null, null, area);
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

        private readonly struct ModifierSpec
        {
            public readonly BattleModifierTarget Target;
            public readonly BattleStatId StatId;
            public readonly BattleDamageModifierStat DamageStat;
            public readonly BattleModifierOperation Operation;
            public readonly float Value;

            private ModifierSpec(
                BattleModifierTarget target,
                BattleStatId statId,
                BattleDamageModifierStat damageStat,
                BattleModifierOperation operation,
                float value)
            {
                Target = target;
                StatId = statId;
                DamageStat = damageStat;
                Operation = operation;
                Value = value;
            }

            public static ModifierSpec Stat(BattleStatId statId, BattleModifierOperation operation, float value)
            {
                return new ModifierSpec(BattleModifierTarget.Stat, statId, default, operation, value);
            }

            public static ModifierSpec Damage(BattleDamageModifierStat damageStat, BattleModifierOperation operation, float value)
            {
                return new ModifierSpec(BattleModifierTarget.Damage, default, damageStat, operation, value);
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
            public readonly float TimeSeconds;
            public readonly int Order;
            public readonly BattleEffectConfig[] Effects;

            public AbilityEffectFrameSpec(string frameId, float timeSeconds, int order, BattleEffectConfig[] effects)
            {
                FrameId = frameId;
                TimeSeconds = timeSeconds;
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

        private readonly struct EmitterSpec
        {
            public readonly ProjectileEmitterAnchorMode AnchorMode;
            public readonly Vector2 AnchorOffset;
            public readonly int DurationTicks;
            public readonly int FireIntervalTicks;
            public readonly ProjectilePatternType PatternType;
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
                BattleEffectConfig[] impactEffects)
            {
                AnchorMode = anchorMode;
                AnchorOffset = anchorOffset;
                DurationTicks = durationTicks;
                FireIntervalTicks = fireIntervalTicks;
                PatternType = patternType;
                Direction = direction;
                ProjectileCount = projectileCount;
                Behavior = behavior;
                Radius = radius;
                Speed = speed;
                LifetimeTicks = lifetimeTicks;
                ImpactEffects = impactEffects;
            }

        }
    }
}
