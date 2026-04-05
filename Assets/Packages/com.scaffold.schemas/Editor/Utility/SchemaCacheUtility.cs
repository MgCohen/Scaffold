using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Scaffold.Schemas
{
    public static class SchemaCacheUtility
    {
        private static Dictionary<Type, string> cachedTypeNames = new Dictionary<Type, string>();
        private static Dictionary<Type, string> cachedTypePaths = new Dictionary<Type, string>();
        private static Dictionary<Type, List<Type>> cachedDerivedTypes = new Dictionary<Type, List<Type>>();

        public static string GetTypeDisplayName(Type type)
        {
            if (!cachedTypeNames.TryGetValue(type, out string name))
            {
                name = type switch
                {
                    { IsGenericType: true } => $"{type.Name.Split('`')[0]}<{string.Join(", ", type.GetGenericArguments().Select(GetTypeDisplayName))}>",
                    _ => type.Name
                };
                cachedTypeNames[type] = name;
            }
            return name;
        }

        public static string GetTypeGroupPath(Type type)
        {
            if (!cachedTypePaths.TryGetValue(type, out string name))
            {
                name = GetTypeDisplayName(type);
                var menuGroupAttribute = type.GetCustomAttribute<SchemaMenuGroupAttribute>();
                if (menuGroupAttribute != null)
                {
                    name = $"{menuGroupAttribute.Path}/{name}";
                }
                cachedTypePaths[type] = name;
            }
            return name;
        }

        public static List<Type> GetDerivedTypes(Type type)
        {
            if (!cachedDerivedTypes.TryGetValue(type, out List<Type> list))
            {
                AppDomain domain = AppDomain.CurrentDomain;
                list = domain.GetAssemblies().SelectMany(x => x.GetTypes()).Where(t => !t.IsAbstract && type.IsAssignableFrom(t)).ToList();

                if (type.IsAbstract)
                {
                    list.Remove(type);
                }

                cachedDerivedTypes[type] = list;
            }
            return list;
        }
    }
}
