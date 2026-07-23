using Combat.Core.Battle;
using Combat.Unity.Authoring;
using UnityEditor;
using UnityEngine;

namespace Combat.Unity.Editor
{
    [CustomPropertyDrawer(typeof(BattleEffectConfig))]
    internal sealed class BattleEffectConfigDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorGUIUtility.singleLineHeight * 2f) + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            SerializedProperty type = property.FindPropertyRelative("_type");
            Rect typeRect = new Rect(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(typeRect, type, label);

            SerializedProperty value = FindValueProperty(property, (BattleEffectType)type.enumValueIndex);
            Rect valueRect = new Rect(
                position.x,
                position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                EditorGUIUtility.singleLineHeight);
            EditorGUI.indentLevel++;
            EditorGUI.PropertyField(valueRect, value, ValueLabel((BattleEffectType)type.enumValueIndex));
            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        private static SerializedProperty FindValueProperty(
            SerializedProperty property,
            BattleEffectType type)
        {
            switch (type)
            {
                case BattleEffectType.Damage:
                case BattleEffectType.Heal:
                    return property.FindPropertyRelative("_amount");
                case BattleEffectType.ApplyStatus:
                    return property.FindPropertyRelative("_status");
                case BattleEffectType.SpawnProjectileEmitter:
                    return property.FindPropertyRelative("_projectileEmitter");
                case BattleEffectType.AreaEffect:
                    return property.FindPropertyRelative("_areaEffect");
                default:
                    return property.FindPropertyRelative("_amount");
            }
        }

        private static GUIContent ValueLabel(BattleEffectType type)
        {
            switch (type)
            {
                case BattleEffectType.Damage:
                case BattleEffectType.Heal:
                    return new GUIContent("Amount");
                case BattleEffectType.ApplyStatus:
                    return new GUIContent("Status");
                case BattleEffectType.SpawnProjectileEmitter:
                    return new GUIContent("Projectile Emitter");
                case BattleEffectType.AreaEffect:
                    return new GUIContent("Area Effect");
                default:
                    return GUIContent.none;
            }
        }
    }
}
