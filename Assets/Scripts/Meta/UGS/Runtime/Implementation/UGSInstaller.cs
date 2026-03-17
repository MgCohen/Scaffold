using Scaffold.Containers;
using UnityEngine;

namespace Scaffold.UGS
{
    public class UGSInstaller : Installer
    {
        public override void Install(IContainerRegistry registry, Transform holder)
        {
            registry.Register<UGSService>(ContainerLifetime.Singleton)
                .AsImplementedInterfaces();
        }
    }
}