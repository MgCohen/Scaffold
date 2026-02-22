using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Scaffold.Types
{
    public class DependencyExtractor : IDependencyExtractor
    {
        private readonly ConcurrentDictionary<Type, Type[]> cache = new ConcurrentDictionary<Type, Type[]>();

        public IEnumerable<Type> GetConstructorDependencies(Type type)
        {
            return cache.GetOrAdd(type, AnalyzeDependencies);
        }

        private Type[] AnalyzeDependencies(Type type)
        {
            var constructors = GetConstructors(type);
            var annotatedConstructors = GetAnnotatedConstructors(constructors);

            ValidateAnnotatedConstructors(type, annotatedConstructors);

            var targetConstructor = GetTargetConstructor(constructors, annotatedConstructors);
            return GetConstructorParameters(targetConstructor);
        }

        private ConstructorInfo[] GetConstructors(Type type)
        {
            return type.GetConstructors(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private List<ConstructorInfo> GetAnnotatedConstructors(ConstructorInfo[] constructors)
        {
            return constructors.Where(c => c.CustomAttributes
                .Any(attr => attr.AttributeType.Name == "InjectAttribute")).ToList();
        }

        private void ValidateAnnotatedConstructors(Type type, List<ConstructorInfo> annotatedConstructors)
        {
            if (annotatedConstructors.Count > 1)
            {
                throw new InvalidOperationException($"Type found multiple [Inject] marked constructors, type: {type.Name}");
            }
        }

        private ConstructorInfo GetTargetConstructor(ConstructorInfo[] constructors, List<ConstructorInfo> annotatedConstructors)
        {
            var hasSingleAnnotatedConstructor = annotatedConstructors.Count == 1;
            var targetConstructor = hasSingleAnnotatedConstructor ? annotatedConstructors[0] : GetConstructorWithMostParameters(constructors);
            return targetConstructor;
        }

        private ConstructorInfo GetConstructorWithMostParameters(ConstructorInfo[] constructors)
        {
            ConstructorInfo targetConstructor = null;
            int maxParameters = -1;

            foreach (var ctor in constructors)
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length > maxParameters)
                {
                    targetConstructor = ctor;
                    maxParameters = parameters.Length;
                }
            }

            return targetConstructor;
        }

        private Type[] GetConstructorParameters(ConstructorInfo constructor)
        {
            var hasConstructor = constructor != null;
            var constructorParameters = hasConstructor ? constructor.GetParameters().Select(p => p.ParameterType).ToArray() : Array.Empty<Type>();
            return constructorParameters;
        }
    }
}
