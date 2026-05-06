#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;

namespace Scaffold.States
{
    public static class RefExtensions
    {
        public static T Resolve<T>(this Ref<T> @ref, IStoreScope scope)
        {
            if (scope is null) throw new ArgumentNullException(nameof(scope));
            return scope.Catalog.Resolve(@ref);
        }

        public static bool TryResolve<T>(this Ref<T> @ref, IStoreScope scope, [MaybeNullWhen(false)] out T obj)
        {
            if (scope is null) throw new ArgumentNullException(nameof(scope));
            return scope.Catalog.TryResolve(@ref, out obj);
        }
    }
}
