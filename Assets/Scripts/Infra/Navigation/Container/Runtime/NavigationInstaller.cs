using UnityEngine;
using Scaffold.Containers;

namespace Scaffold.Navigation.Container
{
    public class NavigationInstaller : Installer
    {
        public NavigationInstaller(NavigationSettings settings)
        {
            this.settings = settings;
        }
        
        private NavigationSettings settings;

        public override void Install(IContainerBuilder builder, Transform holder)
        {
            builder.Register<INavigation, NavigationController>(ContainerLifetime.Scoped).WithParameter<NavigationSettings>(settings).WithParameter<Transform>(holder);
            builder.Register<NavigationInjection>(ContainerLifetime.Scoped).AsImplementedInterfaces();
        }
    }
}
