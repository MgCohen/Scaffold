using UnityEditor;
using UnityEngine;
using Scaffold.Entities;
using VariableSO = Scaffold.Entities.VariableSO;
using VRec = Scaffold.Entities.Variable;

namespace Scaffold.Entities.Editor
{
    internal static class VariableKeySoField
    {
        internal static VariableSO? ResolveSoForDisplay(SerializedProperty? variableAuthoringProp, SerializedProperty? legacyProperty)
        {
            VariableSO? authoring = TryGetSerializedAuthoring(variableAuthoringProp);
            if (authoring != null)
            {
                return authoring;
            }

            return legacyProperty?.objectReferenceValue as VariableSO;
        }

        private static VariableSO? TryGetSerializedAuthoring(SerializedProperty? variableAuthoringProp)
        {
            if (variableAuthoringProp == null ||
                variableAuthoringProp.propertyType != SerializedPropertyType.ObjectReference)
            {
                return null;
            }

            return variableAuthoringProp.objectReferenceValue as VariableSO;
        }

        internal static bool DrawObjectField(Rect soRect, SerializedProperty entryProperty, SerializedProperty? variableAuthoringProp, SerializedProperty keyProp, SerializedProperty legacyProp, string serializedValueRelativePath)
        {
            VariableSO? c = ResolveSoForDisplay(variableAuthoringProp, legacyProp);
            EditorGUI.BeginChangeCheck();
            VariableSO? n = (VariableSO)EditorGUI.ObjectField(soRect, GUIContent.none, c, typeof(VariableSO), false);
            if (!EditorGUI.EndChangeCheck())
            {
                return false;
            }

            WriteAuthoringSerializedIfPresent(variableAuthoringProp, n);
            AssignVariableSerializedFromSo(keyProp, legacyProp, n);
            RebaseManagedReferencePayloadForVariableSo(entryProperty, serializedValueRelativePath, n);
            entryProperty.serializedObject.ApplyModifiedProperties();
            return true;
        }

        internal static SerializedProperty? ResolveNestedValueAfterSoApply(SerializedProperty entryProperty, string serializeReferenceContainerRelativeName)
        {
            entryProperty.serializedObject.Update();
            SerializedProperty container = entryProperty.FindPropertyRelative(serializeReferenceContainerRelativeName);
            return container?.FindPropertyRelative("value");
        }

        internal static SerializedProperty? FindNestedValueProperty(SerializedProperty entryProperty, string serializeReferenceContainerRelativeName)
        {
            SerializedProperty container = entryProperty.FindPropertyRelative(serializeReferenceContainerRelativeName);
            return container?.FindPropertyRelative("value");
        }

        internal static void RebaseManagedReferencePayloadForVariableSo(SerializedProperty entryProperty, string valueRelativePath, VariableSO? selectedSo)
        {
            string expectedId = selectedSo == null ? "string" : selectedSo.PayloadTypeId;
            if (!VariableValueRegistry.TryResolve(expectedId, out System.Type expected))
            {
                Debug.LogWarning($"Unknown VariableSO payload id '{expectedId}'. Skipping managed-reference rebase.");
                return;
            }

            if (!TryGetManagedPayload(entryProperty, valueRelativePath, out SerializedProperty payload))
            {
                return;
            }

            ApplyPayloadDefault(payload, expected);
        }

        private static bool TryGetManagedPayload(SerializedProperty entryProperty, string valueRelativePath, out SerializedProperty payload)
        {
            payload = entryProperty.FindPropertyRelative(valueRelativePath);
            return payload != null && payload.propertyType == SerializedPropertyType.ManagedReference;
        }

        private static void ApplyPayloadDefault(SerializedProperty payload, System.Type expected)
        {
            VariableValue? current = payload.managedReferenceValue as VariableValue;
            if (current != null && current.GetType() == expected)
            {
                return;
            }

            payload.managedReferenceValue = VariableValueFactory.CreateDefault(expected);
        }

        private static void WriteAuthoringSerializedIfPresent(SerializedProperty? authoringProp, VariableSO? so)
        {
            if (authoringProp?.propertyType == SerializedPropertyType.ObjectReference)
            {
                authoringProp.objectReferenceValue = so;
            }
        }

        internal static void AssignVariableSerializedFromSo(SerializedProperty? keyProp, SerializedProperty? legacyProp, VariableSO? so)
        {
            if (keyProp == null)
            {
                return;
            }

            if (!AssignInlineVariableSerializable(keyProp, so))
            {
                WriteLegacyReference(legacyProp, so);
                return;
            }

            ClearLegacyReference(legacyProp);
        }

        internal static void ClearLegacyReference(SerializedProperty? legacyProp)
        {
            if (legacyProp?.objectReferenceValue != null)
            {
                legacyProp.objectReferenceValue = null;
            }
        }

        internal static void WriteLegacyReference(SerializedProperty? legacyProp, VariableSO? so)
        {
            if (legacyProp != null)
            {
                legacyProp.objectReferenceValue = so;
            }
        }

        internal static bool AssignInlineVariableSerializable(SerializedProperty keyRoot, VariableSO? so)
        {
            SerializedProperty? keyMember = keyRoot.FindPropertyRelative("key");
            if (keyMember == null || keyMember.propertyType != SerializedPropertyType.String)
            {
                return false;
            }

            SerializedProperty? payloadMember = keyRoot.FindPropertyRelative("payloadTypeId");
            if (payloadMember == null || payloadMember.propertyType != SerializedPropertyType.String)
            {
                return false;
            }

            return so == null
                ? ClearVariableMembers(keyMember, payloadMember)
                : FillVariableMembers(keyMember, payloadMember, so);
        }

        internal static bool ClearVariableMembers(SerializedProperty keyMember, SerializedProperty payloadMember)
        {
            keyMember.stringValue = "";
            payloadMember.stringValue = "string";
            return true;
        }

        internal static bool FillVariableMembers(SerializedProperty keyMember, SerializedProperty payloadMember, VariableSO so)
        {
            VRec v = so;
            keyMember.stringValue = v.Key;
            payloadMember.stringValue = v.PayloadTypeId;
            return true;
        }
    }
}
