using Scaffold.Entities;
using UnityEditor;
using UnityEngine;

namespace Scaffold.Entities.Editor
{
    [CustomEditor(typeof(EntityComponent), true)]
    public sealed class EntityComponentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            DrawReadOnlyDefinitionBagIfPresent();

            if (Application.isPlaying)
            {
                DrawEffectiveBagEditable();
            }
        }

        private void DrawReadOnlyDefinitionBagIfPresent()
        {
            SerializedProperty inst = serializedObject.FindProperty("instance");
            if (inst == null)
            {
                return;
            }

            SerializedProperty defProp = inst.FindPropertyRelative("definition");
            if (defProp == null || defProp.objectReferenceValue is not EntityDefinition def)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Definition variable bag (read-only)", EditorStyles.boldLabel);
            DrawBagFieldDisabled(def);
        }

        private void DrawBagFieldDisabled(EntityDefinition def)
        {
            using (SerializedObject defSo = new SerializedObject(def))
            {
                defSo.Update();
                SerializedProperty bagProp = defSo.FindProperty("bag");
                if (bagProp == null)
                {
                    return;
                }

                bool wasEnabled = GUI.enabled;
                GUI.enabled = false;
                EditorGUILayout.PropertyField(bagProp, new GUIContent("Shared variables"), true);
                GUI.enabled = wasEnabled;
                defSo.ApplyModifiedProperties();
            }
        }

        private void DrawEffectiveBagEditable()
        {
            serializedObject.Update();
            SerializedProperty inst = serializedObject.FindProperty("instance");
            if (inst == null)
            {
                return;
            }

            SerializedProperty effectiveBag = inst.FindPropertyRelative("instanceEffectiveBag");
            if (effectiveBag == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Effective Variables (debug — edits notify subscribers)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(effectiveBag, new GUIContent("Effective Bag"), true);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
