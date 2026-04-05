using System;

namespace Scaffold.States
{
    public interface IStateEventHandler
    {
        void Notify(IReference reference, State state);
        void Subscribe<TState>(IReference reference, Action<IReference, TState> action) where TState: State;
    }
}
