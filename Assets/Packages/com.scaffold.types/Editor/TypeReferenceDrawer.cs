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
            Type filterType = BuildGetFilterType();
            BuildEnsureMenuBuilt(filterType);
            position = BuildGetLabelledPosition(position, property, label);
            BuildDrawTypeDropdown(position, property, filterType);
        }

        private Type BuildGetFilterType()
        {
            TypeReferenceFilterAttribute attr = fieldInfo.GetCustomAttribute<TypeReferenceFilterAttribute>();
            return attr == null ? typeof(TypeReference) : attr.TypeFilter;
        }

        private static void BuildEnsureMenuBuilt(Type filterType)
        {
            if (!menus.ContainsKey(filterType))
            {
                BuildMenu(filterType);
            }
        }

        private static Rect BuildGetLabelledPosition(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!property.propertyPath.Contains("Array"))
            {
                return EditorGUI.PrefixLabel(position, label);
            }
            return position;
        }

        private static void BuildDrawTypeDropdown(Rect position, SerializedProperty property, Type filterType)
        {
            Type currentType = (property.boxedValue as TypeReference).Type;
            GUIContent dropdownContent = new GUIContent(currentType?.Name);
            if (EditorGUI.DropdownButton(position, dropdownContent, FocusType.Passive))
            {
                BuildShowMenuForType(filterType, property);
            }
        }

        private static void BuildShowMenuForType(Type filterType, SerializedProperty property)
        {
            onClickCallback = (t) => BuildSetClickedType(t, property);
            menus[filterType].ShowAsContext();
        }

        private static void BuildSetClickedType(Type t, SerializedProperty property)
        {
            string serializedType = BuildSerializeType(t);
            SerializedProperty serializedTypeProperty = property.FindPropertyRelative("serializedType");
            serializedTypeProperty.stringValue = serializedType;
            property.serializedObject.ApplyModifiedProperties();
        }

        private static void BuildMenu(Type filterType)
        {
            List<Type> typeOptions = BuildCollectTypeOptions(filterType);
            BuildMenu(filterType, typeOptions);
        }

        private static List<Type> BuildCollectTypeOptions(Type filterType)
        {
            if (filterType != typeof(TypeReference))
            {
                return BuildGetTypesFromCache(filterType);
            }
            return BuildGetAllNonSystemTypes();
        }

        private static void BuildMenu(Type filterType, List<Type> typeOptions)
        {
            GenericMenu menu = new GenericMenu();
            foreach (Type type in typeOptions)
{
    BuildAddMenuItem(menu, type);
}
            menus[filterType] = menu;
        }

        private static List<Type> BuildGetTypesFromCache(Type filterType)
        {
            return UnityEditor.TypeCache.GetTypesDerivedFrom(filterType).Where(t => !t.IsAbstract && !t.ContainsGenericParameters && !t.IsGenericTypeDefinition && !t.FullName.Contains("<") && !t.IsGenericType).ToList();
        }

        private static List<Type> BuildGetAllNonSystemTypes()
        {
            IEnumerable<Assembly> assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !BuildIsSystemAssembly(a));
            List<Type> result = new List<Type>();
            assemblies.ToList().ForEach(a => BuildAddNonSystemTypes(a, result));
            return result;
        }

        private static void BuildAddNonSystemTypes(Assembly assembly, List<Type> result)
        {
            IEnumerable<Type> types = assembly.GetTypes().Where(t => !t.IsAbstract && !t.ContainsGenericParameters && !t.IsGenericTypeDefinition && !t.FullName.Contains("<") && !t.IsGenericType);
            result.AddRange(types);
        }

        private static bool BuildIsSystemAssembly(Assembly assembly)
        {
            AssemblyName assemblyName = assembly.GetName();
            AssemblyName[] referencedAssemblies = assembly.GetReferencedAssemblies();
            return BuildIsSystemAssembly(assemblyName) || referencedAssemblies.Any(BuildIsSystemAssembly);
        }

        private static bool BuildIsSystemAssembly(AssemblyName assemblyName)
        {
            return systemAssemblyNames.Contains(assemblyName.Name);
        }

        private static string BuildSerializeType(Type type)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
            return JsonConvert.SerializeObject(type, settings);
        }

        private static void BuildAddMenuItem(GenericMenu menu, Type type)
        {
            string menuLabel = type.FullName.Replace('.', '/');
            GUIContent menuOption = new GUIContent(menuLabel);
            menu.AddItem(menuOption, false, () => onClickCallback.Invoke(type));
        }
    }
}


