#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Combat.Core.Battle;
using Combat.Foundation.Diagnostics;
using Combat.Runtime.Runner;
using Combat.Unity.Authoring;
using Combat.Unity.Diagnostics;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Combat.Unity.Demo
{
    public static class SampleScenePerformanceProbe
    {
        public const string ScenePath = "Assets/Scenes/SampleScene.scene";
        public const string ScenarioPath = "Assets/CombatSamples/Standalone/Config/DefaultBattleScenario.asset";
        public const string ResultPath = "TestResults/sample-scene-performance-probe-result.txt";
        public const string NoCombatLogsResultPath = "TestResults/sample-scene-performance-probe-no-combat-logs-result.txt";

        public static void Run()
        {
            RunProbe(ResultPath, disableCombatLogs: false);
        }

        public static void RunWithoutCombatLogs()
        {
            RunProbe(NoCombatLogsResultPath, disableCombatLogs: true);
        }

        private static void RunProbe(string resultPath, bool disableCombatLogs)
        {
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var probeObject = new GameObject("SampleScenePerformanceProbe");
                var runner = probeObject.AddComponent<SampleScenePerformanceProbeRunner>();
                runner.Configure(resultPath, disableCombatLogs);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

    }

    public sealed class SampleScenePerformanceProbeRunner : MonoBehaviour
    {
        private const double MaxPlaySeconds = 12.0;
        private const int MaxCoreTicks = 4096;
        private readonly List<double> _frameMilliseconds = new List<double>(1024);
        private readonly int[] _gcStartCollections = new int[3];
        private ProfilerRecorder _gcAllocatedInFrameRecorder;
        private CoreProbeReport _coreReport;
        private double _startTime;
        private double _previousFrameTime;
        private long _managedStartBytes;
        private long _managedPeakBytes;
        private long _managedEndBytes;
        private long _gcAllocatedInFrameBytes;
        private bool _finished;
        private BattleInstance _battle;
        [SerializeField] private string _resultPath = SampleScenePerformanceProbe.ResultPath;
        [SerializeField] private bool _disableCombatLogs;

        private bool BattleFinished { get; set; }
        private int BattleTick { get; set; }
        private int WinningTeamId { get; set; }

        public void Configure(string resultPath, bool disableCombatLogs)
        {
            _resultPath = string.IsNullOrEmpty(resultPath) ? SampleScenePerformanceProbe.ResultPath : resultPath;
            _disableCombatLogs = disableCombatLogs;
        }

        private void Start()
        {
            _coreReport = RunCoreProbe();
            BattleScenarioAsset scenario = AssetDatabase.LoadAssetAtPath<BattleScenarioAsset>(
                SampleScenePerformanceProbe.ScenarioPath);
            if (scenario == null)
            {
                throw new InvalidOperationException(
                    $"Could not load default battle scenario at {SampleScenePerformanceProbe.ScenarioPath}.");
            }

            CombatLogger logger = CreateBattleLogger(
                _disableCombatLogs,
                new UnityDebugCombatLogSink(this));
            _battle = new BattleInstance(
                BattleAuthoringConverter.BuildBattleConfig(scenario),
                logger);
            _startTime = Time.realtimeSinceStartupAsDouble;
            _previousFrameTime = _startTime;
            _managedStartBytes = GC.GetTotalMemory(false);
            _managedPeakBytes = _managedStartBytes;
            for (var i = 0; i < _gcStartCollections.Length; i++)
            {
                _gcStartCollections[i] = GC.CollectionCount(i);
            }

            TryStartGcRecorder();
        }

        internal static CombatLogger CreateBattleLogger(
            bool disableCombatLogs,
            ICombatLogSink enabledSink)
        {
            return disableCombatLogs
                ? CombatLogger.Disabled
                : new CombatLogger(
                    CombatLogSettings.ShowInfoAndAbove,
                    enabledSink ?? throw new ArgumentNullException(nameof(enabledSink)));
        }

        private void Update()
        {
            if (_finished)
            {
                return;
            }

            RecordFrame();
            if (_battle != null && !_battle.Simulation.IsFinished)
            {
                _battle.TickOnce(BattleInputFrame.Empty);
            }

            BattleTick = _battle?.Simulation.CurrentTick.Value ?? 0;
            BattleFinished = _battle?.Result.IsFinished ?? false;
            WinningTeamId = _battle?.Result.WinningTeamId.Value ?? 0;

            if (BattleFinished || Time.realtimeSinceStartupAsDouble - _startTime >= MaxPlaySeconds)
            {
                Finish(0);
            }
        }

        private void OnDestroy()
        {
            if (_gcAllocatedInFrameRecorder.Valid)
            {
                _gcAllocatedInFrameRecorder.Dispose();
            }
        }

        private static CoreProbeReport RunCoreProbe()
        {
            BattleScenarioAsset scenario = AssetDatabase.LoadAssetAtPath<BattleScenarioAsset>(SampleScenePerformanceProbe.ScenarioPath);
            if (scenario == null)
            {
                throw new InvalidOperationException($"Could not load default battle scenario at {SampleScenePerformanceProbe.ScenarioPath}.");
            }

            BattleConfig config = BattleAuthoringConverter.BuildBattleConfig(scenario);
            var recorder = new BattlePerformanceRecorder();
            var simulation = new BattleSimulation(config, recorder);
            while (!simulation.IsFinished && simulation.CurrentTick.Value < simulation.MaxTicks && simulation.CurrentTick.Value < MaxCoreTicks)
            {
                simulation.Step(BattleInputFrame.Empty);
            }

            return CoreProbeReport.Create(simulation, recorder);
        }

        private void RecordFrame()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            double frameMs = (now - _previousFrameTime) * 1000.0;
            if (frameMs > 0.0)
            {
                _frameMilliseconds.Add(frameMs);
            }

            _previousFrameTime = now;
            _managedEndBytes = GC.GetTotalMemory(false);
            if (_managedEndBytes > _managedPeakBytes)
            {
                _managedPeakBytes = _managedEndBytes;
            }

            if (_gcAllocatedInFrameRecorder.Valid)
            {
                long allocated = _gcAllocatedInFrameRecorder.LastValue;
                if (allocated > 0L)
                {
                    _gcAllocatedInFrameBytes += allocated;
                }
            }
        }

        private void TryStartGcRecorder()
        {
            try
            {
                _gcAllocatedInFrameRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not start GC Allocated In Frame recorder: " + exception.Message);
            }
        }

        private void Finish(int exitCode)
        {
            _finished = true;
            string report = BuildReport();
            Directory.CreateDirectory(Path.GetDirectoryName(_resultPath));
            File.WriteAllText(_resultPath, report);
            Debug.Log(report);
            if (_gcAllocatedInFrameRecorder.Valid)
            {
                _gcAllocatedInFrameRecorder.Dispose();
            }

            EditorApplication.Exit(exitCode);
        }

        private string BuildReport()
        {
            double durationSeconds = Math.Max(0.000001, Time.realtimeSinceStartupAsDouble - _startTime);
            double averageFrameMs = Average(_frameMilliseconds);
            double maxFrameMs = Max(_frameMilliseconds);
            double p95FrameMs = Percentile(_frameMilliseconds, 0.95);
            var builder = new StringBuilder(2048);
            builder.AppendLine("PERFORMANCE_PROBE_RESULT_BEGIN");
            builder.AppendLine("scene=" + SampleScenePerformanceProbe.ScenePath);
            builder.AppendLine("scenario=" + SampleScenePerformanceProbe.ScenarioPath);
            builder.AppendLine("playmodeFrames=" + _frameMilliseconds.Count);
            builder.AppendLine("playmodeDurationSeconds=" + Format(durationSeconds));
            builder.AppendLine("playmodeAverageFps=" + Format(1000.0 / Math.Max(0.000001, averageFrameMs)));
            builder.AppendLine("playmodeAverageFrameMs=" + Format(averageFrameMs));
            builder.AppendLine("playmodeP95FrameMs=" + Format(p95FrameMs));
            builder.AppendLine("playmodeMaxFrameMs=" + Format(maxFrameMs));
            builder.AppendLine("managedMemoryStartMb=" + Format(BytesToMb(_managedStartBytes)));
            builder.AppendLine("managedMemoryPeakMb=" + Format(BytesToMb(_managedPeakBytes)));
            builder.AppendLine("managedMemoryEndMb=" + Format(BytesToMb(_managedEndBytes)));
            builder.AppendLine("gcCollectionsGen0=" + (GC.CollectionCount(0) - _gcStartCollections[0]));
            builder.AppendLine("gcCollectionsGen1=" + (GC.CollectionCount(1) - _gcStartCollections[1]));
            builder.AppendLine("gcCollectionsGen2=" + (GC.CollectionCount(2) - _gcStartCollections[2]));
            builder.AppendLine("gcAllocatedInFrameMb=" + Format(BytesToMb(_gcAllocatedInFrameBytes)));
            builder.AppendLine("battleFinished=" + BattleFinished);
            builder.AppendLine("battleTick=" + BattleTick);
            builder.AppendLine("winningTeamId=" + WinningTeamId);
            builder.AppendLine("coreHeadlessTicks=" + _coreReport.TickCount);
            builder.AppendLine("coreHeadlessFinished=" + _coreReport.IsFinished);
            builder.AppendLine("coreHeadlessTotalMs=" + Format(_coreReport.TotalMilliseconds));
            builder.AppendLine("coreHeadlessAverageStepMs=" + Format(_coreReport.AverageStepMilliseconds));
            builder.AppendLine("coreHeadlessMaxStepMs=" + Format(_coreReport.MaxStepMilliseconds));
            builder.AppendLine("coreHeadlessGcAllocatedKb=" + Format(_coreReport.GcAllocatedBytes / 1024.0));
            builder.AppendLine("coreTopSystems=" + _coreReport.TopSystems);
            builder.AppendLine("PERFORMANCE_PROBE_RESULT_END");
            return builder.ToString();
        }

        private readonly struct CoreProbeReport
        {
            private CoreProbeReport(
                int tickCount,
                bool isFinished,
                double totalMilliseconds,
                double averageStepMilliseconds,
                double maxStepMilliseconds,
                long gcAllocatedBytes,
                string topSystems)
            {
                TickCount = tickCount;
                IsFinished = isFinished;
                TotalMilliseconds = totalMilliseconds;
                AverageStepMilliseconds = averageStepMilliseconds;
                MaxStepMilliseconds = maxStepMilliseconds;
                GcAllocatedBytes = gcAllocatedBytes;
                TopSystems = topSystems;
            }

            public int TickCount { get; }
            public bool IsFinished { get; }
            public double TotalMilliseconds { get; }
            public double AverageStepMilliseconds { get; }
            public double MaxStepMilliseconds { get; }
            public long GcAllocatedBytes { get; }
            public string TopSystems { get; }

            public static CoreProbeReport Create(BattleSimulation simulation, BattlePerformanceRecorder recorder)
            {
                double totalMs = 0.0;
                double maxStepMs = 0.0;
                long gcBytes = 0L;
                for (var i = 0; i < recorder.Steps.Count; i++)
                {
                    double stepMs = StopwatchTicksToMs(recorder.Steps[i].ElapsedTicks);
                    totalMs += stepMs;
                    if (stepMs > maxStepMs)
                    {
                        maxStepMs = stepMs;
                    }

                    gcBytes += recorder.Steps[i].GcAllocatedBytesDelta;
                }

                var systems = new Dictionary<string, SystemAggregate>();
                for (var i = 0; i < recorder.SystemSamples.Count; i++)
                {
                    BattlePerformanceSystemSample sample = recorder.SystemSamples[i];
                    if (!systems.TryGetValue(sample.Name, out SystemAggregate aggregate))
                    {
                        aggregate = new SystemAggregate(sample.Name);
                    }

                    aggregate.Add(StopwatchTicksToMs(sample.ElapsedTicks), sample.GcAllocatedBytesDelta);
                    systems[sample.Name] = aggregate;
                }

                var ordered = new List<SystemAggregate>(systems.Values);
                ordered.Sort((left, right) => right.TotalMilliseconds.CompareTo(left.TotalMilliseconds));

                var topBuilder = new StringBuilder(512);
                int count = Math.Min(5, ordered.Count);
                for (var i = 0; i < count; i++)
                {
                    if (i > 0)
                    {
                        topBuilder.Append("; ");
                    }

                    SystemAggregate aggregate = ordered[i];
                    topBuilder.Append(aggregate.Name);
                    topBuilder.Append(" totalMs=");
                    topBuilder.Append(Format(aggregate.TotalMilliseconds));
                    topBuilder.Append(" maxMs=");
                    topBuilder.Append(Format(aggregate.MaxMilliseconds));
                    topBuilder.Append(" gcKb=");
                    topBuilder.Append(Format(aggregate.GcAllocatedBytes / 1024.0));
                }

                int stepCount = Math.Max(1, recorder.Steps.Count);
                return new CoreProbeReport(
                    simulation.CurrentTick.Value,
                    simulation.IsFinished,
                    totalMs,
                    totalMs / stepCount,
                    maxStepMs,
                    gcBytes,
                    topBuilder.ToString());
            }
        }

        private struct SystemAggregate
        {
            public SystemAggregate(string name)
            {
                Name = name;
                TotalMilliseconds = 0.0;
                MaxMilliseconds = 0.0;
                GcAllocatedBytes = 0L;
            }

            public string Name { get; }
            public double TotalMilliseconds { get; private set; }
            public double MaxMilliseconds { get; private set; }
            public long GcAllocatedBytes { get; private set; }

            public void Add(double milliseconds, long gcAllocatedBytes)
            {
                TotalMilliseconds += milliseconds;
                if (milliseconds > MaxMilliseconds)
                {
                    MaxMilliseconds = milliseconds;
                }

                GcAllocatedBytes += gcAllocatedBytes;
            }
        }

        private static double StopwatchTicksToMs(long ticks)
        {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }

        private static double Average(List<double> values)
        {
            if (values.Count == 0)
            {
                return 0.0;
            }

            double total = 0.0;
            for (var i = 0; i < values.Count; i++)
            {
                total += values[i];
            }

            return total / values.Count;
        }

        private static double Max(List<double> values)
        {
            double max = 0.0;
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] > max)
                {
                    max = values[i];
                }
            }

            return max;
        }

        private static double Percentile(List<double> values, double percentile)
        {
            if (values.Count == 0)
            {
                return 0.0;
            }

            var sorted = new List<double>(values);
            sorted.Sort();
            int index = Mathf.Clamp((int)Math.Ceiling(percentile * sorted.Count) - 1, 0, sorted.Count - 1);
            return sorted[index];
        }

        private static double BytesToMb(long bytes)
        {
            return bytes / 1024.0 / 1024.0;
        }

        private static string Format(double value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
#endif
