using Scaffold.Containers;
using Scaffold.Utility.AwaitableQueue;
using UnityEngine;

namespace Scaffold.CloudGateway
{
    /// <summary>
    /// Handles the dependency injection bindings for the Cloud Gateway layer.
    /// The main goal is to register services like the CloudCodeService and CloudGatewayController.
    /// It is used by the application's composition root during bootstrap to instantiate and connect backend components.
    /// </summary>
    public class CloudGatewayInstaller : Installer
    {
        public override void Install(IContainerRegistry registry, Transform holder)
        {
            registry.Register<TaskQueueHandler>(ContainerLifetime.Singleton)
                .AsImplementedInterfaces();

            registry.Register<ICloudService, CloudUgsService>(ContainerLifetime.Singleton);
            registry.Register<ICloudGatewayAuthKey, CloudGatewayAuthKey>(ContainerLifetime.Singleton);
        }
    }
}