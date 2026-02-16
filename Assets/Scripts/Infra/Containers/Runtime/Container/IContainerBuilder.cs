using System;

namespace Scaffold.Containers
{
    /// <summary>
    /// Project-level abstraction over the underlying DI container builder.
    /// Only exposes the subset of functionality used by Scaffold containers and installers.
    /// </summary>
    public interface IContainerBuilder
    {
        IRegistrationBuilder<T> Register<T>(ContainerLifetime lifetime);

        IRegistrationBuilder<TService> Register<TService, TImplementation>(ContainerLifetime lifetime)
            where TImplementation : TService;

        IRegistrationBuilder<T> Register<T>(Func<IContainerResolver, T> factory, ContainerLifetime lifetime);

        IRegistrationBuilder<TEntryPoint> RegisterEntryPoint<TEntryPoint>(ContainerLifetime lifetime)
            where TEntryPoint : class;

        void RegisterBuildCallback(Action<IContainerResolver> callback);
    }
}

