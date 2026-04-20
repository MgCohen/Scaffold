using System;
using VContainer.Unity;

namespace Scaffold.LayeredScope.Internal
{
    internal sealed class LayerEntry
    {
        public LayerEntry(IScopeLayer layer, LifetimeScope scope, IAsyncInitializable[] inits, IAsyncDisposable[] disposables, LayerPublisher publisher)
        {
            Layer = layer;
            Scope = scope;
            OwnedInitializables = inits;
            OwnedDisposables = disposables;
            Publisher = publisher;
        }

        public IScopeLayer Layer { get; }
        public LifetimeScope Scope { get; }
        public IAsyncInitializable[] OwnedInitializables { get; }
        public IAsyncDisposable[] OwnedDisposables { get; }
        public LayerPublisher Publisher { get; }

        public static LayerEntry CreateRoot(LifetimeScope root)
        {
            return new LayerEntry(
                null,
                root,
                Array.Empty<IAsyncInitializable>(),
                Array.Empty<IAsyncDisposable>(),
                new LayerPublisher());
        }
    }
}
