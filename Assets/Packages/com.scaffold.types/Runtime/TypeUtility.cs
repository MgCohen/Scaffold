using System;
using System.Collections.Generic;
using System.Linq;

namespace Scaffold.Types
{
    public static class TypeUtility
    {
        public static IEnumerable<Type> GetTypesDerivedFrom<T>(bool includeAbstract, bool includeSource)
        {
            if (typeof(T) == null)
            {
                throw new InvalidOperationException("Requested type was not resolved.");
            }
            ValidateTypeLookupRequest<T>();
            return GetAllDerivedTypes(typeof(T), includeAbstract, includeSource);
        }

        private static IEnumerable<Type> GetAllDerivedTypes(Type type, bool includeAbstract, bool includeSource)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                   .SelectMany(s => s.GetTypes())
                   .Where(p => type.IsAssignableFrom(p)
                               && (includeSource || p != type)
                               && (!p.IsAbstract || includeAbstract)
                               && !p.IsInterface);
        }

        private static void ValidateTypeLookupRequest<T>()
        {
            var requestedType = typeof(T);
            if (requestedType == null) throw new InvalidOperationException("Requested type was not resolved.");
        }
    }
}

