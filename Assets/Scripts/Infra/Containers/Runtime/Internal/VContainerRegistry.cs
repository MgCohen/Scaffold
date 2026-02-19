using System;
using VContainer;
using VContainer.Unity;

namespace Scaffold.Containers
{
    internal sealed class VContainerRegistry : IContainerRegistry
    {
        private readonly VContainer.IContainerBuilder _inner;

        internal VContainerRegistry(VContainer.IContainerBuilder inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public IRegistrationBuilder<T> Register<T>(ContainerLifetime lifetime)
        {
            var registration = _inner.Register<T>(ToVContainerLifetime(lifetime));
            return new VContainerRegistrationBuilder<T>(registration);
        }

        public IRegistrationBuilder<TService> Register<TService, TImpl>(ContainerLifetime lifetime)
            where TImpl : TService
        {
            var registration = _inner.Register<TService, TImpl>(ToVContainerLifetime(lifetime));
            return new VContainerRegistrationBuilder<TService>(registration);
        }

        public IRegistrationBuilder<T> Register<T>(Func<IContainerResolver, T> factory, ContainerLifetime lifetime)
        {
            var registration = _inner.Register(
                (IObjectResolver vc) => factory(new VContainerResolver(vc)),
                ToVContainerLifetime(lifetime));
            return new VContainerRegistrationBuilder<T>(registration);
        }

        public IRegistrationBuilder<TEntryPoint> RegisterEntryPoint<TEntryPoint>(ContainerLifetime lifetime)
            where TEntryPoint : class
        {
            _inner.RegisterEntryPoint<TEntryPoint>(ToVContainerLifetime(lifetime));
            return new NoOpRegistrationBuilder<TEntryPoint>();
        }

        public void RegisterBuildCallback(Action<IContainerResolver> callback)
        {
            _inner.RegisterBuildCallback((IObjectResolver vc) => callback(new VContainerResolver(vc)));
        }

        private static Lifetime ToVContainerLifetime(ContainerLifetime lifetime)
        {
            return lifetime switch
            {
                ContainerLifetime.Singleton => Lifetime.Singleton,
                ContainerLifetime.Scoped    => Lifetime.Scoped,
                ContainerLifetime.Transient => Lifetime.Transient,
                _                           => Lifetime.Transient
            };
        }
    }
}
