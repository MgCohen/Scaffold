using System;
using System.Collections.Generic;
using System.Linq;

namespace Scaffold.Types
{
    public class TypeUtility
    {
        public static IEnumerable<Type> GetTypesDerivedFrom<T>(bool includeAbstract, bool includeSource)
        {
            return GetAllDerivedTypes(typeof(T), includeAbstract);
        }

        private static IEnumerable<Type> GetAllDerivedTypes(Type type, bool includeAbstract = false)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                   .SelectMany(s => s.GetTypes())
                   .Where(p => type.IsAssignableFrom(p) && (!p.IsAbstract || includeAbstract) && !p.IsInterface);
        }
    }
}
