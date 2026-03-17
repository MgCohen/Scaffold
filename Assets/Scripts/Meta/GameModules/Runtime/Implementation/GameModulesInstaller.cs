using Scaffold.Containers;
using UnityEngine;

namespace Scaffold.GameModules
{
    public class GameModulesInstaller : Installer
    {
        public override void Install(IContainerRegistry registry, Transform holder)
        {
            registry.Register<IGameModulesService, GameModulesController>(ContainerLifetime.Singleton)
                .AsImplementedInterfaces();
        }
    }
}