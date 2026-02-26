using Scaffold.CloudModules.Shared;
using Scaffold.Containers;
using UnityEngine;

namespace Scaffold.GameModules.Shared
{
    public class GameModulesInstaller : Installer
    {
        public override void Install(IContainerRegistry registry, Transform holder)
        {
            registry.Register<ICloudCodeService, CloudCodeUGSService>(ContainerLifetime.Singleton);
            registry.Register<GameModulesController>(ContainerLifetime.Singleton)
                .AsImplementedInterfaces();
        }
    }
}