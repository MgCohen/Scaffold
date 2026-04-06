using System;
using System.Collections.Generic;
using System.Linq;
using Scaffold.Maps;

namespace Scaffold.States
{
    public sealed class Store : IStoreScope
    {
        public Store(IStateEventHandler eventHandler, MutatorRegistry mutatorRegistry, params BaseSlice[] slices)
        {
            this.eventHandler = eventHandler;
            this.mutatorRegistry = mutatorRegistry ?? new MutatorRegistry();

            this.map = new Map<IReference, Type, Slice>();
            this.aggregates = new Map<IReference, Type, AggregateSlice>();

            foreach (var baseSlice in slices)
            {
                if (baseSlice is Slice slice)
                {
                    map.Add(slice.Reference, slice.StateType, slice);
                }
                else if (baseSlice is AggregateSlice aSlice)
                {
                    aggregates.Add(aSlice.Reference, aSlice.StateType, aSlice);
                    aSlice.OnAttachedToStore(this);
                }
            }
        }

        public IStateEventHandler Events => eventHandler;

        private readonly Map<IReference, Type, Slice> map;
        private readonly Map<IReference, Type, AggregateSlice> aggregates;
        private readonly IStateEventHandler eventHandler;
        private readonly MutatorRegistry mutatorRegistry;

        #region Subscriptions
        public void Subscribe<TState>(Action<IReference, TState> action) where TState : BaseState
        {
            Subscribe<TState>(Reference.Null, action);
        }

        public void Subscribe<TState>(IReference reference, Action<IReference, TState> action) where TState : BaseState
        {
            eventHandler.Subscribe<TState>(reference, action);
        }

        public void SubscribeAllReferences<TState>(Action<IReference, TState> action) where TState : BaseState
        {
            eventHandler.SubscribeAllReferences(action);
        }

        public void SubscribeAny(Action<IReference, BaseState> action)
        {
            eventHandler.SubscribeAny(action);
        }
        #endregion

        #region Snapshots
        public Snapshot SaveSnapshot()
        {
            Snapshot snapshot = new Snapshot();
            foreach (var entry in map)
            {
                var state = entry.Value.State as State;
                snapshot.Add(entry.Key.Primary, entry.Key.Secondary, state);
            }
            return snapshot;
        }

        public void LoadSnapshot(Snapshot snapshot)
        {
            foreach (var entry in snapshot)
            {
                Set(entry.Key.Primary, entry.Value);
            }
        }

        #endregion

        #region Mutators
        public void Execute<TState>(Mutator<TState> mutator) where TState : State
        {
            Execute(Reference.Null, mutator);
        }

        public void Execute<TState>(IReference? reference, Mutator<TState> mutator) where TState : State
        {
            var r = reference ?? Reference.Null;
            var runner = new MutatorRunner(new Scratchpad(this));
            runner.RunMutatorWithoutCommit(r, mutator);
            runner.CommitOverlay();
        }

        public void Execute<TState, TPayload>(Mutator<TState, TPayload> mutator, TPayload payload) where TState : State
        {
            Execute(Reference.Null, mutator, payload);
        }

        public void Execute<TState, TPayload>(IReference? reference, Mutator<TState, TPayload> mutator, TPayload payload) where TState : State
        {
            var runner = new MutatorRunner(new Scratchpad(this));
            runner.RunTypedMutatorWithoutCommit(reference ?? Reference.Null, mutator, payload);
            runner.CommitOverlay();
        }

        public void Execute<TPayload>(TPayload payload)
        {
            Execute(Reference.Null, payload);
        }

