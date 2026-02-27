using Scaffold.CloudModules;
using Scaffold.Containers;
using Scaffold.AwaitableQueue;
using UnityEngine;

namespace Scaffold.CloudModules
{
    /// <summary>
    /// Handles the dependency injection bindings for the Cloud Module layer.
    /// The main goal is to register services like the CloudCodeService and GameModulesController.
    /// It is used by the application's composition root during bootstrap to instantiate and connect backend components.
    /// </summary>
    public class GameModulesInstaller : Installer
    {
        public override void Install(IContainerRegistry registry, Transform holder)
        {
            registry.Register<TaskQueueHandler>(ContainerLifetime.Singleton)
                .AsImplementedInterfaces();

            registry.Register<ICloudCodeService, CloudCodeUGSService>(ContainerLifetime.Singleton);

            registry.Register<GameModulesController>(ContainerLifetime.Singleton)
                .AsImplementedInterfaces();
        }
    }
}