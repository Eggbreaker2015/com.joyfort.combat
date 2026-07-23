using System.Collections.Generic;
using Combat.Foundation.Diagnostics;
using NUnit.Framework;

namespace Combat.Tests.Foundation
{
    public sealed class CombatLoggerTests
    {
        [Test]
        public void DefaultVisibleSettings_HideExactTagRule()
        {
            var settings = new CombatLogSettings(
                isEnabled: true,
                defaultVisible: true,
                minimumLevel: CombatLogLevel.Trace,
                rules: new[]
                {
                    new CombatLogTagRule(CombatLogTags.View, CombatLogTagMatchMode.Exact, isVisible: false, minimumLevel: CombatLogLevel.Trace)
                });

            Assert.That(settings.ShouldLog(CombatLogLevel.Info, CombatLogTags.View), Is.False);
            Assert.That(settings.ShouldLog(CombatLogLevel.Info, CombatLogTags.Runtime), Is.True);
        }

        [Test]
        public void DefaultHiddenSettings_ShowWhitelistedTagAtRequiredLevel()
        {
            var settings = new CombatLogSettings(
                isEnabled: true,
                defaultVisible: false,
                minimumLevel: CombatLogLevel.Trace,
                rules: new[]
                {
                    new CombatLogTagRule(CombatLogTags.View, CombatLogTagMatchMode.Exact, isVisible: true, minimumLevel: CombatLogLevel.Warning)
                });

            Assert.That(settings.ShouldLog(CombatLogLevel.Info, CombatLogTags.View), Is.False);
            Assert.That(settings.ShouldLog(CombatLogLevel.Warning, CombatLogTags.View), Is.True);
            Assert.That(settings.ShouldLog(CombatLogLevel.Warning, CombatLogTags.Runtime), Is.False);
        }

        [Test]
        public void ExactTagRuleOverridesPrefixRule()
        {
            var settings = new CombatLogSettings(
                isEnabled: true,
                defaultVisible: false,
                minimumLevel: CombatLogLevel.Trace,
                rules: new[]
                {
                    new CombatLogTagRule("Combat.", CombatLogTagMatchMode.Prefix, isVisible: false, minimumLevel: CombatLogLevel.Trace),
                    new CombatLogTagRule(CombatLogTags.View, CombatLogTagMatchMode.Exact, isVisible: true, minimumLevel: CombatLogLevel.Info)
                });

            Assert.That(settings.ShouldLog(CombatLogLevel.Info, CombatLogTags.View), Is.True);
            Assert.That(settings.ShouldLog(CombatLogLevel.Info, CombatLogTags.Runtime), Is.False);
        }

        [Test]
        public void LongestPrefixRuleWins()
        {
            var settings = new CombatLogSettings(
                isEnabled: true,
                defaultVisible: false,
                minimumLevel: CombatLogLevel.Trace,
                rules: new[]
                {
                    new CombatLogTagRule("Combat.", CombatLogTagMatchMode.Prefix, isVisible: true, minimumLevel: CombatLogLevel.Trace),
                    new CombatLogTagRule("Combat.View", CombatLogTagMatchMode.Prefix, isVisible: false, minimumLevel: CombatLogLevel.Trace)
                });

            Assert.That(settings.ShouldLog(CombatLogLevel.Info, "Combat.Runtime.Tick"), Is.True);
            Assert.That(settings.ShouldLog(CombatLogLevel.Info, "Combat.View.Spawn"), Is.False);
        }

        [Test]
        public void LoggerDoesNotEvaluateMessageFactoryWhenFiltered()
        {
            var sink = new CapturingSink();
            var logger = new CombatLogger(CombatLogSettings.HideAll, sink);
            var evaluated = false;

            logger.Info(CombatLogTags.View, () =>
            {
                evaluated = true;
                return "Hidden message";
            });

            Assert.That(evaluated, Is.False);
            Assert.That(sink.Entries, Is.Empty);
        }

        [Test]
        public void LoggerWritesVisibleEntry()
        {
            var sink = new CapturingSink();
            var logger = new CombatLogger(CombatLogSettings.ShowInfoAndAbove, sink);

            logger.Info(CombatLogTags.View, () => "Battle finished");

            Assert.That(sink.Entries, Has.Count.EqualTo(1));
            Assert.That(sink.Entries[0].Level, Is.EqualTo(CombatLogLevel.Info));
            Assert.That(sink.Entries[0].Tag, Is.EqualTo(CombatLogTags.View));
            Assert.That(sink.Entries[0].Message, Is.EqualTo("Battle finished"));
        }

        private sealed class CapturingSink : ICombatLogSink
        {
            public List<CombatLogEntry> Entries { get; } = new List<CombatLogEntry>();

            public void Write(CombatLogEntry entry)
            {
                Entries.Add(entry);
            }
        }
    }
}
