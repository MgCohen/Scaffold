#nullable enable
using System;
using System.Collections.Generic;
using Scaffold.Pooling;

namespace Scaffold.States
{
    internal sealed class StateEventHandler : IStateEventHandler
    {
        public Dictionary<Reference, Ledger> Subscriptions = new();
        private readonly List<Action<Reference, BaseState, StateChangeEvent>> anySubscriptions = new();
        private readonly Dictionary<Type, List<ISubscription>> typeWideSubscriptions = new();
        private readonly Pool<List<ISubscription>> subscriptionNotifyPool =
            new(static () => new List<ISubscription>(), null, initialSize: 2);
        private readonly Pool<List<Action<Reference, BaseState, StateChangeEvent>>> anyNotifyPool =
            new(static () => new List<Action<Reference, BaseState, StateChangeEvent>>(), null, initialSize: 2);

        public void Notify(Reference reference, BaseState state, StateChangeEvent changeEvent)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            Reference r = reference ?? Reference.Null;
            NotifyReferenceSubscriptions(r, state, changeEvent);
            NotifyAnySubscriptions(r, state, changeEvent);
            NotifyTypeWideSubscriptions(state, r, changeEvent);
        }

        private void NotifyReferenceSubscriptions(Reference reference, BaseState state, StateChangeEvent changeEvent)
        {
            Reference r = reference ?? Reference.Null;
            if (!Subscriptions.TryGetValue(r, out Ledger? ledger))
            {
                return;
            }

            NotifyKeyedSubscriptions(ledger, r, state, changeEvent);
        }

        private void NotifyKeyedSubscriptions(Ledger ledger, Reference reference, BaseState state, StateChangeEvent changeEvent)
        {
            List<ISubscription> snapshot = subscriptionNotifyPool.Take();
            try
            {
                Type runtimeStateType = state.GetType();
                IEnumerable<ISubscription> keyedSubs = ledger.Get(runtimeStateType);
                CopySubscriptions(keyedSubs, snapshot);
                DispatchSubscriptionNotifications(snapshot, reference, state, changeEvent);
            }
            finally
            {
                subscriptionNotifyPool.Return(snapshot);
            }
        }

        private void NotifyTypeWideSubscriptions(BaseState state, Reference reference, StateChangeEvent changeEvent)
        {
            if (!typeWideSubscriptions.TryGetValue(state.GetType(), out List<ISubscription>? typeSubs))
            {
                return;
            }

            List<ISubscription> snapshot = subscriptionNotifyPool.Take();
            try
            {
                CopySubscriptions(typeSubs, snapshot);
                DispatchSubscriptionNotifications(snapshot, reference, state, changeEvent);
            }
            finally
            {
                subscriptionNotifyPool.Return(snapshot);
            }
        }

        private void NotifyAnySubscriptions(Reference reference, BaseState state, StateChangeEvent changeEvent)
        {
            List<Action<Reference, BaseState, StateChangeEvent>> snapshot = anyNotifyPool.Take();
            try
            {
                snapshot.Clear();
                snapshot.AddRange(anySubscriptions);
                DispatchAnyNotifications(snapshot, reference, state, changeEvent);
            }
            finally
            {
                anyNotifyPool.Return(snapshot);
            }
        }

        public void SubscribeAny(Action<Reference, BaseState, StateChangeEvent> action)
        {
            anySubscriptions.Add(action);
        }

        public void Subscribe<TState>(Reference reference, Action<Reference, TState, StateChangeEvent> action) where TState : BaseState
        {
            AddReferenceSubscription(reference, action);
        }

        public void Subscribe<TState>(Reference reference, Action<TState, StateChangeEvent> action) where TState : BaseState
        {
            AddReferenceSubscription(reference, action);
        }

        public void Subscribe<TState>(Reference reference, Action<TState> action) where TState : BaseState
        {
            AddReferenceSubscription(reference, action);
        }

