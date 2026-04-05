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

        public void AddEventHandler(IStateEventHandler eventHandler)
        {
            this.eventHandler = eventHandler;
        }

        public void RegisterMutator<TState, TPayload>(Mutator<TState, TPayload> mutator, IReference? reference = null) where TState : State
        {
            mutatorRegistry ??= new MutatorRegistry();
            mutatorRegistry.Register(mutator, reference);
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
            entries.Add(Slice.Create(reference, state));
        }

        public Store Build()
        {
            IStateEventHandler stateHandler = eventHandler ?? GetDefaultStateEventHandler();
            return new Store(stateHandler, mutatorRegistry ?? new MutatorRegistry(), entries.ToArray());
        }

        private IStateEventHandler GetDefaultStateEventHandler()
        {
            return new StateEventHandler();
        }
    }
}
