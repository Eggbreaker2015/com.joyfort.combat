using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using UnityEngine;

namespace Combat.Unity.Authoring
{
    public static class BattleAuthoringConverter
    {
        private const int DefaultTicksPerSecond = 30;
        private const double TickConversionEpsilon = 0.00001d;
        private const float ProjectileDirectionEpsilon = 0.00001f;

        public static BattleConfig BuildBattleConfig(BattleScenarioAsset scenario)
        {
            return new ConversionContext(scenario == null ? DefaultTicksPerSecond : scenario.TicksPerSecond).BuildBattleConfig(scenario);
        }

        public static CombatantDefinition BuildCombatantDefinition(CombatantConfigAsset combatant)
        {
            return BuildCombatantDefinition(combatant, DefaultTicksPerSecond);
        }

        public static CombatantDefinition BuildCombatantDefinition(
            CombatantConfigAsset combatant,
            int ticksPerSecond)
        {
            return new ConversionContext(ticksPerSecond).BuildCombatantDefinition(combatant);
        }

        public static int ConvertPositiveSecondsToTicks(
            float seconds,
            int ticksPerSecond,
            string propertyName)
        {
            return ConvertSecondsToTicks(seconds, ticksPerSecond, propertyName, allowZero: false);
        }

        public static AbilityDefinition BuildAbilityDefinition(AbilityConfigAsset ability)
        {
            return new ConversionContext(DefaultTicksPerSecond).BuildAbilityDefinition(ability);
        }

        public static StatusDefinition BuildStatusDefinition(StatusConfigAsset status)
        {
            return new ConversionContext(DefaultTicksPerSecond).BuildStatusDefinition(status);
        }

        public static ProjectileEmitterSpawnData BuildProjectileEmitterSpawnData(ProjectileEmitterConfigAsset projectileEmitter)
        {
            if (projectileEmitter == null)
            {
                throw new ArgumentNullException(nameof(projectileEmitter));
            }

            return new ConversionContext(DefaultTicksPerSecond).BuildProjectileEmitterSpawnData(projectileEmitter);
        }

        public static ProjectilePayload BuildProjectilePayload(ProjectileConfigAsset projectile)
        {
            if (projectile == null)
            {
                throw new ArgumentNullException(nameof(projectile));
            }

            return new ConversionContext(DefaultTicksPerSecond).BuildProjectilePayload(projectile);
        }

        public static AreaEffectDefinition BuildAreaEffectDefinition(AreaEffectConfigAsset areaEffect)
        {
            if (areaEffect == null)
            {
                throw new ArgumentNullException(nameof(areaEffect));
            }

            return new ConversionContext(DefaultTicksPerSecond).BuildAreaEffectDefinition(areaEffect);
        }

        public static BattleSpatialMapDefinition BuildSpatialMapDefinition(
            BattleSpatialMapAsset spatialMap)
        {
            if (spatialMap == null)
            {
                return BattleSpatialMapDefinition.Empty;
            }

            IReadOnlyList<BattleSpatialEntry> sourceEntries = spatialMap.Entries;
            if (sourceEntries == null)
            {
                throw new ArgumentException(
                    "Battle spatial map entries list is required.",
                    nameof(spatialMap));
            }

            var entries = new BattleSpatialEntryDefinition[sourceEntries.Count];
            for (var i = 0; i < sourceEntries.Count; i++)
            {
                BattleSpatialEntry source = sourceEntries[i];
                entries[i] = new BattleSpatialEntryDefinition(
                    source.StableId,
                    ToCoreSpatialShape(source.Shape),
                    ToBattleVector2(source.Center),
                    BattleScalar.FromFloat(source.Radius),
                    ToBattleVector2(source.Size),
                    source.CategoryBits,
                    source.MaskBits);
            }

            return new BattleSpatialMapDefinition(entries);
        }

        private static BattleSpatialShapeType ToCoreSpatialShape(
            BattleSpatialShape shape)
        {
            switch (shape)
            {
                case BattleSpatialShape.Circle:
                    return BattleSpatialShapeType.Circle;
                case BattleSpatialShape.Aabb:
                    return BattleSpatialShapeType.Aabb;
                default:
                    throw new ArgumentOutOfRangeException(nameof(shape));
            }
        }

