using System;
using System.Collections.Generic;
using Scaffold.Entities;
using UnityEditor;
using UnityEngine;
using Variable = Scaffold.Variables.Variable;

namespace Scaffold.Entities.Editor
{
    internal static class EntityModifierEntryDrawerGui
    {
        private const float foldoutWidth = 15f;

        internal static Rect BuildInsetHeaderRow(Rect position, SerializedProperty? modifierProp, bool hasExtras, float lineHeight, out float x, out float rowW)
        {
            x = position.x;
            rowW = position.width;
            float rowY = position.y;
            if (hasExtras && modifierProp != null)
            {
                DrawFoldoutStrip(ref x, ref rowW, rowY, lineHeight, modifierProp);
            }

            return new Rect(x, rowY, rowW, lineHeight);
        }

        private static void DrawFoldoutStrip(ref float x, ref float rowW, float rowY, float h, SerializedProperty modifierProp)
        {
            Rect foldRect = new Rect(x, rowY, foldoutWidth, h);
            modifierProp.isExpanded = EditorGUI.Foldout(foldRect, modifierProp.isExpanded, GUIContent.none, true);
            x += foldoutWidth;
            rowW -= foldoutWidth;
        }

        internal static void LayoutHeaderRegions(float x, float rowW, float rowY, float h, out Rect soRect, out Rect typeOrderRect)
        {
            float typeAndOrderW = rowW * 0.42f;
            float soW = rowW - typeAndOrderW - 4f;
            soRect = new Rect(x, rowY, soW, h);
            typeOrderRect = new Rect(x + soW + 4f, rowY, typeAndOrderW, h);
        }

        internal static IReadOnlyList<Type> BuildCompatibleModifierTypes(SerializedProperty entryProperty)
        {
            SerializedProperty? keyRoot = entryProperty.FindPropertyRelative("key");
            SerializedProperty? payloadMember = entryProperty.FindPropertyRelative("payloadTypeId");
            if (keyRoot == null || payloadMember == null)
            {
                return Array.Empty<Type>();
            }

            SerializedProperty? idMember = keyRoot.FindPropertyRelative("id");
            if (idMember == null || string.IsNullOrEmpty(idMember.stringValue))
            {
                return Array.Empty<Type>();
            }

            string payloadTypeId = payloadMember.stringValue;
            if (!VariablePayloadTypeHelpers.TryResolvePayload(payloadTypeId, idMember.stringValue, nameof(EntityModifierEntryDrawer), out Type wrapperType))
            {
                return Array.Empty<Type>();
            }

            Type? valueType = VariablePayloadTypeHelpers.ExtractValueType(wrapperType);
            return valueType == null ? Array.Empty<Type>() : ModifierTypeIndex.ModifiersFor(valueType);
        }

        internal static void DrawModifierTypePopup(Rect rect, SerializedProperty modifierProp, IReadOnlyList<Type> types, Type? currentType)
        {
            string[] labels = BuildPopupLabels(types);
            int selectedIndex = ComputeSelectedIndex(types, currentType);
            int newIndex = EditorGUI.Popup(rect, selectedIndex, labels);
            ApplyPopupSelection(modifierProp, types, selectedIndex, newIndex);
        }

        private static string[] BuildPopupLabels(IReadOnlyList<Type> types)
        {
            var labels = new string[types.Count + 1];
            labels[0] = "(none)";
            for (int i = 0; i < types.Count; i++)
            {
                labels[i + 1] = types[i].Name;
            }

            return labels;
        }

        private static int ComputeSelectedIndex(IReadOnlyList<Type> types, Type? currentType)
        {
            if (currentType == null)
            {
                return 0;
            }

            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] == currentType)
                {
                    return i + 1;
                }
            }

            return 0;
        }

        private static void ApplyPopupSelection(SerializedProperty modifierProp, IReadOnlyList<Type> types, int selectedIndex, int newIndex)
        {
            if (newIndex == selectedIndex)
            {
                return;
            }

            if (newIndex == 0)
            {
                modifierProp.managedReferenceValue = null;
            }
            else
            {
                Type pick = types[newIndex - 1];
                modifierProp.managedReferenceValue = Activator.CreateInstance(pick);
            }

            modifierProp.serializedObject.ApplyModifiedProperties();
        }

        internal static void CollectModifierExtraPaths(SerializedProperty? modifierProp, List<string> paths)
        {
            paths.Clear();
            if (modifierProp == null)
            {
                return;
            }

            AppendModifierDirectChildPaths(modifierProp, paths);
        }

        private static void AppendModifierDirectChildPaths(SerializedProperty modifierProp, List<string> paths)
        {
            SerializedProperty? end = modifierProp.GetEndProperty();
            SerializedProperty? it = modifierProp.Copy();
            if (!it.Next(true))
            {
                return;
            }

            int baseDepth = modifierProp.depth;
            while (!SerializedProperty.EqualContents(it, end))
            {
                TryAppendPath(it, baseDepth, paths);
                if (!it.Next(false))
                {
                    break;
                }
            }
        }

        private static void TryAppendPath(SerializedProperty it, int baseDepth, List<string> paths)
        {
            if (it.depth == baseDepth + 1)
            {
                paths.Add(it.propertyPath);
            }
        }
    }
}
