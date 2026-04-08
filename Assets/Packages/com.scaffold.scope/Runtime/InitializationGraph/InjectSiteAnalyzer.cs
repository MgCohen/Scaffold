using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using VContainer;

namespace Scaffold.Scope.InitializationGraph
{
    internal sealed class InjectSiteAnalyzer
    {
        public InjectSiteAnalyzer()
        {
            Assembly vcontainerAssembly = typeof(IObjectResolver).Assembly;
            Type typeAnalyzer = FindNestedOrTopLevelType(vcontainerAssembly, "VContainer.Internal", "TypeAnalyzer");
            if (typeAnalyzer == null)
            {
                throw new InvalidOperationException(
                    "Could not find VContainer.Internal.TypeAnalyzer. Check VContainer package version.");
            }

            analyzeWithCacheMethod = typeAnalyzer.GetMethod(
                "AnalyzeWithCache",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Type) },
                null);

            if (analyzeWithCacheMethod == null)
            {
                throw new InvalidOperationException(
                    "Could not find public static TypeAnalyzer.AnalyzeWithCache(Type). Check VContainer package version.");
            }
        }

        private readonly ConcurrentDictionary<Type, IReadOnlyList<(Type DependencyType, object Key)>> cache =
            new ConcurrentDictionary<Type, IReadOnlyList<(Type DependencyType, object Key)>>();

        private readonly MethodInfo analyzeWithCacheMethod;

        internal IReadOnlyList<(Type DependencyType, object Key)> GetDependencySites(Type implementationType)
        {
            if (implementationType == null)
            {
                throw new ArgumentNullException(nameof(implementationType));
            }

            return cache.GetOrAdd(implementationType, AnalyzeUsingVContainer);
        }

        private IReadOnlyList<(Type DependencyType, object Key)> AnalyzeUsingVContainer(Type implementationType)
        {
            object injectTypeInfo = analyzeWithCacheMethod.Invoke(null, new object[] { implementationType });
            if (injectTypeInfo == null)
            {
                return Array.Empty<(Type DependencyType, object Key)>();
            }

            var results = new List<(Type DependencyType, object Key)>();
            Type infoType = injectTypeInfo.GetType();
            AppendConstructorSites(injectTypeInfo, infoType, results);
            AppendMethodSites(injectTypeInfo, infoType, results);
            AppendFieldSites(injectTypeInfo, infoType, results);
            AppendPropertySites(injectTypeInfo, infoType, results);
            return results;
        }

        private void AppendConstructorSites(object injectTypeInfo, Type infoType, List<(Type DependencyType, object Key)> results)
        {
            object injectConstructor = GetPublicInstanceField(infoType, injectTypeInfo, "InjectConstructor");
            if (injectConstructor == null)
            {
                return;
            }

            Type ctorInfoType = injectConstructor.GetType();
            ParameterInfo[] parameterInfos = (ParameterInfo[])GetPublicInstanceField(ctorInfoType, injectConstructor, "ParameterInfos");
            object[] parameterKeys = (object[])GetPublicInstanceField(ctorInfoType, injectConstructor, "ParameterKeys");
            if (parameterInfos == null)
            {
                return;
            }

            AddParameterSites(parameterInfos, parameterKeys, results);
        }

        private void AppendMethodSites(object injectTypeInfo, Type infoType, List<(Type DependencyType, object Key)> results)
        {
            object methodsObj = GetPublicInstanceField(infoType, injectTypeInfo, "InjectMethods");
            if (methodsObj is not IEnumerable enumerable)
            {
                return;
            }

            foreach (object injectMethodInfo in enumerable)
            {
                AppendOneMethodSite(injectMethodInfo, results);
            }
        }

        private void AppendOneMethodSite(object injectMethodInfo, List<(Type DependencyType, object Key)> results)
        {
            if (injectMethodInfo == null)
            {
                return;
            }

            Type methodInfoType = injectMethodInfo.GetType();
            ParameterInfo[] parameterInfos = (ParameterInfo[])GetPublicInstanceField(methodInfoType, injectMethodInfo, "ParameterInfos");
            object[] parameterKeys = (object[])GetPublicInstanceField(methodInfoType, injectMethodInfo, "ParameterKeys");
            if (parameterInfos == null)
            {
                return;
            }

            AddParameterSites(parameterInfos, parameterKeys, results);
        }

        private void AddParameterSites(ParameterInfo[] parameterInfos, object[] parameterKeys, List<(Type DependencyType, object Key)> results)
        {
            for (var i = 0; i < parameterInfos.Length; i++)
            {
                object key = parameterKeys != null && i < parameterKeys.Length ? parameterKeys[i] : null;
                results.Add((parameterInfos[i].ParameterType, key));
            }
        }

        private void AppendFieldSites(object injectTypeInfo, Type infoType, List<(Type DependencyType, object Key)> results)
        {
            object fieldsObj = GetPublicInstanceField(infoType, injectTypeInfo, "InjectFields");
            if (fieldsObj is not IEnumerable enumerable)
            {
                return;
            }

            foreach (object injectFieldInfo in enumerable)
            {
                AppendOneFieldSite(injectFieldInfo, results);
            }
        }

        private void AppendOneFieldSite(object injectFieldInfo, List<(Type DependencyType, object Key)> results)
        {
            if (injectFieldInfo == null)
            {
                return;
            }

            Type fieldInfoType = injectFieldInfo.GetType();
            Type fieldDependencyType = (Type)GetPublicInstanceProperty(fieldInfoType, injectFieldInfo, "FieldType");
            object key = GetPublicInstanceField(fieldInfoType, injectFieldInfo, "Key");
            if (fieldDependencyType != null)
            {
                results.Add((fieldDependencyType, key));
            }
        }

        private void AppendPropertySites(object injectTypeInfo, Type infoType, List<(Type DependencyType, object Key)> results)
        {
            object propertiesObj = GetPublicInstanceField(infoType, injectTypeInfo, "InjectProperties");
            if (propertiesObj is not IEnumerable enumerable)
            {
                return;
            }

            foreach (object injectPropertyInfo in enumerable)
            {
                AppendOnePropertySite(injectPropertyInfo, results);
            }
        }

        private void AppendOnePropertySite(object injectPropertyInfo, List<(Type DependencyType, object Key)> results)
        {
            if (injectPropertyInfo == null)
            {
                return;
            }

            Type propertyInfoType = injectPropertyInfo.GetType();
            Type propertyDependencyType = (Type)GetPublicInstanceProperty(propertyInfoType, injectPropertyInfo, "PropertyType");
            object key = GetPublicInstanceField(propertyInfoType, injectPropertyInfo, "Key");
            if (propertyDependencyType != null)
            {
                results.Add((propertyDependencyType, key));
            }
        }

        private Type FindNestedOrTopLevelType(Assembly assembly, string ns, string typeName)
        {
            try
            {
                foreach (Type t in assembly.GetTypes())
                {
                    if (MatchesNamespaceAndName(t, ns, typeName))
                    {
                        return t;
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                return FindTypeInPartialLoad(ex.Types, ns, typeName);
            }

            return null;
        }

        private Type FindTypeInPartialLoad(Type[] types, string ns, string typeName)
        {
            if (types == null)
            {
                return null;
            }

            foreach (Type t in types)
            {
                if (t != null && MatchesNamespaceAndName(t, ns, typeName))
                {
                    return t;
                }
            }

            return null;
        }

        private object GetPublicInstanceField(Type declaringType, object target, string name)
        {
            FieldInfo field = declaringType.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            return field?.GetValue(target);
        }

        private object GetPublicInstanceProperty(Type declaringType, object target, string name)
        {
            PropertyInfo property = declaringType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return property?.GetValue(target);
        }

        private bool MatchesNamespaceAndName(Type t, string ns, string typeName)
        {
            return t.Namespace == ns && t.Name == typeName;
        }
    }
}
