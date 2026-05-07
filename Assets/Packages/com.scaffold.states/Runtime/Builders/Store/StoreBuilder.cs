#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.States
{
    public class StoreBuilder
    {
        private IStateEventHandler? eventHandler;
        private List<BaseSlice> entries = new List<BaseSlice>();
        private MutatorRegistry? mutatorRegistry;
        private IMutatorDispatcher? mutatorDispatcher;
        private readonly HashSet<(Reference Reference, Type StateType)> registeredAggregates = new();
        private readonly HashSet<(Reference Reference, Type StateType)> registeredCanonical = new();
        private readonly List<Action<ICatalog>> catalogConfigurators = new List<Action<ICatalog>>();

        public void AddEventHandler(IStateEventHandler eventHandler)
        {
            this.eventHandler = eventHandler;
        }

        public void UseMutatorDispatcher(IMutatorDispatcher dispatcher)
        {
            mutatorDispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public void RegisterMutator<TState, TPayload>(Mutator<TState, TPayload> mutator) where TState : State
        {
            if (mutator is null)
            {
                throw new ArgumentNullException(nameof(mutator));
            }

            mutatorRegistry ??= new MutatorRegistry();
            mutatorRegistry.Register(mutator);
        }

        public void RegisterAggregate(IAggregateProvider provider)
        {
            RegisterAggregate(Reference.Null, provider);
        }

        public void RegisterAggregate(Reference key, IAggregateProvider provider)
        {
            if (provider is null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            RegisterAggregate(key, new AggregateSlice(key, provider));
        }

        public void RegisterAggregate(AggregateSlice aggregateSlice)
        {
            RegisterAggregate(Reference.Null, aggregateSlice);
        }

        public void RegisterAggregate(Reference key, AggregateSlice aggregateSlice)
        {
            if (aggregateSlice is null)
            {
                throw new ArgumentNullException(nameof(aggregateSlice));
            }

            if (!key.Equals(aggregateSlice.Reference))
            {
                throw new ArgumentException("Aggregate slice reference must match the registration key.", nameof(key));
            }

            Type stateType = aggregateSlice.StateType;
            if (!registeredAggregates.Add((key, stateType)))
            {
                throw new InvalidOperationException(
                    $"An aggregate slice for state type {stateType.Name} is already registered at this reference.");
            }

            entries.Add(aggregateSlice);
        }

        public void AddState(State state)
        {
            AddState(Reference.Null, state);
        }

        public void AddState(Reference reference, State state)
        {
            var slice = Slice.Create(reference, state);
            Type stateType = slice.StateType;
            Reference key = slice.Reference;
            if (!registeredCanonical.Add((key, stateType)))
            {
                throw new InvalidOperationException(
                    $"A canonical slice for state type {stateType.Name} is already registered at this reference.");
            }

            entries.Add(slice);
        }

        public void RegisterCatalogFactory<T>(ICatalogFactory<T> factory)
        {
            if (factory is null) throw new ArgumentNullException(nameof(factory));
            catalogConfigurators.Add(catalog => catalog.RegisterFactory(factory));
        }

        public void RegisterCatalogStub<T>(T stub)
        {
            if (stub is null) throw new ArgumentNullException(nameof(stub));
            catalogConfigurators.Add(catalog => catalog.RegisterStub(stub));
        }

        public Store Build()
        {
            IStateEventHandler stateHandler = eventHandler ?? GetDefaultStateEventHandler();
            var store = new Store(stateHandler, mutatorRegistry ?? new MutatorRegistry(), mutatorDispatcher, entries.ToArray());
            for (int i = 0; i < catalogConfigurators.Count; i++)
            {
                catalogConfigurators[i](store.Catalog);
            }
            return store;
        }

        private IStateEventHandler GetDefaultStateEventHandler()
        {
            return StateEventHandlerFactory.CreateDefault();
        }
    }
}
