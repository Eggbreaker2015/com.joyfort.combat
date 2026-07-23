using System;

namespace Combat.Core.Battle
{
    public readonly struct InitialCombatantSpawn
    {
        public InitialCombatantSpawn(TeamId teamId, CombatantDefinition definition, BattleVector2 position)
        {
            TeamId = teamId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Position = position;
        }

        public TeamId TeamId { get; }
        public CombatantDefinition Definition { get; }
        public BattleVector2 Position { get; }
    }
}
