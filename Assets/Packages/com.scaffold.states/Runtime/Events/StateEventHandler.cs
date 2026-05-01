#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.States
{
    internal sealed class StateEventHandler : IStateEventHandler
    {
        public Dictionary<IReference, Ledger> Subscriptions = new();
        private readonly List<Action<IReference, BaseState, StateChangeEvent>> anySubscriptions = new();
        private readonly Dictionary<Type, List<ISubscription>> typeWideSubscriptions = new();

        public void Notify(IReference reference, BaseState state, StateChangeEvent changeEvent)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            NotifyReferenceSubscriptions(reference, state, changeEvent);
            NotifyAnySubscriptions(reference, state, changeEvent);
            NotifyTypeWideSubscriptions(state, reference, changeEvent);
        }

        private void NotifyReferenceSubscriptions(IReference reference, BaseState state, StateChangeEvent changeEvent)
        {
            IReference r = reference ?? Reference.Null;
            if (!Subscriptions.ContainsKey(r))
            {
                return;
            }

            var ledger = Subscriptions[r];
            var list = ledger.Get(state.GetType());
            foreach (var item in list)
            {
                item.Notify(reference, state, changeEvent);
            }
        }

        private void NotifyTypeWideSubscriptions(BaseState state, IReference reference, StateChangeEvent changeEvent)
        {
            if (!typeWideSubscriptions.TryGetValue(state.GetType(), out var typeSubs))
            {
                return;
            }

            foreach (var sub in typeSubs)
            {
                sub.Notify(reference, state, changeEvent);
            }
        }

        private void NotifyAnySubscriptions(IReference reference, BaseState state, StateChangeEvent changeEvent)
        {
            for (var i = 0; i < anySubscriptions.Count; i++)
            {
                anySubscriptions[i](reference, state, changeEvent);
            }
        }

        public void SubscribeAny(Action<IReference, BaseState, StateChangeEvent> action)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            anySubscriptions.Add(action);
        }

        public void Subscribe<TState>(IReference reference, Action<IReference, TState, StateChangeEvent> action) where TState : BaseState
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            AddReferenceSubscription(reference, action);
        }

        public void Unsubscribe<TState>(IReference reference, Action<IReference, TState, StateChangeEvent> action) where TState : BaseState
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            AttemptRemoveSubscription(reference ?? Reference.Null, action);
        }

        private void AttemptRemoveSubscription<TState>(IReference r, Action<IReference, TState, StateChangeEvent> action) where TState : BaseState
        {
            if (!Subscriptions.TryGetValue(r, out Ledger? ledger))
            {
                return;
            }

            if (!ledger.RemoveSubscription(action))
            {
                return;
            }

            if (ledger.Lookup.Count == 0)
            {
                Subscriptions.Remove(r);
            }
        }

        public void SubscribeAllReferences<TState>(Action<IReference, TState, StateChangeEvent> action) where TState : BaseState
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            AddAllReferencesSubscription(action);
        }

        private void AddReferenceSubscription<TState>(IReference reference, Action<IReference, TState, StateChangeEvent> action) where TState : BaseState
        {
            IReference r = reference ?? Reference.Null;
            if (!Subscriptions.ContainsKey(r))
            {
                Subscriptions[r] = new Ledger();
            }

            Subscriptions[r].Add(new TypedSubscription<TState>(action));
        }

        private void AddAllReferencesSubscription<TState>(Action<IReference, TState, StateChangeEvent> action) where TState : BaseState
        {
            Type t = typeof(TState);
            if (!typeWideSubscriptions.TryGetValue(t, out var list))
            {
                list = new List<ISubscription>();
                typeWideSubscriptions[t] = list;
            }

            list.Add(new TypedSubscription<TState>(action));
        }
    }
}
