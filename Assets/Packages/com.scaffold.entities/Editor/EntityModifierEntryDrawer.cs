using System.Collections.Generic;
using Scaffold.Entities;
using UnityEditor;
using UnityEngine;

namespace Scaffold.Entities.Editor
{
    [CustomPropertyDrawer(typeof(EntityModifierEntry))]
    public sealed class EntityModifierEntryDrawer : PropertyDrawer
    {
        private const float foldoutWidth = 15f;
        private const float soValueSplit = 0.58f;
        private const float soValueGap = 4f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            SerializedProperty modifierValue = property.FindPropertyRelative("modifierValue");
            var extraPaths = new List<string>();
            CollectExtraChildPropertyPaths(modifierValue, extraPaths);
            if (extraPaths.Count == 0 || modifierValue == null || !modifierValue.isExpanded)
            {
                return line;
            }

            float spacing = EditorGUIUtility.standardVerticalSpacing;
            return line + MeasureExpandedExtrasHeight(property.serializedObject, extraPaths, spacing);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            OnGuiForModifierEntry(position, property, label);
        }

        private void OnGuiForModifierEntry(Rect position, SerializedProperty property, GUIContent label)
        {
            GetModifierEntrySerializedState(property, out SerializedProperty keyProp, out SerializedProperty authoringProp, out SerializedProperty legacyProp, out SerializedProperty modifierValue, out SerializedProperty inlineValue, out List<string> extraPaths, out bool hasExtras);
            float singleLineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.BeginProperty(position, label, property);
            DrawHeaderRow(position, property, keyProp, authoringProp, legacyProp, modifierValue, inlineValue, hasExtras, singleLineHeight);
            RefreshExtraPathsAfterSerializeReferencePayloadChanged(property, "modifierValue", extraPaths, out SerializedProperty mv, out bool he);
            if (he && mv != null && mv.isExpanded)
            {
                DrawExpandedExtraFields(position, property.serializedObject, extraPaths, singleLineHeight, spacing);
            }

            EditorGUI.EndProperty();
        }

        private void RefreshExtraPathsAfterSerializeReferencePayloadChanged(SerializedProperty entryProperty, string payloadRelativeName, List<string> extraPaths, out SerializedProperty payload, out bool hasExtrasNow)
        {
            payload = entryProperty.FindPropertyRelative(payloadRelativeName);
            extraPaths.Clear();
            CollectExtraChildPropertyPaths(payload, extraPaths);
            hasExtrasNow = extraPaths.Count > 0;
        }

        private void GetModifierEntrySerializedState(SerializedProperty property, out SerializedProperty keyProp, out SerializedProperty authoringProp, out SerializedProperty legacyProp, out SerializedProperty modifierValue, out SerializedProperty inlineValue, out List<string> extraPaths, out bool hasExtras)
        {
            keyProp = property.FindPropertyRelative("key");
            authoringProp = property.FindPropertyRelative("variableAuthoring");
            legacyProp = property.FindPropertyRelative("variableLegacy");
            modifierValue = property.FindPropertyRelative("modifierValue");
            inlineValue = modifierValue != null ? modifierValue.FindPropertyRelative("value") : null;
            extraPaths = new List<string>();
            CollectExtraChildPropertyPaths(modifierValue, extraPaths);
            hasExtras = extraPaths.Count > 0;
        }

        private float MeasureExpandedExtrasHeight(SerializedObject so, List<string> extraPaths, float spacing)
        {
            float sum = 0f;
            for (int i = 0; i < extraPaths.Count; i++)
            {
                SerializedProperty extra = so.FindProperty(extraPaths[i]);
                if (extra == null)
                {
                    continue;
                }

                sum += spacing + EditorGUI.GetPropertyHeight(extra, true);
            }

            return sum;
        }

        private void DrawHeaderRow(Rect position, SerializedProperty entryProperty, SerializedProperty keyProp, SerializedProperty authoringProp, SerializedProperty legacyProp, SerializedProperty modifierValue, SerializedProperty inlineValue, bool hasExtras, float singleLineHeight)
        {
            float rowW = position.width;
            float x = position.x;
            float rowY = position.y;
            ApplyFoldoutStrip(hasExtras, modifierValue, ref x, ref rowW, rowY, singleLineHeight);
            DrawHeaderSoAndValue(x, rowY, rowW, entryProperty, keyProp, authoringProp, legacyProp, inlineValue, singleLineHeight);
        }

        private void DrawHeaderSoAndValue(float x, float rowY, float rowW, SerializedProperty entryProperty, SerializedProperty keyProp, SerializedProperty authoringProp, SerializedProperty legacyProp, SerializedProperty inlineValue, float singleLineHeight)
        {
            if (inlineValue == null)
            {
                DrawSoOnly(x, rowY, rowW, entryProperty, keyProp, authoringProp, legacyProp, singleLineHeight);
                return;
            }

            bool hasVariableSo = VariableKeySoField.ResolveSoForDisplay(authoringProp, legacyProp) != null;
            if (hasVariableSo)
            {
                DrawSoAndInlineValue(x, rowY, rowW, entryProperty, keyProp, authoringProp, legacyProp, singleLineHeight);
            }
            else
            {
                DrawSoAndReservedValueSlot(x, rowY, rowW, entryProperty, keyProp, authoringProp, legacyProp, singleLineHeight);
            }
        }

