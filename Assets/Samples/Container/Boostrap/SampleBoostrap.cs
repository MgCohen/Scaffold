using Sample.States;
using Scaffold.Containers;
using Scaffold.Events;
using Scaffold.Navigation;
using Scaffold.Navigation.Container;
using Scaffold.States;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Container = Scaffold.Containers.Container;

namespace Sample.Boostraper
{
    public class SampleBoostrap : Boostrap
    {
        protected override void Build(IContext context)
        {
            context.AddChild(new SampleInfraContainer())
                   .AddChild(new SampleGameContainer());
        }
    }

    public class SampleInfraContainer : Container
    {
        protected override void Build(IContainerBuilder builder, ContainerConfig config, Transform holder)
        {
            new SampleNavigationInstaller().Install(builder, config, holder); //use installers inside containers, OR
            builder.Register<IEventBus, EventController>(Lifetime.Scoped); //use simple registers
        }
    }

    public class SampleGameContainer : Container
    {
        protected override void Build(IContainerBuilder builder, ContainerConfig config, Transform holder)
        {
            new SampleInstaller().Install(builder, config, holder);
        }
    }


    #region Installers

    public class SampleInstaller : Installer
    {
        public override void Install(IContainerBuilder builder, ContainerConfig config, Transform holder)
        {
            builder.Register<Store>(BuildStore, Lifetime.Scoped);
            builder.Register<ITurnHandler, TurnHandler>(Lifetime.Scoped);
            builder.RegisterEntryPoint<SampleGameManager>(Lifetime.Scoped);
        }

        private Store BuildStore(IObjectResolver resolver)
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

    public class SampleNavigationInstaller: Installer
    {

        public override void Install(IContainerBuilder builder, ContainerConfig config, Transform holder)
        {
            NavigationSettings settings = config.Fetch<NavigationSettings>();
            builder.Register<INavigation, NavigationController>(Lifetime.Scoped).WithParameter<NavigationSettings>(settings).WithParameter<Transform>(holder);
            builder.Register<NavigationInjection>(Lifetime.Scoped).AsImplementedInterfaces();
        }
    }
    #endregion
}