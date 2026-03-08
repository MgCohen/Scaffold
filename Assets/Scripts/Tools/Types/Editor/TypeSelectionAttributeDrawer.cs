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
        private readonly Dictionary<Type, DerivedTypeDropdown> selectorCache = new Dictionary<Type, DerivedTypeDropdown>();

        private const float spacing = 2.0f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float lineSize = EditorGUIUtility.singleLineHeight;
            EditorGUI.BeginProperty(position, label, property);
            position = DrawBorder(position);
            DerivedTypeDropdown selector = GetOrCreateSelector(property);
            DrawSelector(position, property, selector, lineSize);
            EditorGUI.EndProperty();
        }

        private Rect DrawBorder(Rect position)
        {
            Rect expanded = ExpandRect(position);
            Color borderColor = new Color(0.28f, 0.28f, 0.28f);
            EditorGUI.DrawRect(expanded, borderColor);
            return position;
        }

        private Rect ExpandRect(Rect position)
        {
            position.x -= spacing;
            position.width += spacing * 2.0f;
            position.y -= spacing;
            position.height += spacing * 2.0f;
            return position;
        }

        private DerivedTypeDropdown GetOrCreateSelector(SerializedProperty property)
        {
            Type selectType = GetSelectType();
            DerivedTypeDropdown selector = EnsureSelectorExists(selectType);
            selector.RefreshSelection(property.managedReferenceValue?.GetType());
            return selector;
        }

        private Type GetSelectType()
        {
            TypeSelectionAttribute customAttribute = (TypeSelectionAttribute)attribute;
            return customAttribute.BaseType;
        }

        private DerivedTypeDropdown EnsureSelectorExists(Type selectType)
        {
            if (!selectorCache.TryGetValue(selectType, out DerivedTypeDropdown selector))
            {
                selector = new DerivedTypeDropdown(selectType);
                selectorCache.Add(selectType, selector);
            }
            return selector;
        }

        private void DrawSelector(Rect position, SerializedProperty property, DerivedTypeDropdown selector, float lineSize)
        {
            position.height = lineSize;
            if (selector.ChangeCheck(position))
            {
                ApplyNewSelection(property, selector);
                return;
            }
            position.y += lineSize;
            EditorGUI.PropertyField(position, property, true);
        }

        private void ApplyNewSelection(SerializedProperty property, DerivedTypeDropdown selector)
        {
            object newObj = selector.CreateInstance(property.managedReferenceValue);
            property.managedReferenceValue = newObj;
            property.serializedObject.ApplyModifiedProperties();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, true) + EditorGUIUtility.singleLineHeight;
        }
    }
}
