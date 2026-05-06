#nullable enable
using System;

namespace Scaffold.States
{
    public static class StoreEntityExtensions
    {
        public static Ref<T> RegisterEntity<T>(this Store store, T entity) where T : class, ISliceProvider
        {
            if (store is null) throw new ArgumentNullException(nameof(store));
            if (entity is null) throw new ArgumentNullException(nameof(entity));

            Ref<T> @ref = store.Catalog.Register(entity);
            foreach (State state in entity.ProvideInitialSlices())
            {
                store.RegisterSlice(@ref, state);
            }
            return @ref;
        }

        public static bool UnregisterEntity<T>(this Store store, Ref<T> @ref) where T : class, ISliceProvider
        {
            if (store is null) throw new ArgumentNullException(nameof(store));
            if (@ref is null) throw new ArgumentNullException(nameof(@ref));

            if (!store.Catalog.TryResolve(@ref, out T entity))
            {
                return false;
            }

            foreach (State state in entity.ProvideInitialSlices())
            {
                store.UnregisterSlice(@ref, state.GetType());
            }
            store.Catalog.Unregister(@ref);
            return true;
        }
    }
}
