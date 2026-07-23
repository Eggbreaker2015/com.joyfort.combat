using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Combat.Core.Battle
{
    public readonly struct ProjectileCullingBounds
    {
        public ProjectileCullingBounds(BattleVector2 center, BattleVector2 size, float padding)
            : this(center, size, BattleScalar.FromFloat(padding))
        {
        }

        public ProjectileCullingBounds(BattleVector2 center, BattleVector2 size, BattleScalar padding)
        {
            if (size.XScalar <= BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "Projectile culling width must be positive.");
            }

            if (size.YScalar <= BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "Projectile culling height must be positive.");
            }

            if (padding < BattleScalar.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(padding), "Projectile culling padding must be non-negative.");
            }

            IsEnabled = true;
            Center = center;
            Size = size;
            Padding = padding;
        }

        public bool IsEnabled { get; }
        public BattleVector2 Center { get; }
        public BattleVector2 Size { get; }
        public BattleScalar Padding { get; }

        public bool ShouldCull(BattleVector2 position)
        {
            if (!IsEnabled)
            {
                return false;
            }

            BattleScalar halfWidth = Size.XScalar * BattleScalar.FromFloat(0.5f) + Padding;
            BattleScalar halfHeight = Size.YScalar * BattleScalar.FromFloat(0.5f) + Padding;
            return position.XScalar < Center.XScalar - halfWidth
                || position.XScalar > Center.XScalar + halfWidth
                || position.YScalar < Center.YScalar - halfHeight
                || position.YScalar > Center.YScalar + halfHeight;
        }
    }

    public sealed class BattleConfig
    {
        public BattleConfig(int ticksPerSecond, int maxTicks, IReadOnlyList<InitialCombatantSpawn> initialSpawns)
            : this(ticksPerSecond, maxTicks, initialSpawns, default, automaticVictoryEnabled: true)
        {
        }

        public BattleConfig(
            int ticksPerSecond,
            int maxTicks,
            IReadOnlyList<InitialCombatantSpawn> initialSpawns,
            ProjectileCullingBounds projectileCullingBounds)
            : this(ticksPerSecond, maxTicks, initialSpawns, projectileCullingBounds, automaticVictoryEnabled: true)
        {
        }

        public BattleConfig(
            int ticksPerSecond,
            int maxTicks,
            IReadOnlyList<InitialCombatantSpawn> initialSpawns,
            ProjectileCullingBounds projectileCullingBounds,
            bool automaticVictoryEnabled)
            : this(
                ticksPerSecond,
                maxTicks,
                initialSpawns,
                projectileCullingBounds,
                automaticVictoryEnabled,
                BattleSpatialMapDefinition.Empty)
        {
        }

        public BattleConfig(
            int ticksPerSecond,
            int maxTicks,
            IReadOnlyList<InitialCombatantSpawn> initialSpawns,
            ProjectileCullingBounds projectileCullingBounds,
            bool automaticVictoryEnabled,
            BattleSpatialMapDefinition spatialMap)
            : this(
                ticksPerSecond,
                maxTicks,
                initialSpawns,
                projectileCullingBounds,
                automaticVictoryEnabled,
                localAvoidanceEnabled: false,
                spatialMap)
        {
        }

        public BattleConfig(
            int ticksPerSecond,
            int maxTicks,
            IReadOnlyList<InitialCombatantSpawn> initialSpawns,
            ProjectileCullingBounds projectileCullingBounds,
            bool automaticVictoryEnabled,
            bool localAvoidanceEnabled,
            BattleSpatialMapDefinition spatialMap)
        {
            TicksPerSecond = ticksPerSecond > 0 ? ticksPerSecond : throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            MaxTicks = maxTicks > 0 ? maxTicks : throw new ArgumentOutOfRangeException(nameof(maxTicks));
            if (initialSpawns == null)
            {
                throw new ArgumentNullException(nameof(initialSpawns));
            }

            if (initialSpawns.Count <= 0)
            {
                throw new ArgumentException("At least one initial spawn entry is required.", nameof(initialSpawns));
            }

            var copiedInitialSpawns = new List<InitialCombatantSpawn>(initialSpawns.Count);
            for (var i = 0; i < initialSpawns.Count; i++)
            {
                InitialCombatantSpawn spawn = initialSpawns[i];
                if (spawn.Definition == null)
                {
                    throw new ArgumentException("Initial spawn entries must have a combatant definition.", nameof(initialSpawns));
                }

                copiedInitialSpawns.Add(spawn);
            }

            InitialSpawns = new ReadOnlyCollection<InitialCombatantSpawn>(copiedInitialSpawns);
            ProjectileCullingBounds = projectileCullingBounds;
            AutomaticVictoryEnabled = automaticVictoryEnabled;
            LocalAvoidanceEnabled = localAvoidanceEnabled;
            SpatialMap = spatialMap ?? throw new ArgumentNullException(nameof(spatialMap));
        }

        public int TicksPerSecond { get; }
        public int MaxTicks { get; }
        public ReadOnlyCollection<InitialCombatantSpawn> InitialSpawns { get; }
        public ProjectileCullingBounds ProjectileCullingBounds { get; }
        public bool AutomaticVictoryEnabled { get; }
        public bool LocalAvoidanceEnabled { get; }
        public BattleSpatialMapDefinition SpatialMap { get; }
        public float SecondsPerTick => 1f / TicksPerSecond;
    }
}
