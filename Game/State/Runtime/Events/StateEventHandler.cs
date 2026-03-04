using System;
using System.Collections.Generic;

namespace Scaffold.States
{
    internal class StateEventHandler : IStateEventHandler
    {
        public Dictionary<IReference, Ledger> subscriptions = new();

        public void Notify(IReference reference, State state)
        {
            if (subscriptions.ContainsKey(reference))
            {
                var ledger = subscriptions[reference];
                var list = ledger.Get(state.GetType());
                foreach (var item in list)
                {
                    item.Notify(reference, state);
                }
            }
        }

        public void Subscribe<TState>(IReference reference, Action<IReference, TState> action) where TState : State
        {
            if (!subscriptions.ContainsKey(reference))
            {
                subscriptions[reference] = new Ledger();
            }

            Subscription<TState> sub = new Subscription<TState>(action);
            subscriptions[reference].Add(sub);
        }
    }
}
