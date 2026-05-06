#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.States
{
    public interface IStateEventHandler
    {
        void Notify(Reference reference, BaseState state, StateChangeEvent changeEvent);

        void Subscribe<TState>(Reference reference, Action<Reference, TState, StateChangeEvent> action) where TState : BaseState;

        void Subscribe<TState>(Reference reference, Action<TState, StateChangeEvent> action) where TState : BaseState;

        void Subscribe<TState>(Reference reference, Action<TState> action) where TState : BaseState;

        void Unsubscribe<TState>(Reference reference, Action<Reference, TState, StateChangeEvent> action) where TState : BaseState;

        void Unsubscribe<TState>(Reference reference, Action<TState, StateChangeEvent> action) where TState : BaseState;

        void Unsubscribe<TState>(Reference reference, Action<TState> action) where TState : BaseState;

        void SubscribeAllReferences<TState>(Action<Reference, TState, StateChangeEvent> action) where TState : BaseState;

        void UnsubscribeAllReferences<TState>(Action<Reference, TState, StateChangeEvent> action) where TState : BaseState;

        void SubscribeAny(Action<Reference, BaseState, StateChangeEvent> action);
    }
}