        private sealed class ConversionContext
        {
            private readonly int _ticksPerSecond;
            private readonly Dictionary<CombatantConfigAsset, CombatantDefinition> _combatants = new Dictionary<CombatantConfigAsset, CombatantDefinition>();
            private readonly Dictionary<AbilityConfigAsset, AbilityDefinition> _abilities = new Dictionary<AbilityConfigAsset, AbilityDefinition>();
            private readonly Dictionary<StatusConfigAsset, StatusDefinition> _statuses = new Dictionary<StatusConfigAsset, StatusDefinition>();
            private readonly HashSet<StatusConfigAsset> _buildingStatuses = new HashSet<StatusConfigAsset>();
            private readonly HashSet<AreaEffectConfigAsset> _buildingAreaEffects = new HashSet<AreaEffectConfigAsset>();

            public ConversionContext(int ticksPerSecond)
            {
                _ticksPerSecond = ticksPerSecond;
            }

            public BattleConfig BuildBattleConfig(BattleScenarioAsset scenario)
            {
                if (scenario == null)
                {
                    throw new ArgumentNullException(nameof(scenario));
                }

                IReadOnlyList<SpawnEntry> initialSpawns = scenario.InitialSpawns;
                string scenarioName = ScenarioLabel(scenario);
                if (initialSpawns == null)
                {
                    throw new ArgumentException($"Battle scenario '{scenarioName}' initial spawns list is required.", nameof(scenario));
                }

                if (initialSpawns.Count <= 0)
                {
                    throw new ArgumentException($"Battle scenario '{scenarioName}' requires at least one initial spawn.", nameof(scenario));
                }

                int maxTicks;
                try
                {
                    maxTicks = PositiveSecondsToTicks(scenario.MaxDurationSeconds, "maxDurationSeconds");
                }
                catch (ArgumentException exception)
                {
                    throw new ArgumentException($"Battle scenario '{scenarioName}' is invalid: {exception.Message}", nameof(scenario), exception);
                }

                var spawns = new InitialCombatantSpawn[initialSpawns.Count];
                for (var i = 0; i < initialSpawns.Count; i++)
                {
                    SpawnEntry source = initialSpawns[i];
                    if (source.Combatant == null)
                    {
                        throw new ArgumentException($"Battle scenario '{scenarioName}' initial spawn {i} is missing a combatant reference.", nameof(scenario));
                    }

                    spawns[i] = new InitialCombatantSpawn(
                        new TeamId(source.TeamId),
                        BuildCombatantDefinition(source.Combatant),
                        ToBattleVector2(source.Position));
                }

                try
                {
                    return new BattleConfig(
                        scenario.TicksPerSecond,
                        maxTicks,
                        spawns,
                        BuildProjectileCullingBounds(scenario),
                        scenario.AutomaticVictoryEnabled,
                        scenario.LocalAvoidanceEnabled,
                        BuildSpatialMapDefinition(scenario.SpatialMap));
                }
                catch (ArgumentException exception)
                {
                    throw new ArgumentException($"Battle scenario '{scenarioName}' is invalid: {exception.Message}", nameof(scenario), exception);
                }
            }

            public CombatantDefinition BuildCombatantDefinition(CombatantConfigAsset combatant)
            {
                if (combatant == null)
                {
                    throw new ArgumentNullException(nameof(combatant));
                }

                if (_combatants.TryGetValue(combatant, out CombatantDefinition cached))
                {
                    return cached;
                }

                string combatantId = CombatantLabel(combatant);
                if (combatant.BasicAbility == null)
                {
                    throw new ArgumentException($"Combatant '{combatantId}' basic ability is required.", nameof(combatant));
                }

                IReadOnlyList<AbilityConfigAsset> abilityAssets = combatant.Abilities;
                if (abilityAssets == null)
                {
                    throw new ArgumentException($"Combatant '{combatantId}' ability list is required.", nameof(combatant));
                }

                AbilityDefinition basicAbility = BuildAbilityDefinition(combatant.BasicAbility);
                AbilityDefinition[] abilities = BuildCombatantAbilities(combatant, basicAbility, abilityAssets, combatantId);
                BattleStatBlock stats = BuildStatBlock(combatant, combatantId);
                TargetingBehaviorDefinition targetingBehavior =
                    BuildTargetingBehavior(combatant);

                try
                {
                    var definition = new CombatantDefinition(
                        combatant.Id,
                        BattleScalar.FromFloat(combatant.Radius),
                        stats,
                        basicAbility,
                        abilities,
                        aiDefinition: null,
                        targetingBehavior: targetingBehavior);
                    _combatants.Add(combatant, definition);
                    return definition;
                }
                catch (ArgumentException exception)
                {
                    throw new ArgumentException($"Combatant '{combatantId}' is invalid: {exception.Message}", nameof(combatant), exception);
                }
            }

