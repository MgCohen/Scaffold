using System;
using VContainer.Unity;

namespace Scaffold.LayeredScope.Internal
{
    internal sealed class LayerEntry
    {
        public LayerEntry(IScopeLayer layer, LifetimeScope scope, IAsyncInitializable[] inits, IAsyncDisposable[] disposables)
        {
            Layer = layer;
            Scope = scope;
            OwnedInitializables = inits;
            OwnedDisposables = disposables;
        }

        public IScopeLayer Layer { get; }
        public LifetimeScope Scope { get; }
        public IAsyncInitializable[] OwnedInitializables { get; }
        public IAsyncDisposable[] OwnedDisposables { get; }

        public static LayerEntry CreateRoot(LifetimeScope root)
        {
            return new LayerEntry(null, root, Array.Empty<IAsyncInitializable>(), Array.Empty<IAsyncDisposable>());
        }
    }
}
