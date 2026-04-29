#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Scaffold.Entities
{
    internal static class ModifierTypeIndex
    {
        public static IReadOnlyList<Type> AllModifierTypes
        {
            get
            {
                EnsureBuilt();
                return allModifierTypes!;
            }
        }

        private static readonly object buildLock = new();
        private static Dictionary<Type, List<Type>>? valueTypeToModifiers;
        private static Dictionary<Type, Type>? modifierToValueType;
        private static IReadOnlyList<Type>? allModifierTypes;

        public static IReadOnlyList<Type> ModifiersFor(Type valueType)
        {
            EnsureBuilt();
            if (valueType == null || !valueTypeToModifiers!.TryGetValue(valueType, out List<Type>? list))
            {
                return Array.Empty<Type>();
            }

            return list;
        }

        public static bool TryGetValueType(Type modifierType, out Type valueType)
        {
            EnsureBuilt();
            if (modifierType != null && modifierToValueType!.TryGetValue(modifierType, out Type? found))
            {
                valueType = found;
                return true;
            }

            valueType = default!;
            return false;
        }

        private static void EnsureBuilt()
        {
            if (valueTypeToModifiers != null)
            {
                return;
            }

            lock (buildLock)
            {
                if (valueTypeToModifiers != null)
                {
                    return;
                }

                Build();
            }
        }

        private static void Build()
        {
            var forward = new Dictionary<Type, List<Type>>();
            var reverse = new Dictionary<Type, Type>();
            var all = new List<Type>();
            RegisterAssemblies(forward, reverse, all);
            SortBuckets(forward);
            all.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            valueTypeToModifiers = forward;
            modifierToValueType = reverse;
            allModifierTypes = all;
        }

        private static void RegisterAssemblies(Dictionary<Type, List<Type>> forward, Dictionary<Type, Type> reverse, List<Type> all)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                {
                    continue;
                }

                RegisterAssemblyTypes(assembly, forward, reverse, all);
            }
        }

        private static void RegisterAssemblyTypes(Assembly assembly, Dictionary<Type, List<Type>> forward, Dictionary<Type, Type> reverse, List<Type> all)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types!;
            }

            for (int i = 0; i < types.Length; i++)
            {
                RegisterOneType(types[i], forward, reverse, all);
            }
        }

        private static void RegisterOneType(Type? t, Dictionary<Type, List<Type>> forward, Dictionary<Type, Type> reverse, List<Type> all)
        {
            if (!IsConcreteModifierType(t))
            {
                return;
            }

            if (!TryGetClosedVariableModifierArgument(t!, out Type? valueType) || valueType == null)
            {
                return;
            }

            RegisterMapping(t!, valueType, forward, reverse, all);
        }

        private static bool IsConcreteModifierType(Type? t)
        {
            return t != null && !t.IsAbstract && typeof(VariableModifier).IsAssignableFrom(t);
        }

        private static void RegisterMapping(Type t, Type valueType, Dictionary<Type, List<Type>> forward, Dictionary<Type, Type> reverse, List<Type> all)
        {
            if (!forward.TryGetValue(valueType, out List<Type>? bucket))
            {
                bucket = new List<Type>();
                forward[valueType] = bucket;
            }

            bucket.Add(t);
            reverse[t] = valueType;
            all.Add(t);
        }

        private static void SortBuckets(Dictionary<Type, List<Type>> forward)
        {
            foreach (List<Type> bucket in forward.Values)
            {
                bucket.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            }
        }

        private static bool TryGetClosedVariableModifierArgument(Type type, out Type? valueType)
        {
            for (Type? t = type; t != null; t = t.BaseType)
            {
                if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(VariableModifier<>))
                {
                    continue;
                }

                Type[] args = t.GetGenericArguments();
                valueType = args.Length > 0 ? args[0] : null;
                return valueType != null;
            }

            valueType = null;
            return false;
        }
    }
}