            private TargetingBehaviorDefinition BuildTargetingBehavior(
                CombatantConfigAsset combatant)
            {
                if (!combatant.TargetingBehaviorEnabled)
                {
                    return TargetingBehaviorDefinition.Unrestricted;
                }

                return new TargetingBehaviorDefinition(
                    BattleScalar.FromFloat(combatant.TargetAcquisitionRange),
                    PositiveSecondsToTicks(
                        combatant.NoProgressTimeoutSeconds,
                        "noProgressTimeoutSeconds"),
                    BattleScalar.FromFloat(combatant.MinimumProgressDistance),
                    PositiveSecondsToTicks(
                        combatant.RejectedTargetCooldownSeconds,
                        "rejectedTargetCooldownSeconds"));
            }

            public AbilityDefinition BuildAbilityDefinition(AbilityConfigAsset ability)
            {
                if (ability == null)
                {
                    throw new ArgumentNullException(nameof(ability));
                }

                if (_abilities.TryGetValue(ability, out AbilityDefinition cached))
                {
                    return cached;
                }

                string abilityId = AbilityLabel(ability);
                AbilityEffectFrameDefinition[] effectFrames = BuildAbilityEffectFrames(ability, abilityId);

                try
                {
                    var definition = new AbilityDefinition(
                        ability.Id,
                        BattleScalar.FromFloat(ability.Range),
                        CooldownSecondsToTicks(ability.CooldownSeconds, "cooldownSeconds"),
                        CooldownSecondsToTicks(ability.WindupSeconds, "windupSeconds"),
                        CooldownSecondsToTicks(ability.RecoverySeconds, "recoverySeconds"),
                        ability.TargetSelection,
                        effectFrames,
                        ability.ActionLocks);
                    _abilities.Add(ability, definition);
                    return definition;
                }
                catch (ArgumentException exception)
                {
                    throw new ArgumentException($"Ability '{abilityId}' is invalid: {exception.Message}", nameof(ability), exception);
                }
            }

            public StatusDefinition BuildStatusDefinition(StatusConfigAsset status)
            {
                if (status == null)
                {
                    throw new ArgumentNullException(nameof(status));
                }

                if (_statuses.TryGetValue(status, out StatusDefinition cached))
                {
                    return cached;
                }

                string statusId = StatusLabel(status);
                if (!_buildingStatuses.Add(status))
                {
                    throw new ArgumentException($"Status '{statusId}' has a recursive status trigger reference.", nameof(status));
                }

                try
                {
                    BattleModifierDefinition[] modifiers = BuildStatusModifiers(status, statusId);
                    BattleTriggerDefinition[] triggers = BuildStatusTriggers(status, statusId);

                    var definition = new StatusDefinition(
                        status.Id,
                        status.Polarity,
                        PositiveSecondsToTicks(status.DurationSeconds, "durationSeconds"),
                        PositiveSecondsToTicks(status.TickIntervalSeconds, "tickIntervalSeconds"),
                        status.PeriodicDamage,
                        modifiers,
                        triggers,
                        status.MaxStacks,
                        status.StackPolicy);
                    _statuses.Add(status, definition);
                    return definition;
                }
                catch (ArgumentException exception)
                {
                    throw new ArgumentException($"Status '{statusId}' is invalid: {exception.Message}", nameof(status), exception);
                }
                finally
                {
                    _buildingStatuses.Remove(status);
                }
            }

            public ProjectileEmitterSpawnData BuildProjectileEmitterSpawnData(ProjectileEmitterConfigAsset projectileEmitter)
            {
                return BuildProjectileEmitter(projectileEmitter, BattleEffectAuthoringScope.Ability);
            }

            public ProjectilePayload BuildProjectilePayload(ProjectileConfigAsset projectile)
            {
                return BuildProjectilePayload(projectile, BattleEffectAuthoringScope.Ability);
            }

            public AreaEffectDefinition BuildAreaEffectDefinition(AreaEffectConfigAsset areaEffect)
            {
                return BuildAreaEffect(areaEffect);
            }

