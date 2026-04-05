using System;
using UnityEditor;
using UnityEngine;

namespace Scaffold.Schemas.Editor
{
    public static class SchemaLayout
    {
        internal static Color HeaderNormalColor
        {
            get
            {
                float shade = EditorGUIUtility.isProSkin ? 70f / 255f : 215f / 255f;
                return new Color(shade, shade, shade, 1);
            }
        }

        internal static Color HeaderHoverColor
        {
            get
            {
                float shade = EditorGUIUtility.isProSkin ? 80f / 255f : 230f / 255f;
                return new Color(shade, shade, shade, 1);
            }
        }

        public static void Divider(float spaceBefore = 5, float spaceAfter = 5)
        {
            if (spaceBefore != 0)
            {
                EditorGUILayout.Space(spaceBefore);
            }

            EditorGUI.BeginDisabledGroup(true);
            Rect rect = EditorGUILayout.GetControlRect(false, 1.5f);
            rect.width *= 3;
            rect.x -= rect.width / 3;
            GUI.Box(rect, GUIContent.none, SchemaStyles.Divider);
            EditorGUI.EndDisabledGroup();

            if (spaceAfter != 0)
            {
                EditorGUILayout.Space(spaceAfter);
            }
        }

        public static void Header(SchemaDrawer drawer, Action onHeaderClicked, Action onButtonClicked)
        {
            Rect rect = BuildHeaderRect();
            bool isHover = rect.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(rect, isHover ? HeaderHoverColor : HeaderNormalColor);
            rect.x += 30f;

            if (RemoveButton(rect))
            {
                onButtonClicked?.Invoke();
            }

            if (GUI.Button(rect, GUIContent.none, SchemaStyles.HeaderLabel))
            {
                onHeaderClicked?.Invoke();
            }

            GUIContent content = new GUIContent(drawer.SchemaName, drawer.SchemaDescription);
            EditorGUI.LabelField(rect, content);
        }

        private static Rect BuildHeaderRect()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            rect.x -= 30f;
            rect.width = EditorGUIUtility.currentViewWidth + 60f;
            return rect;
        }

        private static bool RemoveButton(Rect rect)
        {
            float buttonWidth = 25f;
            Rect buttonRect = new Rect(rect);
            buttonRect.width = EditorGUIUtility.currentViewWidth;
            buttonRect.x = buttonRect.width - buttonWidth - 5f;
            buttonRect.width = buttonWidth;
            GUIContent content = EditorGUIUtility.IconContent("close");
            return GUI.Button(buttonRect, content, SchemaStyles.CornerIcon);
        }
    }
}
