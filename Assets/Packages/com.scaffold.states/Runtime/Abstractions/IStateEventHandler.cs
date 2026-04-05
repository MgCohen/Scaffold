#nullable enable
using System;

namespace Scaffold.States
{
    public interface IStateEventHandler
    {
        void Notify(IReference reference, BaseState state);

        void Subscribe<TState>(IReference reference, Action<IReference, TState> action) where TState : BaseState;

        void SubscribeAllReferences<TState>(Action<IReference, TState> action) where TState : BaseState;

        void SubscribeAny(Action<IReference, BaseState> action);
    }
}
