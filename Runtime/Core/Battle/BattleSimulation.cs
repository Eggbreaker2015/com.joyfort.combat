using System;
using Combat.Foundation.Events;

namespace Combat.Core.Battle
{
    public sealed class BattleSimulation : IBattleRuntimeSnapshotSource
    {
        private readonly BattleConfig _config;
        private readonly EventBuffer<BattleEvent> _events = new EventBuffer<BattleEvent>();
        private readonly EventSequence _eventSequence = new EventSequence();
        private readonly IProjectileCollisionDetector _projectileCollisionDetector = new CircleProjectileCollisionDetector();
        private readonly ProjectileSystem.Scratch _projectileSystemScratch = new ProjectileSystem.Scratch();
        private readonly MovementSystem.Scratch _movementSystemScratch =
            new MovementSystem.Scratch();
        private readonly BattleWorld _world = new BattleWorld();
        private readonly BattlePerformanceRecorder _performanceRecorder;
        private int _nextUnitIdValue = 1;

        public BattleSimulation(BattleConfig config)
            : this(config, null)
        {
        }

        public BattleSimulation(BattleConfig config, BattlePerformanceRecorder performanceRecorder)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _performanceRecorder = performanceRecorder;
            CurrentTick = new BattleTick(0);
            SpawnInitialCombatants();
        }

        public BattleTick CurrentTick { get; private set; }
        public float SecondsPerTick => _config.SecondsPerTick;
        public int MaxTicks => _config.MaxTicks;
        public bool IsFinished { get; private set; }
        public EventStream<BattleEvent> Events => _events.AsStream();

        internal BattleConfig Config => _config;
        internal EventBuffer<BattleEvent> EventBuffer => _events;
        internal EventSequence EventSequence => _eventSequence;
        internal IProjectileCollisionDetector ProjectileCollisionDetector => _projectileCollisionDetector;
        internal ProjectileSystem.Scratch ProjectileSystemScratch => _projectileSystemScratch;
        internal MovementSystem.Scratch MovementSystemScratch => _movementSystemScratch;
        internal BattleWorld World => _world;

        public bool TryGetUnitRuntimeSnapshot(UnitId unitId, out UnitRuntimeSnapshot snapshot)
        {
            return _world.TryGetUnitRuntimeSnapshot(unitId, CurrentTick, out snapshot);
        }

        public UnitId SpawnCombatant(InitialCombatantSpawn spawn)
        {
            if (IsFinished || CurrentTick.Value >= _config.MaxTicks)
            {
                throw new InvalidOperationException("Cannot spawn combatants after the battle has finished.");
            }

            return EnqueueCombatantSpawn(spawn);
        }

        public bool ApplyStatus(UnitId sourceUnitId, UnitId targetUnitId, StatusDefinition status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            _events.Clear();
            if (IsFinished
                || !_world.TryFindEntity(sourceUnitId, out EntityId source)
                || !_world.TryFindEntity(targetUnitId, out EntityId target)
                || !_world.IsAliveUnit(source)
                || !_world.IsAliveUnit(target))
            {
                return false;
            }

            StatusApplicationData data = StatusApplicationDataFactory.Create(status);
            StatusApplicationResult result = StatusApplicationResolver.ApplyOrRefresh(_world, source, target, data);
            _events.Write(BattleEvent.StatusApplied(
                _eventSequence.Next(),
                CurrentTick,
                sourceUnitId,
                targetUnitId,
                result.Id,
                result.Polarity));
            return true;
        }

        public bool HealUnit(UnitId sourceUnitId, UnitId targetUnitId, int amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            _events.Clear();
            if (IsFinished
                || !_world.TryFindEntity(sourceUnitId, out EntityId source)
                || !_world.TryFindEntity(targetUnitId, out EntityId target)
                || !_world.IsAliveUnit(source)
                || !_world.IsAliveUnit(target))
            {
                return false;
            }

            _world.CommandBuffer.QueueEffect(BattleEffectCommand.Heal(source, target, amount));
            _world.FlushEffectCommands(_events, _eventSequence, CurrentTick);
            return _events.Count > 0;
        }