            private AbilityDefinition[] BuildCombatantAbilities(
                CombatantConfigAsset combatant,
                AbilityDefinition basicAbility,
                IReadOnlyList<AbilityConfigAsset> abilityAssets,
                string combatantId)
            {
                var abilities = new AbilityDefinition[abilityAssets.Count];
                var ids = new HashSet<string>(StringComparer.Ordinal);
                ids.Add(basicAbility.Id);
                for (var i = 0; i < abilityAssets.Count; i++)
                {
                    AbilityConfigAsset abilityAsset = abilityAssets[i];
                    if (abilityAsset == null)
                    {
                        throw new ArgumentException($"Combatant '{combatantId}' ability {i} is missing an ability reference.", nameof(combatant));
                    }

                    AbilityDefinition ability = BuildAbilityDefinition(abilityAsset);
                    if (!ids.Add(ability.Id))
                    {
                        throw new ArgumentException($"Combatant '{combatantId}' has duplicate ability id '{ability.Id}'.", nameof(combatant));
                    }

                    abilities[i] = ability;
                }

                return abilities;
            }

            private BattleStatBlock BuildStatBlock(CombatantConfigAsset combatant, string combatantId)
            {
                IReadOnlyList<BattleStatConfig> statConfigs = combatant.Stats;
                if (statConfigs == null)
                {
                    throw new ArgumentException($"Combatant '{combatantId}' stats list is required.", nameof(combatant));
                }

                var entries = new BattleStatEntry[statConfigs.Count];
                for (var i = 0; i < statConfigs.Count; i++)
                {
                    BattleStatConfig stat = statConfigs[i];
                    try
                    {
                        entries[i] = new BattleStatEntry(stat.Stat, stat.Value);
                    }
                    catch (ArgumentException exception)
                    {
                        throw new ArgumentException($"Combatant '{combatantId}' stat {stat.Stat} at index {i} is invalid: {exception.Message}", nameof(combatant), exception);
                    }
                }

                try
                {
                    return new BattleStatBlock(entries);
                }
                catch (ArgumentException exception)
                {
                    throw new ArgumentException($"Combatant '{combatantId}' stats are invalid or duplicate: {exception.Message}", nameof(combatant), exception);
                }
            }

            private BattleModifierDefinition[] BuildStatusModifiers(StatusConfigAsset status, string statusId)
            {
                IReadOnlyList<BattleModifierConfig> modifierConfigs = status.Modifiers;
                if (modifierConfigs == null)
                {
                    throw new ArgumentException($"Status '{statusId}' modifier list is required.", nameof(status));
                }

                var modifiers = new BattleModifierDefinition[modifierConfigs.Count];
                for (var i = 0; i < modifierConfigs.Count; i++)
                {
                    BattleModifierConfig modifier = modifierConfigs[i];
                    if (modifier == null)
                    {
                        throw new ArgumentException($"Status '{statusId}' modifier {i} is missing.", nameof(status));
                    }

                    try
                    {
                        BattleScalar value = BattleScalar.FromFloat(modifier.Value);
                        switch (modifier.Target)
                        {
                            case BattleModifierTarget.Damage:
                                ValidateDamageModifierStat(modifier.DamageStat);
                                modifiers[i] = BattleModifierDefinition.Damage(modifier.DamageStat, modifier.Operation, value);
                                break;
                            case BattleModifierTarget.Stat:
                                ValidateModifierStatId(modifier.StatId);
                                modifiers[i] = BattleModifierDefinition.Stat(modifier.StatId, modifier.Operation, value);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(modifier.Target), modifier.Target, "Unsupported battle modifier target.");
                        }
                    }
                    catch (ArgumentException exception)
                    {
                        throw new ArgumentException($"Status '{statusId}' modifier {i} is invalid: {exception.Message}", nameof(status), exception);
                    }
                }

                return modifiers;
            }

            private static void ValidateModifierStatId(BattleStatId stat)
            {
            switch (stat)
            {
                case BattleStatId.MaxHealth:
                case BattleStatId.MoveSpeed:
                    return;
                default:
                        throw new ArgumentOutOfRangeException(nameof(stat), stat, "unsupported stat modifier id.");
                }
            }

            private static void ValidateDamageModifierStat(BattleDamageModifierStat damageStat)
            {
                switch (damageStat)
                {
                    case BattleDamageModifierStat.DamageDealt:
                    case BattleDamageModifierStat.DamageTaken:
                        return;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(damageStat), damageStat, "Unsupported battle damage modifier stat.");
                }
            }

