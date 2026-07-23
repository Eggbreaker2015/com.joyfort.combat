using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattlePerformanceRecorderTests
    {
        [Test]
        public void Step_WithPerformanceRecorder_RecordsStepAndSystemSamples()
        {
            var recorder = new BattlePerformanceRecorder();
            var simulation = new BattleSimulation(TestBattleConfigs.TwoUnitsInRange(), recorder);

            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(1, recorder.Steps.Count);
            BattlePerformanceStepSample step = recorder.Steps[0];
            Assert.AreEqual(new BattleTick(1), step.Tick);
            Assert.AreEqual(recorder.SystemSamples.Count, step.SystemSampleCount);
            Assert.Greater(step.SystemSampleCount, 0);
            Assert.GreaterOrEqual(step.ElapsedTicks, 0);
            Assert.GreaterOrEqual(step.GcAllocatedBytesDelta, 0);
            Assert.AreEqual(simulation.Events.Count, step.EventCount);
            Assert.AreEqual(2, step.UnitCount);

            AssertHasSystemSample(recorder, "StatusSystem");
            AssertHasSystemSample(recorder, "UnitActionExecutionSystem");
            AssertHasSystemSample(recorder, "MovementSystem");
            AssertHasSystemSample(recorder, "AbilitySystem");
        }

        [Test]
        public void Step_WithPerformanceRecorder_RecordsPhasePipelineOrder()
        {
            var recorder = new BattlePerformanceRecorder();
            var simulation = new BattleSimulation(TestBattleConfigs.TwoUnitsInRange(), recorder);

            simulation.Step(BattleInputFrame.Empty);

            string[] expectedNames =
            {
                "FlushSpawnCombatantCommands",
                "StatusSystem",
                "FlushEffectCommands.Status",
                "VictorySystem.Status",
                "ProjectileEmitterSystem",
                "FlushSpawnProjectileCommands",
                "ProjectileSystem",
                "FlushEffectCommands.Projectile",
                "VictorySystem.Projectile",
                "InputIntentSystem",
                "UnitActionExecutionSystem",
                "FlushEffectCommands.ActionRelease",
                "VictorySystem.ActionRelease",
                "TargetingSystem",
                "MovementSystem",
                "AiDecisionSystem",
                "AbilitySystem",
                "FlushActionCommands",
                "FlushEffectCommands.Action",
                "VictorySystem",
                "ApplyStructuralCommands"
            };

            Assert.AreEqual(expectedNames.Length, recorder.SystemSamples.Count);
            for (var i = 0; i < expectedNames.Length; i++)
            {
                Assert.AreEqual(expectedNames[i], recorder.SystemSamples[i].Name, $"Sample {i}");
            }
        }

        [Test]
        public void Step_WithPerformanceRecorder_RecordsEarlyVictoryBeforeBattleEndedEvent()
        {
            var recorder = new BattlePerformanceRecorder();
            var simulation = new BattleSimulation(TestBattleConfigs.SingleUnit(), recorder);

            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(5, recorder.SystemSamples.Count);
            Assert.AreEqual("VictorySystem.Status", recorder.SystemSamples[3].Name);
            Assert.AreEqual(0, recorder.SystemSamples[3].EventCount);
            Assert.AreEqual("ApplyStructuralCommands", recorder.SystemSamples[4].Name);
            Assert.AreEqual(1, recorder.SystemSamples[4].EventCount);
            Assert.AreEqual(BattleEventType.BattleEnded, simulation.Events[0].Type);
        }

        [Test]
        public void Step_WithoutPerformanceRecorder_KeepsAutomaticCombatBehavior()
        {
            var simulation = new BattleSimulation(TestBattleConfigs.TwoUnitsInRange());

            simulation.Step(BattleInputFrame.Empty);
            Assert.AreEqual(new BattleTick(1), simulation.CurrentTick);
            Assert.AreEqual(0, CountEvents(simulation, BattleEventType.DamageApplied));

            simulation.Step(BattleInputFrame.Empty);

            Assert.AreEqual(new BattleTick(2), simulation.CurrentTick);
            Assert.AreEqual(BattleEventType.DamageApplied, FindEvent(simulation, BattleEventType.DamageApplied).Type);
        }

        [Test]
        public void Clear_RemovesPreviousPerformanceSamples()
        {
            var recorder = new BattlePerformanceRecorder();
            var simulation = new BattleSimulation(TestBattleConfigs.TwoUnitsInRange(), recorder);
            simulation.Step(BattleInputFrame.Empty);

            recorder.Clear();

            Assert.AreEqual(0, recorder.Steps.Count);
            Assert.AreEqual(0, recorder.SystemSamples.Count);
        }

        private static void AssertHasSystemSample(BattlePerformanceRecorder recorder, string name)
        {
            for (var i = 0; i < recorder.SystemSamples.Count; i++)
            {
                if (recorder.SystemSamples[i].Name == name)
                {
                    Assert.GreaterOrEqual(recorder.SystemSamples[i].ElapsedTicks, 0);
                    Assert.GreaterOrEqual(recorder.SystemSamples[i].GcAllocatedBytesDelta, 0);
                    return;
                }
            }

            Assert.Fail($"No performance sample named {name}.");
        }

        private static BattleEvent FindEvent(BattleSimulation simulation, BattleEventType type)
        {
            for (var i = 0; i < simulation.Events.Count; i++)
            {
                if (simulation.Events[i].Type == type)
                {
                    return simulation.Events[i];
                }
            }

            Assert.Fail($"No {type} event.");
            return default;
        }

        private static int CountEvents(BattleSimulation simulation, BattleEventType type)
        {
            var count = 0;
            for (var i = 0; i < simulation.Events.Count; i++)
            {
                if (simulation.Events[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
