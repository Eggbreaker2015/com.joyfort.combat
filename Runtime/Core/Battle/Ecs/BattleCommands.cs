using System;

namespace Combat.Core.Battle
{
    internal readonly struct SpawnCombatantCommand
    {
        public SpawnCombatantCommand(UnitId unitId, CombatantSpawnData spawn)
        {
            UnitId = unitId;
            Spawn = spawn;
        }

        public UnitId UnitId { get; }
        public CombatantSpawnData Spawn { get; }
    }

    internal readonly struct SpawnProjectileCommand
    {
        public SpawnProjectileCommand(EntityId source, TeamId teamId, BattleVector2 position, BattleVector2 velocity, ProjectilePayload payload, BattleTick activateOnTick)
        {
            Source = source;
            TeamId = teamId;
            Position = position;
            Velocity = velocity;
            Payload = payload;
            ActivateOnTick = activateOnTick;
        }

        public EntityId Source { get; }
        public TeamId TeamId { get; }
        public BattleVector2 Position { get; }
        public BattleVector2 Velocity { get; }
        public ProjectilePayload Payload { get; }
        public BattleTick ActivateOnTick { get; }
    }

    internal enum BattleActionType
    {
        UseAbility
    }

    internal readonly struct BattleActionCommand
    {
        private BattleActionCommand(BattleActionType type, EntityId source, EntityId target, int abilityIndex)
        {
            Type = type;
            Source = source;
            Target = target;
            AbilityIndex = abilityIndex;
        }

        public BattleActionType Type { get; }
        public EntityId Source { get; }
        public EntityId Target { get; }
        public int AbilityIndex { get; }

        public static BattleActionCommand UseAbility(EntityId source, EntityId target, int abilityIndex)
        {
            return new BattleActionCommand(BattleActionType.UseAbility, source, target, abilityIndex);
        }
    }

    internal enum BattleEffectTriggerPolicy
    {
        CanTriggerReactions,
        SuppressReactions
    }

