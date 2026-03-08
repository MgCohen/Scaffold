using System;
using VContainer;
using VContainer.Unity;

namespace Scaffold.Containers
{
    internal sealed class VContainerRegistry : IContainerRegistry
    {
        private readonly VContainer.IContainerBuilder inner;

        internal VContainerRegistry(VContainer.IContainerBuilder inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public IRegistrationBuilder<T> Register<T>(ContainerLifetime lifetime)
        {
            var vLifetime = ToVContainerLifetime(lifetime);
            var registration = inner.Register<T>(vLifetime);
            return new VContainerRegistrationBuilder<T>(registration);
        }

        public IRegistrationBuilder<TService> Register<TService, TImpl>(ContainerLifetime lifetime)
            where TImpl : TService
        {
            var vLifetime = ToVContainerLifetime(lifetime);
            var registration = inner.Register<TService, TImpl>(vLifetime);
            return new VContainerRegistrationBuilder<TService>(registration);
        }

        public IRegistrationBuilder<T> Register<T>(Func<IContainerResolver, T> factory, ContainerLifetime lifetime)
        {
            var vLifetime = ToVContainerLifetime(lifetime);
            var registration = inner.Register(vc => InvokeWithAdapter(factory, vc), vLifetime);
            return new VContainerRegistrationBuilder<T>(registration);
        }

        public IRegistrationBuilder<TEntryPoint> RegisterEntryPoint<TEntryPoint>(ContainerLifetime lifetime)
            where TEntryPoint : class
        {
            var vLifetime = ToVContainerLifetime(lifetime);
            inner.RegisterEntryPoint<TEntryPoint>(vLifetime);
            return new NoOpRegistrationBuilder<TEntryPoint>();
        }

        public void RegisterBuildCallback(Action<IContainerResolver> callback)
        {
            inner.RegisterBuildCallback(vc => InvokeCallback(callback, vc));
        }

        private T InvokeWithAdapter<T>(Func<IContainerResolver, T> factory, IObjectResolver resolver)
        {
            var adapter = new VContainerResolver(resolver);
            return factory(adapter);
        }

        private void InvokeCallback(Action<IContainerResolver> callback, IObjectResolver resolver)
        {
            var adapter = new VContainerResolver(resolver);
            callback(adapter);
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
