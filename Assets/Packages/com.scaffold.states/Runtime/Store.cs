using System;
using System.Collections.Generic;
using Scaffold.Maps;
using Scaffold.Pooling;

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
                else
                {
                    if (baseSlice is AggregateSlice aSlice)
                    {
                        aggregates.Add(aSlice.Reference, aSlice.StateType, aSlice);
                        aSlice.OnAttachedToStore(this);
                    }
                }
            }

            mutatorRunnerPool = new Pool<MutatorRunner>(() => new MutatorRunner(new Scratchpad(this)), null, initialSize: 2);
        }

        public IStateEventHandler Events => eventHandler;

        private readonly Map<IReference, Type, Slice> map;
        private readonly Map<IReference, Type, AggregateSlice> aggregates;
        private readonly IStateEventHandler eventHandler;
        private readonly MutatorRegistry mutatorRegistry;
        private readonly Pool<MutatorRunner> mutatorRunnerPool;
        private readonly List<Slice> mapSliceBuffer = new List<Slice>();
        private readonly List<AggregateSlice> aggregateSliceBuffer = new List<AggregateSlice>();
        private readonly List<BaseSlice> sliceBuffer = new List<BaseSlice>();
        private readonly List<(IReference Reference, Type StateType)> pruneBuffer = new List<(IReference Reference, Type StateType)>();

        #region Subscriptions
        public void Subscribe<TState>(Action<IReference, TState, StateChangeEvent> action) where TState : BaseState
        {
            Subscribe(Reference.Null, action);
        }

        public void Subscribe<TState>(IReference reference, Action<IReference, TState, StateChangeEvent> action) where TState : BaseState
        {
            eventHandler.Subscribe(reference, action);
        }

        public void SubscribeAllReferences<TState>(Action<IReference, TState, StateChangeEvent> action) where TState : BaseState
        {
            eventHandler.SubscribeAllReferences(action);
        }

        public void SubscribeAny(Action<IReference, BaseState, StateChangeEvent> action)
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
            if (snapshot is null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ApplySnapshot(snapshot);
            PruneCanonicalSlicesNotInSnapshot(snapshot);
        }

        private void ApplySnapshot(Snapshot snapshot)
        {
            foreach (var entry in snapshot)
            {
                Set(entry.Key.Primary, entry.Value);
            }
        }

        private void PruneCanonicalSlicesNotInSnapshot(Snapshot snapshot)
        {
            pruneBuffer.Clear();
            foreach (var entry in map)
            {
                IReference r = entry.Key.Primary;
                Type t = entry.Key.Secondary;
                if (!snapshot.Contains(r, t))
                {
                    pruneBuffer.Add((r, t));
                }
            }

            foreach ((IReference r, Type t) in pruneBuffer)
            {
                UnregisterSlice(r, t);
            }
        }

        #endregion

        #region Mutators
        public void ExecuteMutator<TState>(Mutator<TState> mutator) where TState : State
        {
            ExecuteMutator(Reference.Null, mutator);
        }

        public void ExecuteMutator<TState>(IReference? reference, Mutator<TState> mutator) where TState : State
        {
            var r = reference ?? Reference.Null;
            MutatorRunner runner = mutatorRunnerPool.Take();
            try
            {
                runner.RunMutatorWithoutCommit(r, mutator);
                runner.CommitOverlay();
            }
            finally
            {
                mutatorRunnerPool.Return(runner);
            }
        }

        public void ExecuteMutator<TState, TPayload>(Mutator<TState, TPayload> mutator, TPayload payload) where TState : State
        {
            ExecuteMutator(Reference.Null, mutator, payload);
        }

        public void ExecuteMutator<TState, TPayload>(IReference? reference, Mutator<TState, TPayload> mutator, TPayload payload) where TState : State
        {
            MutatorRunner runner = mutatorRunnerPool.Take();
            try
            {
                runner.RunTypedMutatorWithoutCommit(reference ?? Reference.Null, mutator, payload);
                runner.CommitOverlay();
            }
            finally
            {
                mutatorRunnerPool.Return(runner);
            }
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
            MutatorRunner runner = mutatorRunnerPool.Take();
            try
            {
                RunRegisteredMutatorsWithoutCommit(runner, payload, r);
                runner.CommitOverlay();
            }
            finally
            {
                mutatorRunnerPool.Return(runner);
            }
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

            RunExecuteBatchWithPool(payloads);
        }

        private void RunExecuteBatchWithPool(IReadOnlyList<object> payloads)
        {
            MutatorRunner runner = mutatorRunnerPool.Take();
            try
            {
                ApplyBatchWithoutCommit(runner, payloads);
                runner.CommitOverlay();
            }
            finally
            {
                mutatorRunnerPool.Return(runner);
            }
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
                UnityEngine.Debug.LogWarning($"[Store] No mutators registered for payload type {payloadType.FullName}.");
                return;
            }

            runner.RunMutatorBindingsWithoutCommit(payload, bindings, executeReference);
        }

        public void RegisterMutator<TState, TPayload>(Mutator<TState, TPayload> mutator) where TState : State
        {
            if (mutator is null)
            {
                throw new ArgumentNullException(nameof(mutator));
            }

            mutatorRegistry.Register(mutator);
        }
        #endregion

        #region Slice registration

        public void RegisterSlice(IReference? reference, State state)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var r = reference ?? Reference.Null;
            Slice slice = Slice.Create(r, state);
            Type t = slice.StateType;
            ThrowIfSliceConflict(r, t, map.Contains(r, t), aggregates.Contains(r, t));
            map.Add(r, t, slice);
            eventHandler.Notify(r, state, StateChangeEvent.Created);
        }

        private void ThrowIfSliceConflict(IReference r, Type t, bool hasCanonical, bool hasAggregate)
        {
            if (hasCanonical)
            {
                throw new InvalidOperationException(
                    $"A canonical slice for state type {t.Name} is already registered at this reference.");
            }

            if (hasAggregate)
            {
                throw new InvalidOperationException(
                    $"An aggregate slice for state type {t.Name} is already registered at this reference.");
            }
        }

        public bool UnregisterSlice<TState>(IReference? reference) where TState : State
        {
            return UnregisterSlice(reference, typeof(TState));
        }

        public bool UnregisterSlice(IReference? reference, Type stateType)
        {
            if (stateType is null)
            {
                throw new ArgumentNullException(nameof(stateType));
            }

            var r = reference ?? Reference.Null;
            return TryRemoveCanonicalSliceAndNotify(r, stateType);
        }

        private bool TryRemoveCanonicalSliceAndNotify(IReference r, Type stateType)
        {
            if (!map.TryGetValue(r, stateType, out Slice slice))
            {
                return false;
            }

            if (slice.State is not State lastState)
            {
                throw new InvalidOperationException($"Canonical slice state for {stateType.Name} is not a {nameof(State)} instance.");
            }

            if (!map.Remove(r, stateType))
            {
                return false;
            }

            eventHandler.Notify(r, lastState, StateChangeEvent.Removed);
            return true;
        }

        #endregion

        #region Setters
        private void Set(IReference reference, State state)
        {
            BaseSlice slice = GetSlice(reference, state.GetType());
            slice.Set(state);
            eventHandler.Notify(reference, state, StateChangeEvent.Updated);
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
            FillSlices(stateType, sliceBuffer);
            return EnumerateSliceStates<TState>();
        }

        private IEnumerable<TState> EnumerateSliceStates<TState>() where TState : BaseState
        {
            for (int i = 0; i < sliceBuffer.Count; i++)
            {
                if (sliceBuffer[i].State is TState ts)
                {
                    yield return ts;
                }
            }
        }

        private void FillSlices(Type stateType, List<BaseSlice> buffer)
        {
            buffer.Clear();
            mapSliceBuffer.Clear();
            map.GetAll(stateType, mapSliceBuffer);
            for (int i = 0; i < mapSliceBuffer.Count; i++)
            {
                buffer.Add(mapSliceBuffer[i]);
            }

            aggregateSliceBuffer.Clear();
            aggregates.GetAll(stateType, aggregateSliceBuffer);
            for (int i = 0; i < aggregateSliceBuffer.Count; i++)
            {
                buffer.Add(aggregateSliceBuffer[i]);
            }
        }

        private BaseSlice GetSlice(IReference reference, Type type)
        {
            TryGetSlice(reference, type, out BaseSlice slice);
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
            private readonly List<BaseSlice> sliceBuffer = new List<BaseSlice>();
            private readonly HashSet<IReference> refSet =
                new HashSet<IReference>(ReferenceByValueEqualityComparer.Instance);

            public void Reset()
            {
                overlay.Clear();
            }

            public void Commit()
            {
                owner.ApplySnapshot(overlay);
            }

            public void SetPending<TState>(IReference? reference, TState state) where TState : State
            {
                overlay.Set(reference, state);
            }

            public IEnumerable<TState> GetAll<TState>() where TState : BaseState
            {
                Type stateType = typeof(TState);
                refSet.Clear();
                owner.FillSlices(stateType, sliceBuffer);
                for (int i = 0; i < sliceBuffer.Count; i++)
                {
                    refSet.Add(sliceBuffer[i].Reference);
                }

                CollectOverlayRefs(stateType, refSet);
                foreach (IReference r in refSet)
                {
                    yield return Get<TState>(r);
                }
            }

            private void CollectOverlayRefs(Type stateType, HashSet<IReference> refs)
            {
                IEqualityComparer<Type> comparer = EqualityComparer<Type>.Default;
                foreach (var entry in overlay)
                {
                    if (comparer.Equals(entry.Key.Secondary, stateType))
                    {
                        refs.Add(entry.Key.Primary);
                    }
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

            private sealed class ReferenceByValueEqualityComparer : IEqualityComparer<IReference>
            {
                internal static ReferenceByValueEqualityComparer Instance { get; } = new ReferenceByValueEqualityComparer();

                private ReferenceByValueEqualityComparer()
                {
                }

                public bool Equals(IReference? x, IReference? y)
                {
                    if (ReferenceEquals(x, y))
                    {
                        return true;
                    }

                    if (x is null || y is null)
                    {
                        return false;
                    }

                    return x.Equals(y);
                }

                public int GetHashCode(IReference obj)
                {
                    return obj?.GetHashCode() ?? 0;
                }
            }
        }
    }
}
