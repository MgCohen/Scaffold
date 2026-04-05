using System;
using System.Collections.Generic;

namespace Scaffold.States
{
    internal class StateEventHandler : IStateEventHandler
    {
        public Dictionary<IReference, Ledger> Subscriptions = new();

        public void Notify(IReference reference, State state)
        {
            if (Subscriptions.ContainsKey(reference))
            {
                var ledger = Subscriptions[reference];
                var list = ledger.Get(state.GetType());
                foreach (var item in list)
                {
                    item.Notify(reference, state);
                }
            }
        }

        public void Subscribe<TState>(IReference reference, Action<IReference, TState> action) where TState : State
        {
            if (!Subscriptions.ContainsKey(reference))
            {
                Subscriptions[reference] = new Ledger();
            }

            Subscription<TState> sub = new Subscription<TState>(action);
            Subscriptions[reference].Add(sub);
        }
    }
}
