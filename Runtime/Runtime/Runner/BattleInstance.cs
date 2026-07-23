using System;
using Combat.Core.Battle;
using Combat.Foundation.Diagnostics;
using Combat.Foundation.Events;

namespace Combat.Runtime.Runner
{
    /// <summary>
    /// Owns one headless battle simulation and exposes its rule outputs without
    /// dispatching presentation commands.
    /// </summary>
    public sealed class BattleInstance
    {
        private readonly BattleEventLogger _eventLogger;
        private int _outputRevision;
        private int _committedDiagnosticsRevision;

        public BattleInstance(BattleConfig config)
            : this(config, CombatLogger.Disabled)
        {
        }

        public BattleInstance(BattleConfig config, CombatLogger logger)
            : this(config, logger, deferInitialDiagnostics: false)
        {
        }

        /// <summary>
        /// Creates a battle whose constructor-produced facts must be presented before
        /// diagnostics are committed. The returned token reveals the instance only
        /// after its one-shot completion method is called.
        /// </summary>
        public static BattleInitialPresentationComposition CreateForPresentation(
            BattleConfig config,
            CombatLogger logger)
        {
            return new BattleInitialPresentationComposition(
                new BattleInstance(
                    config,
                    logger ?? CombatLogger.Disabled,
                    deferInitialDiagnostics: true));
        }

        private BattleInstance(
            BattleConfig config,
            CombatLogger logger,
            bool deferInitialDiagnostics)
        {
            Simulation = new BattleSimulation(config);
            _eventLogger = new BattleEventLogger(logger);
            Result = new BattleResult(false, default);
            InitialOutput = CaptureCurrentOutput();
            if (!deferInitialDiagnostics)
            {
                CommitCurrentDiagnostics();
            }
        }

        public BattleSimulation Simulation { get; }
        public BattleResult Result { get; private set; }

        /// <summary>
        /// Gets the constructor-produced spawn events. The event stream is a
        /// synchronous view of the simulation buffer and must be consumed before
        /// another operation clears or replaces that buffer's contents.
        /// </summary>
        public BattleStepOutput InitialOutput { get; }

        public UnitId SpawnCombatant(InitialCombatantSpawn spawn)
        {
            return Simulation.SpawnCombatant(spawn);
        }

        /// <summary>
        /// Applies a status through the Core rule path and returns the current
        /// synchronous event view, including an empty view when application fails.
        /// Direct callers also commit diagnostics before this method returns;
        /// diagnostics failures propagate after the rule result is captured.
        /// </summary>
        public bool ApplyStatus(
            UnitId sourceUnitId,
            UnitId targetUnitId,
            StatusDefinition status,
            out BattleStepOutput output)
        {
            bool applied = ApplyStatusWithDeferredDiagnostics(
                sourceUnitId,
                targetUnitId,
                status,
                out output);
            CommitCurrentDiagnostics();
            return applied;
        }

        public bool HealUnit(
            UnitId sourceUnitId,
            UnitId targetUnitId,
            int amount,
            out BattleStepOutput output)
        {
            bool healed = HealUnitWithDeferredDiagnostics(sourceUnitId, targetUnitId, amount, out output);
            CommitCurrentDiagnostics();
            return healed;
        }

        /// <summary>
        /// Creates a battle without committing constructor-produced diagnostics.
        /// Integration adapters that need to copy the synchronous initial event view
        /// into owned storage must call <see cref="CommitCurrentDiagnostics"/> after
        /// the copy has completed.
        /// </summary>
        public static BattleInstance CreateWithDeferredInitialDiagnostics(
            BattleConfig config,
            CombatLogger logger)
        {
            return new BattleInstance(config, logger, deferInitialDiagnostics: true);
        }

        /// <summary>
        /// Applies a status and leaves diagnostics pending so an integration adapter
        /// can first copy the returned synchronous event view into owned storage.
        /// </summary>
        public bool ApplyStatusWithDeferredDiagnostics(
            UnitId sourceUnitId,
            UnitId targetUnitId,
            StatusDefinition status,
            out BattleStepOutput output)
        {
            bool applied = Simulation.ApplyStatus(sourceUnitId, targetUnitId, status);
            output = CaptureCurrentOutput();
            return applied;
        }

        /// <summary>
        /// Heals a unit and leaves diagnostics pending so an integration adapter can
        /// first copy the returned synchronous event view into owned storage.
        /// </summary>
        public bool HealUnitWithDeferredDiagnostics(
            UnitId sourceUnitId,
            UnitId targetUnitId,
            int amount,
            out BattleStepOutput output)
        {
            bool healed = Simulation.HealUnit(sourceUnitId, targetUnitId, amount);
            output = CaptureCurrentOutput();
            return healed;
        }

        /// <summary>
        /// Requests exactly one Core step and returns its synchronous event view.
        /// A finished or exhausted simulation preserves Core's no-advance behavior.
        /// Direct callers also commit diagnostics before this method returns;
        /// diagnostics failures propagate after the rule result is captured.
        /// </summary>
        public BattleStepOutput TickOnce(BattleInputFrame inputFrame)
        {
            BattleStepOutput output = TickOnceWithDeferredDiagnostics(inputFrame);
            CommitCurrentDiagnostics();
            return output;
        }

        /// <summary>
        /// Advances one Core step and leaves diagnostics pending so an integration
        /// adapter can first copy the returned synchronous event view into owned storage.
        /// </summary>
        public BattleStepOutput TickOnceWithDeferredDiagnostics(BattleInputFrame inputFrame)
        {
            Simulation.Step(inputFrame);
            return CaptureCurrentOutput();
        }

        /// <summary>
        /// Commits diagnostics for the latest captured output at most once. This is
        /// the completion half of the deferred-diagnostics integration methods.
        /// </summary>
        public void CommitCurrentDiagnostics()
        {
            if (_committedDiagnosticsRevision == _outputRevision)
            {
                return;
            }

            _committedDiagnosticsRevision = _outputRevision;
            _eventLogger.Log(Simulation.Events);
        }

        private BattleStepOutput CaptureCurrentOutput()
        {
            EventStream<BattleEvent> events = Simulation.Events;
            for (var i = 0; i < events.Count; i++)
            {
                BattleEvent battleEvent = events[i];
                if (battleEvent.Type == BattleEventType.BattleEnded)
                {
                    Result = new BattleResult(true, battleEvent.WinningTeamId);
                }
            }

            _outputRevision++;
            return new BattleStepOutput(Simulation.CurrentTick, events, Result);
        }
    }

    /// <summary>
    /// One-shot handoff used by outer presentation composition. It prevents callers
    /// from advancing the battle before initial facts have been presented and their
    /// diagnostics commit has been attempted.
    /// </summary>
    public sealed class BattleInitialPresentationComposition
    {
        private BattleInstance _instance;

        internal BattleInitialPresentationComposition(BattleInstance instance)
        {
            _instance = instance;
        }

        public BattleStepOutput InitialOutput
        {
            get
            {
                EnsurePending();
                return _instance.InitialOutput;
            }
        }

        public BattleInstance CompletePresentation()
        {
            EnsurePending();
            BattleInstance instance = _instance;
            _instance = null;
            instance.CommitCurrentDiagnostics();
            return instance;
        }

        private void EnsurePending()
        {
            if (_instance == null)
            {
                throw new InvalidOperationException(
                    "Initial battle presentation composition has already completed.");
            }
        }
    }
}
