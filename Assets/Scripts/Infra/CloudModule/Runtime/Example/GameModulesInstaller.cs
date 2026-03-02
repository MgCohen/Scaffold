using Scaffold.Containers;
using UnityEngine;

namespace Scaffold.CloudModules.Example
{
    /// <summary>
    /// Handles the dependency injection bindings for the Cloud Module layer.
    /// The main goal is to register services like the CloudCodeService and GameModulesController.
    /// It is used by the application's composition root during bootstrap to instantiate and connect backend components.
    /// </summary>
    public class GameModulesExampleInstaller : Installer
    {
        /// <summary>
        /// Installs the specific Cloud Module dependencies into the container layer.
        /// The main goal is to map interfaces to their concrete implementations contextually.
        /// It is used by the encompassing VContainer lifetime scopes during generation.
        /// </summary>
        public override void Install(IContainerRegistry registry, Transform holder)
        {
            registry.Register<SimpleController>(ContainerLifetime.Singleton)
                .AsImplementedInterfaces();
            registry.Register<CounterController>(ContainerLifetime.Singleton)
                .AsImplementedInterfaces();
            registry.Register<ReactiveCounterController>(ContainerLifetime.Singleton)
                .AsImplementedInterfaces();
        }
    }
}