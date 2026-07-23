using Combat.Core.Battle;

namespace Combat.Runtime.Display
{
    public readonly struct UnitSpawnViewSnapshot
    {
        public UnitSpawnViewSnapshot(UnitId unitId, TeamId teamId, string definitionId, BattleVector2 position)
            : this(unitId, teamId, definitionId, position, BattleVector2.Right)
        {
        }

        public UnitSpawnViewSnapshot(UnitId unitId, TeamId teamId, string definitionId, BattleVector2 position, BattleVector2 facing)
        {
            UnitId = unitId;
            TeamId = teamId;
            DefinitionId = definitionId;
            Position = position;
            Facing = facing.SqrMagnitude <= 0.00001f ? BattleVector2.Right : facing.Normalized;
        }

        public UnitId UnitId { get; }
        public TeamId TeamId { get; }
        public string DefinitionId { get; }
        public BattleVector2 Position { get; }
        public BattleVector2 Facing { get; }
    }
}
