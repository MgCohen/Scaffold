#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.States
{
    internal class StateEventHandler : IStateEventHandler
    {
        public Dictionary<IReference, Ledger> Subscriptions = new();
        private readonly List<Action<IReference, BaseState>> anySubscriptions = new();
        private readonly Dictionary<Type, List<ISubscription>> typeWideSubscriptions = new();

        public void Notify(IReference reference, BaseState state)
        {
            NotifyReferenceSubscriptions(reference, state);
            NotifyAnySubscriptions(reference, state);
            NotifyTypeWideSubscriptions(state, reference);
        }

        private void NotifyReferenceSubscriptions(IReference reference, BaseState state)
        {
            if (!Subscriptions.ContainsKey(reference))
            {
                return;
            }

            var ledger = Subscriptions[reference];
            var list = ledger.Get(state.GetType());
            foreach (var item in list)
            {
                item.Notify(reference, state);
            }
        }

        private void NotifyTypeWideSubscriptions(BaseState state, IReference reference)
        {
            if (!typeWideSubscriptions.TryGetValue(state.GetType(), out var typeSubs))
            {
                return;
            }

            foreach (var sub in typeSubs)
            {
                sub.Notify(reference, state);
            }
        }

        private void NotifyAnySubscriptions(IReference reference, BaseState state)
        {
            for (var i = 0; i < anySubscriptions.Count; i++)
            {
                anySubscriptions[i](reference, state);
            }
        }

        public void SubscribeAny(Action<IReference, BaseState> action)
        {
            anySubscriptions.Add(action);
        }

        public void Subscribe<TState>(IReference reference, Action<IReference, TState> action) where TState : BaseState
        {
            if (!Subscriptions.ContainsKey(reference))
            {
                Subscriptions[reference] = new Ledger();
            }

            Subscription<TState> sub = new Subscription<TState>(action);
            Subscriptions[reference].Add(sub);
        }

        public void SubscribeAllReferences<TState>(Action<IReference, TState> action) where TState : BaseState
        {
            Type t = typeof(TState);
            if (!typeWideSubscriptions.TryGetValue(t, out var list))
            {
                list = new List<ISubscription>();
                typeWideSubscriptions[t] = list;
            }

            list.Add(new Subscription<TState>(action));
        }
    }
}
