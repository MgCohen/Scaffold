#nullable enable

using System;

namespace Scaffold.States
{
    internal sealed class TypedSubscription<TState> : ISubscription where TState : BaseState
    {
        public TypedSubscription(Action<IReference, TState, StateChangeEvent> action)
        {
            this.action = action ?? throw new ArgumentNullException(nameof(action));
        }

        private readonly Action<IReference, TState, StateChangeEvent> action;

        internal bool Matches(Action<IReference, TState, StateChangeEvent> other)
        {
            return ReferenceEquals(action, other);
        }

        public Type GetSubscriptionType()
        {
            return typeof(TState);
        }

        public void Notify(IReference reference, BaseState state, StateChangeEvent changeEvent)
        {
            if (state is not TState tState)
            {
                throw new InvalidOperationException("Subscription notified with the wrong state type.");
            }

            action(reference, tState, changeEvent);
        }
    }
}
