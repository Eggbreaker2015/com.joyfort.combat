using Combat.Core.Battle;
using Combat.Unity.Authoring;
using UnityEditor;

namespace Combat.Unity.Editor
{
    [CustomEditor(typeof(ProjectileConfigAsset))]
    internal sealed class ProjectileConfigAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_behavior"));
            SerializedProperty hitPolicy = serializedObject.FindProperty("_hitPolicyMode");
            EditorGUILayout.PropertyField(hitPolicy);
            if ((ProjectileHitPolicyMode)hitPolicy.enumValueIndex == ProjectileHitPolicyMode.Pierce)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxHitCount"));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_radius"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_speed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_lifetimeSeconds"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("_impactEffects"),
                includeChildren: true);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
