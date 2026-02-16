using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VContainer.Unity;

namespace Scaffold.Containers.Editor
{

    [CustomEditor(typeof(oldContainer))]
    public class ContainerEditor : UnityEditor.Editor
    {
        private SerializedProperty stateProp;
        private SerializedProperty parentProp;
        private SerializedProperty installersProp;
        private List<Type> installerTypes;
        private int index;
        private ReorderableList list;

        private GUIStyle rightLabel;

        private void OnEnable()
        {
            stateProp = serializedObject.FindProperty("state");
            parentProp = serializedObject.FindProperty("parentContainer");
            installersProp = serializedObject.FindProperty("installers");
            installerTypes = GetInstallerTypes();
            list = DefineList();

            rightLabel = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.LowerRight,
            };
        }

        #region Definitions
        private ReorderableList DefineList()
        {
            var list = new ReorderableList(serializedObject, installersProp, true, true, true, true);

            list.drawHeaderCallback = (r) =>
            {
                EditorGUI.LabelField(r, installersProp.displayName);
            };

            list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                rect.x += 10f;
                rect.width -= 10f;
                var element = installersProp.GetArrayElementAtIndex(index);
                if (element != null && element.managedReferenceValue != null)
                {
                    var type = element.managedReferenceValue.GetType();
                    if (element.hasChildren)
                    {
                        EditorGUI.BeginChangeCheck();
                        EditorGUI.PropertyField(rect, element, new GUIContent(type.FullName), true);
                        if (EditorGUI.EndChangeCheck())
                        {
                            serializedObject.ApplyModifiedProperties();
                        }
                    }
                    else
                    {
                        EditorGUI.LabelField(rect, type.FullName);
                    }
                }
            };

            list.elementHeightCallback = (int index) =>
            {
                var element = installersProp.GetArrayElementAtIndex(index);
                return EditorGUI.GetPropertyHeight(element, true);
            };

            list.onReorderCallback += (l) =>
            {
                serializedObject.ApplyModifiedProperties();
            };

            list.onAddCallback += (l) =>
            {
                installersProp.arraySize++;
                var newIndex = installersProp.arraySize - 1;
                var newElement = installersProp.GetArrayElementAtIndex(newIndex);
                newElement.managedReferenceValue = CreateInstanceOfSelectedType();
                newElement.serializedObject.ApplyModifiedProperties();
            };

            list.onRemoveCallback += (l) =>
            {
                ReorderableList.defaultBehaviours.DoRemoveButton(l);
                serializedObject.ApplyModifiedProperties();
            };

            return list;
        }

        private List<Type> GetInstallerTypes()
        {
            return TypeCache.GetTypesDerivedFrom<IInstaller>()
                                      .Where(t => !t.IsGenericTypeDefinition)
                                      .Where(t => !t.IsAbstract && !t.IsInterface)
                                      .ToList();
        }

        #endregion

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawScript();
            DrawState();
            DrawDivider();
            list.DoLayoutList();
            DrawTypeSelection();
            EditorGUILayout.PropertyField(parentProp);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScript()
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((MonoBehaviour)target), GetType(), false);
        }

        private void DrawState()
        {
            ContainerState state = (ContainerState)stateProp.enumValueIndex;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(state.ToString(), rightLabel, GUILayout.ExpandWidth(true));
            
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.MaxWidth(20));
            rect.y += 2f;
            EditorGUI.LabelField(rect, GetIcon(state));
            EditorGUI.LabelField(rect, EditorGUIUtility.IconContent("d_lightRim"));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDivider()
        {
            GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(2) });
        }

        private void DrawTypeSelection()
        {
            var rect = EditorGUILayout.GetControlRect();
            rect.y -= (EditorGUIUtility.singleLineHeight + 2f);
            rect.width -= 75;
            rect.x += 3f;
            index = EditorGUI.Popup(rect, index, installerTypes.Select(t => t.ToString()).ToArray());
        }

        private IInstaller CreateInstanceOfSelectedType()
        {
            Type type = GetSelectedType();
            return Activator.CreateInstance(type) as IInstaller;
        }

        private Type GetSelectedType()
        {
            Type type = installerTypes[index];
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                Type baseType = typeof(InstallerWrapper<>);
                type = baseType.MakeGenericType(type);
            }
            return type;
        }


        private GUIContent GetIcon(ContainerState state)
        {
            switch (state)
            {
                case ContainerState.Closed:
                    return EditorGUIUtility.IconContent("d_lightRim");
                case ContainerState.Initializing:
                    return EditorGUIUtility.IconContent("d_orangeLight");
                case ContainerState.Open:
                    return EditorGUIUtility.IconContent("d_greenLight");
                case ContainerState.Failed:
                    return EditorGUIUtility.IconContent("d_redLight");
                default:
                    return EditorGUIUtility.IconContent("d_lightRim");
            }
        }
    }
}
