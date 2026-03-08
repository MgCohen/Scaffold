using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using System.Reflection;
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
            var attribute = fieldInfo.GetCustomAttribute<TypeReferenceFilterAttribute>();
            Type filterType = attribute == null ? typeof(TypeReference) : attribute.TypeFilter;

            if (!menus.ContainsKey(filterType))
            {
                BuildMenu(filterType);
            }

            if (!property.propertyPath.Contains("Array"))
            {
                position = EditorGUI.PrefixLabel(position, label);
            }

            Type currentType = (property.boxedValue as TypeReference).Type;
            var dropdownContent = new GUIContent(currentType?.Name);
            if (EditorGUI.DropdownButton(position, dropdownContent, FocusType.Passive))
            {
                onClickCallback = (t) => SetClickedType(t, property);
                menus[filterType].ShowAsContext();
            }
        }

        private static void SetClickedType(Type t, SerializedProperty property)
        {
            var serializedType = SerializeType(t);
            var serializedTypeProperty = property.FindPropertyRelative("serializedType");
            serializedTypeProperty.stringValue = serializedType;
            property.serializedObject.ApplyModifiedProperties();
        }

        private void BuildMenu(Type filterType)
        {
            var typeOptions = new List<Type>();
            if (filterType != typeof(TypeReference))
            {
                typeOptions = UnityEditor.TypeCache.GetTypesDerivedFrom(filterType).Where(t => !t.IsAbstract && !t.ContainsGenericParameters && !t.IsGenericTypeDefinition && !t.FullName.Contains("<") && !t.IsGenericType).ToList();
            }
            else
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !IsSystemAssembly(a));
                foreach (var assemblie in assemblies)
                {
                    var filteredTypes = assemblie.GetTypes().Where(t => !t.IsAbstract && !t.ContainsGenericParameters && !t.IsGenericTypeDefinition && !t.FullName.Contains("<") && !t.IsGenericType);
                    typeOptions.AddRange(filteredTypes);
                }
            }
            BuildMenu(filterType, typeOptions);
        }

        private void BuildMenu(Type filterType, List<Type> typeOptions)
        {
            GenericMenu menu = new GenericMenu();
            foreach (var type in typeOptions)
            {
                var menuLabel = type.FullName.Replace('.', '/');
                GUIContent menuOption = new GUIContent(menuLabel);
                menu.AddItem(menuOption, false, () =>
                {
                    onClickCallback.Invoke(type);
                });
            }
            menus[filterType] = menu;
        }

        private static bool IsSystemAssembly(Assembly assembly)
        {
            var referencedAssemblies = assembly.GetReferencedAssemblies();
            var assemblyName = assembly.GetName();
            return IsSystemAssembly(assemblyName) || referencedAssemblies.Any(IsSystemAssembly);
        }

        private static bool IsSystemAssembly(AssemblyName assemblyName)
        {
            return systemAssemblyNames.Contains(assemblyName.Name);
        }

        private static string SerializeType(Type type)
        {
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
            return JsonConvert.SerializeObject(type, settings);
        }
    }
}
