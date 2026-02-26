using Scaffold.CloudModules.Shared;
using Scaffold.Containers;
using Scaffold.AwaitableQueue;
using UnityEngine;

namespace Scaffold.GameModules.Shared
{
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