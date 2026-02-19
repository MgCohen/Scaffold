using Sample.States;
using Scaffold.Containers;
using Scaffold.Events.Container;
using Scaffold.Navigation;
using Scaffold.Navigation.Container;
using Scaffold.States;
using UnityEngine;
using VContainer.Unity;

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

        public override void Build(IContainerRegistry registry, Transform holder)
        {
            new NavigationInstaller(navigationSettings).Install(registry, holder);
            new EventsInstaller().Install(registry, holder);
        }
    }

    public class SampleGameContainer : Container
    {
        public override void Build(IContainerRegistry registry, Transform holder)
        {
            new SampleInstaller().Install(registry, holder);
        }
    }


    #region Installers

    public class SampleInstaller : Installer
    {
        public override void Install(IContainerRegistry registry, Transform holder)
        {
            registry.Register<Store>(BuildStore, ContainerLifetime.Scoped);
            registry.Register<ITurnHandler, TurnHandler>(ContainerLifetime.Scoped);
            registry.RegisterEntryPoint<SampleGameManager>(ContainerLifetime.Scoped);
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
    #endregion
}
