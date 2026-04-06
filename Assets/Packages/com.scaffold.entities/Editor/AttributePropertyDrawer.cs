using Scaffold.Entities;
using UnityEditor;
using UnityEngine;

namespace Scaffold.Entities.Editor
{
    /// <summary>
    /// Compact editor for <see cref="Attribute"/> (payload + optional match key).
    /// </summary>
    [CustomPropertyDrawer(typeof(Attribute))]
    public sealed class AttributePropertyDrawer : PropertyDrawer
    {
        private const float gap = 4f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty payloadProp = property.FindPropertyRelative("payload");
            SerializedProperty matchKeyProp = property.FindPropertyRelative("matchKey");

            float line = EditorGUIUtility.singleLineHeight;
            Rect row = EditorGUI.PrefixLabel(position, label);
            float half = (row.width - gap) * 0.5f;
            var payloadRect = new Rect(row.x, row.y, half, line);
            var matchRect = new Rect(row.x + half + gap, row.y, half, line);

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.PropertyField(payloadRect, payloadProp, GUIContent.none);
            EditorGUI.PropertyField(matchRect, matchKeyProp, new GUIContent("Key"));
            EditorGUI.EndProperty();
        }
    }
}
