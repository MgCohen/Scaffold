#nullable enable
using System;
using System.Collections.Generic;
using Scaffold.Maps;
using Scaffold.Pooling;

namespace Scaffold.States
{
    public sealed class Store : IStoreScope
    {
        public Store(IStateEventHandler eventHandler, MutatorRegistry mutatorRegistry, params BaseSlice[] slices)
            : this(eventHandler, mutatorRegistry, null, slices)
        {
        }

        public Store(IStateEventHandler eventHandler, MutatorRegistry mutatorRegistry, IMutatorDispatcher? mutatorDispatcher, params BaseSlice[] slices)
        {
            this.eventHandler = eventHandler;
            this.mutatorRegistry = mutatorRegistry ?? new MutatorRegistry();
            this.mutatorDispatcher = mutatorDispatcher;

            this.map = new Map<Reference, Type, Slice>();
            this.aggregates = new Map<Reference, Type, AggregateSlice>();
            mutatorRunnerPool = new Pool<MutatorRunner>(() => new MutatorRunner(new Scratchpad(this)), null, initialSize: 2);
            baseSliceListPool = new Pool<List<BaseSlice>>(static () => new List<BaseSlice>(), null, initialSize: 2);

            foreach (var baseSlice in slices)
            {
                if (baseSlice is Slice slice)
                {
                    map.Add(slice.Reference, slice.StateType, slice);
                    AddSliceToIndex(slice);
                }
                else
                {
                    if (baseSlice is AggregateSlice aSlice)
                    {
                        aggregates.Add(aSlice.Reference, aSlice.StateType, aSlice);
                        AddSliceToIndex(aSlice);
                        aSlice.OnAttachedToStore(this);
                    }
                }
            }
        }

        public IStateEventHandler Events => eventHandler;

        private readonly Map<Reference, Type, Slice> map;
        private readonly Map<Reference, Type, AggregateSlice> aggregates;
        private readonly IStateEventHandler eventHandler;
        private readonly MutatorRegistry mutatorRegistry;
        private readonly IMutatorDispatcher? mutatorDispatcher;
        private readonly Pool<MutatorRunner> mutatorRunnerPool;
        private readonly Pool<List<BaseSlice>> baseSliceListPool;
        private readonly Dictionary<Type, List<BaseSlice>> slicesByStateType = new();

        #region Subscriptions
        public void Subscribe<TState>(Action<Reference, TState, StateChangeEvent> action) where TState : BaseState
        {
            Subscribe(Reference.Null, action);
        }

        public void Subscribe<TState>(Reference reference, Action<Reference, TState, StateChangeEvent> action) where TState : BaseState
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            eventHandler.Subscribe(reference, action);
        }

