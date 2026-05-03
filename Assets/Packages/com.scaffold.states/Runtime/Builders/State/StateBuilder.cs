namespace Scaffold.States
{
    public abstract class StateBuilder<TRef, TState> where TRef : IReference where TState : State
    {
        public abstract TState Build(TRef reference);
    }
}