        public void Step(BattleInputFrame inputFrame)
        {
            if (IsFinished || CurrentTick.Value >= _config.MaxTicks)
            {
                _events.Clear();
                return;
            }

            _events.Clear();
            CurrentTick = CurrentTick.Next();

            BeginPerformanceStep();
            BattleSimulationPhasePipeline.Run(this, inputFrame);
            EndPerformanceStep();
        }

        private void BeginPerformanceStep()
        {
            _performanceRecorder?.BeginStep(CurrentTick);
        }

        private void EndPerformanceStep()
        {
            _performanceRecorder?.EndStep(_world.CreatePerformanceSnapshot(), _events.Count);
        }

        internal void BeginPerformanceSystem(string name)
        {
            _performanceRecorder?.BeginSystem(name, CurrentTick);
        }

        internal void EndPerformanceSystem()
        {
            _performanceRecorder?.EndSystem(_world.CreatePerformanceSnapshot(), _events.Count);
        }

        internal void FinishBattle(TeamId winningTeam)
        {
            IsFinished = true;
            _events.Write(BattleEvent.BattleEnded(_eventSequence.Next(), CurrentTick, winningTeam));
            _world.DestroyActiveProjectiles(_events, _eventSequence, CurrentTick);
        }

        private void SpawnInitialCombatants()
        {
            for (var i = 0; i < _config.InitialSpawns.Count; i++)
            {
                EnqueueCombatantSpawn(_config.InitialSpawns[i]);
            }

            _world.FlushSpawnCombatantCommands(_events, _eventSequence, CurrentTick);
        }

        private UnitId EnqueueCombatantSpawn(InitialCombatantSpawn spawn)
        {
            var unitId = new UnitId(_nextUnitIdValue++);
            _world.CommandBuffer.SpawnCombatant(new SpawnCombatantCommand(unitId, CreateSpawnData(spawn)));
            return unitId;
        }

        private static CombatantSpawnData CreateSpawnData(InitialCombatantSpawn spawn)
        {
            CombatantDefinition definition = spawn.Definition;
            var abilities = new AbilitySpawnData[definition.Abilities.Count];
            for (var i = 0; i < definition.Abilities.Count; i++)
            {
                abilities[i] = CreateAbilitySpawnData(definition.Abilities[i]);
            }

            AbilitySpawnData basicAbility = CreateAbilitySpawnData(definition.BasicAbility);
            BattleStatBlock stats = definition.Stats;
            BrainSpawnData brain = definition.AiDefinition == null
                ? BrainSpawnData.None
                : new BrainSpawnData(definition.AiDefinition.Id, definition.AiDefinition.Kind);
            TargetingBehaviorDefinition targeting = definition.TargetingBehavior;
            TargetingBehaviorSpawnData targetingBehavior = targeting.LimitsAcquisitionRange
                ? TargetingBehaviorSpawnData.Restricted(
                    targeting.AcquisitionRange,
                    targeting.NoProgressTimeoutTicks,
                    targeting.MinimumProgressDistance,
                    targeting.RejectedTargetCooldownTicks)
                : TargetingBehaviorSpawnData.Unrestricted;
            return new CombatantSpawnData(
                spawn.TeamId,
                definition.Id,
                spawn.Position,
                definition.Radius,
                stats,
                basicAbility,
                abilities,
                brain,
                targetingBehavior);
        }

        private static AbilitySpawnData CreateAbilitySpawnData(AbilityDefinition ability)
        {
            var frames = new AbilityEffectFrameData[ability.EffectFrames.Count];
            for (var frameIndex = 0; frameIndex < ability.EffectFrames.Count; frameIndex++)
            {
                AbilityEffectFrameDefinition frame = ability.EffectFrames[frameIndex];
                var effects = new BattleEffectData[frame.Effects.Count];
                for (var effectIndex = 0; effectIndex < frame.Effects.Count; effectIndex++)
                {
                    effects[effectIndex] = BattleEffectRuntimeDataFactory.CreateEffectData(frame.Effects[effectIndex]);
                }

                frames[frameIndex] = new AbilityEffectFrameData(frame.FrameId, frame.TickOffset, frame.Order, effects);
            }

            return new AbilitySpawnData(
                ability.Id,
                ability.Range,
                ability.CooldownTicks,
                ability.WindupTicks,
                ability.RecoveryTicks,
                ability.TargetSelection,
                frames,
                ability.ActionLocks);
        }
    }
}
