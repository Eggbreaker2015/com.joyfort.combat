using Combat.Core.Battle;

namespace Combat.Core.Spatial
{
    internal readonly struct SpatialHit
    {
        public SpatialHit(
            SpatialProxyId proxyId,
            int payloadIndex,
            SpatialSweepHit sweep)
        {
            ProxyId = proxyId;
            PayloadIndex = payloadIndex;
            Fraction = sweep.Fraction;
            Position = sweep.Position;
            Point = sweep.Point;
            Normal = sweep.Normal;
            StartedOverlapping = sweep.StartedOverlapping;
        }

        public SpatialProxyId ProxyId { get; }
        public int PayloadIndex { get; }
        public BattleScalar Fraction { get; }
        public BattleVector2 Position { get; }
        public BattleVector2 Point { get; }
        public BattleVector2 Normal { get; }
        public bool StartedOverlapping { get; }

        public static SpatialHit QueryResult(SpatialProxy proxy)
        {
            return new SpatialHit(
                proxy.Id,
                proxy.PayloadIndex,
                new SpatialSweepHit(
                    BattleScalar.Zero,
                    proxy.Position,
                    proxy.Position,
                    BattleVector2.Zero,
                    startedOverlapping: true));
        }
    }

    internal readonly struct SpatialSweepHit
    {
        public SpatialSweepHit(
            BattleScalar fraction,
            BattleVector2 position,
            BattleVector2 point,
            BattleVector2 normal,
            bool startedOverlapping)
        {
            Fraction = fraction;
            Position = position;
            Point = point;
            Normal = normal;
            StartedOverlapping = startedOverlapping;
        }

        public BattleScalar Fraction { get; }
        public BattleVector2 Position { get; }
        public BattleVector2 Point { get; }
        public BattleVector2 Normal { get; }
        public bool StartedOverlapping { get; }
    }
}
