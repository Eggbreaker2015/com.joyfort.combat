using System.Linq;
using System.Reflection;
using Combat.Foundation.Diagnostics;
using Combat.Unity.Demo;
using NUnit.Framework;

namespace Combat.Tests.Unity.Authoring
{
    public sealed class SampleScenePerformanceProbeTests
    {
        [Test]
        public void Probe_ExposesDistinctLoggingConfigurationPath()
        {
            MethodInfo configure = typeof(SampleScenePerformanceProbeRunner)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .SingleOrDefault(method =>
                    method.Name == "Configure" && method.GetParameters().Length == 2);
            MethodInfo factory = typeof(SampleScenePerformanceProbeRunner).GetMethod(
                "CreateBattleLogger", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(configure, Is.Not.Null);
            Assert.That(factory, Is.Not.Null);
        }

        [Test]
        public void EnabledAndDisabledProbeLoggers_HaveDistinctSinkBehavior()
        {
            var enabledSink = new RecordingSink();
            CombatLogger enabled = SampleScenePerformanceProbeRunner.CreateBattleLogger(
                disableCombatLogs: false, enabledSink);
            CombatLogger disabled = SampleScenePerformanceProbeRunner.CreateBattleLogger(
                disableCombatLogs: true, new RecordingSink());

            enabled.Info(CombatLogTags.Runtime, "enabled");
            disabled.Info(CombatLogTags.Runtime, "disabled");

            Assert.That(enabledSink.Count, Is.EqualTo(1));
            Assert.That(disabled.ShouldLog(CombatLogLevel.Info, CombatLogTags.Runtime), Is.False);
        }

        private sealed class RecordingSink : ICombatLogSink
        {
            public int Count { get; private set; }
            public void Write(CombatLogEntry entry) => Count++;
        }
    }
}