        public void Unsubscribe<TState>(Reference reference, Action<Reference, TState, StateChangeEvent> action) where TState : BaseState
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            AttemptRemoveSubscription<TState>(reference ?? Reference.Null, action);
        }

        public void Unsubscribe<TState>(Reference reference, Action<TState, StateChangeEvent> action) where TState : BaseState
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            AttemptRemoveSubscription<TState>(reference ?? Reference.Null, action);
        }

        public void Unsubscribe<TState>(Reference reference, Action<TState> action) where TState : BaseState
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            AttemptRemoveSubscription<TState>(reference ?? Reference.Null, action);
        }

        private void AttemptRemoveSubscription<TState>(Reference r, object removalKey) where TState : BaseState
        {
            if (!Subscriptions.TryGetValue(r, out Ledger? ledger))
            {
                return;
            }

            if (!ledger.RemoveSubscription<TState>(removalKey))
            {
                return;
            }

            if (ledger.Lookup.Count == 0)
            {
                Subscriptions.Remove(r);
            }
        }

        public void SubscribeAllReferences<TState>(Action<Reference, TState, StateChangeEvent> action) where TState : BaseState
        {
            AddAllReferencesSubscription(action);
        }

        public void UnsubscribeAllReferences<TState>(Action<Reference, TState, StateChangeEvent> action) where TState : BaseState
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            Type t = typeof(TState);
            if (!typeWideSubscriptions.TryGetValue(t, out List<ISubscription>? list))
            {
                return;
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] is TypedSubscription<TState> typed && typed.MatchesRemoval(action))
                {
                    list.RemoveAt(i);
                    break;
                }
            }

            if (list.Count == 0)
            {
                typeWideSubscriptions.Remove(t);
            }
        }

        private void AddReferenceSubscription<TState>(Reference reference, Action<Reference, TState, StateChangeEvent> action) where TState : BaseState
        {
            Reference r = reference ?? Reference.Null;
            if (!Subscriptions.TryGetValue(r, out Ledger? ledger))
            {
                ledger = new Ledger();
                Subscriptions[r] = ledger;
            }

            ledger.Add(new TypedSubscription<TState>(action));
        }

        private void AddReferenceSubscription<TState>(Reference reference, Action<TState, StateChangeEvent> action) where TState : BaseState
        {
            Reference r = reference ?? Reference.Null;
            if (!Subscriptions.TryGetValue(r, out Ledger? ledger))
            {
                ledger = new Ledger();
                Subscriptions[r] = ledger;
            }

            ledger.Add(new TypedSubscription<TState>(action));
        }

        private void AddReferenceSubscription<TState>(Reference reference, Action<TState> action) where TState : BaseState
        {
            Reference r = reference ?? Reference.Null;
            if (!Subscriptions.TryGetValue(r, out Ledger? ledger))
            {
                ledger = new Ledger();
                Subscriptions[r] = ledger;
            }

            ledger.Add(new TypedSubscription<TState>(action));
        }

        private void AddAllReferencesSubscription<TState>(Action<Reference, TState, StateChangeEvent> action) where TState : BaseState
        {
            Type t = typeof(TState);
            if (!typeWideSubscriptions.TryGetValue(t, out var list))
            {
                list = new List<ISubscription>();
                typeWideSubscriptions[t] = list;
            }

            list.Add(new TypedSubscription<TState>(action));
        }

        private void CopySubscriptions(IEnumerable<ISubscription> source, List<ISubscription> snapshot)
        {
            snapshot.Clear();
            foreach (ISubscription item in source)
            {
                snapshot.Add(item);
            }
        }

        private void DispatchSubscriptionNotifications(List<ISubscription> snapshot, Reference reference, BaseState state, StateChangeEvent changeEvent)
        {
            for (int i = 0; i < snapshot.Count; i++)
            {
                snapshot[i].Notify(reference, state, changeEvent);
            }
        }

        private void DispatchAnyNotifications(List<Action<Reference, BaseState, StateChangeEvent>> snapshot, Reference reference, BaseState state, StateChangeEvent changeEvent)
        {
            for (int i = 0; i < snapshot.Count; i++)
            {
                snapshot[i](reference, state, changeEvent);
            }
        }
    }
}