            private BattleTriggerDefinition[] BuildStatusTriggers(StatusConfigAsset status, string statusId)
            {
                IReadOnlyList<StatusTriggerConfig> triggerConfigs = status.Triggers;
                if (triggerConfigs == null)
                {
                    throw new ArgumentException($"Status '{statusId}' trigger list is required.", nameof(status));
                }

                var triggers = new BattleTriggerDefinition[triggerConfigs.Count];
                for (var i = 0; i < triggerConfigs.Count; i++)
                {
                    StatusTriggerConfig trigger = triggerConfigs[i];
                    try
                    {
                        triggers[i] = new BattleTriggerDefinition(
                            trigger.Timing,
                            new BattleConditionGroup(
                                trigger.ConditionMatchMode,
                                BuildStatusTriggerConditions(trigger.Conditions, statusId, i)),
                            BuildStatusReactionEffects(trigger.Effects, statusId, i));
                    }
                    catch (ArgumentException exception)
                    {
                        throw new ArgumentException($"Status '{statusId}' trigger {i} is invalid: {exception.Message}", nameof(status), exception);
                    }
                }

                return triggers;
            }

            private BattleConditionDefinition[] BuildStatusTriggerConditions(IReadOnlyList<BattleConditionConfig> source, string statusId, int triggerIndex)
            {
                if (source == null)
                {
                    throw new ArgumentException($"Status '{statusId}' trigger {triggerIndex} condition list is required.", nameof(source));
                }

                var conditions = new BattleConditionDefinition[source.Count];
                for (var i = 0; i < source.Count; i++)
                {
                    try
                    {
                        conditions[i] = BuildStatusTriggerCondition(source[i]);
                    }
                    catch (ArgumentException exception)
                    {
                        throw new ArgumentException($"condition {i} is invalid: {exception.Message}", nameof(source), exception);
                    }
                }

                return conditions;
            }

            private BattleConditionDefinition BuildStatusTriggerCondition(BattleConditionConfig condition)
            {
                return BattleConditionDefinition.Compare(
                    BuildConditionOperand(condition.Left, "left"),
                    condition.Comparison,
                    BuildConditionOperand(condition.Right, "right"));
            }

            private static BattleConditionOperandDefinition BuildConditionOperand(BattleConditionOperandConfig operand, string side)
            {
                if (operand == null)
                {
                    throw new ArgumentException($"Condition {side} operand is required.", nameof(operand));
                }

                return operand.BuildDefinition();
            }

            private BattleReactionEffectDefinition[] BuildStatusReactionEffects(IReadOnlyList<StatusReactionEffectConfig> source, string statusId, int triggerIndex)
            {
                if (source == null)
                {
                    throw new ArgumentException($"Status '{statusId}' trigger {triggerIndex} reaction effect list is required.", nameof(source));
                }

                var effects = new BattleReactionEffectDefinition[source.Count];
                for (var i = 0; i < source.Count; i++)
                {
                    StatusReactionEffectConfig effect = source[i];
                    try
                    {
                        effects[i] = BuildStatusReactionEffect(effect);
                    }
                    catch (ArgumentException exception)
                    {
                        throw new ArgumentException($"reaction effect {i} ({effect.Effect.Type}) is invalid: {exception.Message}", nameof(source), exception);
                    }
                }

                return effects;
            }

            private BattleReactionEffectDefinition BuildStatusReactionEffect(StatusReactionEffectConfig effect)
            {
                return BattleReactionEffectDefinition.Create(effect.Target, BuildBattleEffect(effect.Effect, BattleEffectAuthoringScope.StatusReaction));
            }

            private ProjectileEmitterSpawnData BuildProjectileEmitter(ProjectileEmitterConfigAsset emitter, BattleEffectAuthoringScope scope)
            {
                if (emitter.Projectile == null)
                {
                    throw new ArgumentException("Projectile emitter is missing a projectile reference.", nameof(emitter));
                }

                return new ProjectileEmitterSpawnData(
                    ValidateProjectileEmitterAnchorMode(emitter.AnchorMode, "anchorMode"),
                    ToBattleVector2(emitter.AnchorOffset),
                    PositiveSecondsToTicks(emitter.DurationSeconds, "durationSeconds"),
                    PositiveSecondsToTicks(emitter.FireIntervalSeconds, "fireIntervalSeconds"),
                    BuildProjectilePattern(emitter.Pattern),
                    BuildProjectilePayload(emitter.Projectile, scope));
            }

            private static ProjectileEmitterAnchorMode ValidateProjectileEmitterAnchorMode(ProjectileEmitterAnchorMode anchorMode, string propertyName)
            {
                switch (anchorMode)
                {
                    case ProjectileEmitterAnchorMode.FollowSource:
                    case ProjectileEmitterAnchorMode.FixedPosition:
                        return anchorMode;
                    default:
                        throw new ArgumentOutOfRangeException(propertyName, anchorMode, $"Unsupported ProjectileEmitterAnchorMode '{anchorMode}'.");
                }
            }