    internal readonly struct BattleEffectCommand
    {
        private BattleEffectCommand(
            BattleEffectType type,
            EntityId source,
            EntityId target,
            int amount,
            StatusApplicationData status,
            ProjectileEmitterSpawnData projectileEmitter,
            AreaEffectData areaEffect,
            bool hasProjectileEmitterOrigin,
            BattleVector2 projectileEmitterOrigin,
            BattleEffectTriggerPolicy triggerPolicy,
            BattleEffectContext context)
        {
            Type = type;
            Source = source;
            Target = target;
            Amount = amount;
            Status = status;
            ProjectileEmitter = projectileEmitter;
            AreaEffect = areaEffect;
            HasProjectileEmitterOrigin = hasProjectileEmitterOrigin;
            ProjectileEmitterOrigin = projectileEmitterOrigin;
            TriggerPolicy = triggerPolicy;
            Context = context;
        }

        public BattleEffectType Type { get; }
        public EntityId Source { get; }
        public EntityId Target { get; }
        public int Amount { get; }
        public StatusApplicationData Status { get; }
        public ProjectileEmitterSpawnData ProjectileEmitter { get; }
        public AreaEffectData AreaEffect { get; }
        public bool HasProjectileEmitterOrigin { get; }
        public BattleVector2 ProjectileEmitterOrigin { get; }
        public BattleEffectTriggerPolicy TriggerPolicy { get; }
        public BattleEffectContext Context { get; }

        public static BattleEffectCommand Damage(
            EntityId source,
            EntityId target,
            int amount,
            BattleEffectTriggerPolicy triggerPolicy = BattleEffectTriggerPolicy.CanTriggerReactions)
        {
            return Damage(source, target, amount, BattleEffectContext.Unknown(BattleEffectType.Damage), triggerPolicy);
        }

        public static BattleEffectCommand Damage(
            EntityId source,
            EntityId target,
            int amount,
            BattleEffectContext context,
            BattleEffectTriggerPolicy triggerPolicy = BattleEffectTriggerPolicy.CanTriggerReactions)
        {
            return new BattleEffectCommand(BattleEffectType.Damage, source, target, amount, default, default, default, false, default, triggerPolicy, ValidateContext(context, BattleEffectType.Damage));
        }

        public static BattleEffectCommand Heal(EntityId source, EntityId target, int amount)
        {
            return Heal(source, target, amount, BattleEffectContext.Unknown(BattleEffectType.Heal));
        }

        public static BattleEffectCommand Heal(EntityId source, EntityId target, int amount, BattleEffectContext context)
        {
            return new BattleEffectCommand(BattleEffectType.Heal, source, target, amount, default, default, default, false, default, BattleEffectTriggerPolicy.SuppressReactions, ValidateContext(context, BattleEffectType.Heal));
        }

        public static BattleEffectCommand CreateAreaEffect(
            EntityId source,
            EntityId target,
            AreaEffectData areaEffect,
            BattleEffectContext context,
            BattleEffectTriggerPolicy damageTriggerPolicy = BattleEffectTriggerPolicy.CanTriggerReactions)
        {
            return new BattleEffectCommand(BattleEffectType.AreaEffect, source, target, 0, default, default, areaEffect, false, default, damageTriggerPolicy, ValidateContext(context, BattleEffectType.AreaEffect));
        }

        public static BattleEffectCommand ApplyStatus(EntityId source, EntityId target, StatusApplicationData status)
        {
            return ApplyStatus(source, target, status, BattleEffectContext.Unknown(BattleEffectType.ApplyStatus));
        }

        public static BattleEffectCommand ApplyStatus(EntityId source, EntityId target, StatusApplicationData status, BattleEffectContext context)
        {
            return new BattleEffectCommand(BattleEffectType.ApplyStatus, source, target, 0, status, default, default, false, default, BattleEffectTriggerPolicy.SuppressReactions, ValidateContext(context, BattleEffectType.ApplyStatus));
        }

        public static BattleEffectCommand SpawnProjectileEmitter(EntityId source, EntityId target, ProjectileEmitterSpawnData projectileEmitter)
        {
            return SpawnProjectileEmitter(source, target, projectileEmitter, BattleEffectContext.Unknown(BattleEffectType.SpawnProjectileEmitter));
        }

        public static BattleEffectCommand SpawnProjectileEmitter(EntityId source, EntityId target, ProjectileEmitterSpawnData projectileEmitter, BattleEffectContext context)
        {
            return new BattleEffectCommand(BattleEffectType.SpawnProjectileEmitter, source, target, 0, default, projectileEmitter, default, false, default, BattleEffectTriggerPolicy.SuppressReactions, ValidateContext(context, BattleEffectType.SpawnProjectileEmitter));
        }

        public static BattleEffectCommand SpawnProjectileEmitterAt(EntityId source, EntityId target, ProjectileEmitterSpawnData projectileEmitter, BattleVector2 origin)
        {
            return SpawnProjectileEmitterAt(source, target, projectileEmitter, origin, BattleEffectContext.Unknown(BattleEffectType.SpawnProjectileEmitter));
        }

        public static BattleEffectCommand SpawnProjectileEmitterAt(EntityId source, EntityId target, ProjectileEmitterSpawnData projectileEmitter, BattleVector2 origin, BattleEffectContext context)
        {
            return new BattleEffectCommand(BattleEffectType.SpawnProjectileEmitter, source, target, 0, default, projectileEmitter, default, true, origin, BattleEffectTriggerPolicy.SuppressReactions, ValidateContext(context, BattleEffectType.SpawnProjectileEmitter));
        }

        private static BattleEffectContext ValidateContext(BattleEffectContext context, BattleEffectType expectedType)
        {
            if (context.HasEffectType && context.EffectType == expectedType)
            {
                return context;
            }

            string actual = context.HasEffectType ? context.EffectType.ToString() : "<none>";
            throw new ArgumentException($"Effect context type {actual} does not match expected type {expectedType}.", nameof(context));
        }
    }

    internal readonly struct DeathCheckCommand
    {
        public DeathCheckCommand(EntityId entity)
            : this(entity, default, BattleEffectContext.Unknown(), BattleEffectTriggerPolicy.SuppressReactions)
        {
        }

        public DeathCheckCommand(
            EntityId entity,
            EntityId source,
            BattleEffectContext effectContext,
            BattleEffectTriggerPolicy triggerPolicy)
        {
            Entity = entity;
            Source = source;
            EffectContext = effectContext;
            TriggerPolicy = triggerPolicy;
        }

        public EntityId Entity { get; }
        public EntityId Source { get; }
        public BattleEffectContext EffectContext { get; }
        public BattleEffectTriggerPolicy TriggerPolicy { get; }
    }

    internal readonly struct DestroyEntityCommand
    {
        public DestroyEntityCommand(EntityId entity)
        {
            Entity = entity;
        }

        public EntityId Entity { get; }
    }

    internal interface IEntityStructuralCommand
    {
        EntityId Entity { get; }
        void Apply(BattleWorld world);
    }

    internal readonly struct AddComponentCommand<T> : IEntityStructuralCommand where T : struct
    {
        public AddComponentCommand(EntityId entity, T component)
        {
            Entity = entity;
            Component = component;
        }

        public EntityId Entity { get; }
        public T Component { get; }

        public void Apply(BattleWorld world)
        {
            world.SetComponent(Entity, Component);
        }
    }

    internal readonly struct RemoveComponentCommand<T> : IEntityStructuralCommand where T : struct
    {
        public RemoveComponentCommand(EntityId entity)
        {
            Entity = entity;
        }

        public EntityId Entity { get; }

        public void Apply(BattleWorld world)
        {
            world.RemoveComponent<T>(Entity);
        }
    }
}
