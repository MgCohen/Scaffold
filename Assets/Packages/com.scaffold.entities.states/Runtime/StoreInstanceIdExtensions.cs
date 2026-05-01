using System;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public static class StoreInstanceIdExtensions
    {
        public static void RegisterSlice(this Store store, InstanceId entityId, State state)
        {
            store.RegisterSlice(EntityStateReference.From(entityId), state);
        }

        public static TState Get<TState>(this Store store, InstanceId entityId) where TState : BaseState
        {
            return store.Get<TState>(EntityStateReference.From(entityId));
        }

        public static void Subscribe<TState>(this Store store, InstanceId entityId, System.Action<IReference, TState, StateChangeEvent> action) where TState : BaseState
        {
            store.Subscribe(EntityStateReference.From(entityId), action);
        }

        public static void Unsubscribe<TState>(this Store store, InstanceId entityId, System.Action<IReference, TState, StateChangeEvent> action) where TState : BaseState
        {
            store.Unsubscribe(EntityStateReference.From(entityId), action);
        }

        public static void Execute<TPayload>(this Store store, InstanceId entityId, TPayload payload)
        {
            store.Execute(EntityStateReference.From(entityId), payload);
        }

        public static void ExecuteMutator<TState>(this Store store, InstanceId entityId, Mutator<TState> mutator) where TState : State
        {
            store.ExecuteMutator(EntityStateReference.From(entityId), mutator);
        }

        public static void ExecuteMutator<TState, TPayload>(this Store store, InstanceId entityId, Mutator<TState, TPayload> mutator, TPayload payload) where TState : State
        {
            store.ExecuteMutator(EntityStateReference.From(entityId), mutator, payload);
        }

        public static bool UnregisterSlice<TState>(this Store store, InstanceId entityId) where TState : State
        {
            return store.UnregisterSlice<TState>(EntityStateReference.From(entityId));
        }

        public static bool UnregisterSlice(this Store store, InstanceId entityId, Type stateType)
        {
            return store.UnregisterSlice(EntityStateReference.From(entityId), stateType);
        }
    }
}
