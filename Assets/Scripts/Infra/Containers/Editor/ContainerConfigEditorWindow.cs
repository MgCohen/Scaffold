using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Scaffold.Containers.Editor
{
    public class ContainerConfigEditorWindow : EditorWindow
    {
        private const float DividerWidth = 4f;

        private ContainerConfig targetAsset;
        private SerializedObject serializedObject;
        private SerializedProperty configsProp;
        private int selectedIndex = -1;
        private Vector2 leftScrollPosition;
        private Vector2 rightScrollPosition;
        private List<Type> configTypes;

        [MenuItem("Window/Scaffold/Container Config")]
        public static void Open()
        {
            var window = GetWindow<ContainerConfigEditorWindow>("Container Config");
            if (Selection.activeObject is ContainerConfig config)
                window.SetTarget(config);
        }

        [MenuItem("Assets/Open in Container Config Window", true)]
        private static bool ValidateOpenInWindow()
        {
            return Selection.activeObject is ContainerConfig;
        }

        [MenuItem("Assets/Open in Container Config Window", false)]
        private static void OpenInWindow()
        {
            Open();
        }

        private void OnEnable()
        {
            configTypes = GetConfigTypes();
            OnSelectionChange();
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is ContainerConfig config && config != targetAsset)
                SetTarget(config);
            Repaint();
        }

        private void SetTarget(ContainerConfig config)
        {
            targetAsset = config;
            serializedObject = config != null ? new SerializedObject(config) : null;
            configsProp = serializedObject?.FindProperty("configs");
            if (configsProp != null && (selectedIndex < 0 || selectedIndex >= configsProp.arraySize))
                selectedIndex = configsProp.arraySize > 0 ? 0 : -1;
            else if (configsProp == null)
                selectedIndex = -1;
        }

        private static List<Type> GetConfigTypes()
        {
            return TypeCache.GetTypesDerivedFrom<IConfig>()
                .Where(t => !t.IsAbstract && !t.IsInterface && !t.IsGenericTypeDefinition)
                .OrderBy(t => t.Name)
                .ToList();
        }

        private void OnGUI()
        {
            if (targetAsset == null || configsProp == null)
            {
                EditorGUILayout.HelpBox("Select a ContainerConfig asset to edit.", MessageType.Info);
                return;
            }

            serializedObject.Update();

            float leftColumnWidth = position.width / 3f;

            EditorGUILayout.BeginHorizontal();

            DrawLeftColumn(leftColumnWidth);
            DrawDivider();
            DrawRightColumn();

            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawLeftColumn(float width)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(width), GUILayout.ExpandHeight(true));

            if (GUILayout.Button("+", GUILayout.Height(24)))
                ShowAddConfigMenu();

            leftScrollPosition = EditorGUILayout.BeginScrollView(leftScrollPosition);

            for (int i = 0; i < configsProp.arraySize; i++)
            {
                var element = configsProp.GetArrayElementAtIndex(i);
                var value = element.managedReferenceValue;
                var label = value != null ? value.GetType().Name : $"Element {i}";
                if (value == null)
                    label = "(Missing)";

                bool isSelected = i == selectedIndex;
                if (isSelected)
                    GUI.backgroundColor = new Color(0.5f, 0.7f, 1f);

                if (GUILayout.Button(label, GUILayout.Height(22)))
                {
                    selectedIndex = i;
                    Repaint();
                }

                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDivider()
        {
            Rect rect = EditorGUILayout.BeginVertical(GUILayout.Width(DividerWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
            {
                float lineWidth = 1f;
                Rect lineRect = new Rect(rect.x + (rect.width - lineWidth) * 0.5f, rect.y, lineWidth, rect.height);
                EditorGUI.DrawRect(lineRect, new Color(0.35f, 0.35f, 0.35f, 0.8f));
            }
        }

        private void ShowAddConfigMenu()
        {
            var menu = new GenericMenu();
            foreach (var type in configTypes)
            {
                var t = type;
                menu.AddItem(new GUIContent(type.Name), false, () => AddConfigOfType(t));
            }
            if (configTypes.Count == 0)
                menu.AddDisabledItem(new GUIContent("No IConfig types found"));
            menu.ShowAsContext();
        }

        private void AddConfigOfType(Type type)
        {
            if (targetAsset == null || configsProp == null) return;

            serializedObject.Update();

            IConfig toAdd = CreateConfigInstance(type);
            if (toAdd == null) return;

            configsProp.arraySize++;
            var newElement = configsProp.GetArrayElementAtIndex(configsProp.arraySize - 1);
            newElement.managedReferenceValue = toAdd;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetAsset);
            selectedIndex = configsProp.arraySize - 1;
            Repaint();
        }

        private IConfig CreateConfigInstance(Type type)
        {
            object instance;
            if (typeof(ScriptableObject).IsAssignableFrom(type))
            {
                instance = ScriptableObject.CreateInstance(type);
                AssetDatabase.AddObjectToAsset((UnityEngine.Object)instance, targetAsset);
                EditorUtility.SetDirty(targetAsset);
            }
            else
            {
                try
                {
                    instance = Activator.CreateInstance(type);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    return null;
                }
            }

            if (instance is not IConfig)
                return null;

            if (instance is UnityEngine.Object)
            {
                var wrapperType = typeof(ConfigWrapper<>).MakeGenericType(type);
                return (IConfig)Activator.CreateInstance(wrapperType, instance);
            }

            return (IConfig)instance;
        }

        private void DrawRightColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (selectedIndex < 0 || selectedIndex >= configsProp.arraySize)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Select a config from the list.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            var element = configsProp.GetArrayElementAtIndex(selectedIndex);
            var value = element.managedReferenceValue;
            var title = value != null ? value.GetType().Name : "Config";

            rightScrollPosition = EditorGUILayout.BeginScrollView(rightScrollPosition);
            EditorGUILayout.PropertyField(element, new GUIContent(title), true);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
    }
}
