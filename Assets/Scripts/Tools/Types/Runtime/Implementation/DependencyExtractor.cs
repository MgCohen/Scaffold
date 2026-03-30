using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Scaffold.Types.Contracts;
namespace Scaffold.Types
{
    public class DependencyExtractor : IDependencyExtractor
    {
        private readonly ConcurrentDictionary<Type, Type[]> cache = new ConcurrentDictionary<Type, Type[]>();

        public IEnumerable<Type> GetConstructorDependencies(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return cache.GetOrAdd(type, AnalyzeDependencies);
        }

        public Type[] AnalyzeDependencies(Type type)
        {
            ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            List<ConstructorInfo> annotatedConstructors = constructors.Where(c => c.CustomAttributes.Any(attr => attr.AttributeType.Name == "InjectAttribute")).ToList();
            if (annotatedConstructors.Count > 1)
            {
                throw new InvalidOperationException($"Type found multiple [Inject] marked constructors, type: {type.Name}");
            }
            ConstructorInfo targetConstructor = annotatedConstructors.Count == 1 ? annotatedConstructors[0] : GetConstructorWithMostParameters(constructors);
            return targetConstructor != null ? targetConstructor.GetParameters().Select(p => p.ParameterType).ToArray() : Array.Empty<Type>();
        }

        private ConstructorInfo GetConstructorWithMostParameters(ConstructorInfo[] constructors)
        {
            ConstructorInfo best = null;
            int maxCount = -1;
            foreach (ConstructorInfo ctor in constructors)
            {
                int count = ctor.GetParameters().Length;
                if (count > maxCount)
                {
                    best = ctor;
                    maxCount = count;
                }
            }
            return best;
        }
    }
}



