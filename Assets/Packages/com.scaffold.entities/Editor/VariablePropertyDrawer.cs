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
            SerializedProperty variableProp = property.FindPropertyRelative("variable");
            SerializedProperty baseValue = property.FindPropertyRelative("baseValue");
            SerializedProperty inlineValue = baseValue != null ? baseValue.FindPropertyRelative("value") : null;

            var extraPaths = new List<string>();
            CollectExtraChildPropertyPaths(baseValue, extraPaths);
            bool hasExtras = extraPaths.Count > 0;

            float singleLineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            EditorGUI.BeginProperty(position, label, property);
            DrawHeaderRow(position, variableProp, baseValue, inlineValue, hasExtras, singleLineHeight);
            if (hasExtras && baseValue != null && baseValue.isExpanded)
            {
                DrawExpandedExtraFields(position, property.serializedObject, extraPaths, singleLineHeight, spacing);
            }

            EditorGUI.EndProperty();
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

        private void DrawHeaderRow(Rect position, SerializedProperty variableProp, SerializedProperty baseValue, SerializedProperty inlineValue, bool hasExtras, float singleLineHeight)
        {
            float rowY = position.y;
            float rowW = position.width;
            float x = position.x;
            ApplyFoldoutStrip(hasExtras, baseValue, ref x, ref rowW, rowY, singleLineHeight);
            DrawHeaderSoAndValue(x, rowY, rowW, variableProp, inlineValue, singleLineHeight);
        }

        private void DrawHeaderSoAndValue(float x, float rowY, float rowW, SerializedProperty variableProp, SerializedProperty inlineValue, float singleLineHeight)
        {
            if (inlineValue == null)
            {
                DrawSoOnly(x, rowY, rowW, variableProp, singleLineHeight);
                return;
            }

            bool hasVariableSo = variableProp.objectReferenceValue != null;
            if (hasVariableSo)
            {
                DrawSoAndInlineValue(x, rowY, rowW, variableProp, inlineValue, singleLineHeight);
            }
            else
            {
                DrawSoAndReservedValueSlot(x, rowY, rowW, variableProp, singleLineHeight);
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

        private void DrawSoAndInlineValue(float x, float rowY, float rowW, SerializedProperty variableProp, SerializedProperty inlineValue, float singleLineHeight)
        {
            float valueW = ComputeValueColumnWidth(rowW);
            float soW = rowW - valueW - soValueGap;
            Rect soRect = new Rect(x, rowY, soW, singleLineHeight);
            Rect valRect = new Rect(x + soW + soValueGap, rowY, valueW, singleLineHeight);

            EditorGUI.PropertyField(soRect, variableProp, GUIContent.none);
            EditorGUI.PropertyField(valRect, inlineValue, GUIContent.none);
        }

        private void DrawSoAndReservedValueSlot(float x, float rowY, float rowW, SerializedProperty variableProp, float singleLineHeight)
        {
            float valueW = ComputeValueColumnWidth(rowW);
            float soW = rowW - valueW - soValueGap;
            Rect soRect = new Rect(x, rowY, soW, singleLineHeight);
            EditorGUI.PropertyField(soRect, variableProp, GUIContent.none);
        }

        private void DrawSoOnly(float x, float rowY, float rowW, SerializedProperty variableProp, float singleLineHeight)
        {
            Rect soRect = new Rect(x, rowY, rowW, singleLineHeight);
            EditorGUI.PropertyField(soRect, variableProp, GUIContent.none);
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
