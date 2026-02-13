using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scaffold.Navigation.Container
{
    [Serializable]
    public class NavigationInstaller : IInstaller
    {
        [SerializeField] private NavigationSettings settings;
        [SerializeField] private Transform holder;


        public void Install(IContainerBuilder builder)
        {
            builder.Register<INavigation, NavigationController>(Lifetime.Scoped).WithParameter<NavigationSettings>(settings).WithParameter<Transform>(holder);
            builder.Register<NavigationInjection>(Lifetime.Scoped).AsImplementedInterfaces();
        }
    }
}
