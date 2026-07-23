#if UNITY_EDITOR
using System;
using Combat.Core.Battle;
using Combat.Unity.Authoring;
using Combat.Unity.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Combat.Unity.Editor
{
    public static class DemoScenarioInstaller
    {
        private const string DemoRoot = "Assets/CombatSamples/Standalone";
        private const string ConfigFolder = DemoRoot + "/Config";
        private const string SkillFolder = ConfigFolder + "/Skill";
        private const string UnitFolder = ConfigFolder + "/Unit";
        private const string MeleePath = UnitFolder + "/DefaultMelee.asset";
        private const string BurnPath = SkillFolder + "/DefaultBurn.asset";
        private const string MarkPath = SkillFolder + "/DefaultMark.asset";
        private const string ThornsPath = SkillFolder + "/DefaultThorns.asset";
        private const string KillAttackStackPath = SkillFolder + "/DefaultKillAttackStack.asset";
        private const string KillFuryPath = SkillFolder + "/DefaultKillFury.asset";
        private const string BasicSlashPath = SkillFolder + "/DefaultBasicSlash.asset";
        private const string FireboltPath = SkillFolder + "/DefaultFirebolt.asset";
        private const string CounterStancePath = SkillFolder + "/DefaultCounterStance.asset";
        private const string KillFuryStancePath = SkillFolder + "/DefaultKillFuryStance.asset";
        private const string FireboltEmitterPath = SkillFolder + "/DefaultFireboltEmitter.asset";
        private const string FireboltBurstPath = SkillFolder + "/DefaultFireboltBurst.asset";
        private const string FireboltProjectilePath = SkillFolder + "/DefaultFireboltProjectile.asset";
        private const string FireboltBurstProjectilePath = SkillFolder + "/DefaultFireboltBurstProjectile.asset";
        private const string ScenarioPath = ConfigFolder + "/DefaultBattleScenario.asset";
        private const string ScenePath = "Assets/Scenes/SampleScene.scene";
        private const int DefaultTicksPerSecond = 30;

        [MenuItem("Combat/Demo/Rebuild Default Battle Scenario")]
        public static void RebuildDefaultScenario()
        {
            RebuildDefaultScenario(exitInBatchMode: true);
        }

        [MenuItem("Combat/Demo/Rebuild And Validate Default Battle Scenario")]
        public static void RebuildAndValidateDefaultScenario()
        {
            RebuildDefaultScenario(exitInBatchMode: true);
        }

        [MenuItem("Combat/Demo/Validate Default Battle Scenario")]
        public static void ValidateDefaultScenario()
        {
            BattleScenarioAsset scenario = AssetDatabase.LoadAssetAtPath<BattleScenarioAsset>(ScenarioPath);
            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateScenarioGraph(scenario);
            BattleAuthoringValidator.LogReport(report);
            if (report.HasErrors)
            {
                throw new InvalidOperationException("Default battle scenario validation failed.");
            }
        }

        public static BattleScenarioAsset RebuildDefaultScenarioWithoutBatchExit()
        {
            return RebuildDefaultScenario(exitInBatchMode: false);
        }

        private static BattleScenarioAsset RebuildDefaultScenario(bool exitInBatchMode)
        {
            try
            {
                BattleScenarioAsset scenario = RebuildDefaultScenarioCore();
                if (Application.isBatchMode && exitInBatchMode)
                {
                    EditorApplication.Exit(0);
                }

                return scenario;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode && exitInBatchMode)
                {
                    EditorApplication.Exit(1);
                    return null;
                }

                throw;
            }
        }

        private static BattleScenarioAsset RebuildDefaultScenarioCore()
        {
            EnsureFolder("Assets/CombatSamples");
            EnsureFolder(DemoRoot);
            EnsureFolder(ConfigFolder);
            EnsureFolder(SkillFolder);
            EnsureFolder(UnitFolder);

            StatusConfigAsset burn = LoadOrCreate<StatusConfigAsset>(BurnPath);
            ConfigureBurn(burn);

            StatusConfigAsset mark = LoadOrCreate<StatusConfigAsset>(MarkPath);
            ConfigureMark(mark);

            StatusConfigAsset thorns = LoadOrCreate<StatusConfigAsset>(ThornsPath);
            ConfigureThorns(thorns, mark);

            StatusConfigAsset killAttackStack = LoadOrCreate<StatusConfigAsset>(KillAttackStackPath);
            ConfigureKillAttackStack(killAttackStack);

            StatusConfigAsset killFury = LoadOrCreate<StatusConfigAsset>(KillFuryPath);
            ConfigureKillFury(killFury, killAttackStack);

            ProjectileConfigAsset fireboltBurstProjectile =
                LoadOrCreate<ProjectileConfigAsset>(FireboltBurstProjectilePath);
            ConfigureFireboltBurstProjectile(fireboltBurstProjectile);

            ProjectileEmitterConfigAsset fireboltBurst = LoadOrCreate<ProjectileEmitterConfigAsset>(FireboltBurstPath);
            ConfigureFireboltBurst(fireboltBurst, fireboltBurstProjectile);

            ProjectileConfigAsset fireboltProjectile =
                LoadOrCreate<ProjectileConfigAsset>(FireboltProjectilePath);
            ConfigureFireboltProjectile(fireboltProjectile, burn, fireboltBurst);

            ProjectileEmitterConfigAsset fireboltEmitter = LoadOrCreate<ProjectileEmitterConfigAsset>(FireboltEmitterPath);
            ConfigureFireboltEmitter(fireboltEmitter, fireboltProjectile);

            AbilityConfigAsset basicSlash = LoadOrCreate<AbilityConfigAsset>(BasicSlashPath);
            ConfigureBasicSlash(basicSlash);

            AbilityConfigAsset firebolt = LoadOrCreate<AbilityConfigAsset>(FireboltPath);
            ConfigureFirebolt(firebolt, fireboltEmitter);

            AbilityConfigAsset counterStance = LoadOrCreate<AbilityConfigAsset>(CounterStancePath);
            ConfigureCounterStance(counterStance, thorns);

            AbilityConfigAsset killFuryStance = LoadOrCreate<AbilityConfigAsset>(KillFuryStancePath);
            ConfigureKillFuryStance(killFuryStance, killFury);

            CombatantConfigAsset melee = LoadOrCreate<CombatantConfigAsset>(MeleePath);
            ConfigureMelee(melee, basicSlash, firebolt, counterStance, killFuryStance);

            BattleScenarioAsset scenario = LoadOrCreate<BattleScenarioAsset>(ScenarioPath);
            ConfigureScenario(scenario, melee);

            WireScene(scenario);

            AssetDatabase.SaveAssets();
            ValidateScenarioOrThrow(scenario);

            return scenario;
        }

        private static void ConfigureBurn(StatusConfigAsset burn)
        {
            var serialized = new SerializedObject(burn);
            serialized.FindProperty("_id").stringValue = "burn";
            serialized.FindProperty("_polarity").enumValueIndex = (int)StatusPolarity.Debuff;
            serialized.FindProperty("_durationSeconds").floatValue = TicksToSeconds(6);
            serialized.FindProperty("_tickIntervalSeconds").floatValue = TicksToSeconds(2);
            serialized.FindProperty("_periodicDamage").intValue = 1;
            serialized.FindProperty("_modifiers").arraySize = 0;
            serialized.FindProperty("_triggers").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(burn);
        }

        private static void ConfigureMark(StatusConfigAsset mark)
        {
            var serialized = new SerializedObject(mark);
            serialized.FindProperty("_id").stringValue = "mark";
            serialized.FindProperty("_polarity").enumValueIndex = (int)StatusPolarity.Debuff;
            serialized.FindProperty("_durationSeconds").floatValue = TicksToSeconds(4);
            serialized.FindProperty("_tickIntervalSeconds").floatValue = TicksToSeconds(1);
            serialized.FindProperty("_periodicDamage").intValue = 0;
            serialized.FindProperty("_modifiers").arraySize = 0;
            serialized.FindProperty("_triggers").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mark);
        }

        private static void ConfigureThorns(StatusConfigAsset thorns, StatusConfigAsset mark)
        {
            var serialized = new SerializedObject(thorns);
            serialized.FindProperty("_id").stringValue = "thorns";
            serialized.FindProperty("_polarity").enumValueIndex = (int)StatusPolarity.Buff;
            serialized.FindProperty("_durationSeconds").floatValue = TicksToSeconds(8);
            serialized.FindProperty("_tickIntervalSeconds").floatValue = TicksToSeconds(1);
            serialized.FindProperty("_periodicDamage").intValue = 0;
            serialized.FindProperty("_modifiers").arraySize = 0;

            SerializedProperty triggers = serialized.FindProperty("_triggers");
            triggers.arraySize = 1;
            SerializedProperty trigger = triggers.GetArrayElementAtIndex(0);
            trigger.FindPropertyRelative("_timing").enumValueIndex = (int)BattleTriggerTiming.AfterDamageTaken;

            SerializedProperty effects = trigger.FindPropertyRelative("_effects");
            effects.arraySize = 2;
            SetReactionEffect(effects.GetArrayElementAtIndex(0), BattleReactionTarget.Source, BattleEffectType.Damage, amount: 1, status: null, projectileEmitter: null);
            SetReactionEffect(effects.GetArrayElementAtIndex(1), BattleReactionTarget.Source, BattleEffectType.ApplyStatus, amount: 0, status: mark, projectileEmitter: null);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(thorns);
        }

        private static void ConfigureKillAttackStack(StatusConfigAsset killAttackStack)
        {
            var serialized = new SerializedObject(killAttackStack);
            serialized.FindProperty("_id").stringValue = "kill-attack-stack";
            serialized.FindProperty("_polarity").enumValueIndex = (int)StatusPolarity.Buff;
            serialized.FindProperty("_durationSeconds").floatValue = 5f;
            serialized.FindProperty("_tickIntervalSeconds").floatValue = 5f;
            serialized.FindProperty("_periodicDamage").intValue = 0;
            serialized.FindProperty("_maxStacks").intValue = 5;

            SerializedProperty modifiers = serialized.FindProperty("_modifiers");
            modifiers.arraySize = 1;
            SetModifier(
                modifiers.GetArrayElementAtIndex(0),
                BattleModifierTarget.Damage,
                default,
                BattleDamageModifierStat.DamageDealt,
                BattleModifierOperation.Flat,
                1f);

            serialized.FindProperty("_triggers").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(killAttackStack);
        }

        private static void ConfigureKillFury(StatusConfigAsset killFury, StatusConfigAsset killAttackStack)
        {
            var serialized = new SerializedObject(killFury);
            serialized.FindProperty("_id").stringValue = "kill-fury";
            serialized.FindProperty("_polarity").enumValueIndex = (int)StatusPolarity.Buff;
            serialized.FindProperty("_durationSeconds").floatValue = 30f;
            serialized.FindProperty("_tickIntervalSeconds").floatValue = 30f;
            serialized.FindProperty("_periodicDamage").intValue = 0;
            serialized.FindProperty("_maxStacks").intValue = 1;
            serialized.FindProperty("_modifiers").arraySize = 0;

            SerializedProperty triggers = serialized.FindProperty("_triggers");
            triggers.arraySize = 1;
            SerializedProperty trigger = triggers.GetArrayElementAtIndex(0);
            trigger.FindPropertyRelative("_timing").enumValueIndex = (int)BattleTriggerTiming.AfterEnemyKilled;

            SerializedProperty effects = trigger.FindPropertyRelative("_effects");
            effects.arraySize = 1;
            SetReactionEffect(effects.GetArrayElementAtIndex(0), BattleReactionTarget.Self, BattleEffectType.ApplyStatus, amount: 0, status: killAttackStack, projectileEmitter: null);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(killFury);
        }

        private static void ConfigureFireboltBurstProjectile(ProjectileConfigAsset projectile)
        {
            var serialized = new SerializedObject(projectile);
            SetProjectileConfig(
                serialized,
                ProjectileBehavior.Linear,
                radius: 0.1f,
                speed: 3f,
                lifetimeTicks: 12,
                impactEffectsSize: 1);

            SerializedProperty impactEffects = serialized.FindProperty("_impactEffects");
            SetEffect(impactEffects.GetArrayElementAtIndex(0), BattleEffectType.Damage, amount: 1, status: null, projectileEmitter: null);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(projectile);
        }

        private static void ConfigureFireboltBurst(
            ProjectileEmitterConfigAsset burst,
            ProjectileConfigAsset projectile)
        {
            var serialized = new SerializedObject(burst);
            SetEmitterConfig(
                serialized,
                projectile,
                ProjectileEmitterAnchorMode.FixedPosition,
                Vector2.zero,
                durationTicks: 1,
                fireIntervalTicks: 1,
                ProjectilePatternType.Circle,
                Vector2.right,
                projectileCount: 6);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(burst);
        }

        private static void ConfigureFireboltProjectile(
            ProjectileConfigAsset projectile,
            StatusConfigAsset burn,
            ProjectileEmitterConfigAsset fireboltBurst)
        {
            var serialized = new SerializedObject(projectile);
            SetProjectileConfig(
                serialized,
                ProjectileBehavior.Linear,
                radius: 0.15f,
                speed: 5f,
                lifetimeTicks: 24,
                impactEffectsSize: 3);

            SerializedProperty impactEffects = serialized.FindProperty("_impactEffects");
            SetEffect(impactEffects.GetArrayElementAtIndex(0), BattleEffectType.Damage, amount: 3, status: null, projectileEmitter: null);
            SetEffect(impactEffects.GetArrayElementAtIndex(1), BattleEffectType.ApplyStatus, amount: 0, status: burn, projectileEmitter: null);
            SetEffect(impactEffects.GetArrayElementAtIndex(2), BattleEffectType.SpawnProjectileEmitter, amount: 0, status: null, projectileEmitter: fireboltBurst);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(projectile);
        }

        private static void ConfigureFireboltEmitter(
            ProjectileEmitterConfigAsset fireboltEmitter,
            ProjectileConfigAsset projectile)
        {
            var serialized = new SerializedObject(fireboltEmitter);
            SetEmitterConfig(
                serialized,
                projectile,
                ProjectileEmitterAnchorMode.FollowSource,
                Vector2.zero,
                durationTicks: 1,
                fireIntervalTicks: 1,
                ProjectilePatternType.Single,
                Vector2.right,
                projectileCount: 1,
                directionMode: ProjectileDirectionMode.TargetDirection);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fireboltEmitter);
        }

        private static void ConfigureFirebolt(AbilityConfigAsset firebolt, ProjectileEmitterConfigAsset fireboltEmitter)
        {
            var serialized = new SerializedObject(firebolt);
            serialized.FindProperty("_id").stringValue = "firebolt";
            serialized.FindProperty("_range").floatValue = 5f;
            serialized.FindProperty("_cooldownSeconds").floatValue = TicksToSeconds(8);
            serialized.FindProperty("_windupSeconds").floatValue = TicksToSeconds(3);
            serialized.FindProperty("_recoverySeconds").floatValue = TicksToSeconds(4);
            SerializedProperty effects = SetSingleAbilityEffectFrame(serialized, TicksToSeconds(3), effectsSize: 1);
            SetEffect(effects.GetArrayElementAtIndex(0), BattleEffectType.SpawnProjectileEmitter, amount: 0, status: null, projectileEmitter: fireboltEmitter);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(firebolt);
        }

        private static void ConfigureCounterStance(AbilityConfigAsset counterStance, StatusConfigAsset thorns)
        {
            var serialized = new SerializedObject(counterStance);
            serialized.FindProperty("_id").stringValue = "counter-stance";
            serialized.FindProperty("_range").floatValue = 1f;
            serialized.FindProperty("_cooldownSeconds").floatValue = TicksToSeconds(20);
            serialized.FindProperty("_windupSeconds").floatValue = TicksToSeconds(1);
            serialized.FindProperty("_recoverySeconds").floatValue = TicksToSeconds(4);
            SerializedProperty effects = SetSingleAbilityEffectFrame(serialized, TicksToSeconds(1), effectsSize: 1);
            SetEffect(effects.GetArrayElementAtIndex(0), BattleEffectType.ApplyStatus, amount: 0, status: thorns, projectileEmitter: null);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(counterStance);
        }

        private static void ConfigureKillFuryStance(AbilityConfigAsset killFuryStance, StatusConfigAsset killFury)
        {
            var serialized = new SerializedObject(killFuryStance);
            serialized.FindProperty("_id").stringValue = "kill-fury-stance";
            serialized.FindProperty("_range").floatValue = 0f;
            serialized.FindProperty("_cooldownSeconds").floatValue = TicksToSeconds(30);
            serialized.FindProperty("_targetSelection").enumValueIndex = (int)AbilityTargetSelection.Self;
            SerializedProperty effects = SetSingleAbilityEffectFrame(serialized, 0f, effectsSize: 1);
            SetEffect(effects.GetArrayElementAtIndex(0), BattleEffectType.ApplyStatus, amount: 0, status: killFury, projectileEmitter: null);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(killFuryStance);
        }

        private static void ConfigureBasicSlash(AbilityConfigAsset basicSlash)
        {
            var serialized = new SerializedObject(basicSlash);
            serialized.FindProperty("_id").stringValue = "basic-slash";
            serialized.FindProperty("_range").floatValue = 2f;
            serialized.FindProperty("_cooldownSeconds").floatValue = TicksToSeconds(10);
            serialized.FindProperty("_windupSeconds").floatValue = TicksToSeconds(2);
            serialized.FindProperty("_recoverySeconds").floatValue = TicksToSeconds(3);
            SerializedProperty effects = SetSingleAbilityEffectFrame(serialized, TicksToSeconds(2), effectsSize: 1);
            SetEffect(effects.GetArrayElementAtIndex(0), BattleEffectType.Damage, amount: 2, status: null, projectileEmitter: null);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(basicSlash);
        }

        private static void ConfigureMelee(
            CombatantConfigAsset melee,
            AbilityConfigAsset basicSlash,
            AbilityConfigAsset firebolt,
            AbilityConfigAsset counterStance,
            AbilityConfigAsset killFuryStance)
        {
            var serialized = new SerializedObject(melee);
            serialized.FindProperty("_radius").floatValue = 0.25f;
            serialized.FindProperty("_basicAbility").objectReferenceValue = basicSlash;

            SerializedProperty stats = serialized.FindProperty("_stats");
            stats.arraySize = 2;
            SetStat(stats.GetArrayElementAtIndex(0), BattleStatId.MaxHealth, 20);
            SetStat(stats.GetArrayElementAtIndex(1), BattleStatId.MoveSpeed, 2);

            SerializedProperty abilities = serialized.FindProperty("_abilities");
            abilities.arraySize = 3;
            abilities.GetArrayElementAtIndex(0).objectReferenceValue = firebolt;
            abilities.GetArrayElementAtIndex(1).objectReferenceValue = counterStance;
            abilities.GetArrayElementAtIndex(2).objectReferenceValue = killFuryStance;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(melee);
        }

        private static void ConfigureScenario(BattleScenarioAsset scenario, CombatantConfigAsset melee)
        {
            var serialized = new SerializedObject(scenario);
            serialized.FindProperty("_ticksPerSecond").intValue = DefaultTicksPerSecond;
            serialized.FindProperty("_maxDurationSeconds").floatValue = TicksToSeconds(1800);

            SerializedProperty spawns = serialized.FindProperty("_initialSpawns");
            spawns.arraySize = 4;
            SetSpawn(spawns.GetArrayElementAtIndex(0), teamId: 1, melee, new Vector2(-3f, -1f));
            SetSpawn(spawns.GetArrayElementAtIndex(1), teamId: 1, melee, new Vector2(-3f, 1f));
            SetSpawn(spawns.GetArrayElementAtIndex(2), teamId: 2, melee, new Vector2(3f, -1f));
            SetSpawn(spawns.GetArrayElementAtIndex(3), teamId: 2, melee, new Vector2(3f, 1f));

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(scenario);
        }

        private static void WireScene(BattleScenarioAsset scenario)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject bootstrapObject = GameObject.Find("Combat Demo Bootstrap");
            if (bootstrapObject == null)
            {
                bootstrapObject = new GameObject("Combat Demo Bootstrap");
            }

            UnityCombatBootstrap bootstrap = bootstrapObject.GetComponent<UnityCombatBootstrap>();
            if (bootstrap == null)
            {
                bootstrap = bootstrapObject.AddComponent<UnityCombatBootstrap>();
            }

            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("_scenario").objectReferenceValue = scenario;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            int separator = folderPath.LastIndexOf('/');
            string parent = folderPath.Substring(0, separator);
            string folderName = folderPath.Substring(separator + 1);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void SetStat(SerializedProperty statProperty, BattleStatId stat, float value)
        {
            statProperty.FindPropertyRelative("_stat").enumValueIndex = (int)stat;
            statProperty.FindPropertyRelative("_value").floatValue = value;
        }

        private static void SetModifier(
            SerializedProperty modifierProperty,
            BattleModifierTarget target,
            BattleStatId statId,
            BattleDamageModifierStat damageStat,
            BattleModifierOperation operation,
            float value)
        {
            modifierProperty.FindPropertyRelative("_target").enumValueIndex = (int)target;
            modifierProperty.FindPropertyRelative("_statId").enumValueIndex = (int)statId;
            modifierProperty.FindPropertyRelative("_damageStat").enumValueIndex = (int)damageStat;
            modifierProperty.FindPropertyRelative("_operation").enumValueIndex = (int)operation;
            modifierProperty.FindPropertyRelative("_value").floatValue = value;
        }

        private static void SetSpawn(SerializedProperty spawnProperty, int teamId, CombatantConfigAsset combatant, Vector2 position)
        {
            spawnProperty.FindPropertyRelative("_teamId").intValue = teamId;
            spawnProperty.FindPropertyRelative("_combatant").objectReferenceValue = combatant;
            spawnProperty.FindPropertyRelative("_position").vector2Value = position;
        }

        private static void SetReactionEffect(
            SerializedProperty effectProperty,
            BattleReactionTarget target,
            BattleEffectType type,
            int amount,
            StatusConfigAsset status,
            ProjectileEmitterConfigAsset projectileEmitter)
        {
            effectProperty.FindPropertyRelative("_target").enumValueIndex = (int)target;
            SetEffect(effectProperty.FindPropertyRelative("_effect"), type, amount, status, projectileEmitter);
        }

        private static SerializedProperty SetSingleAbilityEffectFrame(SerializedObject serialized, float timeSeconds, int effectsSize)
        {
            SerializedProperty frames = serialized.FindProperty("_effectFrames");
            frames.arraySize = 1;

            SerializedProperty frame = frames.GetArrayElementAtIndex(0);
            frame.FindPropertyRelative("_frameId").stringValue = "release";
            frame.FindPropertyRelative("_timeSeconds").floatValue = timeSeconds;
            frame.FindPropertyRelative("_order").intValue = 0;

            SerializedProperty effects = frame.FindPropertyRelative("_effects");
            effects.arraySize = effectsSize;
            return effects;
        }

        private static void SetEmitterConfig(
            SerializedObject serialized,
            ProjectileConfigAsset projectile,
            ProjectileEmitterAnchorMode anchorMode,
            Vector2 anchorOffset,
            int durationTicks,
            int fireIntervalTicks,
            ProjectilePatternType patternType,
            Vector2 direction,
            int projectileCount,
            ProjectileDirectionMode directionMode = ProjectileDirectionMode.FixedDirection)
        {
            serialized.FindProperty("_anchorMode").enumValueIndex = (int)anchorMode;
            serialized.FindProperty("_anchorOffset").vector2Value = anchorOffset;
            serialized.FindProperty("_durationSeconds").floatValue = TicksToSeconds(durationTicks);
            serialized.FindProperty("_fireIntervalSeconds").floatValue = TicksToSeconds(fireIntervalTicks);

            SerializedProperty pattern = serialized.FindProperty("_pattern");
            pattern.FindPropertyRelative("_type").enumValueIndex = (int)patternType;
            pattern.FindPropertyRelative("_directionMode").enumValueIndex = (int)directionMode;
            pattern.FindPropertyRelative("_direction").vector2Value = direction;
            pattern.FindPropertyRelative("_projectileCount").intValue = projectileCount;
            serialized.FindProperty("_projectile").objectReferenceValue = projectile;
        }

        private static void SetProjectileConfig(
            SerializedObject serialized,
            ProjectileBehavior behavior,
            float radius,
            float speed,
            int lifetimeTicks,
            int impactEffectsSize)
        {
            serialized.FindProperty("_behavior").enumValueIndex = (int)behavior;
            serialized.FindProperty("_hitPolicyMode").enumValueIndex =
                (int)ProjectileHitPolicyMode.DestroyOnFirstHit;
            serialized.FindProperty("_maxHitCount").intValue = 2;
            serialized.FindProperty("_radius").floatValue = radius;
            serialized.FindProperty("_speed").floatValue = speed;
            serialized.FindProperty("_lifetimeSeconds").floatValue = TicksToSeconds(lifetimeTicks);
            serialized.FindProperty("_impactEffects").arraySize = impactEffectsSize;
        }

        private static void SetEffect(
            SerializedProperty effectProperty,
            BattleEffectType type,
            int amount,
            StatusConfigAsset status,
            ProjectileEmitterConfigAsset projectileEmitter)
        {
            effectProperty.FindPropertyRelative("_type").enumValueIndex = (int)type;
            effectProperty.FindPropertyRelative("_amount").intValue = amount;
            effectProperty.FindPropertyRelative("_status").objectReferenceValue = status;
            effectProperty.FindPropertyRelative("_projectileEmitter").objectReferenceValue = projectileEmitter;
        }

        private static void ValidateScenarioOrThrow(BattleScenarioAsset scenario)
        {
            BattleAuthoringValidationReport report = BattleAuthoringValidator.ValidateScenarioGraph(scenario);
            BattleAuthoringValidator.LogReport(report);
            if (report.HasErrors)
            {
                throw new InvalidOperationException("Default battle scenario validation failed.");
            }
        }

        private static float TicksToSeconds(int ticks)
        {
            return ticks / (float)DefaultTicksPerSecond;
        }
    }
}
#endif
