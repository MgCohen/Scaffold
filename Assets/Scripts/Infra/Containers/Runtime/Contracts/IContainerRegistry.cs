using System;

namespace Scaffold.Containers
{
    public interface IContainerRegistry
    {
        IRegistrationBuilder<T> Register<T>(ContainerLifetime lifetime);

        IRegistrationBuilder<TService> Register<TService, TImpl>(ContainerLifetime lifetime)
            where TImpl : TService;

        IRegistrationBuilder<T> Register<T>(Func<IContainerResolver, T> factory, ContainerLifetime lifetime);

        IRegistrationBuilder<TEntryPoint> RegisterEntryPoint<TEntryPoint>(ContainerLifetime lifetime)
            where TEntryPoint : class;

        void RegisterBuildCallback(Action<IContainerResolver> callback);
    }
}
