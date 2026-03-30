using Scaffold.Entities;
using UnityEditor;
using UnityEngine;

namespace Scaffold.Entities.Editor
{
    /// <summary>
    /// Compact list row: attribute asset and base value side by side without per-field labels.
    /// </summary>
    [CustomPropertyDrawer(typeof(EntityAttributeEntry))]
    public sealed class EntityAttributeEntryPropertyDrawer : PropertyDrawer
    {
        private const float gap = 4f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty attributeProp = property.FindPropertyRelative("attribute");
            SerializedProperty baseValueProp = property.FindPropertyRelative("baseValue");

            float line = EditorGUIUtility.singleLineHeight;
            float half = (position.width - gap) * 0.5f;

            var left = new Rect(position.x, position.y, half, line);
            var right = new Rect(position.x + half + gap, position.y, half, line);

            EditorGUI.PropertyField(left, attributeProp, GUIContent.none);
            EditorGUI.PropertyField(right, baseValueProp, GUIContent.none);
        }
    }
}
