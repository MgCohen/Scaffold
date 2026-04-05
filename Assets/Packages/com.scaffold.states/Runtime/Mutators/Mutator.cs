namespace Scaffold.States
{
    public abstract class Mutator<TState> where TState : State
    {
        public abstract TState Change(TState state);
    }
}
