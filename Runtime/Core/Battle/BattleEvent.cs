using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    public readonly struct BattleEvent
    {
        private static readonly ReadOnlyCollection<string> EmptyDamageTags = new ReadOnlyCollection<string>(Array.Empty<string>());

        private readonly string[] _damageTags;
        private readonly ReadOnlyCollection<string> _readOnlyDamageTags;

        private BattleEvent(
            BattleEventType type,
            int sequence,
            BattleTick tick,
            UnitId unitId,
            TeamId teamId,
            UnitId targetUnitId,
            UnitId sourceUnitId,
            ProjectileId projectileId,
            BattleVector2 position,
            BattleVector2 facing,
            int amount,
            TeamId winningTeamId,
            string definitionId,
            string statusId,
            StatusPolarity statusPolarity,
            BattleEffectSourceKind effectSourceKind,
            bool hasEffectType,
            BattleEffectType effectType,
            string abilityId,
            string effectStatusId,
            ProjectileId effectProjectileId,
            IReadOnlyList<string> damageTags,
            BattleActionLocks actionLocks = BattleActionLocks.None)
        {
            Type = type;
            Sequence = sequence;
            Tick = tick;
            UnitId = unitId;
            TeamId = teamId;
            TargetUnitId = targetUnitId;
            SourceUnitId = sourceUnitId;
            ProjectileId = projectileId;
            Position = position;
            Facing = facing;
            Amount = amount;
            WinningTeamId = winningTeamId;
            DefinitionId = definitionId;
            StatusId = statusId;
            StatusPolarity = statusPolarity;
            EffectSourceKind = effectSourceKind;
            HasEffectType = hasEffectType;
            EffectType = hasEffectType ? effectType : default;
            AbilityId = abilityId;
            EffectStatusId = effectStatusId;
            EffectProjectileId = effectProjectileId;
            ActionLocks = actionLocks;
            _damageTags = CopyDamageTags(damageTags);
            _readOnlyDamageTags = _damageTags.Length == 0
                ? EmptyDamageTags
                : new ReadOnlyCollection<string>(_damageTags);
        }

        public BattleEventType Type { get; }
        public int Sequence { get; }
        public BattleTick Tick { get; }
        public UnitId UnitId { get; }
        public TeamId TeamId { get; }
        public UnitId TargetUnitId { get; }
        public UnitId SourceUnitId { get; }
        public ProjectileId ProjectileId { get; }
        public BattleVector2 Position { get; }
        public BattleVector2 Facing { get; }
        public int Amount { get; }
        public TeamId WinningTeamId { get; }
        public string DefinitionId { get; }
        public string StatusId { get; }
        public StatusPolarity StatusPolarity { get; }
        public BattleEffectSourceKind EffectSourceKind { get; }
        public bool HasEffectType { get; }
        public BattleEffectType EffectType { get; }
        public string AbilityId { get; }
        public string EffectStatusId { get; }
        public ProjectileId EffectProjectileId { get; }
        public BattleActionLocks ActionLocks { get; }
        public IReadOnlyList<string> DamageTags => _readOnlyDamageTags ?? EmptyDamageTags;

        public static BattleEvent UnitSpawned(int sequence, BattleTick tick, UnitId unitId, TeamId teamId, string definitionId, BattleVector2 position)
        {
            return UnitSpawned(sequence, tick, unitId, teamId, definitionId, position, BattleVector2.Right);
        }

        public static BattleEvent UnitSpawned(int sequence, BattleTick tick, UnitId unitId, TeamId teamId, string definitionId, BattleVector2 position, BattleVector2 facing)
        {
            return new BattleEvent(BattleEventType.UnitSpawned, sequence, tick, unitId, teamId, default, default, default, position, new FacingComponent(facing).Direction, 0, default, definitionId, null, default, BattleEffectSourceKind.Unknown, false, default, null, null, default, null);
        }

        public static BattleEvent UnitMoved(int sequence, BattleTick tick, UnitId unitId, TeamId teamId, BattleVector2 position)
        {
            return new BattleEvent(BattleEventType.UnitMoved, sequence, tick, unitId, teamId, default, default, default, position, default, 0, default, null, null, default, BattleEffectSourceKind.Unknown, false, default, null, null, default, null);
        }

        public static BattleEvent UnitFacingChanged(int sequence, BattleTick tick, UnitId unitId, TeamId teamId, BattleVector2 facing)
        {
            return new BattleEvent(BattleEventType.UnitFacingChanged, sequence, tick, unitId, teamId, default, default, default, default, new FacingComponent(facing).Direction, 0, default, null, null, default, BattleEffectSourceKind.Unknown, false, default, null, null, default, null);
        }

        public static BattleEvent UnitGarrisoned(int sequence, BattleTick tick, UnitId unitId, TeamId teamId)
        {
            return new BattleEvent(BattleEventType.UnitGarrisoned, sequence, tick, unitId, teamId, default, default, default, default, default, 0, default, null, null, default, BattleEffectSourceKind.Unknown, false, default, null, null, default, null);
        }

        public static BattleEvent UnitDeployed(int sequence, BattleTick tick, UnitId unitId, TeamId teamId)
        {
            return new BattleEvent(BattleEventType.UnitDeployed, sequence, tick, unitId, teamId, default, default, default, default, default, 0, default, null, null, default, BattleEffectSourceKind.Unknown, false, default, null, null, default, null);
        }

        public static BattleEvent AbilityStarted(
            int sequence,
            BattleTick tick,
            UnitId sourceUnitId,
            UnitId targetUnitId,
            string abilityId,
            BattleEffectSourceKind sourceKind,
            BattleActionLocks actionLocks = BattleActionLocks.None)
        {
            return AbilityAction(BattleEventType.AbilityStarted, sequence, tick, sourceUnitId, targetUnitId, abilityId, sourceKind, actionLocks);
        }

        public static BattleEvent AbilityReleased(int sequence, BattleTick tick, UnitId sourceUnitId, UnitId targetUnitId, string abilityId, BattleEffectSourceKind sourceKind)
        {
            return AbilityAction(BattleEventType.AbilityReleased, sequence, tick, sourceUnitId, targetUnitId, abilityId, sourceKind);
        }

        public static BattleEvent AbilityEnded(int sequence, BattleTick tick, UnitId sourceUnitId, UnitId targetUnitId, string abilityId, BattleEffectSourceKind sourceKind)
        {
            return AbilityAction(BattleEventType.AbilityEnded, sequence, tick, sourceUnitId, targetUnitId, abilityId, sourceKind);
        }

        private static BattleEvent AbilityAction(
            BattleEventType type,
            int sequence,
            BattleTick tick,
            UnitId sourceUnitId,
            UnitId targetUnitId,
            string abilityId,
            BattleEffectSourceKind sourceKind,
            BattleActionLocks actionLocks = BattleActionLocks.None)
        {
            return new BattleEvent(type, sequence, tick, sourceUnitId, default, targetUnitId, sourceUnitId, default, default, default, 0, default, null, null, default, sourceKind, false, default, abilityId, null, default, null, actionLocks);
        }

        public static BattleEvent DamageApplied(int sequence, BattleTick tick, UnitId sourceUnitId, UnitId targetUnitId, int amount)
        {
            return DamageApplied(sequence, tick, sourceUnitId, targetUnitId, amount, BattleEffectContext.Unknown(BattleEffectType.Damage));
        }

        public static BattleEvent DamageApplied(int sequence, BattleTick tick, UnitId sourceUnitId, UnitId targetUnitId, int amount, BattleEffectContext context)
        {
            return new BattleEvent(
                BattleEventType.DamageApplied,
                sequence,
                tick,
                sourceUnitId,
                default,
                targetUnitId,
                sourceUnitId,
                default,
                default,
                default,
                amount,
                default,
                null,
                null,
                default,
                context.SourceKind,
                context.HasEffectType,
                context.EffectType,
                context.AbilityId,
                context.StatusId,
                context.ProjectileId,
                context.DamageTags);
        }

        public static BattleEvent HealingApplied(int sequence, BattleTick tick, UnitId sourceUnitId, UnitId targetUnitId, int amount, BattleEffectContext context)
        {
            return new BattleEvent(
                BattleEventType.HealingApplied,
                sequence,
                tick,
                targetUnitId,
                default,
                targetUnitId,
                sourceUnitId,
                default,
                default,
                default,
                amount,
                default,
                null,
                null,
                default,
                context.SourceKind,
                context.HasEffectType,
                context.EffectType,
                context.AbilityId,
                context.StatusId,
                context.ProjectileId,
                context.DamageTags);
        }

        public static BattleEvent UnitDied(int sequence, BattleTick tick, UnitId unitId, TeamId teamId)
        {
            return new BattleEvent(BattleEventType.UnitDied, sequence, tick, unitId, teamId, default, default, default, default, default, 0, default, null, null, default, BattleEffectSourceKind.Unknown, false, default, null, null, default, null);
        }

        public static BattleEvent ProjectileSpawned(int sequence, BattleTick tick, ProjectileId projectileId, TeamId teamId, UnitId sourceUnitId, BattleVector2 position)
        {
            return new BattleEvent(BattleEventType.ProjectileSpawned, sequence, tick, default, teamId, default, sourceUnitId, projectileId, position, default, 0, default, null, null, default, BattleEffectSourceKind.Unknown, false, default, null, null, default, null);
        }

        public static BattleEvent ProjectileMoved(int sequence, BattleTick tick, ProjectileId projectileId, BattleVector2 position)
        {
            return new BattleEvent(BattleEventType.ProjectileMoved, sequence, tick, default, default, default, default, projectileId, position, default, 0, default, null, null, default, BattleEffectSourceKind.Unknown, false, default, null, null, default, null);
        }

        public static BattleEvent ProjectileHit(int sequence, BattleTick tick, ProjectileId projectileId, UnitId sourceUnitId, UnitId targetUnitId, BattleVector2 position)
        {
            return new BattleEvent(BattleEventType.ProjectileHit, sequence, tick, targetUnitId, default, targetUnitId, sourceUnitId, projectileId, position, default, 0, default, null, null, default, BattleEffectSourceKind.Unknown, false, default, null, null, default, null);
        }

        public static BattleEvent ProjectileDestroyed(int sequence, BattleTick tick, ProjectileId projectileId)
        {
            return new BattleEvent(BattleEventType.ProjectileDestroyed, sequence, tick, default, default, default, default, projectileId, default, default, 0, default, null, null, default, BattleEffectSourceKind.Unknown, false, default, null, null, default, null);
        }

        public static BattleEvent BattleEnded(int sequence, BattleTick tick, TeamId winningTeamId)
        {
            return new BattleEvent(BattleEventType.BattleEnded, sequence, tick, default, default, default, default, default, default, default, 0, winningTeamId, null, null, default, BattleEffectSourceKind.Unknown, false, default, null, null, default, null);
        }

        public static BattleEvent StatusApplied(int sequence, BattleTick tick, UnitId sourceUnitId, UnitId targetUnitId, string statusId, StatusPolarity polarity)
        {
            return new BattleEvent(BattleEventType.StatusApplied, sequence, tick, targetUnitId, default, targetUnitId, sourceUnitId, default, default, default, 0, default, null, statusId, polarity, BattleEffectSourceKind.Unknown, false, default, null, null, default, null);
        }

        public static BattleEvent StatusExpired(int sequence, BattleTick tick, UnitId unitId, string statusId, StatusPolarity polarity)
        {
            return new BattleEvent(BattleEventType.StatusExpired, sequence, tick, unitId, default, default, default, default, default, default, 0, default, null, statusId, polarity, BattleEffectSourceKind.Unknown, false, default, null, null, default, null);
        }

        private static string[] CopyDamageTags(IReadOnlyList<string> damageTags)
        {
            if (damageTags == null || damageTags.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[damageTags.Count];
            for (var i = 0; i < damageTags.Count; i++)
            {
                copy[i] = damageTags[i] ?? string.Empty;
            }

            return copy;
        }
    }
}
