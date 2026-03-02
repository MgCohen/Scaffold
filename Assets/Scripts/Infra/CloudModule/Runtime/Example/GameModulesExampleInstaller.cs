using Scaffold.Containers;
using UnityEngine;

namespace Scaffold.CloudModules.Example
{
    public class GameModulesExampleInstaller : Installer
    {
        public override void Install(IContainerRegistry registry, Transform holder)
        {
            GameModulesInstaller gameModulesInstaller = new GameModulesInstaller();
            gameModulesInstaller.Install(registry, holder);
            
            registry.Register<SimpleController>(ContainerLifetime.Singleton)
                .AsImplementedInterfaces();
            registry.Register<CounterController>(ContainerLifetime.Singleton)
                .AsImplementedInterfaces();
            registry.Register<ReactiveCounterController>(ContainerLifetime.Singleton)
                .AsImplementedInterfaces();
        }
    }
}