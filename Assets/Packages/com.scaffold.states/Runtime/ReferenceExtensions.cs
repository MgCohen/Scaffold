#nullable enable
using System;

namespace Scaffold.States
{
    public static class ReferenceExtensions
    {
        public static TState GetSlice<TState>(this Reference @ref, IStoreScope scope) where TState : BaseState
        {
            if (scope is null) throw new ArgumentNullException(nameof(scope));
            return scope.Get<TState>(@ref);
        }

        public static bool TryGetSlice<TState>(this Reference @ref, IStoreScope scope, out TState state) where TState : BaseState
        {
            if (scope is null) throw new ArgumentNullException(nameof(scope));
            return scope.TryGet(@ref, out state);
        }
    }
}
