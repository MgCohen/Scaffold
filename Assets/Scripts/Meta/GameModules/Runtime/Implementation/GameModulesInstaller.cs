using Scaffold.Containers;
using Scaffold.LifeCycle;
using UnityEngine;

namespace Scaffold.GameModules
{
    public class GameModulesInstaller : Installer
    {
        public override void Install(IContainerRegistry registry, Transform holder)
        {
            registry.Register<GameModulesController>(ContainerLifetime.Singleton);
            registry.Register<IGameModulesService>(resolver => resolver.Resolve<GameModulesController>(), ContainerLifetime.Singleton);
            registry.Register<IController>(resolver => resolver.Resolve<GameModulesController>(), ContainerLifetime.Singleton);
            
            registry.Register<AdsController>(ContainerLifetime.Singleton)
                .AsSelf()
                .AsImplementedInterfaces();
            registry.Register<AdsConfigController>(ContainerLifetime.Singleton)
                .AsSelf()
                .AsImplementedInterfaces();
            registry.Register<GoldController>(ContainerLifetime.Singleton)
                .AsSelf()
                .AsImplementedInterfaces();
            registry.Register<GoldConfigController>(ContainerLifetime.Singleton)
                .AsSelf()
                .AsImplementedInterfaces();
            registry.Register<LevelController>(ContainerLifetime.Singleton)
                .AsSelf()
                .AsImplementedInterfaces();
            registry.Register<LevelConfigController>(ContainerLifetime.Singleton)
                .AsSelf()
                .AsImplementedInterfaces();
            registry.Register<TutorialController>(ContainerLifetime.Singleton)
                .AsSelf()
                .AsImplementedInterfaces();
            registry.Register<TutorialConfigController>(ContainerLifetime.Singleton)
                .AsSelf()
                .AsImplementedInterfaces();
            registry.Register<GlobalConfigController>(ContainerLifetime.Singleton)
                .AsSelf()
                .AsImplementedInterfaces();
        }
    }
}