using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Scaffold.Entities
{
    internal static class VariableValueRegistry
    {
        public static IReadOnlyList<Type> AllConcreteTypes
        {
            get
            {
                EnsureInitialized();
                return allConcreteTypesList!;
            }
        }

        private static readonly object gateLock = new object();
        private static bool initialized;
        private static Dictionary<string, Type> idToType = default!;
        private static Dictionary<Type, string> typeToId = default!;
        private static List<Type>? allConcreteTypesList;

        public static bool TryResolve(string id, out Type type)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(id))
            {
                type = default!;
                return false;
            }

            return idToType.TryGetValue(id, out type!);
        }

        public static bool TryGetId(Type type, out string id)
        {
            EnsureInitialized();
            if (type == null)
            {
                id = default!;
                return false;
            }

            return typeToId.TryGetValue(type, out id!);
        }

        public static bool Contains(Type type)
        {
            EnsureInitialized();
            return type != null && typeToId.ContainsKey(type);
        }

        private static void EnsureInitialized()
        {
            lock (gateLock)
            {
                if (initialized)
                {
                    return;
                }

                Build();
                initialized = true;
            }
        }

        private static void Build()
        {
            idToType = new Dictionary<string, Type>(StringComparer.Ordinal);
            typeToId = new Dictionary<Type, string>();
            var orderedConcrete = new List<Type>();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                CollectFromAssembly(assembly, orderedConcrete);
            }

            orderedConcrete.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            allConcreteTypesList = orderedConcrete;
        }

        private static void CollectFromAssembly(Assembly assembly, List<Type> orderedConcrete)
        {
            foreach (Type t in GetLoadableTypes(assembly))
            {
                TryRegisterConcrete(t, orderedConcrete);
            }
        }

        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return FilterNonNullTypes(ex.Types);
            }
        }

        private static Type[] FilterNonNullTypes(Type?[]? raw)
        {
            if (raw == null)
            {
                return Array.Empty<Type>();
            }

            var list = new List<Type>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] != null)
                {
                    list.Add(raw[i]!);
                }
            }

            return list.ToArray();
        }

        private static void TryRegisterConcrete(Type t, List<Type> orderedConcrete)
        {
            if (!IsConcretePayloadCandidate(t))
            {
                return;
            }

            object[] attrs = t.GetCustomAttributes(typeof(VariableValueIdAttribute), inherit: false);
            if (attrs.Length == 0)
            {
                return;
            }

            RegisterAttributed(t, ((VariableValueIdAttribute)attrs[0]).Id, orderedConcrete);
        }

        private static bool IsConcretePayloadCandidate(Type t)
        {
            return t != null && typeof(VariableValue).IsAssignableFrom(t) && !t.IsAbstract && !t.IsGenericTypeDefinition;
        }

        private static void RegisterAttributed(Type t, string id, List<Type> orderedConcrete)
        {
            if (idToType.TryGetValue(id, out Type? existing))
            {
                HandleDuplicateId(id, existing, t);
                return;
            }

            idToType[id] = t;
            typeToId[t] = id;
            orderedConcrete.Add(t);
        }

        private static void HandleDuplicateId(string id, Type existing, Type candidate)
        {
            if (existing == candidate)
            {
                return;
            }

#if UNITY_EDITOR
            throw new InvalidOperationException(
                $"Duplicate [VariableValueId(\"{id}\")] on '{existing.FullName}' and '{candidate.FullName}'.");
#else
            Debug.LogError(
                $"Duplicate [VariableValueId(\"{id}\")] on '{existing.FullName}' and '{candidate.FullName}'. Keeping '{existing.FullName}'.");
#endif
        }
    }
}
