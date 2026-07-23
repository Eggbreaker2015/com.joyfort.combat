using System;
using Combat.Core.Battle;

namespace Combat.Core.LocalAvoidance
{
    internal readonly struct LocalAvoidanceSettings
    {
        public LocalAvoidanceSettings(
            int predictionTicks,
            int maxConflictResolutionPasses,
            BattleScalar cellSize,
            int softSpacingNumerator,
            int softSpacingDenominator,
            BattleScalar fullSpeedFriendlyOverlapTolerance,
            BattleScalar friendlyOverlapWeight,
            BattleScalar progressLossWeight,
            BattleScalar directionWeight,
            BattleScalar stepLossWeight,
            BattleScalar turnWeight)
        {
            if (predictionTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(predictionTicks));
            }

            if (maxConflictResolutionPasses <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxConflictResolutionPasses));
            }

            if (cellSize <= BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            }

            if (softSpacingNumerator < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(softSpacingNumerator));
            }

            if (softSpacingDenominator <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(softSpacingDenominator));
            }

            if (fullSpeedFriendlyOverlapTolerance < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fullSpeedFriendlyOverlapTolerance));
            }

            if (friendlyOverlapWeight < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(friendlyOverlapWeight));
            }

            if (progressLossWeight < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(progressLossWeight));
            }

            if (directionWeight < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(directionWeight));
            }

            if (stepLossWeight < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(stepLossWeight));
            }

            if (turnWeight < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(turnWeight));
            }

            PredictionTicks = predictionTicks;
            MaxConflictResolutionPasses = maxConflictResolutionPasses;
            CellSize = cellSize;
            SoftSpacingNumerator = softSpacingNumerator;
            SoftSpacingDenominator = softSpacingDenominator;
            FullSpeedFriendlyOverlapTolerance = fullSpeedFriendlyOverlapTolerance;
            FriendlyOverlapWeight = friendlyOverlapWeight;
            ProgressLossWeight = progressLossWeight;
            DirectionWeight = directionWeight;
            StepLossWeight = stepLossWeight;
            TurnWeight = turnWeight;
        }

        public static LocalAvoidanceSettings Default => new LocalAvoidanceSettings(
            predictionTicks: 4,
            maxConflictResolutionPasses: 3,
            cellSize: BattleScalar.FromInt(2),
            softSpacingNumerator: 3,
            softSpacingDenominator: 4,
            fullSpeedFriendlyOverlapTolerance:
                BattleScalar.One / BattleScalar.FromInt(8),
            friendlyOverlapWeight: BattleScalar.FromInt(16),
            progressLossWeight: BattleScalar.FromInt(8),
            directionWeight: BattleScalar.FromInt(4),
            stepLossWeight: BattleScalar.FromInt(2),
            turnWeight: BattleScalar.One);

        public int PredictionTicks { get; }
        public int MaxConflictResolutionPasses { get; }
        public BattleScalar CellSize { get; }
        public int SoftSpacingNumerator { get; }
        public int SoftSpacingDenominator { get; }
        public BattleScalar FullSpeedFriendlyOverlapTolerance { get; }
        public BattleScalar FriendlyOverlapWeight { get; }
        public BattleScalar ProgressLossWeight { get; }
        public BattleScalar DirectionWeight { get; }
        public BattleScalar StepLossWeight { get; }
        public BattleScalar TurnWeight { get; }
    }
}
