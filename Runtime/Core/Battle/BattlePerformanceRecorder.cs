using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Combat.Core.Battle
{
    public sealed class BattlePerformanceRecorder
    {
        private readonly List<BattlePerformanceStepSample> _steps = new List<BattlePerformanceStepSample>(128);
        private readonly List<BattlePerformanceSystemSample> _systemSamples = new List<BattlePerformanceSystemSample>(1024);
        private readonly ReadOnlyCollection<BattlePerformanceStepSample> _readOnlySteps;
        private readonly ReadOnlyCollection<BattlePerformanceSystemSample> _readOnlySystemSamples;
        private BattlePerformanceActiveSample _activeStep;
        private BattlePerformanceActiveSample _activeSystem;
        private int _stepSystemStartIndex;

        public BattlePerformanceRecorder()
        {
            _readOnlySteps = new ReadOnlyCollection<BattlePerformanceStepSample>(_steps);
            _readOnlySystemSamples = new ReadOnlyCollection<BattlePerformanceSystemSample>(_systemSamples);
        }

        public IReadOnlyList<BattlePerformanceStepSample> Steps => _readOnlySteps;
        public IReadOnlyList<BattlePerformanceSystemSample> SystemSamples => _readOnlySystemSamples;

        public void Clear()
        {
            _steps.Clear();
            _systemSamples.Clear();
            _activeStep = default;
            _activeSystem = default;
            _stepSystemStartIndex = 0;
        }

        internal void BeginStep(BattleTick tick)
        {
            if (_activeStep.IsActive)
            {
                throw new InvalidOperationException("A battle performance step sample is already active.");
            }

            _stepSystemStartIndex = _systemSamples.Count;
            _activeStep = BattlePerformanceActiveSample.Start("Step", tick);
        }

        internal void EndStep(BattlePerformanceWorldSnapshot worldSnapshot, int eventCount)
        {
            if (!_activeStep.IsActive)
            {
                return;
            }

            BattlePerformanceActiveSample active = _activeStep;
            _activeStep = default;
            _steps.Add(new BattlePerformanceStepSample(
                active.Tick,
                StopElapsedTicks(active.StartTimestamp),
                StopGcAllocatedBytesDelta(active.StartGcAllocatedBytes),
                eventCount,
                _systemSamples.Count - _stepSystemStartIndex,
                worldSnapshot.UnitCount,
                worldSnapshot.ProjectileCount,
                worldSnapshot.ProjectileEmitterCount));
        }

        internal void BeginSystem(string name, BattleTick tick)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("System sample name cannot be null or empty.", nameof(name));
            }

            if (_activeSystem.IsActive)
            {
                throw new InvalidOperationException("A battle performance system sample is already active.");
            }

            _activeSystem = BattlePerformanceActiveSample.Start(name, tick);
        }

        internal void EndSystem(BattlePerformanceWorldSnapshot worldSnapshot, int eventCount)
        {
            if (!_activeSystem.IsActive)
            {
                return;
            }

            BattlePerformanceActiveSample active = _activeSystem;
            _activeSystem = default;
            _systemSamples.Add(new BattlePerformanceSystemSample(
                active.Name,
                active.Tick,
                StopElapsedTicks(active.StartTimestamp),
                StopGcAllocatedBytesDelta(active.StartGcAllocatedBytes),
                eventCount,
                worldSnapshot.UnitCount,
                worldSnapshot.ProjectileCount,
                worldSnapshot.ProjectileEmitterCount));
        }

        private static long StopElapsedTicks(long startTimestamp)
        {
            long elapsed = Stopwatch.GetTimestamp() - startTimestamp;
            return elapsed >= 0L ? elapsed : 0L;
        }

        private static long StopGcAllocatedBytesDelta(long startBytes)
        {
            long delta = GC.GetAllocatedBytesForCurrentThread() - startBytes;
            return delta >= 0L ? delta : 0L;
        }

        private readonly struct BattlePerformanceActiveSample
        {
            private BattlePerformanceActiveSample(
                string name,
                BattleTick tick,
                long startTimestamp,
                long startGcAllocatedBytes)
            {
                Name = name;
                Tick = tick;
                StartTimestamp = startTimestamp;
                StartGcAllocatedBytes = startGcAllocatedBytes;
                IsActive = true;
            }

            public string Name { get; }
            public BattleTick Tick { get; }
            public long StartTimestamp { get; }
            public long StartGcAllocatedBytes { get; }
            public bool IsActive { get; }

            public static BattlePerformanceActiveSample Start(string name, BattleTick tick)
            {
                return new BattlePerformanceActiveSample(
                    name,
                    tick,
                    Stopwatch.GetTimestamp(),
                    GC.GetAllocatedBytesForCurrentThread());
            }
        }
    }

    public readonly struct BattlePerformanceStepSample
    {
        public BattlePerformanceStepSample(
            BattleTick tick,
            long elapsedTicks,
            long gcAllocatedBytesDelta,
            int eventCount,
            int systemSampleCount,
            int unitCount,
            int projectileCount,
            int projectileEmitterCount)
        {
            Tick = tick;
            ElapsedTicks = elapsedTicks;
            GcAllocatedBytesDelta = gcAllocatedBytesDelta;
            EventCount = eventCount;
            SystemSampleCount = systemSampleCount;
            UnitCount = unitCount;
            ProjectileCount = projectileCount;
            ProjectileEmitterCount = projectileEmitterCount;
        }

        public BattleTick Tick { get; }
        public long ElapsedTicks { get; }
        public long GcAllocatedBytesDelta { get; }
        public int EventCount { get; }
        public int SystemSampleCount { get; }
        public int UnitCount { get; }
        public int ProjectileCount { get; }
        public int ProjectileEmitterCount { get; }
    }

    public readonly struct BattlePerformanceSystemSample
    {
        public BattlePerformanceSystemSample(
            string name,
            BattleTick tick,
            long elapsedTicks,
            long gcAllocatedBytesDelta,
            int eventCount,
            int unitCount,
            int projectileCount,
            int projectileEmitterCount)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Tick = tick;
            ElapsedTicks = elapsedTicks;
            GcAllocatedBytesDelta = gcAllocatedBytesDelta;
            EventCount = eventCount;
            UnitCount = unitCount;
            ProjectileCount = projectileCount;
            ProjectileEmitterCount = projectileEmitterCount;
        }

        public string Name { get; }
        public BattleTick Tick { get; }
        public long ElapsedTicks { get; }
        public long GcAllocatedBytesDelta { get; }
        public int EventCount { get; }
        public int UnitCount { get; }
        public int ProjectileCount { get; }
        public int ProjectileEmitterCount { get; }
    }

    public readonly struct BattlePerformanceWorldSnapshot
    {
        public BattlePerformanceWorldSnapshot(int unitCount, int projectileCount, int projectileEmitterCount)
        {
            UnitCount = unitCount;
            ProjectileCount = projectileCount;
            ProjectileEmitterCount = projectileEmitterCount;
        }

        public int UnitCount { get; }
        public int ProjectileCount { get; }
        public int ProjectileEmitterCount { get; }
    }
}
