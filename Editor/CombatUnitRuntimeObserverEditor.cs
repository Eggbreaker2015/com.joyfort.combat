using Combat.Unity.Display;
using UnityEditor;

namespace Combat.Unity.Editor
{
    [CustomEditor(typeof(CombatUnitRuntimeObserver))]
    internal sealed class CombatUnitRuntimeObserverEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
            {
                DrawSerializedFields();
            }
        }

        private void DrawSerializedFields()
        {
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script")
                {
                    continue;
                }

                EditorGUILayout.PropertyField(property, includeChildren: true);
            }
        }
    }
}
