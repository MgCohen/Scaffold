using Scaffold.Events;
using Scaffold.Navigation;
using Scaffold.Navigation.Container;
using Scaffold.States;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Sample.States
{
    public class SampleBuilder : LifetimeScope
    {
        [SerializeField] private NavigationSettings settings;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            BuildInfra(builder);
            BuildState(builder);
            BuildControllers(builder);
            BuildView(builder);
        }

        private void BuildInfra(IContainerBuilder builder)
        {
            builder.Register<IEventBus, EventController>(Lifetime.Scoped);
            builder.Register<INavigation, NavigationController>(Lifetime.Scoped).WithParameter<NavigationSettings>(settings).WithParameter<Transform>(transform);
            builder.Register<NavigationInjection>(Lifetime.Scoped).AsImplementedInterfaces();
        }

        private void BuildState(IContainerBuilder builder)
        {
            builder.Register<Store>(BuildStore, Lifetime.Scoped);
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

        private void BuildControllers(IContainerBuilder builder)
        {
            builder.Register<ITurnHandler, TurnHandler>(Lifetime.Scoped);
            builder.RegisterEntryPoint<SampleGameManager>(Lifetime.Scoped);
        }

        private void BuildView(IContainerBuilder builder)
        {
            //nothing for now, VM are created by hand and injected by proxy
            //views should not require injection for now
        }
    }
}
