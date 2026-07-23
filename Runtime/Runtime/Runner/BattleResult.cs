using Combat.Core.Battle;

namespace Combat.Runtime.Runner
{
    public readonly struct BattleResult
    {
        public BattleResult(bool isFinished, TeamId winningTeamId)
        {
            IsFinished = isFinished;
            WinningTeamId = winningTeamId;
        }

        public bool IsFinished { get; }
        public TeamId WinningTeamId { get; }
    }
}
