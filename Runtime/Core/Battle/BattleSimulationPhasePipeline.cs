namespace Combat.Core.Battle
{
    internal static class BattleSimulationPhasePipeline
    {
        private static readonly BattleSimulationPhase[] Phases =
        {
            // PreAction phase
            new BattleSimulationPhase(BattleSimulationPhaseKind.FlushSpawnCombatantCommands, "FlushSpawnCombatantCommands"),
            new BattleSimulationPhase(BattleSimulationPhaseKind.StatusSystem, "StatusSystem"),
            new BattleSimulationPhase(BattleSimulationPhaseKind.FlushEffectCommands, "FlushEffectCommands.Status"),
            new BattleSimulationPhase(BattleSimulationPhaseKind.VictorySystem, "VictorySystem.Status", stopRemainingPhasesOnVictory: true),
            new BattleSimulationPhase(BattleSimulationPhaseKind.ProjectileEmitterSystem, "ProjectileEmitterSystem"),
            new BattleSimulationPhase(BattleSimulationPhaseKind.FlushSpawnProjectileCommands, "FlushSpawnProjectileCommands"),
            new BattleSimulationPhase(BattleSimulationPhaseKind.ProjectileSystem, "ProjectileSystem"),
            new BattleSimulationPhase(BattleSimulationPhaseKind.FlushEffectCommands, "FlushEffectCommands.Projectile"),
            new BattleSimulationPhase(BattleSimulationPhaseKind.VictorySystem, "VictorySystem.Projectile", stopRemainingPhasesOnVictory: true),
            new BattleSimulationPhase(BattleSimulationPhaseKind.InputIntentSystem, "InputIntentSystem"),
            new BattleSimulationPhase(BattleSimulationPhaseKind.UnitActionExecutionSystem, "UnitActionExecutionSystem"),
            new BattleSimulationPhase(BattleSimulationPhaseKind.FlushEffectCommands, "FlushEffectCommands.ActionRelease"),
            new BattleSimulationPhase(BattleSimulationPhaseKind.VictorySystem, "VictorySystem.ActionRelease", stopRemainingPhasesOnVictory: true),

            // Decision phase
            new BattleSimulationPhase(BattleSimulationPhaseKind.TargetingSystem, "TargetingSystem"),
            new BattleSimulationPhase(BattleSimulationPhaseKind.MovementSystem, "MovementSystem"),
            new BattleSimulationPhase(BattleSimulationPhaseKind.AiDecisionSystem, "AiDecisionSystem"),
            new BattleSimulationPhase(BattleSimulationPhaseKind.AbilitySystem, "AbilitySystem"),

            // Resolve phase
            new BattleSimulationPhase(BattleSimulationPhaseKind.FlushActionCommands, "FlushActionCommands"),
            new BattleSimulationPhase(BattleSimulationPhaseKind.FlushEffectCommands, "FlushEffectCommands.Action"),
            new BattleSimulationPhase(BattleSimulationPhaseKind.VictorySystem, "VictorySystem"),
            new BattleSimulationPhase(BattleSimulationPhaseKind.ApplyStructuralCommands, "ApplyStructuralCommands")
        };

        public static void Run(BattleSimulation simulation, BattleInputFrame inputFrame)
        {
            for (var i = 0; i < Phases.Length; i++)
            {
                BattleSimulationPhase phase = Phases[i];
                simulation.BeginPerformanceSystem(phase.PerformanceName);
                BattleSimulationPhaseResult result = RunPhase(simulation, inputFrame, phase);
                simulation.EndPerformanceSystem();

                if (result.StopRemainingPhases)
                {
                    simulation.FinishBattle(result.WinningTeam);
                    RunApplyStructuralCommands(simulation);
                    return;
                }
            }
        }

        private static BattleSimulationPhaseResult RunPhase(BattleSimulation simulation, BattleInputFrame inputFrame, BattleSimulationPhase phase)
        {
            switch (phase.Kind)
            {
                case BattleSimulationPhaseKind.FlushSpawnCombatantCommands:
                    simulation.World.FlushSpawnCombatantCommands(simulation.EventBuffer, simulation.EventSequence, simulation.CurrentTick);
                    return default;
                case BattleSimulationPhaseKind.StatusSystem:
                    StatusSystem.Run(simulation.World, simulation.EventBuffer, simulation.EventSequence, simulation.CurrentTick);
                    return default;
                case BattleSimulationPhaseKind.FlushEffectCommands:
                    simulation.World.FlushEffectCommands(simulation.EventBuffer, simulation.EventSequence, simulation.CurrentTick);
                    return default;
                case BattleSimulationPhaseKind.VictorySystem:
                    return RunVictoryCheck(simulation, phase.StopRemainingPhasesOnVictory);
                case BattleSimulationPhaseKind.ProjectileEmitterSystem:
                    ProjectileEmitterSystem.Run(simulation.World, simulation.Config.TicksPerSecond, simulation.CurrentTick);
                    return default;
                case BattleSimulationPhaseKind.FlushSpawnProjectileCommands:
                    simulation.World.FlushSpawnProjectileCommands(simulation.EventBuffer, simulation.EventSequence, simulation.CurrentTick);
                    return default;
                case BattleSimulationPhaseKind.ProjectileSystem:
                    ProjectileSystem.Run(
                        simulation.World,
                        simulation.ProjectileCollisionDetector,
                        simulation.EventBuffer,
                        simulation.EventSequence,
                        simulation.CurrentTick,
                        simulation.ProjectileSystemScratch,
                        simulation.Config.ProjectileCullingBounds);
                    return default;
                case BattleSimulationPhaseKind.UnitActionExecutionSystem:
                    UnitActionExecutionSystem.Run(simulation.World, simulation.EventBuffer, simulation.EventSequence, simulation.CurrentTick);
                    return default;
                case BattleSimulationPhaseKind.InputIntentSystem:
                    InputIntentSystem.Run(
                        simulation.World,
                        inputFrame,
                        simulation.EventBuffer,
                        simulation.EventSequence,
                        simulation.CurrentTick);
                    return default;
                case BattleSimulationPhaseKind.TargetingSystem:
                    TargetingSystem.Run(simulation.World, simulation.EventBuffer, simulation.EventSequence, simulation.CurrentTick);
                    return default;
                case BattleSimulationPhaseKind.MovementSystem:
                    MovementSystem.Run(
                        simulation.World,
                        simulation.Config.TicksPerSecond,
                        simulation.Config.LocalAvoidanceEnabled,
                        simulation.EventBuffer,
                        simulation.EventSequence,
                        simulation.CurrentTick,
                        simulation.MovementSystemScratch);
                    return default;
                case BattleSimulationPhaseKind.AiDecisionSystem:
                    AiDecisionSystem.Run(simulation.World, simulation.CurrentTick);
                    return default;
                case BattleSimulationPhaseKind.AbilitySystem:
                    AbilitySystem.Run(simulation.World, simulation.EventBuffer, simulation.EventSequence, simulation.CurrentTick);
                    return default;
                case BattleSimulationPhaseKind.FlushActionCommands:
                    simulation.World.FlushActionCommands(simulation.EventBuffer, simulation.EventSequence, simulation.CurrentTick);
                    return default;
                case BattleSimulationPhaseKind.ApplyStructuralCommands:
                    simulation.World.ApplyStructuralCommands();
                    return default;
                default:
                    return default;
            }
        }

        private static BattleSimulationPhaseResult RunVictoryCheck(BattleSimulation simulation, bool stopRemainingPhasesOnVictory)
        {
            if (!simulation.Config.AutomaticVictoryEnabled)
            {
                return default;
            }

            if (!VictorySystem.TryGetWinningTeam(simulation.World, out TeamId winningTeam))
            {
                return default;
            }

            if (stopRemainingPhasesOnVictory)
            {
                return new BattleSimulationPhaseResult(winningTeam);
            }

            simulation.FinishBattle(winningTeam);
            return default;
        }

        private static void RunApplyStructuralCommands(BattleSimulation simulation)
        {
            simulation.BeginPerformanceSystem("ApplyStructuralCommands");
            simulation.World.ApplyStructuralCommands();
            simulation.EndPerformanceSystem();
        }

        private readonly struct BattleSimulationPhase
        {
            public BattleSimulationPhase(
                BattleSimulationPhaseKind kind,
                string performanceName,
                bool stopRemainingPhasesOnVictory = false)
            {
                Kind = kind;
                PerformanceName = performanceName;
                StopRemainingPhasesOnVictory = stopRemainingPhasesOnVictory;
            }

            public BattleSimulationPhaseKind Kind { get; }
            public string PerformanceName { get; }
            public bool StopRemainingPhasesOnVictory { get; }
        }

        private readonly struct BattleSimulationPhaseResult
        {
            public BattleSimulationPhaseResult(TeamId winningTeam)
            {
                StopRemainingPhases = true;
                WinningTeam = winningTeam;
            }

            public bool StopRemainingPhases { get; }
            public TeamId WinningTeam { get; }
        }

        private enum BattleSimulationPhaseKind
        {
            FlushSpawnCombatantCommands,
            StatusSystem,
            FlushEffectCommands,
            VictorySystem,
            ProjectileEmitterSystem,
            FlushSpawnProjectileCommands,
            ProjectileSystem,
            UnitActionExecutionSystem,
            InputIntentSystem,
            TargetingSystem,
            MovementSystem,
            AiDecisionSystem,
            AbilitySystem,
            FlushActionCommands,
            ApplyStructuralCommands
        }
    }
}
