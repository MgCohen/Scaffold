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
        private readonly HashSet<(IReference Reference, Type StateType)> registeredAggregates = new();
        private readonly HashSet<(IReference Reference, Type StateType)> registeredCanonical = new();

        public void AddEventHandler(IStateEventHandler eventHandler)
        {
            this.eventHandler = eventHandler;
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

        public void RegisterAggregate(IReference key, IAggregateProvider provider)
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

        public void RegisterAggregate(IReference key, AggregateSlice aggregateSlice)
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

        public void AddState(IReference reference, State state)
        {
            var slice = Slice.Create(reference, state);
            Type stateType = slice.StateType;
            IReference key = slice.Reference;
            if (!registeredCanonical.Add((key, stateType)))
            {
                throw new InvalidOperationException(
                    $"A canonical slice for state type {stateType.Name} is already registered at this reference.");
            }

            entries.Add(slice);
        }

        public Store Build()
        {
            IStateEventHandler stateHandler = eventHandler ?? GetDefaultStateEventHandler();
            return new Store(stateHandler, mutatorRegistry ?? new MutatorRegistry(), entries.ToArray());
        }

        private IStateEventHandler GetDefaultStateEventHandler()
        {
            return StateEventHandlerFactory.CreateDefault();
        }
    }
}
