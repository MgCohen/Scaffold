using Scaffold.Navigation.Contracts;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scaffold.Navigation.Container
{
    public class NavigationInstaller : IInstaller
    {
        public NavigationInstaller(Transform holder, bool registerDefaultViewModelInjector = true)
        {
            this.holder = holder;
            this.registerDefaultViewModelInjector = registerDefaultViewModelInjector;
        }

        private readonly Transform holder;
        private readonly bool registerDefaultViewModelInjector;

        public void Install(IContainerBuilder builder)
        {
            if (registerDefaultViewModelInjector)
            {
                builder.Register<SingleScopeNavigationViewModelInjector>(Lifetime.Singleton)
                    .As<INavigationViewModelInjector>();
            }

            builder.Register<INavigation, NavigationController>(Lifetime.Singleton)
                   .WithParameter<Transform>(holder);
            builder.Register<NavigationInjection>(Lifetime.Singleton).AsImplementedInterfaces();
        }
    }
}

