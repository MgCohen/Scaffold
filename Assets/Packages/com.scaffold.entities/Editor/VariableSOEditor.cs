using System;
using System.Linq;
using Scaffold.Entities;
using UnityEditor;
using UnityEngine;

namespace Scaffold.Entities.Editor
{
    [CustomEditor(typeof(VariableSO))]
    public sealed class VariableSOEditor : UnityEditor.Editor
    {
        private static Type[]? cachedTypes;
        private static GUIContent[]? cachedLabels;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDisabledScript();
            BuildTypeCachesIfNeeded();
            SerializedProperty? payloadProp = serializedObject.FindProperty("payloadTypeId");
            if (!TryDrawPayloadSection(payloadProp))
            {
                return;
            }

            DrawDescription();
            WarnUnknownPayload(payloadProp);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDisabledScript()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            }
        }

        private bool TryDrawPayloadSection(SerializedProperty? payloadProp)
        {
            if (payloadProp == null)
            {
                EditorGUILayout.HelpBox("Missing serialized field payloadTypeId.", MessageType.Error);
                serializedObject.ApplyModifiedProperties();
                return false;
            }

            if (cachedTypes == null || cachedTypes.Length == 0 || cachedLabels == null || cachedLabels.Length == 0)
            {
                EditorGUILayout.HelpBox("No concrete VariableValue types registered.", MessageType.Warning);
                serializedObject.ApplyModifiedProperties();
                return false;
            }

            DrawPayloadPopup(payloadProp);
            return true;
        }

        private void DrawPayloadPopup(SerializedProperty payloadProp)
        {
            int currentIndex = ResolvePopupIndex(payloadProp.stringValue);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Payload Type");
            int newIndex = EditorGUILayout.Popup(currentIndex, cachedLabels);
            EditorGUILayout.EndHorizontal();
            if (newIndex >= 0 && newIndex < cachedTypes!.Length &&
                VariableValueRegistry.TryGetId(cachedTypes[newIndex], out string newId))
            {
                payloadProp.stringValue = newId;
            }
        }

        private int ResolvePopupIndex(string currentId)
        {
            for (int i = 0; i < cachedTypes!.Length; i++)
            {
                if (VariableValueRegistry.TryGetId(cachedTypes[i], out string id) && id == currentId)
                {
                    return i;
                }
            }

            return 0;
        }

        private void DrawDescription()
        {
            SerializedProperty? descriptionProp = serializedObject.FindProperty("description");
            if (descriptionProp != null)
            {
                EditorGUILayout.PropertyField(descriptionProp, true);
            }
        }

        private void WarnUnknownPayload(SerializedProperty payloadProp)
        {
            if (!VariableValueRegistry.TryResolve(payloadProp.stringValue, out _))
            {
                EditorGUILayout.HelpBox($"Unknown payload type id '{payloadProp.stringValue}'.", MessageType.Error);
            }
        }

        private static void BuildTypeCachesIfNeeded()
        {
            if (cachedTypes != null)
            {
                return;
            }

            cachedTypes = VariableValueRegistry.AllConcreteTypes.Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition && VariableValueRegistry.Contains(t)).OrderBy(t => t.Name).ToArray();
            cachedLabels = cachedTypes.Select(t => new GUIContent(t.Name)).ToArray();
        }
    }
}
