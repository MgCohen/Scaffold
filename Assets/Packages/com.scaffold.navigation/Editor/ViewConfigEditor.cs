using Scaffold.Navigation.Contracts;
using Scaffold.Schemas.Editor;
using UnityEditor;
using UnityEngine;

namespace Scaffold.Navigation.Editor
{
    [CustomEditor(typeof(ViewConfig), false)]
    public class ViewConfigEditor : SchemaObjectEditor
    {
        private static readonly string[] ExcludingViewFields =
        {
            "m_Script",
            "schemas",
            "viewAssetSource",
            "asset",
            "directPrefab"
        };

        protected override void DrawDefaultProperties()
        {
            SerializedProperty viewAssetSourceProp = serializedObject.FindProperty("viewAssetSource");
            bool isAddressables = viewAssetSourceProp.enumValueIndex == (int)ViewAssetSource.Addressables;
            SerializedProperty referenceProp = isAddressables
                ? serializedObject.FindProperty("asset")
                : serializedObject.FindProperty("directPrefab");

            Rect rowRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);

            Rect labelRect = new Rect(rowRect.x, rowRect.y, EditorGUIUtility.labelWidth, rowRect.height);
            EditorGUI.LabelField(labelRect, "View Asset");

            float fieldStart = rowRect.x + EditorGUIUtility.labelWidth;
            float remaining = rowRect.xMax - fieldStart;
            float enumWidth = remaining * 0.32f;

            Rect enumRect = new Rect(fieldStart, rowRect.y, enumWidth - 2, rowRect.height);
            EditorGUI.PropertyField(enumRect, viewAssetSourceProp, GUIContent.none);

            Rect refRect = new Rect(fieldStart + enumWidth, rowRect.y, remaining - enumWidth, rowRect.height);
            EditorGUI.PropertyField(refRect, referenceProp, GUIContent.none);

            DrawPropertiesExcluding(serializedObject, ExcludingViewFields);
        }
    }
}