        public void Unsubscribe<TState>(Reference reference, Action<Reference, TState, StateChangeEvent> action) where TState : BaseState
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            eventHandler.Unsubscribe(reference, action);
        }

        public void SubscribeAllReferences<TState>(Action<Reference, TState, StateChangeEvent> action) where TState : BaseState
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            eventHandler.SubscribeAllReferences(action);
        }

        public void Subscribe<TState>(Reference reference, Action<TState, StateChangeEvent> action) where TState : BaseState
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            eventHandler.Subscribe(reference, action);
        }

        public void Subscribe<TState>(Reference reference, Action<TState> action) where TState : BaseState
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            eventHandler.Subscribe(reference, action);
        }

        public void Unsubscribe<TState>(Reference reference, Action<TState, StateChangeEvent> action) where TState : BaseState
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            eventHandler.Unsubscribe(reference, action);
        }

        public void Unsubscribe<TState>(Reference reference, Action<TState> action) where TState : BaseState
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            eventHandler.Unsubscribe(reference, action);
        }

        public void UnsubscribeAllReferences<TState>(Action<Reference, TState, StateChangeEvent> action) where TState : BaseState
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            eventHandler.UnsubscribeAllReferences(action);
        }

        public void SubscribeAny(Action<Reference, BaseState, StateChangeEvent> action)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            eventHandler.SubscribeAny(action);
        }
        #endregion

        #region Snapshots
        public Snapshot SaveSnapshot()
        {
            Snapshot snapshot = new Snapshot();
            foreach (var entry in map)
            {
                snapshot.Add(entry.Key.Primary, entry.Key.Secondary, (State)entry.Value.State);
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
                Reference r = entry.Key.Primary;
                Type t = entry.Key.Secondary;
                State value = entry.Value;
                if (TryGetSlice(r, t, out _))
                {
                    Set(r, value);
                }
                else
                {
                    ReregisterCanonicalSliceFromSnapshot(r, t, value);
                }
            }
        }

        private void ReregisterCanonicalSliceFromSnapshot(Reference r, Type t, State value)
        {
            Slice slice = Slice.Create(r, value);
            ThrowIfSliceConflict(r, t, map.Contains(r, t), aggregates.Contains(r, t));
            map.Add(r, t, slice);
            AddSliceToIndex(slice);
            eventHandler.Notify(r, value, StateChangeEvent.Created);
        }

        private void PruneCanonicalSlicesNotInSnapshot(Snapshot snapshot)
        {
            var pruneBuffer = new List<(Reference Reference, Type StateType)>();
            foreach (var entry in map)
            {
                Reference r = entry.Key.Primary;
                Type t = entry.Key.Secondary;
                if (!snapshot.Contains(r, t))
                {
                    pruneBuffer.Add((r, t));
                }
            }

            foreach ((Reference r, Type t) in pruneBuffer)
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

        public void ExecuteMutator<TState>(Reference? reference, Mutator<TState> mutator) where TState : State
        {
            var r = FromReference(reference);
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

        public void ExecuteMutator<TState, TPayload>(Reference? reference, Mutator<TState, TPayload> mutator, TPayload payload) where TState : State
        {
            MutatorRunner runner = mutatorRunnerPool.Take();
            try
            {
                runner.RunTypedMutatorWithoutCommit(FromReference(reference), mutator, payload);
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

        public void Execute<TPayload>(Reference? reference, TPayload payload)
        {
            if (payload is null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            var r = FromReference(reference);
            if (mutatorDispatcher != null && mutatorDispatcher.TryDispatch(this, r, payload))
            {
                return;
            }

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

            ThrowIfBatchContainsNull(payloads);
            RunExecuteBatchWithPool(payloads);
        }

        private void ThrowIfBatchContainsNull(IReadOnlyList<object> payloads)
        {
            for (int i = 0; i < payloads.Count; i++)
            {
                if (payloads[i] is null)
                {
                    throw new ArgumentException("Batch contains a null command payload.", nameof(payloads));
                }
            }
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
            for (int i = 0; i < payloads.Count; i++)
            {
                RunRegisteredMutatorsWithoutCommit(runner, payloads[i], Reference.Null);
            }
        }

        private void RunRegisteredMutatorsWithoutCommit(MutatorRunner runner, object payload, Reference executeReference)
        {
            Type payloadType = payload.GetType();
            if (!mutatorRegistry.TryGet(payloadType, out var bindings) || bindings.Count == 0)
            {
                throw new MutatorNotRegisteredException(payloadType);
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

        public void RegisterSlice(Reference? reference, State state)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var r = FromReference(reference);
            Slice slice = Slice.Create(r, state);
            Type t = slice.StateType;
            ThrowIfSliceConflict(r, t, map.Contains(r, t), aggregates.Contains(r, t));
            map.Add(r, t, slice);
            AddSliceToIndex(slice);
            eventHandler.Notify(r, state, StateChangeEvent.Created);
        }

        public bool UnregisterSlice<TState>(Reference? reference) where TState : State
        {
            return UnregisterSlice(reference, typeof(TState));
        }

        public bool UnregisterSlice(Reference? reference, Type stateType)
        {
            if (stateType is null)
            {
                throw new ArgumentNullException(nameof(stateType));
            }

            var r = FromReference(reference);
            return TryRemoveCanonicalSliceAndNotify(r, stateType);
        }

        private bool TryRemoveCanonicalSliceAndNotify(Reference r, Type stateType)
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

            RemoveSliceFromIndex(slice);

            eventHandler.Notify(r, lastState, StateChangeEvent.Removed);
            return true;
        }

        #endregion

        #region Aggregate registration

        public void RegisterAggregate(Reference? reference, IAggregateProvider provider)
        {
            if (provider is null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            var r = FromReference(reference);
            var aSlice = new AggregateSlice(r, provider);
            Type t = aSlice.StateType;
            ThrowIfSliceConflict(r, t, map.Contains(r, t), aggregates.Contains(r, t));
            aggregates.Add(r, t, aSlice);
            AddSliceToIndex(aSlice);
            aSlice.OnAttachedToStore(this);
            eventHandler.Notify(r, aSlice.State, StateChangeEvent.Created);
        }

        public bool UnregisterAggregate<TAggregate>(Reference? reference) where TAggregate : AggregateState
        {
            return UnregisterAggregate(reference, typeof(TAggregate));
        }

        public bool UnregisterAggregate(Reference? reference, Type aggregateStateType)
        {
            if (aggregateStateType is null)
            {
                throw new ArgumentNullException(nameof(aggregateStateType));
            }

            var r = FromReference(reference);
            if (!aggregates.TryGetValue(r, aggregateStateType, out AggregateSlice? slice))
            {
                return false;
            }

            if (!aggregates.Remove(r, aggregateStateType))
            {
                return false;
            }

            RemoveSliceFromIndex(slice);
            slice.DisposeWireSubscription();
            BaseState lastState = slice.State;
            eventHandler.Notify(r, lastState, StateChangeEvent.Removed);
            return true;
        }

        private void ThrowIfSliceConflict(Reference r, Type t, bool hasCanonical, bool hasAggregate)
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

        #endregion

        #region Setters
        private void Set(Reference reference, State state)
        {
            if (!TryGetSlice(reference, state.GetType(), out BaseSlice slice))
            {
                throw new KeyNotFoundException($"No slice registered for state type {state.GetType().Name} at the given reference.");
            }

            slice.Set(state);
            eventHandler.Notify(reference, state, StateChangeEvent.Updated);
        }
        #endregion

        #region Getters
        public TState Get<TState>() where TState : BaseState
        {
            return Get<TState>(Reference.Null);
        }

        public TState Get<TState>(Reference? reference) where TState : BaseState
        {
            var r = FromReference(reference);
            var t = typeof(TState);
            if (!TryGetSlice(r, t, out BaseSlice slice))
            {
                throw new KeyNotFoundException($"No slice registered for state type {t.Name} at the given reference.");
            }
            return (TState)slice.State;
        }

        public bool TryGet<TState>(Reference? reference, out TState state) where TState : BaseState
        {
            Reference r = FromReference(reference);
            if (TryGetSlice(r, typeof(TState), out BaseSlice slice))
            {
                if (slice is AggregateSlice aSlice)
                {
                    state = (TState)aSlice.BuildForScope(this);
                    return true;
                }
                state = (TState)slice.State;
                return true;
            }

            state = default!;
            return false;
        }

        public IEnumerable<TState> GetAll<TState>() where TState : BaseState
        {
            return IterateSlicesAsStates<TState>(typeof(TState));
        }

        public EnumerateAllPairsResult<TState> EnumerateAllPairs<TState>() where TState : BaseState
        {
            return new EnumerateAllPairsResult<TState>(this);
        }

        public EnumerateAllResult<TState> EnumerateAll<TState>() where TState : BaseState
        {
            return new EnumerateAllResult<TState>(this);
        }

        private IEnumerable<TState> IterateSlicesAsStates<TState>(Type stateType) where TState : BaseState
        {
            List<BaseSlice> buffer = baseSliceListPool.Take();
            try
            {
                FillSlices(stateType, buffer);
                foreach (TState ts in YieldStatesFromBuffer<TState>(buffer))
                {
                    yield return ts;
                }
            }
            finally
            {
                baseSliceListPool.Return(buffer);
            }
        }

        private IEnumerable<TState> YieldStatesFromBuffer<TState>(List<BaseSlice> buffer) where TState : BaseState
        {
            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i].State is TState ts)
                {
                    yield return ts;
                }
            }
        }

        internal List<BaseSlice> RentFilledEnumerationBuffer(Type stateType)
        {
            List<BaseSlice> buffer = baseSliceListPool.Take();
            FillSlices(stateType, buffer);
            return buffer;
        }

        internal void ReturnEnumerationBuffer(List<BaseSlice> buffer)
        {
            baseSliceListPool.Return(buffer);
        }

        private void FillSlices(Type stateType, List<BaseSlice> buffer)
        {
            buffer.Clear();
            if (!slicesByStateType.TryGetValue(stateType, out List<BaseSlice>? list))
            {
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                buffer.Add(list[i]);
            }
        }

        private void AddSliceToIndex(BaseSlice slice)
        {
            Type t = slice.StateType;
            if (!slicesByStateType.TryGetValue(t, out List<BaseSlice>? row))
            {
                row = new List<BaseSlice>();
                slicesByStateType[t] = row;
            }

            row.Add(slice);
        }

        private void RemoveSliceFromIndex(BaseSlice slice)
        {
            Type t = slice.StateType;
            if (!slicesByStateType.TryGetValue(t, out List<BaseSlice>? row))
            {
                return;
            }

            row.Remove(slice);
            if (row.Count == 0)
            {
                slicesByStateType.Remove(t);
            }
        }

        private bool TryGetSlice(Reference reference, Type type, out BaseSlice slice)
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

            slice = default!;
            return false;
        }
        #endregion

        private Reference FromReference(Reference? reference)
        {
            return reference ?? Reference.Null;
        }

        private sealed class Scratchpad : IStoreScratchpad
        {
            public Scratchpad(Store owner)
            {
                this.owner = owner;
            }

            private readonly Store owner;
            private readonly Snapshot overlay = new Snapshot();
            private readonly List<BaseSlice> sliceBuffer = new List<BaseSlice>();
            private readonly HashSet<Reference> refSet = new HashSet<Reference>();

            public void Reset()
            {
                overlay.Clear();
                sliceBuffer.Clear();
                refSet.Clear();
            }

            public void Commit()
            {
                owner.ApplySnapshot(overlay);
            }

            public void SetPending<TState>(Reference? reference, TState state) where TState : State
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
                foreach (Reference r in refSet)
                {
                    yield return Get<TState>(r);
                }
            }

            private void CollectOverlayRefs(Type stateType, HashSet<Reference> refs)
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

            public TState Get<TState>(Reference? reference) where TState : BaseState
            {
                var r = owner.FromReference(reference);
                if (overlay.TryGetValue(r, typeof(TState), out var fromOverlay))
                {
                    return (TState)(object)fromOverlay!;
                }

                if (owner.TryGetSlice(r, typeof(TState), out BaseSlice slice))
                {
                    if (slice is AggregateSlice aSlice)
                    {
                        return (TState)aSlice.BuildForScope(this);
                    }
                    return (TState)(object)slice.State!;
                }

                return owner.Get<TState>(r);
            }

            public bool TryGet<TState>(Reference? reference, out TState state) where TState : BaseState
            {
                Reference r = owner.FromReference(reference);
                if (overlay.TryGetValue(r, typeof(TState), out var fromOverlay))
                {
                    state = (TState)(object)fromOverlay!;
                    return true;
                }

                if (owner.TryGetSlice(r, typeof(TState), out BaseSlice slice))
                {
                    if (slice is AggregateSlice aSlice)
                    {
                        state = (TState)aSlice.BuildForScope(this);
                        return true;
                    }
                    state = (TState)(object)slice.State!;
                    return true;
                }

                state = default!;
                return false;
            }
        }
    }
}
