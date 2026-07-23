using System;
using Combat.Core.Battle;

namespace Combat.Core.Spatial
{
    internal static class SpatialGeometry
    {
        public static bool CirclesOverlap(
            BattleVector2 firstPosition,
            BattleScalar firstRadius,
            BattleVector2 secondPosition,
            BattleScalar secondRadius)
        {
            ValidateRadius(firstRadius, nameof(firstRadius));
            ValidateRadius(secondRadius, nameof(secondRadius));
            SpatialDomain.ValidatePosition(firstPosition, nameof(firstPosition));
            SpatialDomain.ValidatePosition(secondPosition, nameof(secondPosition));

            BattleScalar combinedRadius = firstRadius + secondRadius;
            return BattleVector2.SqrDistanceScalar(firstPosition, secondPosition)
                <= combinedRadius * combinedRadius;
        }

        public static bool TrySweepCircleAgainstCircle(
            BattleVector2 start,
            BattleVector2 delta,
            BattleScalar movingRadius,
            BattleVector2 targetPosition,
            BattleScalar targetRadius,
            out SpatialSweepHit hit)
        {
            ValidateRadius(movingRadius, nameof(movingRadius));
            ValidateRadius(targetRadius, nameof(targetRadius));
            SpatialDomain.ValidatePosition(start, nameof(start));
            SpatialDomain.ValidatePosition(targetPosition, nameof(targetPosition));
            SpatialDomain.ValidateStep(delta, nameof(delta));

            BattleVector2 relativeStart = start - targetPosition;
            BattleScalar combinedRadius = movingRadius + targetRadius;
            BattleScalar c = SpatialMath.Dot(relativeStart, relativeStart)
                - combinedRadius * combinedRadius;
            if (c <= BattleScalar.Zero)
            {
                bool startedOverlapping = c < BattleScalar.Zero;
                BattleVector2 normal = ResolveNormal(relativeStart, delta);
                hit = CreateHit(
                    BattleScalar.Zero,
                    start,
                    movingRadius,
                    normal,
                    startedOverlapping);
                return true;
            }

            BattleScalar lengthSquared = SpatialMath.Dot(delta, delta);
            if (lengthSquared <= BattleScalar.Epsilon)
            {
                hit = default;
                return false;
            }

            BattleScalar length = BattleScalar.Sqrt(lengthSquared);
            BattleVector2 direction = delta / length;
            BattleScalar projection = -SpatialMath.Dot(relativeStart, direction);
            if (projection < BattleScalar.Zero)
            {
                hit = default;
                return false;
            }

            BattleScalar radiusSquared = combinedRadius * combinedRadius;
            BattleScalar closestDistanceSquared = SpatialMath.Dot(relativeStart, relativeStart)
                - projection * projection;
            if (closestDistanceSquared < BattleScalar.Zero)
            {
                closestDistanceSquared = BattleScalar.Zero;
            }

            if (closestDistanceSquared > radiusSquared)
            {
                hit = default;
                return false;
            }

            BattleScalar contactOffsetSquared = radiusSquared - closestDistanceSquared;
            if (contactOffsetSquared < BattleScalar.Zero)
            {
                contactOffsetSquared = BattleScalar.Zero;
            }

            BattleScalar contactDistance = projection - BattleScalar.Sqrt(contactOffsetSquared);
            if (contactDistance < BattleScalar.Zero)
            {
                contactDistance = BattleScalar.Zero;
            }

            if (contactDistance > length)
            {
                hit = default;
                return false;
            }

            BattleScalar fraction = contactDistance / length;
            BattleVector2 position = start + delta * fraction;
            BattleVector2 normalAtHit = ResolveNormal(position - targetPosition, delta);
            hit = CreateHit(
                fraction,
                position,
                movingRadius,
                normalAtHit,
                startedOverlapping: false);
            return true;
        }

        public static bool CircleOverlapsAabb(
            BattleVector2 circlePosition,
            BattleScalar circleRadius,
            SpatialAabb bounds)
        {
            ValidateRadius(circleRadius, nameof(circleRadius));
            SpatialDomain.ValidatePosition(circlePosition, nameof(circlePosition));

            BattleScalar closestX = SpatialMath.Clamp(
                circlePosition.XScalar,
                bounds.Minimum.XScalar,
                bounds.Maximum.XScalar);
            BattleScalar closestY = SpatialMath.Clamp(
                circlePosition.YScalar,
                bounds.Minimum.YScalar,
                bounds.Maximum.YScalar);
            var closest = new BattleVector2(closestX, closestY);
            return BattleVector2.SqrDistanceScalar(circlePosition, closest)
                <= circleRadius * circleRadius;
        }

        private static SpatialSweepHit CreateHit(
            BattleScalar fraction,
            BattleVector2 position,
            BattleScalar movingRadius,
            BattleVector2 normal,
            bool startedOverlapping)
        {
            BattleVector2 point = position - normal * movingRadius;
            return new SpatialSweepHit(
                fraction,
                position,
                point,
                normal,
                startedOverlapping);
        }

        private static BattleVector2 ResolveNormal(
            BattleVector2 relativePosition,
            BattleVector2 delta)
        {
            if (relativePosition.SqrMagnitudeScalar > BattleScalar.Epsilon)
            {
                return relativePosition.Normalized;
            }

            if (delta.SqrMagnitudeScalar > BattleScalar.Epsilon)
            {
                return (delta * -BattleScalar.One).Normalized;
            }

            return BattleVector2.Right;
        }

        private static void ValidateRadius(BattleScalar radius, string parameterName)
        {
            SpatialDomain.ValidateShapeExtent(radius, parameterName);
        }
    }
}