        private void ApplyFoldoutStrip(bool hasExtras, SerializedProperty modifierValue, ref float x, ref float rowW, float rowY, float singleLineHeight)
        {
            Rect stripRect = new Rect(x, rowY, foldoutWidth, singleLineHeight);
            if (hasExtras && modifierValue != null)
            {
                modifierValue.isExpanded = EditorGUI.Foldout(stripRect, modifierValue.isExpanded, GUIContent.none, true);
            }

            x += foldoutWidth;
            rowW -= foldoutWidth;
        }

        private void DrawSoAndInlineValue(float x, float rowY, float rowW, SerializedProperty entryProperty, SerializedProperty keyProp, SerializedProperty authoringProp, SerializedProperty legacyProp, float singleLineHeight)
        {
            float valueW = ComputeValueColumnWidth(rowW);
            float soW = rowW - valueW - soValueGap;
            Rect soRect = new Rect(x, rowY, soW, singleLineHeight);
            Rect valRect = new Rect(x + soW + soValueGap, rowY, valueW, singleLineHeight);
            bool appliedSoChanges = VariableKeySoField.DrawObjectField(soRect, entryProperty, authoringProp, keyProp, legacyProp, "modifierValue");
            SerializedProperty? valueNested = appliedSoChanges ? VariableKeySoField.ResolveNestedValueAfterSoApply(entryProperty, "modifierValue") : VariableKeySoField.FindNestedValueProperty(entryProperty, "modifierValue");
            if (valueNested != null)
            {
                EditorGUI.PropertyField(valRect, valueNested, GUIContent.none);
            }
        }

        private void DrawSoAndReservedValueSlot(float x, float rowY, float rowW, SerializedProperty entryProperty, SerializedProperty keyProp, SerializedProperty authoringProp, SerializedProperty legacyProp, float singleLineHeight)
        {
            float valueW = ComputeValueColumnWidth(rowW);
            float soW = rowW - valueW - soValueGap;
            Rect soRect = new Rect(x, rowY, soW, singleLineHeight);
            VariableKeySoField.DrawObjectField(soRect, entryProperty, authoringProp, keyProp, legacyProp, "modifierValue");
        }

        private void DrawSoOnly(float x, float rowY, float rowW, SerializedProperty entryProperty, SerializedProperty keyProp, SerializedProperty authoringProp, SerializedProperty legacyProp, float singleLineHeight)
        {
            Rect soRect = new Rect(x, rowY, rowW, singleLineHeight);
            VariableKeySoField.DrawObjectField(soRect, entryProperty, authoringProp, keyProp, legacyProp, "modifierValue");
        }

        private float ComputeValueColumnWidth(float rowW)
        {
            float valueW = rowW * (1f - soValueSplit) - soValueGap;
            if (valueW < 40f)
            {
                valueW = Mathf.Min(120f, rowW * 0.4f);
            }

            return valueW;
        }

        private void DrawExpandedExtraFields(Rect position, SerializedObject so, List<string> extraPaths, float singleLineHeight, float spacing)
        {
            float y = position.y + singleLineHeight + spacing;

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
            SerializedProperty extra = so.FindProperty(path);
            if (extra == null)
            {
                return;
            }

            float h = EditorGUI.GetPropertyHeight(extra, true);
            Rect extraRect = new Rect(position.x, y, position.width, h);
            EditorGUI.PropertyField(extraRect, extra, true);
            y += h + spacing;
        }

        private void CollectExtraChildPropertyPaths(SerializedProperty modifierValue, List<string> paths)
        {
            paths.Clear();
            if (modifierValue == null)
            {
                return;
            }

            AppendDirectChildPathsSkippingValue(modifierValue, paths);
        }

        private void AppendDirectChildPathsSkippingValue(SerializedProperty modifierValue, List<string> paths)
        {
            SerializedProperty end = modifierValue.GetEndProperty();
            SerializedProperty it = modifierValue.Copy();
            if (!it.Next(true))
            {
                return;
            }

            int baseDepth = modifierValue.depth;
            while (!SerializedProperty.EqualContents(it, end))
            {
                TryAddExtraPath(it, baseDepth, paths);
                if (!it.Next(false))
                {
                    break;
                }
            }
        }

        private void TryAddExtraPath(SerializedProperty it, int baseDepth, List<string> paths)
        {
            if (it.depth == baseDepth + 1 && it.name != "value")
            {
                paths.Add(it.propertyPath);
            }
        }
    }
}
