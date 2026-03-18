using Scaffold.Containers;
using UnityEngine;

namespace Scaffold.LifeCycle
{
    /// <summary>
    /// Handles the dependency injection registration for the LifeCycle management system.
    /// The main goal is to bind the LifeCycleManager as an entry point for the application.
    /// It is used during the bootstrap process to enable automatic initialization of system components.
    /// </summary>
    public class LifeCycleInstaller : Installer
    {
        /// <summary>
        /// Installs the LifeCycle-related services into the container registry.
        /// </summary>
        /// <param name="registry">The container registry to register services in.</param>
        /// <param name="holder">The transform holder for component-based services (unused here).</param>
        public override void Install(IContainerRegistry registry, Transform holder)
        {
            registry.RegisterEntryPoint<LifeCycleManager>(ContainerLifetime.Singleton);
        }
    }
}
