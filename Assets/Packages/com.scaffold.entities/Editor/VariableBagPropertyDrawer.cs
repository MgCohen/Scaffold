using Scaffold.Entities;
using UnityEditor;
using UnityEngine;

namespace Scaffold.Entities.Editor
{
    [CustomPropertyDrawer(typeof(VariableBag))]
    public sealed class VariableBagPropertyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty entries = property.FindPropertyRelative("entries");
            if (entries == null)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            return EditorGUI.GetPropertyHeight(entries, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty entries = property.FindPropertyRelative("entries");
            EditorGUI.BeginProperty(position, label, property);
            if (entries != null)
            {
                EditorGUI.PropertyField(position, entries, label, true);
            }

            EditorGUI.EndProperty();
        }
    }
}
