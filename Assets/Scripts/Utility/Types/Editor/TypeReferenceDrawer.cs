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
        private static readonly HashSet<string> _systemAssemblyNames = new HashSet<string> { "mscorlib", "System", "System.Core" };
        private static Dictionary<Type, GenericMenu> menus = new Dictionary<Type, GenericMenu>();
        private static Action<Type> OnClickCallback;

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
            if (EditorGUI.DropdownButton(position, new GUIContent(currentType?.Name), FocusType.Passive))
            {
                OnClickCallback = (t) =>
                {
                    var serializedType = JsonConvert.SerializeObject(t, new JsonSerializerSettings()
                    {
                        TypeNameHandling = TypeNameHandling.All,
                    });
                    property.FindPropertyRelative("serializedType").stringValue = serializedType;
                    property.serializedObject.ApplyModifiedProperties();
                };
                menus[filterType].ShowAsContext();
            }
        }

        private void BuildMenu(Type filterType)
        {
            var typeOptions = new List<Type>();
            if (filterType != typeof(TypeReference))
            {
                typeOptions = UnityEditor.TypeCache.GetTypesDerivedFrom(filterType)
                    .Where(t => !t.IsAbstract 
                                && !t.ContainsGenericParameters
                                && !t.IsGenericTypeDefinition 
                                && !t.FullName.Contains("<") 
                                && !t.IsGenericType)
                                .ToList();
            }
            else
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !IsSystemAssembly(a));
                foreach (var assemblie in assemblies)
                {
                    typeOptions.AddRange(assemblie.GetTypes().Where(t => !t.IsAbstract && !t.ContainsGenericParameters && !t.IsGenericTypeDefinition && !t.FullName.Contains("<") && !t.IsGenericType));
                }
            }
            BuildMenu(filterType, typeOptions);
        }

        private void BuildMenu(Type filterType, List<Type> typeOptions)
        {
            GenericMenu menu = new GenericMenu();
            foreach (var type in typeOptions)
            {
                GUIContent menuOption = new GUIContent(type.FullName.Replace('.', '/'));
                menu.AddItem(menuOption, false, () =>
                {
                    OnClickCallback.Invoke(type);
                });
            }
            menus[filterType] = menu;
        }

        private static bool IsSystemAssembly(Assembly assembly)
        {
            var referencedAssemblies = assembly.GetReferencedAssemblies();
            return IsSystemAssembly(assembly.GetName()) || referencedAssemblies.Any(IsSystemAssembly);
        }

        private static bool IsSystemAssembly(AssemblyName assemblyName)
        {
            return _systemAssemblyNames.Contains(assemblyName.Name);
        }

    }
}
