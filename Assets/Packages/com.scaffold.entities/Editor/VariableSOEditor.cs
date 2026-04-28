using Scaffold.Entities;
using UnityEditor;
using UnityEngine;

namespace Scaffold.Entities.Editor
{
    [CustomEditor(typeof(VariableSO))]
    public sealed class VariableSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            }

            SerializedProperty valueTypeProp = serializedObject.FindProperty("valueType");
            if (valueTypeProp != null)
            {
                EditorGUILayout.PropertyField(valueTypeProp);
            }

            SerializedProperty descriptionProp = serializedObject.FindProperty("description");
            if (descriptionProp != null)
            {
                EditorGUILayout.PropertyField(descriptionProp, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
