using System;
using Combat.Core.Battle;

namespace Combat.Runtime.Runner
{
    public static class HeadlessBattleRunner
    {
        public static BattleResult RunToEnd(BattleInstance instance, int maxTicks)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (maxTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTicks));
            }

            while (!instance.Simulation.IsFinished
                && instance.Simulation.CurrentTick.Value < maxTicks
                && instance.Simulation.CurrentTick.Value < instance.Simulation.MaxTicks)
            {
                instance.TickOnce(BattleInputFrame.Empty);
            }

            return instance.Result;
        }
    }
}
