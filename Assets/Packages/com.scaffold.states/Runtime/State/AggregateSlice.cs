#nullable enable

using System;

namespace Scaffold.States
{
    /// <summary>
    /// A slice row whose <see cref="BaseSlice.State"/> is a derived <see cref="AggregateState"/> built from canonical slices.
    /// Wiring and <see cref="IAggregateProvider.Build"/> live in the injected <see cref="IAggregateProvider"/>.
    /// </summary>
    public sealed class AggregateSlice : BaseSlice<AggregateState>
    {
        public AggregateSlice(IReference reference, IAggregateProvider provider) : base(reference)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public sealed override Type StateType => provider.AggregateStateType;

        private readonly IAggregateProvider provider;
        private Store? attachedStore;

        internal BaseState BuildForScope(IStateScope scope)
        {
            return provider.Build(scope);
        }

        internal void OnAttachedToStore(Store store)
        {
            attachedStore = store;
            provider.Wire(store, new RebuildCallback(this));
            var built = provider.Build(store);
            ReplaceState((AggregateState)built);
        }

        public sealed override void Set(State state)
        {
            throw new InvalidOperationException($"Cannot Set derived aggregate type {StateType.Name}; it is read-only.");
        }

        private void RebuildAndNotifyAggregate()
        {
            if (attachedStore is null)
            {
                throw new InvalidOperationException("Aggregate slice is not attached to a store.");
            }

            var built = provider.Build(attachedStore);
            var aggregate = (AggregateState)built;
            ReplaceState(aggregate);
            attachedStore.Events.Notify(Reference, aggregate, StateChangeEvent.Updated);
        }

        private void ReplaceState(AggregateState aggregate)
        {
            State = aggregate;
        }

        private sealed class RebuildCallback : IAggregateRebuild
        {
            public RebuildCallback(AggregateSlice owner)
            {
                this.owner = owner;
            }

            private readonly AggregateSlice owner;

            public void RequestRebuild()
            {
                owner.RebuildAndNotifyAggregate();
            }
        }
    }
}
