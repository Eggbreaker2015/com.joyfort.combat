#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Combat.Unity.Authoring;
using UnityEditor;
using UnityEngine;

namespace Combat.Unity.Editor
{
    [CustomPropertyDrawer(typeof(BattleConditionOperandConfig), true)]
    public sealed class BattleConditionOperandConfigDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            BattleConditionManagedReferenceDrawer.Draw(position, property, label, typeof(BattleConditionOperandConfig));
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return BattleConditionManagedReferenceDrawer.GetHeight(property);
        }
    }

    [CustomPropertyDrawer(typeof(BattleStatusConditionFilterConfig), true)]
    public sealed class BattleStatusConditionFilterConfigDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            BattleConditionManagedReferenceDrawer.Draw(position, property, label, typeof(BattleStatusConditionFilterConfig));
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return BattleConditionManagedReferenceDrawer.GetHeight(property);
        }
    }

    internal static class BattleConditionManagedReferenceDrawer
    {
        private const float VerticalSpacing = 2f;
        private const string NoneDisplayName = "None";
        private static readonly Dictionary<Type, ManagedReferenceOption[]> OptionsByBaseType = new Dictionary<Type, ManagedReferenceOption[]>();

        public static void Draw(Rect position, SerializedProperty property, GUIContent label, Type baseType)
        {
            EditorGUI.BeginProperty(position, label, property);

            Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            float labelWidth = Math.Min(EditorGUIUtility.labelWidth, headerRect.width * 0.55f);
            Rect foldoutRect = new Rect(headerRect.x, headerRect.y, labelWidth, headerRect.height);
            Rect popupRect = new Rect(headerRect.x + labelWidth, headerRect.y, headerRect.width - labelWidth, headerRect.height);

            bool hasValue = property.managedReferenceValue != null;
            property.isExpanded = hasValue && EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            ManagedReferenceOption[] options = GetOptions(baseType);
            string[] displayNames = GetDisplayNames(options);
            int currentIndex = GetCurrentIndex(property, options);

            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUI.Popup(popupRect, currentIndex, displayNames);
            if (EditorGUI.EndChangeCheck())
            {
                property.managedReferenceValue = selectedIndex == 0
                    ? null
                    : Activator.CreateInstance(options[selectedIndex - 1].Type);
                property.isExpanded = selectedIndex != 0;
            }

            if (property.managedReferenceValue != null && property.isExpanded)
            {
                DrawChildren(position, property);
            }

            EditorGUI.EndProperty();
        }

        public static float GetHeight(SerializedProperty property)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property.managedReferenceValue == null || !property.isExpanded)
            {
                return height;
            }

            SerializedProperty iterator = property.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                height += VerticalSpacing + EditorGUI.GetPropertyHeight(iterator, true);
                enterChildren = false;
            }

            return height;
        }

        private static void DrawChildren(Rect position, SerializedProperty property)
        {
            float y = position.y + EditorGUIUtility.singleLineHeight + VerticalSpacing;
            SerializedProperty iterator = property.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            bool enterChildren = true;

            EditorGUI.indentLevel++;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                float childHeight = EditorGUI.GetPropertyHeight(iterator, true);
                var childRect = new Rect(position.x, y, position.width, childHeight);
                EditorGUI.PropertyField(childRect, iterator, true);
                y += childHeight + VerticalSpacing;
                enterChildren = false;
            }

            EditorGUI.indentLevel--;
        }

        private static int GetCurrentIndex(SerializedProperty property, ManagedReferenceOption[] options)
        {
            object value = property.managedReferenceValue;
            if (value == null)
            {
                return 0;
            }

            Type currentType = value.GetType();
            for (var i = 0; i < options.Length; i++)
            {
                if (options[i].Type == currentType)
                {
                    return i + 1;
                }
            }

            return 0;
        }

        private static ManagedReferenceOption[] GetOptions(Type baseType)
        {
            if (OptionsByBaseType.TryGetValue(baseType, out ManagedReferenceOption[] cached))
            {
                return cached;
            }

            var options = new List<ManagedReferenceOption>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom(baseType))
            {
                if (type.IsAbstract || type.IsGenericType || type.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }

                options.Add(new ManagedReferenceOption(type, BuildDisplayName(type, baseType)));
            }

            options.Sort((left, right) => string.CompareOrdinal(left.DisplayName, right.DisplayName));
            ManagedReferenceOption[] result = options.ToArray();
            OptionsByBaseType[baseType] = result;
            return result;
        }

        private static string[] GetDisplayNames(ManagedReferenceOption[] options)
        {
            var names = new string[options.Length + 1];
            names[0] = NoneDisplayName;
            for (var i = 0; i < options.Length; i++)
            {
                names[i + 1] = options[i].DisplayName;
            }

            return names;
        }

        private static string BuildDisplayName(Type type, Type baseType)
        {
            string name = type.Name;
            if (name.StartsWith("Battle", StringComparison.Ordinal))
            {
                name = name.Substring("Battle".Length);
            }

            string suffix = baseType == typeof(BattleConditionOperandConfig)
                ? "ConditionOperandConfig"
                : "ConditionFilterConfig";
            if (name.EndsWith(suffix, StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - suffix.Length);
            }

            return SplitPascalCase(name);
        }

        private static string SplitPascalCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var chars = new List<char>(value.Length + 8);
            chars.Add(value[0]);
            for (var i = 1; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsUpper(c) && !char.IsUpper(value[i - 1]))
                {
                    chars.Add(' ');
                }

                chars.Add(c);
            }

            return new string(chars.ToArray());
        }

        private readonly struct ManagedReferenceOption
        {
            public ManagedReferenceOption(Type type, string displayName)
            {
                Type = type;
                DisplayName = displayName;
            }

            public Type Type { get; }
            public string DisplayName { get; }
        }
    }
}
#endif
