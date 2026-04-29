#nullable enable
using System;

namespace Scaffold.States
{
    public interface IStateEventHandler
    {
        void Notify(IReference reference, BaseState state, StateChangeEvent changeEvent);

        void Notify(IReference reference, BaseState state)
        {
            Notify(reference, state, StateChangeEvent.Updated);
        }

        void Subscribe<TState>(IReference reference, Action<IReference, TState, StateChangeEvent> action) where TState : BaseState;

        void Unsubscribe<TState>(IReference reference, Action<IReference, TState, StateChangeEvent> action) where TState : BaseState;

        void SubscribeAllReferences<TState>(Action<IReference, TState, StateChangeEvent> action) where TState : BaseState;

        void SubscribeAny(Action<IReference, BaseState, StateChangeEvent> action);
    }
}