            private AbilityEffectFrameDefinition[] BuildAbilityEffectFrames(AbilityConfigAsset ability, string abilityId)
            {
                IReadOnlyList<AbilityEffectFrameConfig> source = ability.EffectFrames;
                if (source == null)
                {
                    throw new ArgumentException($"Ability '{abilityId}' effect frame list is required.", nameof(ability));
                }

                var frames = new AbilityEffectFrameDefinition[source.Count];
                for (var i = 0; i < source.Count; i++)
                {
                    AbilityEffectFrameConfig frame = source[i];
                    if (frame == null)
                    {
                        throw new ArgumentException($"Ability '{abilityId}' effect frame {i} is missing.", nameof(ability));
                    }

                    try
                    {
                        frames[i] = new AbilityEffectFrameDefinition(
                            frame.FrameId,
                            CooldownSecondsToTicks(frame.TimeSeconds, $"effectFrames[{i}].timeSeconds"),
                            frame.Order,
                            BuildBattleEffects(
                                frame.Effects,
                                $"Ability '{abilityId}' effect frame {i} effect",
                                BattleEffectAuthoringScope.Ability,
                                ability.TargetSelection));
                    }
                    catch (ArgumentException exception)
                    {
                        throw new ArgumentException($"Ability '{abilityId}' effect frame {i} is invalid: {exception.Message}", nameof(ability), exception);
                    }
                }

                return frames;
            }

            private static ProjectilePattern BuildProjectilePattern(ProjectilePatternConfig pattern)
            {
                ProjectileDirectionMode directionMode = ValidateProjectileDirectionMode(pattern.DirectionMode, "pattern.directionMode");
                switch (pattern.Type)
                {
                    case ProjectilePatternType.Single:
                        if (directionMode == ProjectileDirectionMode.FixedDirection && pattern.Direction.sqrMagnitude <= ProjectileDirectionEpsilon)
                        {
                            throw new ArgumentException("Fixed single projectile direction must be non-zero.", "pattern.direction");
                        }

                        return ProjectilePattern.Single(
                            ToBattleVector2(pattern.Direction),
                            directionMode);
                    case ProjectilePatternType.Circle:
                        return ProjectilePattern.Circle(pattern.ProjectileCount);
                    default:
                        throw new ArgumentOutOfRangeException("pattern.type", pattern.Type, $"Unsupported ProjectilePatternType '{pattern.Type}'.");
                }
            }

            private static ProjectileDirectionMode ValidateProjectileDirectionMode(ProjectileDirectionMode directionMode, string propertyName)
            {
                switch (directionMode)
                {
                    case ProjectileDirectionMode.FixedDirection:
                    case ProjectileDirectionMode.TargetDirection:
                        return directionMode;
                    default:
                        throw new ArgumentOutOfRangeException(propertyName, directionMode, $"Unsupported ProjectileDirectionMode '{directionMode}'.");
                }
            }

            private ProjectilePayload BuildProjectilePayload(ProjectileConfigAsset projectile, BattleEffectAuthoringScope scope)
            {
                if (projectile == null)
                {
                    throw new ArgumentNullException(nameof(projectile));
                }

                ValidatePositiveFinite(projectile.Radius, "projectile.radius");
                ValidatePositiveFinite(projectile.Speed, "projectile.speed");
                if (projectile.ImpactEffects == null || projectile.ImpactEffects.Length <= 0)
                {
                    throw new ArgumentException("projectile.impactEffects requires at least one impact effect.", "projectile.impactEffects");
                }

                return new ProjectilePayload(
                    ValidateProjectileBehavior(projectile.Behavior, "projectile.behavior"),
                    BuildProjectileHitPolicy(
                        projectile.HitPolicyMode,
                        projectile.MaxHitCount,
                        "projectile.hitPolicy"),
                    projectile.Radius,
                    projectile.Speed,
                    PositiveSecondsToTicks(projectile.LifetimeSeconds, "projectile.lifetimeSeconds"),
                    BuildBattleEffects(
                        projectile.ImpactEffects,
                        "Projectile impact effect",
                        BattleEffectAuthoringRules.ProjectileImpactScopeForParent(scope)));
            }

            private static ProjectileBehavior ValidateProjectileBehavior(ProjectileBehavior behavior, string propertyName)
            {
                switch (behavior)
                {
                    case ProjectileBehavior.Linear:
                        return behavior;
                    default:
                        throw new ArgumentOutOfRangeException(propertyName, behavior, $"Unsupported ProjectileBehavior '{behavior}'.");
                }
            }

