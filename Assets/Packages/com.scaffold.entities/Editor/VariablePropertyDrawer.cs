using System.Collections.Generic;
using Scaffold.Entities;
using UnityEditor;
using UnityEngine;

namespace Scaffold.Entities.Editor
{
    [CustomPropertyDrawer(typeof(VariableEntry))]
    public sealed class VariablePropertyDrawer : PropertyDrawer
    {
        private const float foldoutWidth = 15f;
        private const float soValueSplit = 0.58f;
        private const float soValueGap = 4f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            SerializedProperty baseValue = property.FindPropertyRelative("baseValue");
            var extraPaths = new List<string>();
            CollectExtraChildPropertyPaths(baseValue, extraPaths);
            if (extraPaths.Count == 0 || baseValue == null || !baseValue.isExpanded)
            {
                return line;
            }

            float spacing = EditorGUIUtility.standardVerticalSpacing;
            return line + MeasureExpandedExtrasHeight(property.serializedObject, extraPaths, spacing);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            OnGuiForVariableEntry(position, property, label);
        }

        private void OnGuiForVariableEntry(Rect position, SerializedProperty property, GUIContent label)
        {
            GetVariableEntrySerializedState(property, out SerializedProperty keyProp, out SerializedProperty authoringProp, out SerializedProperty legacyProp, out SerializedProperty baseValue, out SerializedProperty inlineValue, out List<string> extraPaths, out bool hasExtras);
            float singleLineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.BeginProperty(position, label, property);
            DrawHeaderRow(position, property, keyProp, authoringProp, legacyProp, baseValue, inlineValue, hasExtras, singleLineHeight);
            RefreshExtraPathsAfterSerializeReferencePayloadChanged(property, "baseValue", extraPaths, out SerializedProperty bv, out bool he);
            if (he && bv != null && bv.isExpanded)
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

        private void GetVariableEntrySerializedState(SerializedProperty property, out SerializedProperty keyProp, out SerializedProperty authoringProp, out SerializedProperty legacyProp, out SerializedProperty baseValue, out SerializedProperty inlineValue, out List<string> extraPaths, out bool hasExtras)
        {
            keyProp = property.FindPropertyRelative("key");
            authoringProp = property.FindPropertyRelative("variableAuthoring");
            legacyProp = property.FindPropertyRelative("variableLegacy");
            baseValue = property.FindPropertyRelative("baseValue");
            inlineValue = baseValue != null ? baseValue.FindPropertyRelative("value") : null;
            extraPaths = new List<string>();
            CollectExtraChildPropertyPaths(baseValue, extraPaths);
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

        private void DrawHeaderRow(Rect position, SerializedProperty entryProperty, SerializedProperty keyProp, SerializedProperty authoringProp, SerializedProperty legacyProp, SerializedProperty baseValue, SerializedProperty inlineValue, bool hasExtras, float singleLineHeight)
        {
            float rowW = position.width;
            float x = position.x;
            float rowY = position.y;
            ApplyFoldoutStrip(hasExtras, baseValue, ref x, ref rowW, rowY, singleLineHeight);
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

        private void ApplyFoldoutStrip(bool hasExtras, SerializedProperty baseValue, ref float x, ref float rowW, float rowY, float singleLineHeight)
        {
            Rect stripRect = new Rect(x, rowY, foldoutWidth, singleLineHeight);
            if (hasExtras && baseValue != null)
            {
                baseValue.isExpanded = EditorGUI.Foldout(stripRect, baseValue.isExpanded, GUIContent.none, true);
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
            bool appliedSoChanges = VariableKeySoField.DrawObjectField(soRect, entryProperty, authoringProp, keyProp, legacyProp, "baseValue");
            SerializedProperty? valueNested = appliedSoChanges ? VariableKeySoField.ResolveNestedValueAfterSoApply(entryProperty, "baseValue") : VariableKeySoField.FindNestedValueProperty(entryProperty, "baseValue");
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
            VariableKeySoField.DrawObjectField(soRect, entryProperty, authoringProp, keyProp, legacyProp, "baseValue");
        }

        private void DrawSoOnly(float x, float rowY, float rowW, SerializedProperty entryProperty, SerializedProperty keyProp, SerializedProperty authoringProp, SerializedProperty legacyProp, float singleLineHeight)
        {
            Rect soRect = new Rect(x, rowY, rowW, singleLineHeight);
            VariableKeySoField.DrawObjectField(soRect, entryProperty, authoringProp, keyProp, legacyProp, "baseValue");
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

        private void CollectExtraChildPropertyPaths(SerializedProperty baseValue, List<string> paths)
        {
            paths.Clear();
            if (baseValue == null)
            {
                return;
            }

            AppendDirectChildPathsSkippingValue(baseValue, paths);
        }

        private void AppendDirectChildPathsSkippingValue(SerializedProperty baseValue, List<string> paths)
        {
            SerializedProperty end = baseValue.GetEndProperty();
            SerializedProperty it = baseValue.Copy();
            if (!it.Next(true))
            {
                return;
            }

            int baseDepth = baseValue.depth;
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
