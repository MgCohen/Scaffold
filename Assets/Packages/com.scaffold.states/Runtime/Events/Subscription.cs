using System;

namespace Scaffold.States
{
    internal class Subscription<TState> : ISubscription where TState : BaseState
    {
        public Subscription(Action<IReference, TState> action)
        {
            this.action = action;
            this.type = typeof(TState);
        }

        private Action<IReference, TState> action;
        private Type type;
        
        public Type GetSubscriptionType()
        {
            return type;
        }

        public void Notify(IReference reference, BaseState state)
        {
            if(state is not TState tState)
            {
                throw new Exception("Trying to notify subscription of the wrong type");
            }
            action?.Invoke(reference, tState);
        }
    }
}
