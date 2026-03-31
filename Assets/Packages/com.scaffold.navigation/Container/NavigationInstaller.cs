using Scaffold.Navigation.Contracts;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scaffold.Navigation.Container
{
    public class NavigationInstaller : IInstaller
    {
        public NavigationInstaller(Transform holder)
        {
            this.holder = holder;
        }

        private readonly Transform holder;

        public void Install(IContainerBuilder builder)
        {
            builder.Register<INavigation, NavigationController>(Lifetime.Singleton)
                   .WithParameter<Transform>(holder);
            builder.Register<NavigationInjection>(Lifetime.Singleton).AsImplementedInterfaces();
        }
    }
}

