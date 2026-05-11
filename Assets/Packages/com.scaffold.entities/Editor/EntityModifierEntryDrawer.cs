using System.Collections.Generic;
using Scaffold.Entities;
using UnityEditor;
using UnityEngine;

namespace Scaffold.Entities.Editor
{
    [CustomPropertyDrawer(typeof(EntityModifierEntry))]
    public sealed class EntityModifierEntryDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            SerializedProperty modifierProp = property.FindPropertyRelative("modifier");
            var extraPaths = new List<string>();
            EntityModifierEntryDrawerGui.CollectModifierExtraPaths(modifierProp, extraPaths);
            if (extraPaths.Count == 0 || modifierProp == null || !modifierProp.isExpanded)
            {
                return line;
            }

            float spacing = EditorGUIUtility.standardVerticalSpacing;
            return line + MeasureExpandedExtrasHeight(property.serializedObject, extraPaths, spacing);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            DrawHeaderAndPayloadRow(position, property);
            EditorGUI.EndProperty();
        }

        private void DrawHeaderAndPayloadRow(Rect position, SerializedProperty property)
        {
            SerializedProperty modifierProp = property.FindPropertyRelative("modifier");
            var extras = new List<string>();
            EntityModifierEntryDrawerGui.CollectModifierExtraPaths(modifierProp, extras);
            bool hasExtras = extras.Count > 0;
            float h = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            Rect inner = EntityModifierEntryDrawerGui.BuildInsetHeaderRow(position, modifierProp, hasExtras, h, out float x, out float rowW);
            DrawSoAndPickerRow(inner, position, property, ref modifierProp, x, rowW, h, spacing, hasExtras, extras);
        }

        private void DrawSoAndPickerRow(Rect inner, Rect fullPosition, SerializedProperty property, ref SerializedProperty? modifierProp, float x, float rowW, float h, float spacing, bool hasExtras, List<string> extras)
        {
            SerializedProperty keyProp = property.FindPropertyRelative("key");
            SerializedProperty authoringProp = property.FindPropertyRelative("variableAuthoring");
            SerializedProperty legacyProp = property.FindPropertyRelative("variableLegacy");
            EntityModifierEntryDrawerGui.LayoutHeaderRegions(x, rowW, inner.y, h, out Rect soRect, out Rect typeOrderRect);
            VariableKeySoField.DrawObjectField(soRect, property, authoringProp, keyProp, legacyProp, "modifier");
            property.serializedObject.Update();
            modifierProp = property.FindPropertyRelative("modifier");
            DrawModifierTypeRow(typeOrderRect, property, ref modifierProp, keyProp);
            DrawExpandedIfNeeded(fullPosition, property, modifierProp, extras, inner.y, h, spacing, hasExtras);
        }

        private void DrawModifierTypeRow(Rect rect, SerializedProperty entryProperty, ref SerializedProperty? modifierProp, SerializedProperty? keyProp)
        {
            if (modifierProp == null || keyProp == null)
            {
                return;
            }

            DrawModifierTypePicker(rect, entryProperty, ref modifierProp, keyProp);
        }

        private void DrawModifierTypePicker(Rect typeRect, SerializedProperty entryProperty, ref SerializedProperty? modifierProp, SerializedProperty keyProp)
        {
            IReadOnlyList<System.Type> types = EntityModifierEntryDrawerGui.BuildCompatibleModifierTypes(entryProperty);
            VariableModifier? current = modifierProp!.managedReferenceValue as VariableModifier;
            System.Type? currentType = current?.GetType();
            EditorGUI.BeginChangeCheck();
            EntityModifierEntryDrawerGui.DrawModifierTypePopup(typeRect, modifierProp, types, currentType);
            if (EditorGUI.EndChangeCheck())
            {
                entryProperty.serializedObject.ApplyModifiedProperties();
            }

            entryProperty.serializedObject.Update();
            modifierProp = entryProperty.FindPropertyRelative("modifier");
        }

        private void DrawExpandedIfNeeded(Rect fullPosition, SerializedProperty property, SerializedProperty? modifierProp, List<string> extras, float headerRowY, float lineHeight, float spacing, bool hasExtras)
        {
            if (!hasExtras || modifierProp == null || !modifierProp.isExpanded)
            {
                return;
            }

            DrawExpandedExtraFields(fullPosition, property.serializedObject, extras, headerRowY, lineHeight, spacing);
        }

        private float MeasureExpandedExtrasHeight(SerializedObject so, List<string> extraPaths, float spacing)
        {
            float sum = 0f;
            for (int i = 0; i < extraPaths.Count; i++)
            {
                SerializedProperty? extra = so.FindProperty(extraPaths[i]);
                if (extra == null)
                {
                    continue;
                }

                sum += spacing + EditorGUI.GetPropertyHeight(extra, true);
            }

            return sum;
        }

        private void DrawExpandedExtraFields(Rect position, SerializedObject so, List<string> extraPaths, float headerRowY, float lineHeight, float spacing)
        {
            float y = headerRowY + lineHeight + spacing;
            EditorGUI.indentLevel++;
            try
            {
                for (int i = 0; i < extraPaths.Count; i++)
                {
                    DrawOneExtraField(position, so, extraPaths[i], ref y, spacing);
                }
            }
            finally
            {
                EditorGUI.indentLevel--;
            }
        }

        private void DrawOneExtraField(Rect position, SerializedObject so, string path, ref float y, float spacing)
        {
            SerializedProperty? extra = so.FindProperty(path);
            if (extra == null)
            {
                return;
            }

            float ph = EditorGUI.GetPropertyHeight(extra, true);
            Rect extraRect = new Rect(position.x, y, position.width, ph);
            EditorGUI.PropertyField(extraRect, extra, true);
            y += ph + spacing;
        }
    }
}
