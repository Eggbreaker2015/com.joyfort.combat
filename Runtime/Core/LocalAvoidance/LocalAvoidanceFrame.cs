using System;

namespace Combat.Core.LocalAvoidance
{
    internal readonly struct LocalAvoidanceFrame
    {
        private readonly LocalAvoidanceAgent[] _agents;

        public LocalAvoidanceFrame(
            LocalAvoidanceAgent[] agents,
            int agentCount,
            LocalAvoidanceSettings settings)
        {
            if (agents == null)
            {
                throw new ArgumentNullException(nameof(agents));
            }

            if (agentCount < 0 || agentCount > agents.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(agentCount));
            }

            // The Solver validates AgentId uniqueness after sorting its workspace copy.
            _agents = agents;
            AgentCount = agentCount;
            Settings = settings;
        }

        public int AgentCount { get; }
        public LocalAvoidanceSettings Settings { get; }

        public LocalAvoidanceAgent GetAgent(int index)
        {
            if (index < 0 || index >= AgentCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _agents[index];
        }
    }
}
