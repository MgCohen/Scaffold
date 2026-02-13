namespace Scaffold.States
{
    public abstract class StateBuilder<TRef, TState> where TState: State
    {
        public abstract TState Build(TRef reference);
    }
}