        public void Execute<TPayload>(IReference? reference, TPayload payload)
        {
            if (payload is null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            var r = reference ?? Reference.Null;
            var runner = new MutatorRunner(new Scratchpad(this));
            RunRegisteredMutatorsWithoutCommit(runner, payload, r);
            runner.CommitOverlay();
        }

        public void ExecuteBatch(IReadOnlyList<object> payloads)
        {
            if (payloads is null)
            {
                throw new ArgumentNullException(nameof(payloads));
            }

            if (payloads.Count == 0)
            {
                return;
            }

            var runner = new MutatorRunner(new Scratchpad(this));
            ApplyBatchWithoutCommit(runner, payloads);
            runner.CommitOverlay();
        }

        private void ApplyBatchWithoutCommit(MutatorRunner runner, IReadOnlyList<object> payloads)
        {
            foreach (object payload in payloads)
            {
                ApplyOnePayloadToOverlay(runner, payload, payloads);
            }
        }

        private void ApplyOnePayloadToOverlay(MutatorRunner runner, object payload, IReadOnlyList<object> payloads)
        {
            if (payload is null)
            {
                throw new ArgumentException("Batch contains a null command payload.", nameof(payloads));
            }

            RunRegisteredMutatorsWithoutCommit(runner, payload, Reference.Null);
        }

        private void RunRegisteredMutatorsWithoutCommit(MutatorRunner runner, object payload, IReference executeReference)
        {
            Type payloadType = payload.GetType();
            if (!mutatorRegistry.TryGet(payloadType, out IReadOnlyList<IPayloadMutatorBinding>? bindings) || bindings == null || bindings.Count == 0)
            {
                throw new InvalidOperationException($"No mutators registered for payload type {payloadType.FullName}.");
            }

            runner.RunMutatorBindingsWithoutCommit(payload, bindings, executeReference);
        }
        #endregion

        #region Setters
        private void Set(IReference reference, State state)
        {
            BaseSlice slice = GetSlice(reference, state.GetType());
            slice.Set(state);
            eventHandler.Notify(reference, state);
        }
        #endregion

        #region Getters
        public TState Get<TState>() where TState : BaseState
        {
            return Get<TState>(Reference.Null);
        }

        public TState Get<TState>(IReference reference) where TState : BaseState
        {
            var r = reference ?? Reference.Null;
            var t = typeof(TState);
            if (!TryGetSlice(r, t, out BaseSlice slice))
            {
                throw new KeyNotFoundException($"No slice registered for state type {t.Name} at the given reference.");
            }
            return (TState)slice.State;
        }

        public IEnumerable<TState> GetAll<TState>() where TState : BaseState
        {
            Type stateType = typeof(TState);
            var slices = map.GetAll(stateType).OfType<BaseSlice>();
            var aSlices = aggregates.GetAll(stateType).OfType<BaseSlice>();
            return slices.Union(aSlices).Select(s => s.State as TState);
        }

        private BaseSlice GetSlice(IReference reference, Type type)
        {
            var found = TryGetSlice(reference, type, out BaseSlice slice);
            return slice;
        }

        private bool TryGetSlice(IReference reference, Type type, out BaseSlice slice)
        {
            if (map.TryGetValue(reference, type, out var tSlice))
            {
                slice = tSlice;
                return true;
            }

            if (aggregates.TryGetValue(reference, type, out var aSlice))
            {
                slice = aSlice;
                return true;
            }

            slice = null;
            return false;
        }
        #endregion

        private sealed class Scratchpad : IStoreScratchpad
        {
            public Scratchpad(Store owner)
            {
                this.owner = owner;
            }

            private readonly Store owner;
            private readonly Snapshot overlay = new Snapshot();

            public void Commit()
            {
                owner.LoadSnapshot(overlay);
            }

            public void SetPending<TState>(IReference? reference, TState state) where TState : State
            {
                overlay.Set(reference, state);
            }

            public IEnumerable<TState> GetAll<TState>() where TState : BaseState
            {
                Type stateType = typeof(TState);
                if (owner.TryGetSlice(Reference.Null, stateType, out BaseSlice slice) && slice is AggregateSlice)
                {
                    yield return Get<TState>();
                    yield break;
                }

                foreach (var r in CollectReferencesForCanonicalType(stateType))
                {
                    yield return Get<TState>(r);
                }
            }

            public TState Get<TState>() where TState : BaseState
            {
                return Get<TState>(Reference.Null);
            }

            public TState Get<TState>(IReference reference) where TState : BaseState
            {
                var r = reference ?? Reference.Null;
                if (overlay.TryGetValue(r, typeof(TState), out var fromOverlay))
                {
                    return fromOverlay as TState;
                }

                if (owner.TryGetSlice(r, typeof(TState), out BaseSlice slice))
                {
                    if (slice is AggregateSlice aSlice)
                    {
                        return (TState)aSlice.BuildForScope(this);
                    }
                    return slice.State as TState;
                }

                return owner.Get<TState>(r);
            }

            private IEnumerable<IReference> CollectReferencesForCanonicalType(Type stateType)
            {
                var overlayReferences = overlay.GetPrimaryKeys();
                var storeReferences = owner.map.GetPrimaryKeys();
                return overlayReferences.Union(storeReferences).ToHashSet();
            }
        }
    }
}
