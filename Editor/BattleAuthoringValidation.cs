#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Combat.Core.Battle;
using Combat.Unity.Authoring;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Combat.Unity.Editor
{
    public enum BattleAuthoringValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class BattleAuthoringValidationIssue
    {
        public BattleAuthoringValidationIssue(
            BattleAuthoringValidationSeverity severity,
            Object asset,
            string propertyPath,
            string message)
        {
            Severity = severity;
            Asset = asset;
            AssetPath = asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset);
            PropertyPath = propertyPath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public BattleAuthoringValidationSeverity Severity { get; }
        public Object Asset { get; }
        public string AssetPath { get; }
        public string PropertyPath { get; }
        public string Message { get; }
    }

    public sealed class BattleAuthoringValidationReport
    {
        private readonly List<BattleAuthoringValidationIssue> _issues = new List<BattleAuthoringValidationIssue>();
        private readonly ReadOnlyCollection<BattleAuthoringValidationIssue> _readOnlyIssues;

        public BattleAuthoringValidationReport()
        {
            _readOnlyIssues = new ReadOnlyCollection<BattleAuthoringValidationIssue>(_issues);
        }

        public IReadOnlyList<BattleAuthoringValidationIssue> Issues => _readOnlyIssues;
        public bool HasErrors
        {
            get
            {
                for (var i = 0; i < _issues.Count; i++)
                {
                    if (_issues[i].Severity == BattleAuthoringValidationSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        internal void AddError(Object asset, string propertyPath, string message)
        {
            _issues.Add(new BattleAuthoringValidationIssue(BattleAuthoringValidationSeverity.Error, asset, propertyPath, message));
        }
    }

    public static partial class BattleAuthoringValidator
    {
        internal static Func<BattleConditionGroup, BattleConditionProgram> ConditionCompilerForTesting { get; set; }

        [MenuItem("Combat/Authoring/Validate All Battle Assets")]
        public static void ValidateAllBattleAssetsMenu()
        {
            BattleAuthoringValidationReport report = ValidateProjectAssets();
            LogReport(report);
            if (report.HasErrors)
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                    return;
                }

                throw new InvalidOperationException("Battle authoring validation failed.");
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        public static BattleAuthoringValidationReport ValidateProjectAssets()
        {
            BattleAuthoringValidationReport report = ValidateAssets(
                LoadAssets<BattleScenarioAsset>(),
                LoadAssets<CombatantConfigAsset>(),
                LoadAssets<AbilityConfigAsset>(),
                LoadAssets<StatusConfigAsset>(),
                LoadAssets<ProjectileConfigAsset>(),
                LoadAssets<ProjectileEmitterConfigAsset>(),
                LoadAssets<AreaEffectConfigAsset>());
            ValidateSpatialMaps(report, LoadAssets<BattleSpatialMapAsset>());
            return report;
        }

        public static BattleAuthoringValidationReport ValidateScenarioGraph(BattleScenarioAsset scenario)
        {
            var scenarios = new List<BattleScenarioAsset>();
            var combatants = new List<CombatantConfigAsset>();
            var abilities = new List<AbilityConfigAsset>();
            var statuses = new List<StatusConfigAsset>();
            var projectileEmitters = new List<ProjectileEmitterConfigAsset>();
            var areaEffects = new List<AreaEffectConfigAsset>();

            if (scenario != null)
            {
                scenarios.Add(scenario);
                CollectScenarioGraph(scenario, combatants, abilities, statuses, projectileEmitters, areaEffects);
            }

            BattleAuthoringValidationReport report = ValidateAssets(
                scenarios,
                combatants,
                abilities,
                statuses,
                Array.Empty<ProjectileConfigAsset>(),
                projectileEmitters,
                areaEffects);
            if (scenario != null && scenario.SpatialMap != null)
            {
                ValidateSpatialMaps(report, new[] { scenario.SpatialMap });
            }

            return report;
        }

        public static BattleAuthoringValidationReport ValidateAssets(
            IReadOnlyList<BattleScenarioAsset> scenarios,
            IReadOnlyList<CombatantConfigAsset> combatants,
            IReadOnlyList<AbilityConfigAsset> abilities,
            IReadOnlyList<StatusConfigAsset> statuses)
        {
            return ValidateAssets(
                scenarios,
                combatants,
                abilities,
                statuses,
                Array.Empty<ProjectileConfigAsset>(),
                Array.Empty<ProjectileEmitterConfigAsset>(),
                Array.Empty<AreaEffectConfigAsset>());
        }

        public static BattleAuthoringValidationReport ValidateAssets(
            IReadOnlyList<BattleScenarioAsset> scenarios,
            IReadOnlyList<CombatantConfigAsset> combatants,
            IReadOnlyList<AbilityConfigAsset> abilities,
            IReadOnlyList<StatusConfigAsset> statuses,
            IReadOnlyList<ProjectileEmitterConfigAsset> projectileEmitters)
        {
            return ValidateAssets(
                scenarios,
                combatants,
                abilities,
                statuses,
                Array.Empty<ProjectileConfigAsset>(),
                projectileEmitters,
                Array.Empty<AreaEffectConfigAsset>());
        }

        public static BattleAuthoringValidationReport ValidateAssets(
            IReadOnlyList<BattleScenarioAsset> scenarios,
            IReadOnlyList<CombatantConfigAsset> combatants,
            IReadOnlyList<AbilityConfigAsset> abilities,
            IReadOnlyList<StatusConfigAsset> statuses,
            IReadOnlyList<ProjectileEmitterConfigAsset> projectileEmitters,
            IReadOnlyList<AreaEffectConfigAsset> areaEffects)
        {
            return ValidateAssets(
                scenarios,
                combatants,
                abilities,
                statuses,
                Array.Empty<ProjectileConfigAsset>(),
                projectileEmitters,
                areaEffects);
        }

        public static BattleAuthoringValidationReport ValidateAssets(
            IReadOnlyList<BattleScenarioAsset> scenarios,
            IReadOnlyList<CombatantConfigAsset> combatants,
            IReadOnlyList<AbilityConfigAsset> abilities,
            IReadOnlyList<StatusConfigAsset> statuses,
            IReadOnlyList<ProjectileConfigAsset> projectiles,
            IReadOnlyList<ProjectileEmitterConfigAsset> projectileEmitters,
            IReadOnlyList<AreaEffectConfigAsset> areaEffects)
        {
            var report = new BattleAuthoringValidationReport();
            scenarios = scenarios ?? Array.Empty<BattleScenarioAsset>();
            combatants = combatants ?? Array.Empty<CombatantConfigAsset>();
            abilities = abilities ?? Array.Empty<AbilityConfigAsset>();
            statuses = statuses ?? Array.Empty<StatusConfigAsset>();
            projectiles = projectiles ?? Array.Empty<ProjectileConfigAsset>();
            projectileEmitters = projectileEmitters ?? Array.Empty<ProjectileEmitterConfigAsset>();
            areaEffects = areaEffects ?? Array.Empty<AreaEffectConfigAsset>();

            ValidateDuplicateIds(report, combatants, "combatant", CombatantLabel);
            ValidateDuplicateIds(report, abilities, "ability", AbilityLabel);
            ValidateDuplicateIds(report, statuses, "status", StatusLabel);

            ValidateScenarios(report, scenarios);
            ValidateCombatants(report, combatants);
            ValidateAbilities(report, abilities);
            ValidateStatuses(report, statuses);
            ValidateProjectileConfigAssets(report, projectiles);
            ValidateProjectileEmitterAssets(report, projectileEmitters);
            ValidateAreaEffectAssets(report, areaEffects);
            if (!report.HasErrors)
            {
                ValidateConverterPass(report, scenarios, combatants, abilities, statuses, projectiles, projectileEmitters, areaEffects);
            }

            return report;
        }

        private static void ValidateScenarios(BattleAuthoringValidationReport report, IReadOnlyList<BattleScenarioAsset> scenarios)
        {
            for (var i = 0; i < scenarios.Count; i++)
            {
                BattleScenarioAsset scenario = scenarios[i];
                if (scenario == null)
                {
                    report.AddError(null, $"scenarios[{i}]", $"scenarios[{i}] is missing a BattleScenarioAsset reference.");
                    continue;
                }

                ValidateProjectileCulling(report, scenario);

                IReadOnlyList<SpawnEntry> spawns = scenario.InitialSpawns;
                if (spawns == null || spawns.Count <= 0)
                {
                    report.AddError(scenario, "initialSpawns", $"{ScenarioLabel(scenario)} initialSpawns is required.");
                    continue;
                }

                for (var spawnIndex = 0; spawnIndex < spawns.Count; spawnIndex++)
                {
                    if (spawns[spawnIndex].Combatant == null)
                    {
                        report.AddError(
                            scenario,
                            $"initialSpawns[{spawnIndex}].combatant",
                            $"{ScenarioLabel(scenario)} initialSpawns[{spawnIndex}].combatant is missing a combatant reference.");
                    }
                }
            }
        }

        private static void ValidateProjectileCulling(BattleAuthoringValidationReport report, BattleScenarioAsset scenario)
        {
            if (!scenario.ProjectileCullingEnabled)
            {
                return;
            }

            Vector2 size = scenario.ProjectileCullingSize;
            if (size.x <= 0f || size.y <= 0f)
            {
                report.AddError(
                    scenario,
                    "projectileCullingSize",
                    $"{ScenarioLabel(scenario)} projectileCullingSize must have positive width and height.");
            }

            if (scenario.ProjectileCullingPadding < 0f)
            {
                report.AddError(
                    scenario,
                    "projectileCullingPadding",
                    $"{ScenarioLabel(scenario)} projectileCullingPadding must be non-negative.");
            }
        }

        private static void ValidateCombatants(BattleAuthoringValidationReport report, IReadOnlyList<CombatantConfigAsset> combatants)
        {
            for (var i = 0; i < combatants.Count; i++)
            {
                CombatantConfigAsset combatant = combatants[i];
                if (combatant == null)
                {
                    report.AddError(null, $"combatants[{i}]", $"combatants[{i}] is missing a CombatantConfigAsset reference.");
                    continue;
                }

                ValidateDuplicateStats(report, combatant);
                ValidateCombatantAbilities(report, combatant);
                ValidateCombatantTargetingBehavior(report, combatant);
            }
        }

        private static void ValidateCombatantTargetingBehavior(
            BattleAuthoringValidationReport report,
            CombatantConfigAsset combatant)
        {
            if (!combatant.TargetingBehaviorEnabled)
            {
                return;
            }

            ValidatePositiveFiniteScalar(
                report,
                combatant,
                "targetAcquisitionRange",
                combatant.TargetAcquisitionRange);
            ValidatePositiveFiniteSeconds(
                report,
                combatant,
                "noProgressTimeoutSeconds",
                combatant.NoProgressTimeoutSeconds);
            ValidatePositiveFiniteScalar(
                report,
                combatant,
                "minimumProgressDistance",
                combatant.MinimumProgressDistance);
            ValidatePositiveFiniteSeconds(
                report,
                combatant,
                "rejectedTargetCooldownSeconds",
                combatant.RejectedTargetCooldownSeconds);
        }

        private static void ValidatePositiveFiniteScalar(
            BattleAuthoringValidationReport report,
            Object asset,
            string path,
            float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                report.AddError(asset, path, $"{AssetLabel(asset)} {path} must be finite.");
            }
            else if (value <= 0f)
            {
                report.AddError(asset, path, $"{AssetLabel(asset)} {path} must be greater than 0.");
            }
        }

        private static void ValidateAbilities(BattleAuthoringValidationReport report, IReadOnlyList<AbilityConfigAsset> abilities)
        {
            for (var i = 0; i < abilities.Count; i++)
            {
                AbilityConfigAsset ability = abilities[i];
                if (ability == null)
                {
                    report.AddError(null, $"abilities[{i}]", $"abilities[{i}] is missing an AbilityConfigAsset reference.");
                    continue;
                }

                if (ability.WindupSeconds < 0f)
                {
                    report.AddError(ability, "windupSeconds", $"{AbilityLabel(ability)} windupSeconds must be non-negative.");
                }

                if (ability.RecoverySeconds < 0f)
                {
                    report.AddError(ability, "recoverySeconds", $"{AbilityLabel(ability)} recoverySeconds must be non-negative.");
                }

                ValidateAbilityEffectFrames(report, ability);
            }
        }

        private static void ValidateProjectileEmitterAssets(BattleAuthoringValidationReport report, IReadOnlyList<ProjectileEmitterConfigAsset> projectileEmitters)
        {
            for (var i = 0; i < projectileEmitters.Count; i++)
            {
                ProjectileEmitterConfigAsset projectileEmitter = projectileEmitters[i];
                if (projectileEmitter == null)
                {
                    report.AddError(null, $"projectileEmitters[{i}]", $"projectileEmitters[{i}] is missing a ProjectileEmitterConfigAsset reference.");
                    continue;
                }

                ValidateProjectileEmitterAsset(
                    report,
                    projectileEmitter,
                    "projectileEmitter",
                    new List<ProjectileEmitterConfigAsset>(),
                    new List<AreaEffectConfigAsset>(),
                    BattleEffectAuthoringScope.Ability);
            }
        }

        private static void ValidateProjectileConfigAssets(
            BattleAuthoringValidationReport report,
            IReadOnlyList<ProjectileConfigAsset> projectiles)
        {
            for (var i = 0; i < projectiles.Count; i++)
            {
                ProjectileConfigAsset projectile = projectiles[i];
                if (projectile == null)
                {
                    report.AddError(null, $"projectiles[{i}]", $"projectiles[{i}] is missing a ProjectileConfigAsset reference.");
                    continue;
                }

                ValidateProjectileConfig(report, projectile, "projectile");
                ValidateBattleEffects(
                    report,
                    projectile,
                    projectile.ImpactEffects,
                    "projectile.impactEffects",
                    new List<ProjectileEmitterConfigAsset>(),
                    new List<AreaEffectConfigAsset>(),
                    BattleEffectAuthoringRules.ProjectileImpactScopeForParent(BattleEffectAuthoringScope.Ability));
            }
        }

        private static void ValidateAreaEffectAssets(BattleAuthoringValidationReport report, IReadOnlyList<AreaEffectConfigAsset> areaEffects)
        {
            for (var i = 0; i < areaEffects.Count; i++)
            {
                AreaEffectConfigAsset areaEffect = areaEffects[i];
                if (areaEffect == null)
                {
                    report.AddError(null, $"areaEffects[{i}]", $"areaEffects[{i}] is missing an AreaEffectConfigAsset reference.");
                    continue;
                }

                ValidateAreaEffectAsset(
                    report,
                    areaEffect,
                    "areaEffect",
                    new List<ProjectileEmitterConfigAsset>(),
                    new List<AreaEffectConfigAsset>());
            }
        }

        private static void ValidateStatuses(BattleAuthoringValidationReport report, IReadOnlyList<StatusConfigAsset> statuses)
        {
            for (var i = 0; i < statuses.Count; i++)
            {
                StatusConfigAsset status = statuses[i];
                if (status == null)
                {
                    report.AddError(null, $"statuses[{i}]", $"statuses[{i}] is missing a StatusConfigAsset reference.");
                    continue;
                }

                if (status.MaxStacks <= 0)
                {
                    report.AddError(status, $"{StatusLabel(status)}.MaxStacks", "Status MaxStacks must be greater than 0.");
                }

                if (!IsSupportedStatusStackPolicy(status.StackPolicy))
                {
                    report.AddError(status, $"{StatusLabel(status)}.StackPolicy", $"Status StackPolicy '{status.StackPolicy}' is unsupported.");
                }

                ValidateStatusModifiers(report, status);
                ValidateStatusTriggers(report, status);
            }

            ValidateCrossStatusModifierConflicts(report, statuses);

            var reportedRecursions = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < statuses.Count; i++)
            {
                if (statuses[i] != null)
                {
                    ValidateStatusRecursion(report, statuses[i], new List<StatusConfigAsset>(), reportedRecursions);
                }
            }
        }

        private static bool IsSupportedStatusStackPolicy(StatusStackPolicy stackPolicy)
        {
            switch (stackPolicy)
            {
                case StatusStackPolicy.RefreshDurationAndAddStack:
                    return true;
                default:
                    return false;
            }
        }

        private static void ValidateStatusModifiers(BattleAuthoringValidationReport report, StatusConfigAsset status)
        {
            IReadOnlyList<BattleModifierConfig> modifiers = status.Modifiers;
            if (modifiers == null)
            {
                report.AddError(status, "modifiers", $"{StatusLabel(status)} modifiers list is required.");
                return;
            }

            var overrides = new HashSet<string>(StringComparer.Ordinal);
            var minClamps = new Dictionary<string, float>(StringComparer.Ordinal);
            var maxClamps = new Dictionary<string, float>(StringComparer.Ordinal);

            for (var i = 0; i < modifiers.Count; i++)
            {
                BattleModifierConfig modifier = modifiers[i];
                string path = $"modifiers[{i}]";
                if (modifier == null)
                {
                    report.AddError(status, path, $"{StatusLabel(status)} {path} is missing.");
                    continue;
                }

                bool targetValid = ValidateBattleModifierTarget(report, status, modifier.Target, $"{path}.target");
                bool operationValid = ValidateBattleModifierOperation(report, status, modifier.Operation, $"{path}.operation");
                string key = null;
                bool keyValid = false;

                if (targetValid)
                {
                    switch (modifier.Target)
                    {
                        case BattleModifierTarget.Stat:
                            keyValid = ValidateStatModifierId(report, status, modifier.StatId, $"{path}.statId");
                            key = $"Stat:{modifier.StatId}";
                            break;
                        case BattleModifierTarget.Damage:
                            keyValid = ValidateBattleDamageModifierStat(report, status, modifier.DamageStat, $"{path}.damageStat");
                            key = $"Damage:{modifier.DamageStat}";
                            break;
                    }
                }

                if (!operationValid || !keyValid)
                {
                    continue;
                }

                if (modifier.Operation == BattleModifierOperation.Override && !overrides.Add(key))
                {
                    report.AddError(status, path, $"{StatusLabel(status)} has multiple Override modifiers for {key}.");
                }
                else if (modifier.Operation == BattleModifierOperation.MinClamp)
                {
                    if (!minClamps.TryGetValue(key, out float currentMin) || modifier.Value > currentMin)
                    {
                        minClamps[key] = modifier.Value;
                    }
                }
                else if (modifier.Operation == BattleModifierOperation.MaxClamp)
                {
                    if (!maxClamps.TryGetValue(key, out float currentMax) || modifier.Value < currentMax)
                    {
                        maxClamps[key] = modifier.Value;
                    }
                }
            }

            foreach (KeyValuePair<string, float> min in minClamps)
            {
                if (maxClamps.TryGetValue(min.Key, out float max) && min.Value > max)
                {
                    report.AddError(status, "modifiers", $"{StatusLabel(status)} modifier {min.Key} MinClamp cannot be greater than MaxClamp.");
                }
            }
        }

        private static void ValidateCrossStatusModifierConflicts(BattleAuthoringValidationReport report, IReadOnlyList<StatusConfigAsset> statuses)
        {
            var overrides = new Dictionary<string, StatusConfigAsset>(StringComparer.Ordinal);
            var minClamps = new Dictionary<string, ModifierClampRecord>(StringComparer.Ordinal);
            var maxClamps = new Dictionary<string, ModifierClampRecord>(StringComparer.Ordinal);

            for (var statusIndex = 0; statusIndex < statuses.Count; statusIndex++)
            {
                StatusConfigAsset status = statuses[statusIndex];
                if (status == null)
                {
                    continue;
                }

                IReadOnlyList<BattleModifierConfig> modifiers = status.Modifiers;
                if (modifiers == null)
                {
                    continue;
                }

                for (var modifierIndex = 0; modifierIndex < modifiers.Count; modifierIndex++)
                {
                    BattleModifierConfig modifier = modifiers[modifierIndex];
                    if (modifier == null ||
                        !IsValidBattleModifierOperation(modifier.Operation) ||
                        !TryGetValidModifierKey(modifier, out string key))
                    {
                        continue;
                    }

                    if (modifier.Operation == BattleModifierOperation.Override)
                    {
                        if (overrides.TryGetValue(key, out StatusConfigAsset firstStatus))
                        {
                            if (firstStatus != status)
                            {
                                report.AddError(
                                    status,
                                    "modifiers",
                                    $"{StatusLabel(status)} and {StatusLabel(firstStatus)} have multiple Override modifiers for {key} across statuses.");
                            }
                        }
                        else
                        {
                            overrides.Add(key, status);
                        }
                    }
                    else if (modifier.Operation == BattleModifierOperation.MinClamp)
                    {
                        if (!minClamps.TryGetValue(key, out ModifierClampRecord currentMin) || modifier.Value > currentMin.Value)
                        {
                            minClamps[key] = new ModifierClampRecord(status, modifier.Value);
                        }
                    }
                    else if (modifier.Operation == BattleModifierOperation.MaxClamp)
                    {
                        if (!maxClamps.TryGetValue(key, out ModifierClampRecord currentMax) || modifier.Value < currentMax.Value)
                        {
                            maxClamps[key] = new ModifierClampRecord(status, modifier.Value);
                        }
                    }
                }
            }

            foreach (KeyValuePair<string, ModifierClampRecord> min in minClamps)
            {
                if (maxClamps.TryGetValue(min.Key, out ModifierClampRecord max) &&
                    min.Value.Value > max.Value &&
                    min.Value.Status != max.Status)
                {
                    report.AddError(
                        max.Status,
                        "modifiers",
                        $"{StatusLabel(min.Value.Status)} and {StatusLabel(max.Status)} modifier {min.Key} MinClamp cannot be greater than MaxClamp across statuses.");
                }
            }
        }

        private static bool TryGetValidModifierKey(BattleModifierConfig modifier, out string key)
        {
            key = null;
            if (!IsValidBattleModifierTarget(modifier.Target))
            {
                return false;
            }

            switch (modifier.Target)
            {
                case BattleModifierTarget.Stat:
                    if (!IsSupportedStatModifierId(modifier.StatId))
                    {
                        return false;
                    }

                    key = $"Stat:{modifier.StatId}";
                    return true;
                case BattleModifierTarget.Damage:
                    if (!IsValidBattleDamageModifierStat(modifier.DamageStat))
                    {
                        return false;
                    }

                    key = $"Damage:{modifier.DamageStat}";
                    return true;
                default:
                    return false;
            }
        }

        private static bool ValidateBattleModifierTarget(
            BattleAuthoringValidationReport report,
            StatusConfigAsset status,
            BattleModifierTarget target,
            string path)
        {
            if (IsValidBattleModifierTarget(target))
            {
                return true;
            }

            report.AddError(status, path, $"{StatusLabel(status)} {path} has unsupported BattleModifierTarget '{target}'.");
            return false;
        }

        private static bool ValidateBattleModifierOperation(
            BattleAuthoringValidationReport report,
            StatusConfigAsset status,
            BattleModifierOperation operation,
            string path)
        {
            if (IsValidBattleModifierOperation(operation))
            {
                return true;
            }

            report.AddError(status, path, $"{StatusLabel(status)} {path} has unsupported BattleModifierOperation '{operation}'.");
            return false;
        }

        private static bool ValidateStatModifierId(
            BattleAuthoringValidationReport report,
            StatusConfigAsset status,
            BattleStatId stat,
            string path)
        {
            if (IsSupportedStatModifierId(stat))
            {
                return true;
            }

            report.AddError(status, path, $"{StatusLabel(status)} {path} has unsupported stat modifier id '{stat}'.");
            return false;
        }

        private static bool ValidateBattleDamageModifierStat(
            BattleAuthoringValidationReport report,
            StatusConfigAsset status,
            BattleDamageModifierStat damageStat,
            string path)
        {
            if (IsValidBattleDamageModifierStat(damageStat))
            {
                return true;
            }

            report.AddError(status, path, $"{StatusLabel(status)} {path} has unsupported BattleDamageModifierStat '{damageStat}'.");
            return false;
        }

        private static bool IsValidBattleModifierTarget(BattleModifierTarget target)
        {
            switch (target)
            {
                case BattleModifierTarget.Damage:
                case BattleModifierTarget.Stat:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsValidBattleModifierOperation(BattleModifierOperation operation)
        {
            switch (operation)
            {
                case BattleModifierOperation.Flat:
                case BattleModifierOperation.PercentAdd:
                case BattleModifierOperation.Override:
                case BattleModifierOperation.MinClamp:
                case BattleModifierOperation.MaxClamp:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsSupportedStatModifierId(BattleStatId stat)
        {
            switch (stat)
            {
                case BattleStatId.MaxHealth:
                case BattleStatId.MoveSpeed:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsValidBattleDamageModifierStat(BattleDamageModifierStat damageStat)
        {
            switch (damageStat)
            {
                case BattleDamageModifierStat.DamageDealt:
                case BattleDamageModifierStat.DamageTaken:
                    return true;
                default:
                    return false;
            }
        }

        private readonly struct ModifierClampRecord
        {
            public readonly StatusConfigAsset Status;
            public readonly float Value;

            public ModifierClampRecord(StatusConfigAsset status, float value)
            {
                Status = status;
                Value = value;
            }
        }

        private static void ValidateDuplicateIds<T>(
            BattleAuthoringValidationReport report,
            IReadOnlyList<T> assets,
            string kind,
            Func<T, string> idSelector)
            where T : Object
        {
            var firstById = new Dictionary<string, T>(StringComparer.Ordinal);
            for (var i = 0; i < assets.Count; i++)
            {
                T asset = assets[i];
                if (asset == null)
                {
                    continue;
                }

                string id = idSelector(asset);
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (firstById.ContainsKey(id))
                {
                    report.AddError(asset, "id", $"{AssetLabel(asset)} has duplicate {kind} id '{id}'.");
                    continue;
                }

                firstById.Add(id, asset);
            }
        }

        private static void ValidateDuplicateStats(BattleAuthoringValidationReport report, CombatantConfigAsset combatant)
        {
            IReadOnlyList<BattleStatConfig> stats = combatant.Stats;
            if (stats == null)
            {
                report.AddError(combatant, "stats", $"{CombatantLabel(combatant)} stats list is required.");
                return;
            }

            var seen = new HashSet<BattleStatId>();
            for (var i = 0; i < stats.Count; i++)
            {
                BattleStatId stat = stats[i].Stat;
                if (!seen.Add(stat))
                {
                    report.AddError(combatant, $"stats[{i}]", $"{CombatantLabel(combatant)} stats[{i}] duplicates stat {stat}.");
                }
            }
        }

        private static void ValidateCombatantAbilities(BattleAuthoringValidationReport report, CombatantConfigAsset combatant)
        {
            IReadOnlyList<AbilityConfigAsset> abilities = combatant.Abilities;
            AbilityConfigAsset basicAbility = combatant.BasicAbility;
            if (basicAbility == null)
            {
                report.AddError(combatant, "basicAbility", $"{CombatantLabel(combatant)} basicAbility is missing an ability reference.");
            }

            if (abilities == null)
            {
                report.AddError(combatant, "abilities", $"{CombatantLabel(combatant)} abilities list is required.");
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (basicAbility != null)
            {
                seen.Add(basicAbility.Id);
            }

            for (var i = 0; i < abilities.Count; i++)
            {
                AbilityConfigAsset ability = abilities[i];
                if (ability == null)
                {
                    report.AddError(combatant, $"abilities[{i}]", $"{CombatantLabel(combatant)} abilities[{i}] is missing an ability reference.");
                    continue;
                }

                if (!seen.Add(ability.Id))
                {
                    report.AddError(combatant, $"abilities[{i}]", $"{CombatantLabel(combatant)} abilities[{i}] has duplicate ability id '{ability.Id}'.");
                }
            }
        }

        private static void ValidateProjectileEmitter(
            BattleAuthoringValidationReport report,
            ProjectileEmitterConfigAsset emitter,
            string path,
            List<ProjectileEmitterConfigAsset> visitingEmitterAssets,
            List<AreaEffectConfigAsset> visitingAreaEffectAssets,
            BattleEffectAuthoringScope scope)
        {
            ValidateProjectileEmitterAnchorMode(report, emitter, emitter.AnchorMode, $"{path}.anchorMode");
            ValidatePositiveFiniteSeconds(report, emitter, $"{path}.durationSeconds", emitter.DurationSeconds);
            ValidatePositiveFiniteSeconds(report, emitter, $"{path}.fireIntervalSeconds", emitter.FireIntervalSeconds);
            ValidateProjectilePattern(report, emitter, emitter.Pattern, $"{path}.pattern");
            if (emitter.Projectile == null)
            {
                report.AddError(emitter, $"{path}.projectile", $"{AssetLabel(emitter)} {path}.projectile is missing a projectile reference.");
                return;
            }

            ValidateProjectileConfig(report, emitter.Projectile, "projectile");
            ValidateBattleEffects(
                report,
                emitter.Projectile,
                emitter.Projectile.ImpactEffects,
                "projectile.impactEffects",
                visitingEmitterAssets,
                visitingAreaEffectAssets,
                BattleEffectAuthoringRules.ProjectileImpactScopeForParent(scope));
        }

        private static void ValidatePositiveFiniteSeconds(BattleAuthoringValidationReport report, Object asset, string path, float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                report.AddError(asset, path, $"{AssetLabel(asset)} {path} must be finite.");
            }
            else if (seconds <= 0f)
            {
                report.AddError(asset, path, $"{AssetLabel(asset)} {path} must be greater than 0.");
            }
        }

        private static void ValidateProjectileEmitterAnchorMode(BattleAuthoringValidationReport report, Object asset, ProjectileEmitterAnchorMode anchorMode, string path)
        {
            switch (anchorMode)
            {
                case ProjectileEmitterAnchorMode.FollowSource:
                case ProjectileEmitterAnchorMode.FixedPosition:
                    break;
                default:
                    report.AddError(asset, path, $"{AssetLabel(asset)} {path} has unsupported ProjectileEmitterAnchorMode '{anchorMode}'.");
                    break;
            }
        }

        private static void ValidateProjectilePattern(BattleAuthoringValidationReport report, Object asset, ProjectilePatternConfig pattern, string path)
        {
            ValidateProjectileDirectionMode(report, asset, pattern.DirectionMode, $"{path}.directionMode");
            switch (pattern.Type)
            {
                case ProjectilePatternType.Single:
                    if (pattern.DirectionMode == ProjectileDirectionMode.FixedDirection && pattern.Direction.sqrMagnitude <= 0.00001f)
                    {
                        report.AddError(asset, $"{path}.direction", $"{AssetLabel(asset)} {path}.direction must be non-zero for fixed single projectile patterns.");
                    }

                    break;
                case ProjectilePatternType.Circle:
                    if (pattern.ProjectileCount <= 0)
                    {
                        report.AddError(asset, $"{path}.projectileCount", $"{AssetLabel(asset)} {path}.projectileCount must be greater than 0.");
                    }

                    break;
                default:
                    report.AddError(asset, $"{path}.type", $"{AssetLabel(asset)} {path}.type has unsupported ProjectilePatternType '{pattern.Type}'.");
                    break;
            }
        }

        private static void ValidateProjectileDirectionMode(BattleAuthoringValidationReport report, Object asset, ProjectileDirectionMode directionMode, string path)
        {
            switch (directionMode)
            {
                case ProjectileDirectionMode.FixedDirection:
                case ProjectileDirectionMode.TargetDirection:
                    break;
                default:
                    report.AddError(asset, path, $"{AssetLabel(asset)} {path} has unsupported ProjectileDirectionMode '{directionMode}'.");
                    break;
            }
        }

        private static void ValidateProjectileConfig(BattleAuthoringValidationReport report, ProjectileConfigAsset projectile, string path)
        {
            ValidateProjectileBehavior(report, projectile, projectile.Behavior, $"{path}.behavior");
            ValidateProjectileHitPolicy(
                report,
                projectile,
                projectile.HitPolicyMode,
                projectile.MaxHitCount,
                $"{path}.hitPolicy");

            if (projectile.Radius <= 0f || float.IsNaN(projectile.Radius) || float.IsInfinity(projectile.Radius))
            {
                report.AddError(projectile, $"{path}.radius", $"{AssetLabel(projectile)} {path}.radius must be finite and greater than 0.");
            }

            if (projectile.Speed <= 0f || float.IsNaN(projectile.Speed) || float.IsInfinity(projectile.Speed))
            {
                report.AddError(projectile, $"{path}.speed", $"{AssetLabel(projectile)} {path}.speed must be finite and greater than 0.");
            }

            ValidatePositiveFiniteSeconds(report, projectile, $"{path}.lifetimeSeconds", projectile.LifetimeSeconds);
            if (projectile.ImpactEffects == null || projectile.ImpactEffects.Length <= 0)
            {
                report.AddError(projectile, $"{path}.impactEffects", $"{AssetLabel(projectile)} {path}.impactEffects requires at least one effect.");
            }
        }

        private static void ValidateProjectileBehavior(BattleAuthoringValidationReport report, Object asset, ProjectileBehavior behavior, string path)
        {
            switch (behavior)
            {
                case ProjectileBehavior.Linear:
                    break;
                default:
                    report.AddError(asset, path, $"{AssetLabel(asset)} {path} has unsupported ProjectileBehavior '{behavior}'.");
                    break;
            }
        }

        private static void ValidateProjectileHitPolicy(
            BattleAuthoringValidationReport report,
            Object asset,
            ProjectileHitPolicyMode mode,
            int maxHitCount,
            string path)
        {
            switch (mode)
            {
                case ProjectileHitPolicyMode.DestroyOnFirstHit:
                    break;
                case ProjectileHitPolicyMode.Pierce:
                    if (maxHitCount < 2)
                    {
                        report.AddError(
                            asset,
                            $"{path}.maxHitCount",
                            $"{AssetLabel(asset)} {path}.maxHitCount must be at least 2 for piercing projectiles.");
                    }

                    break;
                default:
                    report.AddError(
                        asset,
                        $"{path}.mode",
                        $"{AssetLabel(asset)} {path}.mode has unsupported ProjectileHitPolicyMode '{mode}'.");
                    break;
            }
        }

        private static void ValidateAbilityEffectFrames(BattleAuthoringValidationReport report, AbilityConfigAsset ability)
        {
            IReadOnlyList<AbilityEffectFrameConfig> frames = ability.EffectFrames;
            if (frames == null)
            {
                report.AddError(ability, "effectFrames", $"{AbilityLabel(ability)} effectFrames list is required.");
                return;
            }

            for (var i = 0; i < frames.Count; i++)
            {
                AbilityEffectFrameConfig frame = frames[i];
                string path = $"effectFrames[{i}]";
                if (frame == null)
                {
                    report.AddError(ability, path, $"{AbilityLabel(ability)} {path} is missing.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(frame.FrameId))
                {
                    report.AddError(ability, $"{path}.frameId", $"{AbilityLabel(ability)} {path}.frameId is required.");
                }

                if (float.IsNaN(frame.TimeSeconds) || float.IsInfinity(frame.TimeSeconds))
                {
                    report.AddError(ability, $"{path}.timeSeconds", $"{AbilityLabel(ability)} {path}.timeSeconds must be finite.");
                }
                else if (frame.TimeSeconds < 0f)
                {
                    report.AddError(ability, $"{path}.timeSeconds", $"{AbilityLabel(ability)} {path}.timeSeconds must be non-negative.");
                }

                if (frame.Order < 0)
                {
                    report.AddError(ability, $"{path}.order", $"{AbilityLabel(ability)} {path}.order must be non-negative.");
                }

                IReadOnlyList<BattleEffectConfig> effects = frame.Effects;
                if (effects == null || effects.Count <= 0)
                {
                    report.AddError(ability, $"{path}.effects", $"{AbilityLabel(ability)} {path}.effects requires at least one effect.");
                    continue;
                }

                ValidateBattleEffects(
                    report,
                    ability,
                    effects,
                    $"{path}.effects",
                    new List<ProjectileEmitterConfigAsset>(),
                    new List<AreaEffectConfigAsset>(),
                    BattleEffectAuthoringScope.Ability,
                    ability.TargetSelection);
            }
        }

        private static void ValidateBattleEffects(
            BattleAuthoringValidationReport report,
            Object asset,
            IReadOnlyList<BattleEffectConfig> effects,
            string path,
            List<ProjectileEmitterConfigAsset> visitingEmitterAssets,
            List<AreaEffectConfigAsset> visitingAreaEffectAssets,
            BattleEffectAuthoringScope scope,
            AbilityTargetSelection abilityTargetSelection = AbilityTargetSelection.CurrentEnemyTarget)
        {
            if (effects == null)
            {
                report.AddError(asset, path, $"{AssetLabel(asset)} {path} list is required.");
                return;
            }

            for (var i = 0; i < effects.Count; i++)
            {
                ValidateBattleEffect(
                    report,
                    asset,
                    effects[i],
                    $"{path}[{i}]",
                    visitingEmitterAssets,
                    visitingAreaEffectAssets,
                    scope,
                    abilityTargetSelection);
            }
        }

        private static void ValidateBattleEffect(
            BattleAuthoringValidationReport report,
            Object asset,
            BattleEffectConfig effect,
            string path,
            List<ProjectileEmitterConfigAsset> visitingEmitterAssets,
            List<AreaEffectConfigAsset> visitingAreaEffectAssets,
            BattleEffectAuthoringScope scope,
            AbilityTargetSelection abilityTargetSelection = AbilityTargetSelection.CurrentEnemyTarget)
        {
            switch (effect.Type)
            {
                case BattleEffectType.Damage:
                    if (effect.Amount <= 0)
                    {
                        report.AddError(asset, $"{path}.amount", $"{AssetLabel(asset)} {path}.amount must be greater than 0.");
                    }

                    break;
                case BattleEffectType.Heal:
                    if (!BattleEffectAuthoringRules.AllowsDirectHeal(scope, abilityTargetSelection))
                    {
                        report.AddError(asset, path, $"{AssetLabel(asset)} {path} Heal requires explicit target context; use Self or LowestHealthAlly target selection, AreaEffect, or status reaction.");
                    }

                    if (effect.Amount <= 0)
                    {
                        report.AddError(asset, $"{path}.amount", $"{AssetLabel(asset)} {path}.amount must be greater than 0.");
                    }

                    break;
                case BattleEffectType.ApplyStatus:
                    if (effect.Status == null)
                    {
                        report.AddError(asset, $"{path}.status", $"{AssetLabel(asset)} {path}.status is missing a status reference.");
                    }

                    break;
                case BattleEffectType.SpawnProjectileEmitter:
                    if (effect.ProjectileEmitter == null)
                    {
                        report.AddError(asset, $"{path}.projectileEmitter", $"{AssetLabel(asset)} {path}.projectileEmitter is missing a projectile emitter reference.");
                        break;
                    }

                    ValidateProjectileEmitterAsset(
                        report,
                        effect.ProjectileEmitter,
                        "projectileEmitter",
                        visitingEmitterAssets,
                        visitingAreaEffectAssets,
                        scope);
                    break;
                case BattleEffectType.AreaEffect:
                    if (effect.AreaEffect == null)
                    {
                        report.AddError(asset, $"{path}.areaEffect", $"{AssetLabel(asset)} {path}.areaEffect is missing an area effect reference.");
                        if (!BattleEffectAuthoringRules.AllowsAreaEffect(scope))
                        {
                            report.AddError(asset, $"{path}.areaEffect", $"{AssetLabel(asset)} {path}.areaEffect has a nested AreaEffect reference; recursive areaEffect chains are not supported.");
                        }

                        break;
                    }

                    if (!BattleEffectAuthoringRules.AllowsAreaEffect(scope))
                    {
                        report.AddError(asset, $"{path}.areaEffect", $"{AssetLabel(asset)} {path}.areaEffect has a nested AreaEffect reference; recursive areaEffect chains are not supported.");
                        break;
                    }

                    ValidateAreaEffectAsset(
                        report,
                        effect.AreaEffect,
                        "areaEffect",
                        visitingEmitterAssets,
                        visitingAreaEffectAssets);
                    break;
            }
        }

        private static void ValidateProjectileEmitterAsset(
            BattleAuthoringValidationReport report,
            ProjectileEmitterConfigAsset asset,
            string path,
            List<ProjectileEmitterConfigAsset> visitingEmitterAssets,
            List<AreaEffectConfigAsset> visitingAreaEffectAssets,
            BattleEffectAuthoringScope scope)
        {
            int existingIndex = visitingEmitterAssets.IndexOf(asset);
            if (existingIndex >= 0)
            {
                report.AddError(asset, path, $"{AssetLabel(asset)} has a recursive projectile emitter reference.");
                return;
            }

            visitingEmitterAssets.Add(asset);
            ValidateProjectileEmitter(report, asset, path, visitingEmitterAssets, visitingAreaEffectAssets, scope);
            visitingEmitterAssets.RemoveAt(visitingEmitterAssets.Count - 1);
        }

        private static void ValidateAreaEffectAsset(
            BattleAuthoringValidationReport report,
            AreaEffectConfigAsset asset,
            string path,
            List<ProjectileEmitterConfigAsset> visitingEmitterAssets,
            List<AreaEffectConfigAsset> visitingAreaEffectAssets)
        {
            int existingIndex = visitingAreaEffectAssets.IndexOf(asset);
            if (existingIndex >= 0)
            {
                report.AddError(asset, path, $"{AssetLabel(asset)} has a recursive areaEffect reference.");
                return;
            }

            visitingAreaEffectAssets.Add(asset);
            if (float.IsNaN(asset.Radius) || float.IsInfinity(asset.Radius))
            {
                report.AddError(asset, $"{path}.radius", $"{AssetLabel(asset)} {path}.radius must be finite.");
            }
            else if (asset.Radius <= 0f)
            {
                report.AddError(asset, $"{path}.radius", $"{AssetLabel(asset)} {path}.radius must be greater than 0.");
            }

            if (!IsValidAreaEffectTargetFilter(asset.TargetFilter))
            {
                report.AddError(asset, $"{path}.targetFilter", $"{AssetLabel(asset)} {path}.targetFilter has unsupported AreaEffectTargetFilter '{asset.TargetFilter}'.");
            }

            IReadOnlyList<BattleEffectConfig> effects = asset.Effects;
            if (effects == null || effects.Count <= 0)
            {
                report.AddError(asset, $"{path}.effects", $"{AssetLabel(asset)} {path}.effects requires at least one child effect.");
            }
            else
            {
                ValidateBattleEffects(
                    report,
                    asset,
                    effects,
                    $"{path}.effects",
                    visitingEmitterAssets,
                    visitingAreaEffectAssets,
                    BattleEffectAuthoringScope.AreaChild);
            }

            visitingAreaEffectAssets.RemoveAt(visitingAreaEffectAssets.Count - 1);
        }

        private static bool IsValidAreaEffectTargetFilter(AreaEffectTargetFilter targetFilter)
        {
            switch (targetFilter)
            {
                case AreaEffectTargetFilter.Allies:
                case AreaEffectTargetFilter.Enemies:
                case AreaEffectTargetFilter.AllUnits:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsValidBattleConditionMatchMode(BattleConditionMatchMode matchMode)
        {
            switch (matchMode)
            {
                case BattleConditionMatchMode.All:
                case BattleConditionMatchMode.Any:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsValidBattleConditionSubject(BattleConditionSubject subject)
        {
            switch (subject)
            {
                case BattleConditionSubject.Owner:
                case BattleConditionSubject.Source:
                case BattleConditionSubject.Target:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsValidBattleConditionComparison(BattleConditionComparison comparison)
        {
            switch (comparison)
            {
                case BattleConditionComparison.Equal:
                case BattleConditionComparison.NotEqual:
                case BattleConditionComparison.Less:
                case BattleConditionComparison.LessOrEqual:
                case BattleConditionComparison.Greater:
                case BattleConditionComparison.GreaterOrEqual:
                    return true;
                default:
                    return false;
            }
        }

        private static void ValidateStatusTriggers(BattleAuthoringValidationReport report, StatusConfigAsset status)
        {
            IReadOnlyList<StatusTriggerConfig> triggers = status.Triggers;
            if (triggers == null)
            {
                report.AddError(status, "triggers", $"{StatusLabel(status)} triggers list is required.");
                return;
            }

            for (var triggerIndex = 0; triggerIndex < triggers.Count; triggerIndex++)
            {
                StatusTriggerConfig trigger = triggers[triggerIndex];
                if (!IsValidBattleConditionMatchMode(trigger.ConditionMatchMode))
                {
                    report.AddError(status, $"triggers[{triggerIndex}].conditionMatchMode", $"{StatusLabel(status)} triggers[{triggerIndex}].conditionMatchMode has unsupported BattleConditionMatchMode '{trigger.ConditionMatchMode}'.");
                }

                ValidateStatusTriggerConditions(report, status, trigger.Conditions, $"triggers[{triggerIndex}].conditions");

                IReadOnlyList<StatusReactionEffectConfig> effects = trigger.Effects;
                if (effects == null)
                {
                    report.AddError(status, $"triggers[{triggerIndex}].effects", $"{StatusLabel(status)} triggers[{triggerIndex}].effects list is required.");
                    continue;
                }

                for (var effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                {
                    StatusReactionEffectConfig effect = effects[effectIndex];
                    ValidateBattleEffect(
                        report,
                        status,
                        effect.Effect,
                        $"triggers[{triggerIndex}].effects[{effectIndex}].effect",
                        new List<ProjectileEmitterConfigAsset>(),
                        new List<AreaEffectConfigAsset>(),
                        BattleEffectAuthoringScope.StatusReaction);
                }
            }
        }

        private static void ValidateStatusTriggerConditions(
            BattleAuthoringValidationReport report,
            StatusConfigAsset status,
            IReadOnlyList<BattleConditionConfig> conditions,
            string path)
        {
            if (conditions == null)
            {
                report.AddError(status, path, $"{StatusLabel(status)} {path} list is required.");
                return;
            }

            for (var conditionIndex = 0; conditionIndex < conditions.Count; conditionIndex++)
            {
                ValidateStatusTriggerCondition(report, status, conditions[conditionIndex], $"{path}[{conditionIndex}]");
            }
        }

        private static void ValidateStatusTriggerCondition(
            BattleAuthoringValidationReport report,
            StatusConfigAsset status,
            BattleConditionConfig condition,
            string path)
        {
            bool comparisonValid = IsValidBattleConditionComparison(condition.Comparison);
            if (!comparisonValid)
            {
                report.AddError(status, $"{path}.comparison", $"{StatusLabel(status)} {path}.comparison has unsupported BattleConditionComparison '{condition.Comparison}'.");
            }

            bool leftValid = ValidateStatusTriggerConditionOperand(report, status, condition.Left, $"{path}.left", out BattleConditionOperandValueKind leftKind);
            bool rightValid = ValidateStatusTriggerConditionOperand(report, status, condition.Right, $"{path}.right", out BattleConditionOperandValueKind rightKind);
            if (!comparisonValid || !leftValid || !rightValid)
            {
                return;
            }

            if (leftKind != rightKind)
            {
                report.AddError(status, path, $"{StatusLabel(status)} {path} operands must resolve to the same value kind.");
                return;
            }

            if (!AllowsComparisonForValueKind(leftKind, condition.Comparison))
            {
                report.AddError(status, $"{path}.comparison", $"{StatusLabel(status)} {path}.comparison is not supported for {leftKind} operands.");
                return;
            }

            try
            {
                CompileConditionGroup(new BattleConditionGroup(BattleConditionMatchMode.All, new[]
                {
                    BattleConditionDefinition.Compare(
                        BuildValidationOperand(condition.Left),
                        condition.Comparison,
                        BuildValidationOperand(condition.Right))
                }));
            }
            catch (ArgumentException exception)
            {
                report.AddError(status, path, $"{StatusLabel(status)} {path} condition compile failed: {exception.Message}");
            }
        }

        private static BattleConditionProgram CompileConditionGroup(BattleConditionGroup group)
        {
            Func<BattleConditionGroup, BattleConditionProgram> compiler = ConditionCompilerForTesting;
            return compiler == null ? BattleConditionCompiler.Compile(group) : compiler(group);
        }

        private static bool ValidateStatusTriggerConditionOperand(
            BattleAuthoringValidationReport report,
            StatusConfigAsset status,
            BattleConditionOperandConfig operand,
            string path,
            out BattleConditionOperandValueKind valueKind)
        {
            valueKind = default;
            if (operand == null)
            {
                report.AddError(status, path, $"{StatusLabel(status)} {path} operand is required.");
                return false;
            }

            valueKind = operand.ValueKind;
            var issues = new List<BattleConditionAuthoringValidationIssue>();
            operand.Validate(issues, path);
            for (var i = 0; i < issues.Count; i++)
            {
                BattleConditionAuthoringValidationIssue issue = issues[i];
                report.AddError(status, issue.Path, $"{StatusLabel(status)} {issue.Path} {issue.Message}");
            }

            return issues.Count == 0;
        }

        private static BattleConditionOperandDefinition BuildValidationOperand(BattleConditionOperandConfig operand)
        {
            if (operand == null)
            {
                throw new ArgumentException("Condition operand is required.", nameof(operand));
            }

            return operand.BuildDefinition();
        }

        private static bool AllowsComparisonForValueKind(BattleConditionOperandValueKind valueKind, BattleConditionComparison comparison)
        {
            if (valueKind == BattleConditionOperandValueKind.Bool || valueKind == BattleConditionOperandValueKind.Identifier)
            {
                return comparison == BattleConditionComparison.Equal || comparison == BattleConditionComparison.NotEqual;
            }

            return true;
        }

        private static void ValidateStatusRecursion(
            BattleAuthoringValidationReport report,
            StatusConfigAsset status,
            List<StatusConfigAsset> visiting,
            HashSet<string> reportedRecursions)
        {
            int existingIndex = visiting.IndexOf(status);
            if (existingIndex >= 0)
            {
                string key = RecursionKey(visiting, existingIndex, status);
                if (reportedRecursions.Add(key))
                {
                    report.AddError(status, "triggers", $"{StatusLabel(status)} has a recursive status trigger reference.");
                }

                return;
            }

            visiting.Add(status);
            try
            {
                IReadOnlyList<StatusTriggerConfig> triggers = status.Triggers;
                if (triggers == null)
                {
                    return;
                }

                for (var triggerIndex = 0; triggerIndex < triggers.Count; triggerIndex++)
                {
                    IReadOnlyList<StatusReactionEffectConfig> effects = triggers[triggerIndex].Effects;
                    if (effects == null)
                    {
                        continue;
                    }

                    for (var effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                    {
                        StatusReactionEffectConfig effect = effects[effectIndex];
                        if (effect.Effect.Type == BattleEffectType.ApplyStatus && effect.Effect.Status != null)
                        {
                            ValidateStatusRecursion(report, effect.Effect.Status, visiting, reportedRecursions);
                        }
                    }
                }
            }
            finally
            {
                visiting.RemoveAt(visiting.Count - 1);
            }
        }

        private static string RecursionKey(List<StatusConfigAsset> visiting, int existingIndex, StatusConfigAsset repeated)
        {
            var key = new System.Text.StringBuilder();
            for (var i = existingIndex; i < visiting.Count; i++)
            {
                key.Append(visiting[i].GetInstanceID()).Append(">");
            }

            key.Append(repeated.GetInstanceID());
            return key.ToString();
        }

        private static void ValidateConverterPass(
            BattleAuthoringValidationReport report,
            IReadOnlyList<BattleScenarioAsset> scenarios,
            IReadOnlyList<CombatantConfigAsset> combatants,
            IReadOnlyList<AbilityConfigAsset> abilities,
            IReadOnlyList<StatusConfigAsset> statuses,
            IReadOnlyList<ProjectileConfigAsset> projectiles,
            IReadOnlyList<ProjectileEmitterConfigAsset> projectileEmitters,
            IReadOnlyList<AreaEffectConfigAsset> areaEffects)
        {
            for (var i = 0; i < scenarios.Count; i++)
            {
                TryConvert(report, scenarios[i], () => BattleAuthoringConverter.BuildBattleConfig(scenarios[i]));
            }

            for (var i = 0; i < combatants.Count; i++)
            {
                TryConvert(report, combatants[i], () => BattleAuthoringConverter.BuildCombatantDefinition(combatants[i]));
            }

            for (var i = 0; i < abilities.Count; i++)
            {
                TryConvert(report, abilities[i], () => BattleAuthoringConverter.BuildAbilityDefinition(abilities[i]));
            }

            for (var i = 0; i < statuses.Count; i++)
            {
                TryConvert(report, statuses[i], () => BattleAuthoringConverter.BuildStatusDefinition(statuses[i]));
            }

            for (var i = 0; i < projectiles.Count; i++)
            {
                TryConvert(report, projectiles[i], () => BattleAuthoringConverter.BuildProjectilePayload(projectiles[i]));
            }

            for (var i = 0; i < projectileEmitters.Count; i++)
            {
                TryConvert(report, projectileEmitters[i], () => BattleAuthoringConverter.BuildProjectileEmitterSpawnData(projectileEmitters[i]));
            }

            for (var i = 0; i < areaEffects.Count; i++)
            {
                TryConvert(report, areaEffects[i], () => BattleAuthoringConverter.BuildAreaEffectDefinition(areaEffects[i]));
            }
        }

        private static void TryConvert(BattleAuthoringValidationReport report, Object asset, Action convert)
        {
            if (asset == null)
            {
                return;
            }

            try
            {
                convert();
            }
            catch (ArgumentException exception)
            {
                report.AddError(asset, "converter", $"{AssetLabel(asset)} converter validation failed: {exception.Message}");
            }
        }

        private static string ScenarioLabel(BattleScenarioAsset scenario)
        {
            return AssetLabel(scenario);
        }

        private static string CombatantLabel(CombatantConfigAsset combatant)
        {
            return string.IsNullOrWhiteSpace(combatant.Id) ? AssetLabel(combatant) : combatant.Id;
        }

        private static string AbilityLabel(AbilityConfigAsset ability)
        {
            return string.IsNullOrWhiteSpace(ability.Id) ? AssetLabel(ability) : ability.Id;
        }

        private static string StatusLabel(StatusConfigAsset status)
        {
            return string.IsNullOrWhiteSpace(status.Id) ? AssetLabel(status) : status.Id;
        }

        private static string AssetLabel(Object asset)
        {
            return asset == null || string.IsNullOrWhiteSpace(asset.name) ? "<unnamed asset>" : asset.name;
        }
    }
}
#endif
