using Scaffold.Events;
using Scaffold.Navigation;
using Scaffold.Navigation.Container;
using Scaffold.States;
using System;
using VContainer;
using VContainer.Unity;

namespace Sample.States
{
    [Serializable]
    public class SampleInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            BuildInfra(builder);
            BuildState(builder);
            BuildControllers(builder);
            BuildView(builder);
        }

        private void BuildInfra(IContainerBuilder builder)
        {

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
