using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Scaffold.Schemas.Editor
{
    [CustomEditor(typeof(SchemaObject), true)]
    public class SchemaObjectEditor : UnityEditor.Editor
    {
        protected virtual string[] PropertiesToIgnore => new string[]
        {
            "m_Script",
            "schemas"
        };

        private List<Type> schemaOptions = new List<Type>();

        private SchemaValidator validator;

        protected void OnEnable()
        {
            ValidateSchemas();
            Setup();
        }

        private void ValidateSchemas()
        {
            validator = new SchemaValidator(target as SchemaObject, serializedObject, this);
            validator.Validate();
            schemaOptions = validator.GetSchemaOptions();
        }

        protected virtual void Setup()
        {
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultProperties();
            EditorGUILayout.Space(5);
            DrawSchemas();
            EditorGUILayout.Space(5);
            DrawControls();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSchemas()
        {
            SerializedProperty collectionProp = serializedObject.FindProperty("schemas.Collection");
            if (collectionProp.arraySize == 0)
            {
                SchemaLayout.Divider(0, 0);
                return;
            }

            for (int i = 0; i < collectionProp.arraySize; i++)
            {
                DrawSingleSchemaElement(collectionProp, i);
            }
        }

        private void DrawSingleSchemaElement(SerializedProperty collectionProp, int index)
        {
            SerializedProperty prop = collectionProp.GetArrayElementAtIndex(index);
            if (prop == null || prop.boxedValue == null)
            {
                return;
            }
            SchemaDrawer drawer = SchemaDrawerContainer.instance.GetDrawer(prop, this);
            if (drawer.Expired)
            {
                return;
            }
            drawer.Draw();
            if (index == collectionProp.arraySize - 1 && prop.isExpanded)
            {
                SchemaLayout.Divider(0, 0);
            }
        }

        protected virtual void DrawDefaultProperties()
        {
            DrawPropertiesExcluding(serializedObject, PropertiesToIgnore);
        }

        private void DrawControls()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(schemaOptions.Count <= 0);
            string buttonText = schemaOptions.Count > 0 ? "Add Schema" : "No Schema option available";
            if (EditorGUILayout.DropdownButton(new GUIContent(buttonText, "Add new schema to this object."), FocusType.Keyboard, SchemaStyles.CenterButton))
            {
                ShowSchemaMenu();
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void ShowSchemaMenu()
        {
            GenericMenu menu = new GenericMenu();
            AddSchemaMenuItems(menu);
            menu.ShowAsContext();
        }

        private void AddSchemaMenuItems(GenericMenu menu)
        {
            for (int i = 0; i < schemaOptions.Count; i++)
            {
                Type type = schemaOptions[i];
                GUIContent menuOption = new GUIContent(SchemaCacheUtility.GetTypeGroupPath(type), "");
                bool canAdd = validator.CanAddType(type);

                if (canAdd)
                {
                    menu.AddItem(menuOption, false, () => AddSchema(type));
                }
                else
                {
                    menu.AddDisabledItem(menuOption, true);
                }
            }
        }

        public void AddSchema(Type schema)
        {
            SchemaSet set = serializedObject.FindProperty("schemas").boxedValue as SchemaSet;
            Undo.RecordObject(target, "adding schema to object");
            set.AddSchema(schema);
            Refresh();
        }

        public void RemoveSchema(Schema schema)
        {
            SchemaSet set = serializedObject.FindProperty("schemas").boxedValue as SchemaSet;
            Undo.RecordObject(target, "removing schema from object");
            set.RemoveSchema(schema);
            Refresh();
        }

        public void Refresh()
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            serializedObject.Update();
        }
    }
}