            private static ProjectileHitPolicy BuildProjectileHitPolicy(
                ProjectileHitPolicyMode mode,
                int maxHitCount,
                string propertyName)
            {
                switch (mode)
                {
                    case ProjectileHitPolicyMode.DestroyOnFirstHit:
                        return ProjectileHitPolicy.DestroyOnFirstHit;
                    case ProjectileHitPolicyMode.Pierce:
                        if (maxHitCount < 2)
                        {
                            throw new ArgumentOutOfRangeException(
                                $"{propertyName}.maxHitCount",
                                maxHitCount,
                                "Piercing projectile maxHitCount must be at least 2.");
                        }

                        return ProjectileHitPolicy.Pierce(maxHitCount);
                    default:
                        throw new ArgumentOutOfRangeException(
                            $"{propertyName}.mode",
                            mode,
                            $"Unsupported ProjectileHitPolicyMode '{mode}'.");
                }
            }

            private static void ValidatePositiveFinite(float value, string propertyName)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(propertyName, value, $"{propertyName} must be finite.");
                }

                if (value <= 0f)
                {
                    throw new ArgumentOutOfRangeException(propertyName, value, $"{propertyName} must be positive.");
                }
            }

            private BattleEffectDefinition[] BuildBattleEffects(
                IReadOnlyList<BattleEffectConfig> source,
                string label,
                BattleEffectAuthoringScope scope,
                AbilityTargetSelection abilityTargetSelection = AbilityTargetSelection.CurrentEnemyTarget)
            {
                if (source == null)
                {
                    throw new ArgumentException($"{label} list is required.", nameof(source));
                }

                var effects = new BattleEffectDefinition[source.Count];
                for (var i = 0; i < source.Count; i++)
                {
                    BattleEffectConfig effect = source[i];
                    try
                    {
                        effects[i] = BuildBattleEffect(effect, scope, abilityTargetSelection);
                    }
                    catch (ArgumentException exception)
                    {
                        throw new ArgumentException($"{label} {i} is invalid: {exception.Message}", nameof(source), exception);
                    }
                }

                return effects;
            }

            private BattleEffectDefinition BuildBattleEffect(
                BattleEffectConfig effect,
                BattleEffectAuthoringScope scope,
                AbilityTargetSelection abilityTargetSelection = AbilityTargetSelection.CurrentEnemyTarget)
            {
                switch (effect.Type)
                {
                    case BattleEffectType.Damage:
                        return BattleEffectDefinition.Damage(effect.Amount);
                    case BattleEffectType.Heal:
                        if (!BattleEffectAuthoringRules.AllowsDirectHeal(scope, abilityTargetSelection))
                        {
                            throw new ArgumentException("Heal requires explicit target context; use Self or LowestHealthAlly target selection, AreaEffect, or status reaction.", nameof(effect));
                        }

                        return BattleEffectDefinition.Heal(effect.Amount);
                    case BattleEffectType.ApplyStatus:
                        if (effect.Status == null)
                        {
                            throw new ArgumentException("ApplyStatus effect is missing a status reference.", nameof(effect));
                        }

                        return BattleEffectDefinition.ApplyStatus(BuildStatusDefinition(effect.Status));
                    case BattleEffectType.SpawnProjectileEmitter:
                        if (effect.ProjectileEmitter == null)
                        {
                            throw new ArgumentException("SpawnProjectileEmitter effect is missing a projectile emitter reference.", nameof(effect));
                        }

                        return BattleEffectDefinition.SpawnProjectileEmitter(BuildProjectileEmitter(effect.ProjectileEmitter, scope));
                    case BattleEffectType.AreaEffect:
                        if (effect.AreaEffect == null)
                        {
                            throw new ArgumentException("AreaEffect effect is missing an area effect reference.", nameof(effect));
                        }

                        if (!BattleEffectAuthoringRules.AllowsAreaEffect(scope))
                        {
                            throw new ArgumentException("AreaEffect has a nested AreaEffect reference; recursive AreaEffect authoring is not supported.", nameof(effect));
                        }

                        return BattleEffectDefinition.AreaEffect(BuildAreaEffect(effect.AreaEffect));
                    default:
                        throw new ArgumentOutOfRangeException(nameof(effect), $"Unknown battle effect type '{effect.Type}'.");
                }
            }

            private AreaEffectDefinition BuildAreaEffect(AreaEffectConfigAsset areaEffect)
            {
                string areaEffectLabel = AreaEffectLabel(areaEffect);
                if (_buildingAreaEffects.Count > 0)
                {
                    throw new ArgumentException($"AreaEffect '{areaEffectLabel}' has a nested AreaEffect reference; recursive AreaEffect authoring is not supported.", nameof(areaEffect));
                }

                if (!_buildingAreaEffects.Add(areaEffect))
                {
                    throw new ArgumentException($"AreaEffect '{areaEffectLabel}' has a recursive AreaEffect reference.", nameof(areaEffect));
                }

                try
                {
                    return new AreaEffectDefinition(
                        BattleScalar.FromFloat(areaEffect.Radius),
                        areaEffect.TargetFilter,
                        BuildBattleEffects(areaEffect.Effects, $"AreaEffect '{areaEffectLabel}' child effect", BattleEffectAuthoringScope.AreaChild));
                }
                finally
                {
                    _buildingAreaEffects.Remove(areaEffect);
                }
            }

            private static ProjectileCullingBounds BuildProjectileCullingBounds(BattleScenarioAsset scenario)
            {
                return scenario.ProjectileCullingEnabled
                    ? new ProjectileCullingBounds(
                        ToBattleVector2(scenario.ProjectileCullingCenter),
                        ToBattleVector2(scenario.ProjectileCullingSize),
                        scenario.ProjectileCullingPadding)
                    : default;
            }

            private static string ScenarioLabel(BattleScenarioAsset scenario)
            {
                return string.IsNullOrWhiteSpace(scenario.name) ? "<unnamed scenario>" : scenario.name;
            }

            private static string CombatantLabel(CombatantConfigAsset combatant)
            {
                if (!string.IsNullOrWhiteSpace(combatant.Id))
                {
                    return combatant.Id;
                }

                return string.IsNullOrWhiteSpace(combatant.name) ? "<unnamed combatant>" : combatant.name;
            }

            private static string AbilityLabel(AbilityConfigAsset ability)
            {
                if (!string.IsNullOrWhiteSpace(ability.Id))
                {
                    return ability.Id;
                }

                return string.IsNullOrWhiteSpace(ability.name) ? "<unnamed ability>" : ability.name;
            }

            private static string StatusLabel(StatusConfigAsset status)
            {
                if (!string.IsNullOrWhiteSpace(status.Id))
                {
                    return status.Id;
                }

                return string.IsNullOrWhiteSpace(status.name) ? "<unnamed status>" : status.name;
            }

            private static string AreaEffectLabel(AreaEffectConfigAsset areaEffect)
            {
                return string.IsNullOrWhiteSpace(areaEffect.name) ? "<unnamed area effect>" : areaEffect.name;
            }

            private int CooldownSecondsToTicks(float seconds, string propertyName)
            {
                return SecondsToTicks(seconds, propertyName, allowZero: true);
            }

            private int PositiveSecondsToTicks(float seconds, string propertyName)
            {
                return ConvertPositiveSecondsToTicks(seconds, _ticksPerSecond, propertyName);
            }

            private int SecondsToTicks(float seconds, string propertyName, bool allowZero)
            {
                return ConvertSecondsToTicks(
                    seconds, _ticksPerSecond, propertyName, allowZero);
            }

            private static int PercentToBasisPoints(float percent, string propertyName)
            {
                if (float.IsNaN(percent) || float.IsInfinity(percent))
                {
                    throw new ArgumentOutOfRangeException(propertyName, "Percent value must be finite.");
                }

                if (percent < 0f || percent > 100f)
                {
                    throw new ArgumentOutOfRangeException(propertyName, "Percent value must be between 0 and 100.");
                }

                return (int)Math.Round(percent * 100d, MidpointRounding.AwayFromZero);
            }
        }

        private static BattleVector2 ToBattleVector2(Vector2 value)
        {
            return new BattleVector2(value.x, value.y);
        }

        private static int ConvertSecondsToTicks(
            float seconds,
            int ticksPerSecond,
            string propertyName,
            bool allowZero)
        {
            if (ticksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ticksPerSecond), "Ticks per second must be positive.");
            }

            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException("A property name is required.", nameof(propertyName));
            }

            if (float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(propertyName, "Seconds value must be finite.");
            }

            if (allowZero ? seconds < 0f : seconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    propertyName,
                    allowZero
                        ? "Seconds value must be non-negative."
                        : "Seconds value must be positive.");
            }

            double rawTicks = (double)seconds * ticksPerSecond;
            if (rawTicks > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    propertyName, "Seconds value converts to too many ticks.");
            }

            int ticks = (int)Math.Ceiling(rawTicks - TickConversionEpsilon);
            if (ticks < 0)
            {
                ticks = 0;
            }

            return seconds > 0f && ticks < 1 ? 1 : ticks;
        }
    }
}
