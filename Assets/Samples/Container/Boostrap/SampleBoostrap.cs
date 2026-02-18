using Sample.States;
using Scaffold.Containers;
using Scaffold.Events.Container;
using Scaffold.Navigation;
using Scaffold.Navigation.Container;
using Scaffold.States;
using UnityEngine;

namespace Sample.Boostraper
{
    public class SampleBoostrap : Boostrap
    {
        [SerializeField] private NavigationSettings navigationSettings;

        protected override void Build(IContext context)
        {
            context.AddChild(new SampleInfraContainer(navigationSettings))
                   .AddChild(new SampleGameContainer());
        }
    }

    public class SampleInfraContainer : Container
    {
        private readonly NavigationSettings navigationSettings;

        public SampleInfraContainer(NavigationSettings navigationSettings)
        {
            this.navigationSettings = navigationSettings;
        }

        protected override void Build(IContainerBuilder builder, Transform holder)
        {
            new SampleNavigationInstaller(navigationSettings).Install(builder, holder);
            new EventsInstaller().Install(builder, holder);
        }
    }

    public class SampleGameContainer : Container
    {
        protected override void Build(IContainerBuilder builder, Transform holder)
        {
            new SampleInstaller().Install(builder, holder);
        }
    }


    #region Installers

    public class SampleInstaller : Installer
    {
        public override void Install(IContainerBuilder builder, Transform holder)
        {
            builder.Register<Store>(BuildStore, ContainerLifetime.Scoped);
            builder.Register<ITurnHandler, TurnHandler>(ContainerLifetime.Scoped);
            builder.RegisterEntryPoint<SampleGameManager>(ContainerLifetime.Scoped);
        }

        private Store BuildStore(IContainerResolver resolver)
        {
            StoreBuilder storeBuilder = new StoreBuilder();
            TurnState turnState = new TurnState()
            {
                CurrentTurn = 0,
                //ActivePlayer = match.Players[0],
                //PriorityPlayer = match.Players[0],

                //CurrentPhase = match.Phases[0],
                //CurrentStep = match.Phases[0],
            };
            Store store = storeBuilder.BuildSlice(turnState)
                                      .Build();

            return store;
        }
    }

    public class SampleNavigationInstaller : Installer
    {
        private readonly NavigationSettings settings;

        public SampleNavigationInstaller(NavigationSettings settings)
        {
            this.settings = settings;
        }

        public override void Install(IContainerBuilder builder, Transform holder)
        {
            builder.Register<INavigation, NavigationController>(ContainerLifetime.Scoped).WithParameter<NavigationSettings>(settings).WithParameter<Transform>(holder);
            builder.Register<NavigationInjection>(ContainerLifetime.Scoped).AsImplementedInterfaces();
        }
    }
    #endregion
}
