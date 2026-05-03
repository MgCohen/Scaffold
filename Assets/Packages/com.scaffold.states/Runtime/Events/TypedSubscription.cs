#nullable enable

using System;

namespace Scaffold.States
{
    internal sealed class TypedSubscription<TState> : ISubscription where TState : BaseState
    {
        public TypedSubscription(Action<Reference, TState, StateChangeEvent> action)
        {
            invoke = action ?? throw new ArgumentNullException(nameof(action));
            removalKey = action;
        }

        public TypedSubscription(Action<TState, StateChangeEvent> action)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            invoke = (r, ts, ev) => action(ts, ev);
            removalKey = action;
        }

        public TypedSubscription(Action<TState> action)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            invoke = (r, ts, ev) => action(ts);
            removalKey = action;
        }

        private readonly Action<Reference, TState, StateChangeEvent> invoke;
        private readonly object removalKey;

        internal bool MatchesRemoval(object key)
        {
            return ReferenceEquals(removalKey, key);
        }

        public Type GetSubscriptionType()
        {
            return typeof(TState);
        }

        public void Notify(Reference reference, BaseState state, StateChangeEvent changeEvent)
        {
            if (state is not TState tState)
            {
                throw new InvalidOperationException("Subscription notified with the wrong state type.");
            }

            invoke(reference, tState, changeEvent);
        }
    }
}
