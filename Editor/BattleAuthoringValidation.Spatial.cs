#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Combat.Core.Battle;
using Combat.Unity.Authoring;
using UnityEngine;

namespace Combat.Unity.Editor
{
    public static partial class BattleAuthoringValidator
    {
        public static BattleAuthoringValidationReport ValidateSpatialMap(
            BattleSpatialMapAsset spatialMap)
        {
            var report = new BattleAuthoringValidationReport();
            ValidateSpatialMaps(report, new[] { spatialMap });
            return report;
        }

        private static void ValidateSpatialMaps(
            BattleAuthoringValidationReport report,
            IReadOnlyList<BattleSpatialMapAsset> spatialMaps)
        {
            if (spatialMaps == null)
            {
                return;
            }

            for (var mapIndex = 0; mapIndex < spatialMaps.Count; mapIndex++)
            {
                BattleSpatialMapAsset spatialMap = spatialMaps[mapIndex];
                if (spatialMap == null)
                {
                    report.AddError(
                        null,
                        "spatialMaps[" + mapIndex + "]",
                        "spatialMaps[" + mapIndex + "] is missing a BattleSpatialMapAsset reference.");
                    continue;
                }

                ValidateSpatialMapEntries(report, spatialMap);
            }
        }

        private static void ValidateSpatialMapEntries(
            BattleAuthoringValidationReport report,
            BattleSpatialMapAsset spatialMap)
        {
            IReadOnlyList<BattleSpatialEntry> entries = spatialMap.Entries;
            if (entries == null)
            {
                report.AddError(spatialMap, "entries", "Spatial map entries list is required.");
                return;
            }

            var stableIds = new HashSet<int>();
            bool hasEntryErrors = false;
            for (var i = 0; i < entries.Count; i++)
            {
                BattleSpatialEntry entry = entries[i];
                string path = "entries[" + i + "]";
                if (entry.StableId <= 0)
                {
                    report.AddError(
                        spatialMap,
                        path + ".stableId",
                        "Spatial entry stableId must be positive.");
                    hasEntryErrors = true;
                }
                else if (!stableIds.Add(entry.StableId))
                {
                    report.AddError(
                        spatialMap,
                        path + ".stableId",
                        "Spatial entry stableId must be unique within the map.");
                    hasEntryErrors = true;
                }

                if (!Enum.IsDefined(typeof(BattleSpatialShape), entry.Shape))
                {
                    report.AddError(
                        spatialMap,
                        path + ".shape",
                        "Spatial entry shape is unsupported.");
                    hasEntryErrors = true;
                    continue;
                }

                if (!IsFinite(entry.Center.x) || !IsFinite(entry.Center.y))
                {
                    report.AddError(
                        spatialMap,
                        path + ".center",
                        "Spatial entry center must be finite.");
                    hasEntryErrors = true;
                    continue;
                }

                float maximumCoordinate =
                    BattleSpatialMapDefinition.MaxCoordinateMagnitude.ToFloat();
                if (Mathf.Abs(entry.Center.x) > maximumCoordinate
                    || Mathf.Abs(entry.Center.y) > maximumCoordinate)
                {
                    report.AddError(
                        spatialMap,
                        path + ".center",
                        "Spatial entry center is outside the supported deterministic domain.");
                    hasEntryErrors = true;
                }

                if (entry.Shape == BattleSpatialShape.Circle)
                {
                    hasEntryErrors |= ValidateCircleEntry(
                        report,
                        spatialMap,
                        entry,
                        path,
                        maximumCoordinate);
                }
                else
                {
                    hasEntryErrors |= ValidateAabbEntry(
                        report,
                        spatialMap,
                        entry,
                        path,
                        maximumCoordinate);
                }
            }

            if (hasEntryErrors)
            {
                return;
            }

            try
            {
                BattleAuthoringConverter.BuildSpatialMapDefinition(spatialMap);
            }
            catch (Exception exception)
            {
                report.AddError(
                    spatialMap,
                    "entries",
                    "Spatial map deterministic conversion failed: " + exception.Message);
            }
        }

        private static bool ValidateCircleEntry(
            BattleAuthoringValidationReport report,
            BattleSpatialMapAsset spatialMap,
            BattleSpatialEntry entry,
            string path,
            float maximumCoordinate)
        {
            float maximumExtent = BattleSpatialMapDefinition.MaxShapeExtent.ToFloat();
            if (!IsFinite(entry.Radius)
                || entry.Radius <= 0f
                || entry.Radius > maximumExtent)
            {
                report.AddError(
                    spatialMap,
                    path + ".radius",
                    "Circle radius must be finite, positive, and inside the supported domain.");
                return true;
            }

            if (Mathf.Abs(entry.Center.x) + entry.Radius > maximumCoordinate
                || Mathf.Abs(entry.Center.y) + entry.Radius > maximumCoordinate)
            {
                report.AddError(
                    spatialMap,
                    path + ".radius",
                    "Circle bounds extend outside the supported deterministic domain.");
                return true;
            }

            return false;
        }

        private static bool ValidateAabbEntry(
            BattleAuthoringValidationReport report,
            BattleSpatialMapAsset spatialMap,
            BattleSpatialEntry entry,
            string path,
            float maximumCoordinate)
        {
            float maximumExtent = BattleSpatialMapDefinition.MaxShapeExtent.ToFloat();
            if (!IsFinite(entry.Size.x)
                || !IsFinite(entry.Size.y)
                || entry.Size.x <= 0f
                || entry.Size.y <= 0f
                || entry.Size.x * 0.5f > maximumExtent
                || entry.Size.y * 0.5f > maximumExtent)
            {
                report.AddError(
                    spatialMap,
                    path + ".size",
                    "Aabb size must be finite, positive, and inside the supported domain.");
                return true;
            }

            if (Mathf.Abs(entry.Center.x) + entry.Size.x * 0.5f > maximumCoordinate
                || Mathf.Abs(entry.Center.y) + entry.Size.y * 0.5f > maximumCoordinate)
            {
                report.AddError(
                    spatialMap,
                    path + ".size",
                    "Aabb bounds extend outside the supported deterministic domain.");
                return true;
            }

            return false;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
#endif
