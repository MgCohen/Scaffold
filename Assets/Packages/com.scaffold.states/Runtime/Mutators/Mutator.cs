#nullable enable

namespace Scaffold.States
{
    public abstract class Mutator<TState> where TState : State
    {
        public abstract TState Change(TState state, IStateScope scope);
    }

    public abstract class Mutator<TState, TPayload> where TState : State
    {
        public abstract TState Change(TState state, TPayload payload, IStateScope scope);
    }
}
