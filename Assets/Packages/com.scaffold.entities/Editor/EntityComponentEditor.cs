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
            if (defProp == null || defProp.objectReferenceValue is not EntityDefinitionAsset def)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Definition variable bag (read-only)", EditorStyles.boldLabel);
            DrawBagFieldDisabled(def);
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

        private void DrawBagFieldDisabled(EntityDefinitionAsset def)
        {
            using SerializedObject defSo = new SerializedObject(def);
            defSo.Update();
            SerializedProperty bagProp = FindDefinitionBagProperty(defSo);
            if (bagProp == null)
            {
                return;
            }

            RenderReadOnlyBagField(defSo, bagProp);
        }

        private SerializedProperty? FindDefinitionBagProperty(SerializedObject defAssetSo)
        {
            SerializedProperty definitionProp = defAssetSo.FindProperty("definition");
            return definitionProp == null ? null : definitionProp.FindPropertyRelative("bag");
        }

        private void RenderReadOnlyBagField(SerializedObject defSo, SerializedProperty bagProp)
        {
            bool wasEnabled = GUI.enabled;
            GUI.enabled = false;
            EditorGUILayout.PropertyField(bagProp, new GUIContent("Shared variables"), true);
            GUI.enabled = wasEnabled;
            defSo.ApplyModifiedProperties();
        }
    }
}
