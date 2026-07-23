using System;
using Combat.Core.Battle;

namespace Combat.Core.LocalAvoidance
{
    internal enum LocalAvoidanceMobility
    {
        Moving,
        Anchored
    }

    internal readonly struct LocalAvoidanceAgent
    {
        public LocalAvoidanceAgent(
            int agentId,
            int groupId,
            BattleVector2 position,
            BattleVector2 heading,
            BattleVector2 preferredStep,
            BattleScalar radius,
            BattleScalar maxStepDistance,
            LocalAvoidanceMobility mobility,
            bool stopsAtPreferredStep = false)
        {
            if (agentId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(agentId));
            }

            if (radius < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            if (maxStepDistance < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maxStepDistance));
            }

            AgentId = agentId;
            GroupId = groupId;
            Position = position;
            Heading = heading.SqrMagnitudeScalar <= BattleScalar.Epsilon
                ? BattleVector2.Right
                : heading.Normalized;
            PreferredStep = preferredStep;
            Radius = radius;
            MaxStepDistance = maxStepDistance;
            Mobility = mobility;
            StopsAtPreferredStep = stopsAtPreferredStep;
        }

        public int AgentId { get; }
        public int GroupId { get; }
        public BattleVector2 Position { get; }
        public BattleVector2 Heading { get; }
        public BattleVector2 PreferredStep { get; }
        public BattleScalar Radius { get; }
        public BattleScalar MaxStepDistance { get; }
        public LocalAvoidanceMobility Mobility { get; }
        public bool StopsAtPreferredStep { get; }
    }
}
