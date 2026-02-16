using System;
using VContainer;
using VContainer.Unity;

namespace Scaffold.Containers.Adapters
{
    /// <summary>
    /// Adapter that wraps VContainer.IContainerBuilder and exposes the project-level IContainerBuilder interface.
    /// </summary>
    internal sealed class VContainerBuilderAdapter : IContainerBuilder
    {
        private readonly VContainer.IContainerBuilder _inner;

        public VContainerBuilderAdapter(VContainer.IContainerBuilder inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public IRegistrationBuilder<T> Register<T>(ContainerLifetime lifetime)
        {
            var registration = _inner.Register<T>(ToVContainerLifetime(lifetime));
            return new VContainerRegistrationBuilderAdapter<T>(registration);
        }

        public IRegistrationBuilder<TService> Register<TService, TImplementation>(ContainerLifetime lifetime)
            where TImplementation : TService
        {
            var registration = _inner.Register<TService, TImplementation>(ToVContainerLifetime(lifetime));
            return new VContainerRegistrationBuilderAdapter<TService>(registration);
        }

        public IRegistrationBuilder<T> Register<T>(Func<IContainerResolver, T> factory, ContainerLifetime lifetime)
        {
            var registration = _inner.Register((IObjectResolver vc) => factory(new VContainerResolverAdapter(vc)), ToVContainerLifetime(lifetime));
            return new VContainerRegistrationBuilderAdapter<T>(registration);
        }

        // Note: VContainer's RegisterEntryPoint does not return a registration builder; we only mirror usage needed here.
        public IRegistrationBuilder<TEntryPoint> RegisterEntryPoint<TEntryPoint>(ContainerLifetime lifetime)
            where TEntryPoint : class
        {
            _inner.RegisterEntryPoint<TEntryPoint>();
            // There is no underlying fluent builder; return a no-op adapter.
            return new NoOpRegistrationBuilderAdapter<TEntryPoint>();
        }

        public void RegisterBuildCallback(Action<IContainerResolver> callback)
        {
            _inner.RegisterBuildCallback((IObjectResolver vc) => callback(new VContainerResolverAdapter(vc)));
        }

        private static Lifetime ToVContainerLifetime(ContainerLifetime lifetime)
        {
            return lifetime switch
            {
                ContainerLifetime.Singleton => Lifetime.Singleton,
                ContainerLifetime.Scoped => Lifetime.Scoped,
                ContainerLifetime.Transient => Lifetime.Transient,
                _ => Lifetime.Transient
            };
        }
    }
}

