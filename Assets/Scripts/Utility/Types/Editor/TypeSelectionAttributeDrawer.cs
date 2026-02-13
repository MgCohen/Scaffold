using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(TypeSelectionAttribute))]
public class TypeSelectionAttributeDrawer : PropertyDrawer
{
    Dictionary<Type, DerivedTypeDropdown> selectorCache = new Dictionary<Type, DerivedTypeDropdown>();

    private const float M_SPACING = 2.0f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        float lineSize = EditorGUIUtility.singleLineHeight;
        float fieldSpacing = EditorGUIUtility.standardVerticalSpacing;

        EditorGUI.BeginProperty(position, label, property);
        position.x -= M_SPACING;
        position.width += M_SPACING * 2.0f;
        position.y -= M_SPACING;
        position.height += M_SPACING * 2.0f;

        EditorGUI.DrawRect(position, new Color(0.28f, 0.28f, 0.28f));
        position.x += M_SPACING;
        position.y += M_SPACING;

        position.width -= M_SPACING * 2.0f;
        position.height -= M_SPACING * 2.0f;

        var customAttribute = (TypeSelectionAttribute)attribute;

        var selectType = customAttribute.baseType;

        if (!selectorCache.TryGetValue(selectType, out DerivedTypeDropdown selector))
        {
            selector = new DerivedTypeDropdown(selectType);
            selectorCache.Add(selectType, selector);
        }

        selector.RefreshSelection(property.managedReferenceValue?.GetType());

        position.height = lineSize;

        if (selector.ChangeCheck(position))
        {
            var oldObj = property.managedReferenceValue;
            var newObj = selector.CreateInstance(property.managedReferenceValue);
            //ReflectionHelper.CopyFields(oldObj, newObj);
            property.managedReferenceValue = newObj;
            property.serializedObject.ApplyModifiedProperties();
        }
        else
        {
            position.y += lineSize;
            EditorGUI.PropertyField(position, property, true);
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, true) + EditorGUIUtility.singleLineHeight;
    }
}

public class DerivedTypeDropdown
{
    private readonly List<Type> subTypes;
    private readonly string[] subTypeNames;
    private int selectedIndex = -1;


    public Type SelectedType => subTypes.ElementAtOrDefault(Math.Max(0, selectedIndex));

    public DerivedTypeDropdown(Type targetType, Type currentType = null)
    {
        subTypes = new List<Type>();

        if (!targetType.IsAbstract)
        {
            subTypes.Add(targetType);
        }

        IEnumerable<Type> foundClasses = TypeCache.GetTypesDerivedFrom(targetType);
        subTypes.AddRange(foundClasses.Where(t => !t.ContainsGenericParameters).ToList());
        subTypeNames = subTypes.Select(x => x.Name).ToArray();
        selectedIndex = subTypes.IndexOf(currentType);
    }

    public void RefreshSelection(Type currentType)
    {
        selectedIndex = subTypes.IndexOf(currentType);
    }

    public bool ChangeCheck(Rect position)
    {
        int oldIndex = selectedIndex;
        int newIndex = EditorGUI.Popup(position, Math.Max(0, selectedIndex), subTypeNames);
        selectedIndex = newIndex;

        return oldIndex != newIndex;
    }

    public bool DrawLayout()
    {
        int oldIndex = selectedIndex;
        int newIndex = EditorGUILayout.Popup(Math.Max(0, selectedIndex), subTypeNames);
        selectedIndex = newIndex;
        return oldIndex != newIndex;
    }

    public object CreateInstance(object oldValue = null)
    {
        return Activator.CreateInstance(SelectedType);
    }
}