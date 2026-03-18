using Scaffold.Containers;
using Scaffold.LifeCycle;
using UnityEngine;

namespace Scaffold.GameModules
{
    public class GameModulesInstaller : Installer
    {
        public override void Install(IContainerRegistry registry, Transform holder)
        {
            //registry.Register<IGameModulesService, GameModulesController>(ContainerLifetime.Singleton)
            //    .AsImplementedInterfaces();
            registry.Register<GameModulesController>(ContainerLifetime.Singleton);
            registry.Register<IGameModulesService>(resolver => resolver.Resolve<GameModulesController>(), ContainerLifetime.Singleton);
            registry.Register<IController>(resolver => resolver.Resolve<GameModulesController>(), ContainerLifetime.Singleton);
        }
    }
}