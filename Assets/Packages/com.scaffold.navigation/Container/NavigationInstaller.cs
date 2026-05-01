using Scaffold.Navigation;
using Scaffold.Navigation.Contracts;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scaffold.Navigation.Container
{
    public class NavigationInstaller : IInstaller
    {
        public NavigationInstaller(Transform holder, NavigationSettings settings = null)
        {
            this.holder = holder;
            this.settings = settings;
        }

        private readonly Transform holder;
        private readonly NavigationSettings settings;

        public void Install(IContainerBuilder builder)
        {
            if (settings != null)
            {
                builder.RegisterInstance(settings);
            }

            builder.Register<INavigation, NavigationController>(Lifetime.Singleton)
                   .WithParameter<Transform>(holder);
            builder.Register<NavigationInjection>(Lifetime.Singleton).AsImplementedInterfaces();
        }
    }
}
