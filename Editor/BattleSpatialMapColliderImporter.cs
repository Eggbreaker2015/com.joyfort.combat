#if UNITY_EDITOR
using Combat.Unity.Authoring;
using UnityEngine;

namespace Combat.Unity.Editor
{
    public static class BattleSpatialMapColliderImporter
    {
        private const float AxisTolerance = 0.0001f;

        public static bool TryCreateEntry(
            Collider2D collider,
            int stableId,
            uint categoryBits,
            uint maskBits,
            out BattleSpatialEntry entry,
            out string error)
        {
            entry = default;
            error = string.Empty;
            if (collider == null)
            {
                error = "A Collider2D selection is required.";
                return false;
            }

            if (stableId <= 0)
            {
                error = "stableId must be positive.";
                return false;
            }

            if (collider is CircleCollider2D circle)
            {
                return TryCreateCircle(
                    circle,
                    stableId,
                    categoryBits,
                    maskBits,
                    out entry,
                    out error);
            }

            if (collider is BoxCollider2D box)
            {
                return TryCreateAabb(
                    box,
                    stableId,
                    categoryBits,
                    maskBits,
                    out entry,
                    out error);
            }

            error = collider.GetType().Name
                + " is unsupported. Use CircleCollider2D or an axis-aligned BoxCollider2D.";
            return false;
        }

        private static bool TryCreateCircle(
            CircleCollider2D collider,
            int stableId,
            uint categoryBits,
            uint maskBits,
            out BattleSpatialEntry entry,
            out string error)
        {
            Vector3 axisX = collider.transform.TransformVector(Vector3.right);
            Vector3 axisY = collider.transform.TransformVector(Vector3.up);
            float scaleX = new Vector2(axisX.x, axisX.y).magnitude;
            float scaleY = new Vector2(axisY.x, axisY.y).magnitude;
            if (scaleX <= AxisTolerance
                || scaleY <= AxisTolerance
                || Mathf.Abs(scaleX - scaleY) > AxisTolerance
                || Mathf.Abs(axisX.z) > AxisTolerance
                || Mathf.Abs(axisY.z) > AxisTolerance
                || Mathf.Abs(Vector2.Dot(
                    new Vector2(axisX.x, axisX.y).normalized,
                    new Vector2(axisY.x, axisY.y).normalized)) > AxisTolerance)
            {
                entry = default;
                error = "CircleCollider2D requires a finite, uniform world scale.";
                return false;
            }

            Vector3 worldCenter = collider.transform.TransformPoint(collider.offset);
            float radius = collider.radius * scaleX;
            entry = new BattleSpatialEntry(
                stableId,
                BattleSpatialShape.Circle,
                new Vector2(worldCenter.x, worldCenter.y),
                radius,
                Vector2.zero,
                categoryBits,
                maskBits);
            error = string.Empty;
            return true;
        }

        private static bool TryCreateAabb(
            BoxCollider2D collider,
            int stableId,
            uint categoryBits,
            uint maskBits,
            out BattleSpatialEntry entry,
            out string error)
        {
            Vector3 worldAxisX = collider.transform.TransformVector(
                new Vector3(collider.size.x, 0f, 0f));
            Vector3 worldAxisY = collider.transform.TransformVector(
                new Vector3(0f, collider.size.y, 0f));
            if (!AreWorldAxesAligned(worldAxisX, worldAxisY))
            {
                entry = default;
                error = "BoxCollider2D must remain axis-aligned in world space.";
                return false;
            }

            Vector3 worldCenter = collider.transform.TransformPoint(collider.offset);
            var worldSize = new Vector2(
                Mathf.Abs(worldAxisX.x) + Mathf.Abs(worldAxisY.x),
                Mathf.Abs(worldAxisX.y) + Mathf.Abs(worldAxisY.y));
            if (worldSize.x <= AxisTolerance || worldSize.y <= AxisTolerance)
            {
                entry = default;
                error = "BoxCollider2D requires a finite, non-zero world size.";
                return false;
            }

            entry = new BattleSpatialEntry(
                stableId,
                BattleSpatialShape.Aabb,
                new Vector2(worldCenter.x, worldCenter.y),
                0f,
                worldSize,
                categoryBits,
                maskBits);
            error = string.Empty;
            return true;
        }

        private static bool AreWorldAxesAligned(Vector3 axisX, Vector3 axisY)
        {
            if (Mathf.Abs(axisX.z) > AxisTolerance
                || Mathf.Abs(axisY.z) > AxisTolerance)
            {
                return false;
            }

            bool firstHorizontal = Mathf.Abs(axisX.y) <= AxisTolerance
                && Mathf.Abs(axisX.x) > AxisTolerance;
            bool firstVertical = Mathf.Abs(axisX.x) <= AxisTolerance
                && Mathf.Abs(axisX.y) > AxisTolerance;
            bool secondHorizontal = Mathf.Abs(axisY.y) <= AxisTolerance
                && Mathf.Abs(axisY.x) > AxisTolerance;
            bool secondVertical = Mathf.Abs(axisY.x) <= AxisTolerance
                && Mathf.Abs(axisY.y) > AxisTolerance;
            return (firstHorizontal && secondVertical)
                || (firstVertical && secondHorizontal);
        }
    }
}
#endif
