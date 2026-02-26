using System;
using System.Collections.Generic;
using System.Linq;
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
            position.x -= spacing;
            position.width += spacing * 2.0f;
            position.y -= spacing;
            position.height += spacing * 2.0f;

            EditorGUI.DrawRect(position, new Color(0.28f, 0.28f, 0.28f));
            position.x += spacing;
            position.y += spacing;

            position.width -= spacing * 2.0f;
            position.height -= spacing * 2.0f;

            var customAttribute = (TypeSelectionAttribute)attribute;

            var selectType = customAttribute.baseType;

            if (!selectorCache.TryGetValue(selectType, out DerivedTypeDropdown selector))
            {
                selector = new DerivedTypeDropdown(selectType);
                selectorCache.Add(selectType, selector);
            }

            selector.RefreshSelection(property.managedReferenceValue?.GetType());

            position.height = lineSize;

            if (selector.ChangeCheck(position))
            {
                var newObj = selector.CreateInstance(property.managedReferenceValue);
                property.managedReferenceValue = newObj;
                property.serializedObject.ApplyModifiedProperties();
            }
            else
            {
                position.y += lineSize;
                EditorGUI.PropertyField(position, property, true);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, true) + EditorGUIUtility.singleLineHeight;
        }
    }
}