using System;
using UnityEngine;

namespace Scaffold.Containers.Samples
{
    public class ContainersUseCases
    {
        public void UseCaseDefineContainer()
        {
            AppContainer container = new AppContainer();
            NullRegistry registry = new NullRegistry();
            container.Build(registry, null);
        }

        public void UseCaseDefineInstaller()
        {
            AppInstaller installer = new AppInstaller();
            NullRegistry registry = new NullRegistry();
            installer.Install(registry, null);
        }

        private class AppContainer : Container
        {
            public override void Build(IContainerRegistry registry, Transform holder)
            {
                registry.Register<AppContainer>(ContainerLifetime.Singleton);
            }
        }

        private class AppInstaller : Installer
        {
            public override void Install(IContainerRegistry registry, Transform holder)
            {
                registry.Register<AppInstaller>(ContainerLifetime.Singleton);
            }
        }

        private class NullRegistry : IContainerRegistry
        {
            public IRegistrationBuilder<T> Register<T>(ContainerLifetime lifetime)
            {
                return new NullBuilder<T>();
            }

            public IRegistrationBuilder<TService> Register<TService, TImpl>(ContainerLifetime lifetime) where TImpl : TService
            {
                return new NullBuilder<TService>();
            }

            public IRegistrationBuilder<T> Register<T>(Func<IContainerResolver, T> factory, ContainerLifetime lifetime)
            {
                return new NullBuilder<T>();
            }

            public IRegistrationBuilder<TEntryPoint> RegisterEntryPoint<TEntryPoint>(ContainerLifetime lifetime) where TEntryPoint : class
            {
                return new NullBuilder<TEntryPoint>();
            }

            public void RegisterBuildCallback(Action<IContainerResolver> callback)
            {
            }
        }

        private class NullBuilder<T> : IRegistrationBuilder<T>
        {
            public IRegistrationBuilder<T> WithParameter<TParam>(TParam value)
            {
                return this;
            }

            public IRegistrationBuilder<T> AsImplementedInterfaces()
            {
                return this;
            }
        }
    }
}
