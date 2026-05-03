using System;

namespace Scaffold.States
{
    public class GenericStateBuilder<TRef, TState> : StateBuilder<TRef, TState> where TRef : Reference where TState : State
    {
        public GenericStateBuilder(Func<TRef, TState> factory)
        {
            this.factory = factory;
        }

        private Func<TRef, TState> factory;

        public override TState Build(TRef reference)
        {
            return factory(reference);
        }
    }
}
