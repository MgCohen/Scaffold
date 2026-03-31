using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using Scaffold.Types;

namespace Scaffold.Types.Editor
{
    [CustomPropertyDrawer(typeof(TypeSelectionAttribute))]
    public class TypeSelectionAttributeDrawer : PropertyDrawer
    {
        private const float spacing = 2.0f;
        private readonly Dictionary<Type, DerivedTypeDropdown> selectorCache = new Dictionary<Type, DerivedTypeDropdown>();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float lineSize = EditorGUIUtility.singleLineHeight; EditorGUI.BeginProperty(position, label, property);
            Rect expanded = position; expanded.x -= spacing; expanded.width += spacing * 2.0f; expanded.y -= spacing; expanded.height += spacing * 2.0f;
            Color borderColor = new Color(0.28f, 0.28f, 0.28f); EditorGUI.DrawRect(expanded, borderColor);
            Type selectType = ((TypeSelectionAttribute)attribute).BaseType;
            if (!selectorCache.TryGetValue(selectType, out DerivedTypeDropdown selector))
            {
                selector = new DerivedTypeDropdown(selectType);
                selectorCache.Add(selectType, selector);
            }
            selector.RefreshSelection(property.managedReferenceValue?.GetType());
            DrawSelector(position, property, selector, lineSize);
            EditorGUI.EndProperty();
        }

        private void DrawSelector(Rect position, SerializedProperty property, DerivedTypeDropdown selector, float lineSize)
        {
            position.height = lineSize;
            if (selector.ChangeCheck(position))
            {
                property.managedReferenceValue = selector.CreateInstance(property.managedReferenceValue);
                property.serializedObject.ApplyModifiedProperties();
                return;
            }

            position.y += lineSize;
            EditorGUI.PropertyField(position, property, true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, true) + EditorGUIUtility.singleLineHeight;
        }
    }
}


