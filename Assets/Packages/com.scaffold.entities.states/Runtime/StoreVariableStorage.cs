#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    internal sealed class StoreVariableStorage : IEntityVariableStorage, IDisposable
    {
        public StoreVariableStorage(Store store, InstanceId id, IEntityDefinition definition)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            entityId = id ?? throw new ArgumentNullException(nameof(id));
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            onStateChangedHandler = OnStateChanged;

            EntityVariableState initial = store.Get<EntityVariableState>(entityId);
            RebuildCachesFromState(initial, fireNotifications: false);

            store.Subscribe<EntityVariableState>(entityId, onStateChangedHandler);
        }

        public IEnumerable<Variable> Variables
        {
            get
            {
                ThrowIfDisposed();
                return lastKeySnapshot.OrderBy(v => v.Key, StringComparer.Ordinal);
            }
        }

        private readonly Dictionary<Variable, VariableValue> effectiveCache = new();
        private readonly Dictionary<Variable, List<Action<VariableValue>>> perVariableSubscribers = new();
        private readonly List<Action<VariableStructuralChange, Variable, VariableValue?>> structuralSubscribers = new();
        private readonly HashSet<Variable> lastKeySnapshot = new();
        private readonly Action<IReference, EntityVariableState, StateChangeEvent> onStateChangedHandler;

        private Store store = default!;
        private InstanceId entityId = default!;
        private IEntityDefinition definition = default!;
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            store.Unsubscribe<EntityVariableState>(entityId, onStateChangedHandler);
            store = null!;
            entityId = null!;
            definition = null!;
            perVariableSubscribers.Clear();
            structuralSubscribers.Clear();
            effectiveCache.Clear();
            lastKeySnapshot.Clear();
        }

        public IDisposable Subscribe(Variable key, Action<VariableValue> onChange)
        {
            if (key == null || onChange == null)
            {
                return EmptyDisposable.Instance;
            }

            ThrowIfDisposed();
            RegisterSubscriber(key, onChange);
            FireInitialSubscriptionValue(key, onChange);
            return new CallbackDisposable(() => Unsubscribe(key, onChange));
        }

        private void RegisterSubscriber(Variable key, Action<VariableValue> onChange)
        {
            if (!perVariableSubscribers.TryGetValue(key, out List<Action<VariableValue>>? list))
            {
                list = new List<Action<VariableValue>>();
                perVariableSubscribers[key] = list;
            }

            list.Add(onChange);
        }

        private void FireInitialSubscriptionValue(Variable key, Action<VariableValue> onChange)
        {
            if (TryGetEffective(key, out VariableValue? current) && current != null)
            {
                onChange(current);
            }
        }

        public bool TryGetEffective(Variable key, out VariableValue value)
        {
            ThrowIfDisposed();
            if (key == null)
            {
                value = null!;
                return false;
            }

            if (effectiveCache.TryGetValue(key, out VariableValue? cached) && cached != null)
            {
                value = cached;
                return true;
            }

            EntityVariableState state = store.Get<EntityVariableState>(entityId);
            return BuildCurrentValueMap(state).TryGetValue(key, out value!);
        }

        public bool TryGetBase(Variable key, out VariableValue value)
        {
            ThrowIfDisposed();
            if (key == null)
            {
                value = null!;
                return false;
            }

            EntityVariableState state = store.Get<EntityVariableState>(entityId);
            if (state.BaseValues.TryGetValue(key, out VariableValue? bv) && bv != null)
            {
                value = bv;
                return true;
            }

            return definition.TryGetDefaultValue(key, out value!);
        }

        public void Unsubscribe(Variable key, Action<VariableValue> onChange)
        {
            if (key == null || onChange == null)
            {
                return;
            }

            if (!perVariableSubscribers.TryGetValue(key, out List<Action<VariableValue>>? list))
            {
                return;
            }

            list.Remove(onChange);
            if (list.Count == 0)
            {
                perVariableSubscribers.Remove(key);
            }
        }

        public IDisposable SubscribeToVariableStructuralChanges(Action<VariableStructuralChange, Variable, VariableValue?> handler)
        {
            if (handler == null)
            {
                return EmptyDisposable.Instance;
            }

            ThrowIfDisposed();
            structuralSubscribers.Add(handler);
            return new CallbackDisposable(() => structuralSubscribers.Remove(handler));
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(StoreVariableStorage));
            }
        }

        private void OnStateChanged(IReference _, EntityVariableState newState, StateChangeEvent ev)
        {
            if (disposed)
            {
                return;
            }

            if (ev == StateChangeEvent.Removed)
            {
                HandleCanonicalRemoved();
                return;
            }

            ApplyCanonicalUpdate(newState);
        }

        private void HandleCanonicalRemoved()
        {
            NotifyStructuralRemovedAll();
            effectiveCache.Clear();
            lastKeySnapshot.Clear();
        }

        private void ApplyCanonicalUpdate(EntityVariableState newState)
        {
            HashSet<Variable> newKeys = ComputeKeySet(newState);
            Dictionary<Variable, VariableValue> newMap = BuildCurrentValueMap(newState);
            NotifyVariableSubscribers(newMap);
            NotifyStructuralDiffs(newKeys, newMap);
            CopyMapsIntoCaches(newMap, newKeys);
        }

        private void RebuildCachesFromState(EntityVariableState state, bool fireNotifications)
        {
            HashSet<Variable> newKeys = ComputeKeySet(state);
            Dictionary<Variable, VariableValue> newMap = BuildCurrentValueMap(state);
            if (fireNotifications)
            {
                NotifyVariableSubscribers(newMap);
                NotifyStructuralDiffs(newKeys, newMap);
            }

            CopyMapsIntoCaches(newMap, newKeys);
        }

        private void CopyMapsIntoCaches(Dictionary<Variable, VariableValue> newMap, HashSet<Variable> newKeys)
        {
            effectiveCache.Clear();
            foreach (KeyValuePair<Variable, VariableValue> kv in newMap)
            {
                effectiveCache[kv.Key] = kv.Value;
            }

            lastKeySnapshot.Clear();
            foreach (Variable v in newKeys)
            {
                lastKeySnapshot.Add(v);
            }
        }

        private void NotifyStructuralRemovedAll()
        {
            Variable[] keys = lastKeySnapshot.ToArray();
            Action<VariableStructuralChange, Variable, VariableValue?>[] handlers = structuralSubscribers.ToArray();
            foreach (Variable v in keys)
            {
                foreach (Action<VariableStructuralChange, Variable, VariableValue?> h in handlers)
                {
                    h(VariableStructuralChange.Removed, v, null);
                }
            }
        }

        private void NotifyVariableSubscribers(Dictionary<Variable, VariableValue> newMap)
        {
            foreach (Variable subKey in perVariableSubscribers.Keys.ToList())
            {
                NotifyOneVariableSubscribers(subKey, newMap);
            }
        }

        private void NotifyOneVariableSubscribers(Variable subKey, Dictionary<Variable, VariableValue> newMap)
        {
            if (!perVariableSubscribers.TryGetValue(subKey, out List<Action<VariableValue>>? list) || list.Count == 0)
            {
                return;
            }

            newMap.TryGetValue(subKey, out VariableValue? currentValue);
            effectiveCache.TryGetValue(subKey, out VariableValue? prevValue);
            if (SameTypedPayloadValues(prevValue, currentValue) || currentValue == null)
            {
                return;
            }

            foreach (Action<VariableValue> cb in list.ToArray())
            {
                cb(currentValue);
            }
        }

        private void NotifyStructuralDiffs(HashSet<Variable> newKeys, Dictionary<Variable, VariableValue> newMap)
        {
            Action<VariableStructuralChange, Variable, VariableValue?>[] handlers = structuralSubscribers.ToArray();
            EmitStructuralAdded(newKeys, newMap, handlers);
            EmitStructuralRemoved(newKeys, handlers);
        }

        private void EmitStructuralAdded(HashSet<Variable> newKeys, Dictionary<Variable, VariableValue> newMap, Action<VariableStructuralChange, Variable, VariableValue?>[] handlers)
        {
            foreach (Variable v in newKeys.Except(lastKeySnapshot))
            {
                newMap.TryGetValue(v, out VariableValue? val);
                foreach (Action<VariableStructuralChange, Variable, VariableValue?> h in handlers)
                {
                    h(VariableStructuralChange.Added, v, val);
                }
            }
        }

        private void EmitStructuralRemoved(HashSet<Variable> newKeys, Action<VariableStructuralChange, Variable, VariableValue?>[] handlers)
        {
            foreach (Variable v in lastKeySnapshot.Except(newKeys))
            {
                foreach (Action<VariableStructuralChange, Variable, VariableValue?> h in handlers)
                {
                    h(VariableStructuralChange.Removed, v, null);
                }
            }
        }

        private Dictionary<Variable, VariableValue> BuildCurrentValueMap(EntityVariableState state)
        {
            IReadOnlyDictionary<Variable, VariableValue> withMods = state.ResolveEffectiveValues(definition);
            HashSet<Variable> keys = ComputeKeySet(state);
            var map = new Dictionary<Variable, VariableValue>();
            foreach (Variable key in keys)
            {
                TryAddResolvedValueForKey(state, withMods, key, map);
            }

            return map;
        }

        private void TryAddResolvedValueForKey(EntityVariableState state, IReadOnlyDictionary<Variable, VariableValue> withMods, Variable key, Dictionary<Variable, VariableValue> map)
        {
            if (withMods.TryGetValue(key, out VariableValue? eff))
            {
                map[key] = eff;
                return;
            }

            if (state.BaseValues.TryGetValue(key, out VariableValue? bv))
            {
                map[key] = bv;
                return;
            }

            if (definition.TryGetDefaultValue(key, out VariableValue? dv))
            {
                map[key] = dv;
            }
        }

        private HashSet<Variable> ComputeKeySet(EntityVariableState state)
        {
            var newKeys = new HashSet<Variable>();
            foreach (Variable k in state.BaseValues.Keys)
            {
                newKeys.Add(k);
            }

            foreach (Variable k in state.ModifierStacks.Keys)
            {
                newKeys.Add(k);
            }

            foreach (Variable v in definition.DefinedVariables)
            {
                newKeys.Add(v);
            }

            return newKeys;
        }

        private bool SameTypedPayloadValues(VariableValue? prev, VariableValue? cur)
        {
            if (ReferenceEquals(prev, cur))
            {
                return true;
            }

            if (prev is null || cur is null)
            {
                return prev is null && cur is null;
            }

            if (prev.GetType() != cur.GetType())
            {
                return false;
            }

            return MatchKnownPayloadEquals(prev, cur) || ReferenceEquals(prev, cur);
        }

        private bool MatchKnownPayloadEquals(VariableValue prev, VariableValue cur)
        {
            return MatchFloatEquals(prev, cur)
                || MatchIntEquals(prev, cur)
                || MatchDoubleEquals(prev, cur)
                || MatchLongEquals(prev, cur)
                || MatchBoolEquals(prev, cur)
                || MatchStringEquals(prev, cur);
        }

        private bool MatchFloatEquals(VariableValue prev, VariableValue cur)
        {
            return prev is IVariableValue<float> pf && cur is IVariableValue<float> cf && pf.Get().Equals(cf.Get());
        }

        private bool MatchIntEquals(VariableValue prev, VariableValue cur)
        {
            return prev is IVariableValue<int> pi && cur is IVariableValue<int> ci && pi.Get().Equals(ci.Get());
        }

        private bool MatchDoubleEquals(VariableValue prev, VariableValue cur)
        {
            return prev is IVariableValue<double> pd && cur is IVariableValue<double> cd && pd.Get().Equals(cd.Get());
        }

        private bool MatchLongEquals(VariableValue prev, VariableValue cur)
        {
            return prev is IVariableValue<long> pl && cur is IVariableValue<long> cl && pl.Get().Equals(cl.Get());
        }

        private bool MatchBoolEquals(VariableValue prev, VariableValue cur)
        {
            return prev is IVariableValue<bool> pb && cur is IVariableValue<bool> cb && pb.Get().Equals(cb.Get());
        }

        private bool MatchStringEquals(VariableValue prev, VariableValue cur)
        {
            return prev is IVariableValue<string> ps && cur is IVariableValue<string> cs && string.Equals(ps.Get(), cs.Get(), StringComparison.Ordinal);
        }
    }
}
