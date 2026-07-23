#if UNITY_EDITOR
using Combat.Core.Battle;
using Combat.Unity.Authoring;
using UnityEditor;
using UnityEngine;

namespace Combat.Unity.Editor
{
    public sealed class BattleSpatialMapEditorWindow : EditorWindow
    {
        private BattleSpatialMapAsset _spatialMap;
        private int _nextStableId = 1;
        private uint _categoryBits = uint.MaxValue;
        private uint _maskBits = uint.MaxValue;

        [MenuItem("Combat/Authoring/Spatial Map Editor")]
        public static void Open()
        {
            GetWindow<BattleSpatialMapEditorWindow>("Battle Spatial Map");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            _spatialMap = (BattleSpatialMapAsset)EditorGUILayout.ObjectField(
                "Spatial Map",
                _spatialMap,
                typeof(BattleSpatialMapAsset),
                allowSceneObjects: false);
            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }

            _nextStableId = EditorGUILayout.IntField("Next Stable ID", _nextStableId);
            _categoryBits = (uint)EditorGUILayout.LongField("Category Bits", _categoryBits);
            _maskBits = (uint)EditorGUILayout.LongField("Mask Bits", _maskBits);

            using (new EditorGUI.DisabledScope(_spatialMap == null))
            {
                if (GUILayout.Button("Import Selected Collider2D"))
                {
                    ImportSelectedCollider();
                }

                if (GUILayout.Button("Validate / Bake Preview"))
                {
                    ValidateAndPreview();
                }
            }

            EditorGUILayout.HelpBox(
                "Collider import is one-way. The spatial map asset becomes authoritative; runtime collision never reads Collider2D.",
                MessageType.Info);
        }

        private void ImportSelectedCollider()
        {
            Collider2D collider = Selection.activeGameObject == null
                ? null
                : Selection.activeGameObject.GetComponent<Collider2D>();
            if (!BattleSpatialMapColliderImporter.TryCreateEntry(
                    collider,
                    _nextStableId,
                    _categoryBits,
                    _maskBits,
                    out BattleSpatialEntry entry,
                    out string error))
            {
                Debug.LogError(error);
                return;
            }

            Undo.RecordObject(_spatialMap, "Import Spatial Collider");
            var serialized = new SerializedObject(_spatialMap);
            SerializedProperty entries = serialized.FindProperty("_entries");
            int index = entries.arraySize;
            entries.arraySize++;
            WriteEntry(entries.GetArrayElementAtIndex(index), entry);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_spatialMap);
            _nextStableId++;
            SceneView.RepaintAll();
        }

        private void ValidateAndPreview()
        {
            BattleAuthoringValidationReport report =
                BattleAuthoringValidator.ValidateSpatialMap(_spatialMap);
            if (report.HasErrors)
            {
                for (var i = 0; i < report.Issues.Count; i++)
                {
                    BattleAuthoringValidationIssue issue = report.Issues[i];
                    Debug.LogError(issue.PropertyPath + ": " + issue.Message, issue.Asset);
                }

                return;
            }

            BattleSpatialMapDefinition definition =
                BattleAuthoringConverter.BuildSpatialMapDefinition(_spatialMap);
            Debug.Log(
                "Spatial map deterministic preview succeeded with "
                + definition.Entries.Count
                + " entries.",
                _spatialMap);
            SceneView.RepaintAll();
        }

        private void OnSceneGui(SceneView sceneView)
        {
            if (_spatialMap == null)
            {
                return;
            }

            BattleSpatialMapDefinition definition;
            try
            {
                definition = BattleAuthoringConverter.BuildSpatialMapDefinition(_spatialMap);
            }
            catch
            {
                return;
            }

            var serialized = new SerializedObject(_spatialMap);
            serialized.Update();
            SerializedProperty entries = serialized.FindProperty("_entries");
            for (var i = 0; i < definition.Entries.Count; i++)
            {
                BattleSpatialEntryDefinition converted = definition.Entries[i];
                int serializedIndex = FindEntryIndex(entries, converted.StableId);
                if (serializedIndex < 0)
                {
                    continue;
                }

                EditEntryHandle(entries.GetArrayElementAtIndex(serializedIndex), converted);
            }

            serialized.ApplyModifiedProperties();
        }

        private void EditEntryHandle(
            SerializedProperty entry,
            BattleSpatialEntryDefinition converted)
        {
            SerializedProperty centerProperty = entry.FindPropertyRelative("_center");
            Vector2 center = new Vector2(
                converted.Center.XScalar.ToFloat(),
                converted.Center.YScalar.ToFloat());
            Vector3 position = new Vector3(center.x, center.y, 0f);

            Handles.color = converted.ShapeType == BattleSpatialShapeType.Circle
                ? new Color(0.2f, 0.8f, 1f, 1f)
                : new Color(1f, 0.7f, 0.2f, 1f);
            if (converted.ShapeType == BattleSpatialShapeType.Circle)
            {
                Handles.DrawWireDisc(position, Vector3.forward, converted.Radius.ToFloat());
            }
            else
            {
                Handles.DrawWireCube(
                    position,
                    new Vector3(
                        converted.Size.XScalar.ToFloat(),
                        converted.Size.YScalar.ToFloat(),
                        0f));
            }

            Handles.Label(position, "Spatial " + converted.StableId);
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(position, Quaternion.identity);
            float resizedRadius = converted.Radius.ToFloat();
            Vector2 resizedSize = new Vector2(
                converted.Size.XScalar.ToFloat(),
                converted.Size.YScalar.ToFloat());
            if (converted.ShapeType == BattleSpatialShapeType.Circle)
            {
                resizedRadius = Handles.RadiusHandle(
                    Quaternion.identity,
                    moved,
                    resizedRadius);
            }
            else
            {
                Vector3 scaled = Handles.ScaleHandle(
                    new Vector3(resizedSize.x, resizedSize.y, 1f),
                    moved,
                    Quaternion.identity,
                    HandleUtility.GetHandleSize(moved));
                resizedSize = new Vector2(
                    Mathf.Max(0.0001f, Mathf.Abs(scaled.x)),
                    Mathf.Max(0.0001f, Mathf.Abs(scaled.y)));
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_spatialMap, "Move Spatial Entry");
                centerProperty.vector2Value = new Vector2(moved.x, moved.y);
                if (converted.ShapeType == BattleSpatialShapeType.Circle)
                {
                    entry.FindPropertyRelative("_radius").floatValue =
                        Mathf.Max(0.0001f, resizedRadius);
                }
                else
                {
                    entry.FindPropertyRelative("_size").vector2Value = resizedSize;
                }

                EditorUtility.SetDirty(_spatialMap);
            }
        }

        private static int FindEntryIndex(SerializedProperty entries, int stableId)
        {
            for (var i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("_stableId").intValue == stableId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void WriteEntry(
            SerializedProperty destination,
            BattleSpatialEntry entry)
        {
            destination.FindPropertyRelative("_stableId").intValue = entry.StableId;
            destination.FindPropertyRelative("_shape").enumValueIndex = (int)entry.Shape;
            destination.FindPropertyRelative("_center").vector2Value = entry.Center;
            destination.FindPropertyRelative("_radius").floatValue = entry.Radius;
            destination.FindPropertyRelative("_size").vector2Value = entry.Size;
            destination.FindPropertyRelative("_categoryBits").longValue = entry.CategoryBits;
            destination.FindPropertyRelative("_maskBits").longValue = entry.MaskBits;
        }
    }
}
#endif
