using Scaffold.CloudGateway;
using Scaffold.Containers;
using UnityEngine;

namespace Scaffold.GameModules
{
    public class CloudGatewayExampleInstaller : Installer
    {
        public override void Install(IContainerRegistry registry, Transform holder)
        {
            CloudGatewayInstaller gatewayInstaller = new CloudGatewayInstaller();
            gatewayInstaller.Install(registry, holder);
            
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