using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    internal sealed class EntityCommandBuffer
    {
        private readonly List<SpawnCombatantCommand> _spawnCombatantCommands = new List<SpawnCombatantCommand>(16);
        private readonly List<SpawnProjectileCommand> _spawnProjectileCommands = new List<SpawnProjectileCommand>(16);
        private readonly List<BattleActionCommand> _actionCommands = new List<BattleActionCommand>(16);
        private readonly List<BattleEffectCommand> _effectCommands = new List<BattleEffectCommand>(16);
        private readonly List<BattleEffectCommand> _reactionEffectCommands = new List<BattleEffectCommand>(16);
        private readonly List<DeathCheckCommand> _deathCheckCommands = new List<DeathCheckCommand>(16);
        private readonly List<DestroyEntityCommand> _destroyEntityCommands = new List<DestroyEntityCommand>(16);
        private readonly List<IEntityStructuralCommand> _structuralCommands = new List<IEntityStructuralCommand>(16);
        private readonly ReadOnlyCollection<SpawnCombatantCommand> _readOnlySpawnCombatantCommands;
        private readonly ReadOnlyCollection<SpawnProjectileCommand> _readOnlySpawnProjectileCommands;
        private readonly ReadOnlyCollection<BattleActionCommand> _readOnlyActionCommands;
        private readonly ReadOnlyCollection<BattleEffectCommand> _readOnlyEffectCommands;
        private readonly ReadOnlyCollection<BattleEffectCommand> _readOnlyReactionEffectCommands;
        private readonly ReadOnlyCollection<DeathCheckCommand> _readOnlyDeathCheckCommands;
        private readonly ReadOnlyCollection<DestroyEntityCommand> _readOnlyDestroyEntityCommands;
        private readonly ReadOnlyCollection<IEntityStructuralCommand> _readOnlyStructuralCommands;

        public EntityCommandBuffer()
        {
            _readOnlySpawnCombatantCommands = new ReadOnlyCollection<SpawnCombatantCommand>(_spawnCombatantCommands);
            _readOnlySpawnProjectileCommands = new ReadOnlyCollection<SpawnProjectileCommand>(_spawnProjectileCommands);
            _readOnlyActionCommands = new ReadOnlyCollection<BattleActionCommand>(_actionCommands);
            _readOnlyEffectCommands = new ReadOnlyCollection<BattleEffectCommand>(_effectCommands);
            _readOnlyReactionEffectCommands = new ReadOnlyCollection<BattleEffectCommand>(_reactionEffectCommands);
            _readOnlyDeathCheckCommands = new ReadOnlyCollection<DeathCheckCommand>(_deathCheckCommands);
            _readOnlyDestroyEntityCommands = new ReadOnlyCollection<DestroyEntityCommand>(_destroyEntityCommands);
            _readOnlyStructuralCommands = new ReadOnlyCollection<IEntityStructuralCommand>(_structuralCommands);
        }

        public IReadOnlyList<SpawnCombatantCommand> SpawnCombatantCommands => _readOnlySpawnCombatantCommands;
        public IReadOnlyList<SpawnProjectileCommand> SpawnProjectileCommands => _readOnlySpawnProjectileCommands;
        public IReadOnlyList<BattleActionCommand> ActionCommands => _readOnlyActionCommands;
        public IReadOnlyList<BattleEffectCommand> EffectCommands => _readOnlyEffectCommands;
        public IReadOnlyList<BattleEffectCommand> ReactionEffectCommands => _readOnlyReactionEffectCommands;
        public IReadOnlyList<DeathCheckCommand> DeathCheckCommands => _readOnlyDeathCheckCommands;
        public IReadOnlyList<DestroyEntityCommand> DestroyEntityCommands => _readOnlyDestroyEntityCommands;
        public IReadOnlyList<IEntityStructuralCommand> StructuralCommands => _readOnlyStructuralCommands;

        public void SpawnCombatant(SpawnCombatantCommand command)
        {
            _spawnCombatantCommands.Add(command);
        }

        public void SpawnProjectile(SpawnProjectileCommand command)
        {
            _spawnProjectileCommands.Add(command);
        }

        public void QueueAction(BattleActionCommand command)
        {
            _actionCommands.Add(command);
        }

        public void QueueEffect(BattleEffectCommand command)
        {
            if (command.Type == BattleEffectType.Damage && command.Amount <= 0)
            {
                return;
            }

            _effectCommands.Add(command);
        }

        public void QueueReactionEffect(BattleEffectCommand command)
        {
            if (command.Type == BattleEffectType.Damage && command.Amount <= 0)
            {
                return;
            }

            _reactionEffectCommands.Add(command);
        }

        public void QueueDeathCheck(DeathCheckCommand command)
        {
            _deathCheckCommands.Add(command);
        }

        public void DestroyEntity(DestroyEntityCommand command)
        {
            _destroyEntityCommands.Add(command);
        }

        public void AddComponent<T>(EntityId entity, T component) where T : struct
        {
            _structuralCommands.Add(new AddComponentCommand<T>(entity, component));
        }

        public void RemoveComponent<T>(EntityId entity) where T : struct
        {
            _structuralCommands.Add(new RemoveComponentCommand<T>(entity));
        }

        public void ClearSpawnCombatantCommands()
        {
            _spawnCombatantCommands.Clear();
        }

        public void ClearSpawnProjectileCommands()
        {
            _spawnProjectileCommands.Clear();
        }

        public void ClearActionCommands()
        {
            _actionCommands.Clear();
        }

        public void ClearEffectCommands()
        {
            _effectCommands.Clear();
        }

        public BattleEffectCommand[] DrainEffectCommands()
        {
            return Drain(_effectCommands);
        }

        public void ClearReactionEffectCommands()
        {
            _reactionEffectCommands.Clear();
        }

        public BattleEffectCommand[] DrainReactionEffectCommands()
        {
            return Drain(_reactionEffectCommands);
        }

        public void ClearDeathCheckCommands()
        {
            _deathCheckCommands.Clear();
        }

        public DeathCheckCommand[] DrainDeathCheckCommands()
        {
            return Drain(_deathCheckCommands);
        }

        public void ClearStructuralCommands()
        {
            _destroyEntityCommands.Clear();
            _structuralCommands.Clear();
        }

        private static T[] Drain<T>(List<T> commands)
        {
            if (commands.Count == 0)
            {
                return System.Array.Empty<T>();
            }

            T[] batch = commands.ToArray();
            commands.Clear();
            return batch;
        }
    }
}
