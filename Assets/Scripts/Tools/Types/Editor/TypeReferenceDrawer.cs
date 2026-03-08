using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using Newtonsoft.Json;

namespace Scaffold.Types.Editor
{
    [CustomPropertyDrawer(typeof(TypeReference))]
    public class TypeReferenceDrawer : PropertyDrawer
    {
        private static readonly HashSet<string> systemAssemblyNames = new HashSet<string> { "mscorlib", "System", "System.Core" };
        private static Dictionary<Type, GenericMenu> menus = new Dictionary<Type, GenericMenu>();
        private static Action<Type> onClickCallback;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Type filterType = GetFilterType();
            EnsureMenuBuilt(filterType);
            position = GetLabelledPosition(position, property, label);
            DrawTypeDropdown(position, property, filterType);
        }

        private Type GetFilterType()
        {
            TypeReferenceFilterAttribute attr = fieldInfo.GetCustomAttribute<TypeReferenceFilterAttribute>();
            return attr == null ? typeof(TypeReference) : attr.TypeFilter;
        }

        private void EnsureMenuBuilt(Type filterType)
        {
            if (!menus.ContainsKey(filterType))
            {
                BuildMenu(filterType);
            }
        }

        private Rect GetLabelledPosition(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!property.propertyPath.Contains("Array"))
            {
                return EditorGUI.PrefixLabel(position, label);
            }
            return position;
        }

        private void DrawTypeDropdown(Rect position, SerializedProperty property, Type filterType)
        {
            Type currentType = (property.boxedValue as TypeReference).Type;
            GUIContent dropdownContent = new GUIContent(currentType?.Name);
            if (EditorGUI.DropdownButton(position, dropdownContent, FocusType.Passive))
            {
                ShowMenuForType(filterType, property);
            }
        }

        private void ShowMenuForType(Type filterType, SerializedProperty property)
        {
            onClickCallback = (t) => SetClickedType(t, property);
            menus[filterType].ShowAsContext();
        }

        private static void SetClickedType(Type t, SerializedProperty property)
        {
            string serializedType = SerializeType(t);
            SerializedProperty serializedTypeProperty = property.FindPropertyRelative("serializedType");
            serializedTypeProperty.stringValue = serializedType;
            property.serializedObject.ApplyModifiedProperties();
        }

        private void BuildMenu(Type filterType)
        {
            List<Type> typeOptions = CollectTypeOptions(filterType);
            BuildMenu(filterType, typeOptions);
        }

        private List<Type> CollectTypeOptions(Type filterType)
        {
            if (filterType != typeof(TypeReference))
            {
                return GetTypesFromCache(filterType);
            }
            return GetAllNonSystemTypes();
        }

        private void BuildMenu(Type filterType, List<Type> typeOptions)
        {
            GenericMenu menu = new GenericMenu();
            foreach (Type type in typeOptions)
            {
                AddMenuItem(menu, type);
            }
            menus[filterType] = menu;
        }

        private List<Type> GetTypesFromCache(Type filterType)
        {
            return UnityEditor.TypeCache.GetTypesDerivedFrom(filterType).Where(t => !t.IsAbstract && !t.ContainsGenericParameters && !t.IsGenericTypeDefinition && !t.FullName.Contains("<") && !t.IsGenericType).ToList();
        }

        private List<Type> GetAllNonSystemTypes()
        {
            IEnumerable<Assembly> assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !IsSystemAssembly(a));
            List<Type> result = new List<Type>();
            assemblies.ToList().ForEach(a => AddNonSystemTypes(a, result));
            return result;
        }

        private void AddNonSystemTypes(Assembly assembly, List<Type> result)
        {
            IEnumerable<Type> types = assembly.GetTypes().Where(t => !t.IsAbstract && !t.ContainsGenericParameters && !t.IsGenericTypeDefinition && !t.FullName.Contains("<") && !t.IsGenericType);
            result.AddRange(types);
        }

        private static bool IsSystemAssembly(Assembly assembly)
        {
            AssemblyName assemblyName = assembly.GetName();
            AssemblyName[] referencedAssemblies = assembly.GetReferencedAssemblies();
            return IsSystemAssembly(assemblyName) || referencedAssemblies.Any(IsSystemAssembly);
        }

        private static bool IsSystemAssembly(AssemblyName assemblyName)
        {
            return systemAssemblyNames.Contains(assemblyName.Name);
        }

        private static string SerializeType(Type type)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
            return JsonConvert.SerializeObject(type, settings);
        }

        private void AddMenuItem(GenericMenu menu, Type type)
        {
            string menuLabel = type.FullName.Replace('.', '/');
            GUIContent menuOption = new GUIContent(menuLabel);
            menu.AddItem(menuOption, false, () => onClickCallback.Invoke(type));
        }
    }
}
