#nullable enable
using System;
using System.Collections.Generic;
using Scaffold.Entities;
using Scaffold.States;
using Scaffold.Variables;
using Variable = Scaffold.Variables.Variable;

namespace Scaffold.Entities.States
{
    // Fluent builder for a store-backed IVariableBag. Bindings are collected
    // by (Reference, TState) slice; Build() validates payloads, installs one
    // store subscription per slice that fans out to each handle, primes each
    // handle's cached _last from the current slice state, and returns a
    // disposable bag.
    public sealed class StoreVariableBagBuilder
    {
        readonly Store _store;
        readonly Dictionary<(Reference Reference, Type StateType), IBindingGroup> _groups = new();
        readonly Dictionary<string, IVariableHandle> _handles = new();
        readonly List<Type> _writablePayloadTypes = new();

        IEnumerable<(string id, VariableDefault? @default)>? _fallbackSeed;
        FallbackMode _fallbackMode = FallbackMode.InMemoryDefault;
        bool _hasFallback;

        public StoreVariableBagBuilder(Store store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public SliceScope<TState> ForSlice<TState>(Reference reference) where TState : State
        {
            if (reference is null) throw new ArgumentNullException(nameof(reference));
            return new SliceScope<TState>(this, reference);
        }

        public EntityScope ForEntity(Reference reference)
        {
            if (reference is null) throw new ArgumentNullException(nameof(reference));
            return new EntityScope(this, reference);
        }

        // The seed shape matches Scaffold.Variables.InMemoryVariableBag's seed
        // constructor so callers can forward whatever they already use to build
        // an in-memory bag (e.g. graphflow's RuntimeVariable list projected to
        // (id, defaultValue) pairs).
        public StoreVariableBagBuilder WithFallback(IEnumerable<(string id, VariableDefault? @default)> seed, FallbackMode mode)
        {
            _fallbackSeed = seed;
            _fallbackMode = mode;
            _hasFallback = true;
            return this;
        }

        public StoreBackedVariableBag Build()
        {
            ValidatePayloadsOrThrow();

            var cleanups = new List<Action>();
            foreach (var group in _groups.Values)
            {
                group.Install(_store, cleanups);
            }

            ApplyFallback();
            return new StoreBackedVariableBag(_handles, cleanups);
        }

        internal Store Store => _store;

        internal void AddTypedHandle<TState, T>(Reference reference, StoreBackedHandle<TState, T> handle, Type payloadType) where TState : State
        {
            ThrowIfDuplicateId(handle.Id);
            var group = GetOrCreateGroup<TState>(reference);
            group.AddListener(handle);
            _handles.Add(handle.Id, handle);
            _writablePayloadTypes.Add(payloadType);
        }

        internal void AddReadOnlyHandle<TState, T>(Reference reference, ReadOnlyStoreBackedHandle<TState, T> handle) where TState : State
        {
            ThrowIfDuplicateId(handle.Id);
            var group = GetOrCreateGroup<TState>(reference);
            group.AddListener(handle);
            _handles.Add(handle.Id, handle);
        }

        void ThrowIfDuplicateId(string id)
        {
            if (_handles.ContainsKey(id))
            {
                throw new InvalidOperationException(
                    $"StoreVariableBagBuilder: variable id '{id}' is already bound.");
            }
        }

        TypedBindingGroup<TState> GetOrCreateGroup<TState>(Reference reference) where TState : State
        {
            var key = (reference, typeof(TState));
            if (!_groups.TryGetValue(key, out var existing))
            {
                var created = new TypedBindingGroup<TState>(reference);
                _groups[key] = created;
                return created;
            }
            return (TypedBindingGroup<TState>)existing;
        }

        void ValidatePayloadsOrThrow()
        {
            List<Type>? missing = null;
            for (int i = 0; i < _writablePayloadTypes.Count; i++)
            {
                var t = _writablePayloadTypes[i];
                if (_store.IsPayloadRegistered(t)) continue;
                missing ??= new List<Type>();
                if (!missing.Contains(t)) missing.Add(t);
            }
            if (missing == null) return;

            var names = string.Join(", ", missing.ConvertAll(t => t.FullName));
            throw new InvalidOperationException(
                $"StoreVariableBagBuilder.Build: writable bindings reference unregistered payload types: {names}.");
        }

        void ApplyFallback()
        {
            if (!_hasFallback || _fallbackSeed == null) return;

            List<string>? unbound = null;
            foreach (var (id, def) in _fallbackSeed)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (_handles.ContainsKey(id)) continue;

                if (_fallbackMode == FallbackMode.Throw)
                {
                    unbound ??= new List<string>();
                    unbound.Add(id);
                    continue;
                }

                if (def == null) continue;
                var handle = def.CreateHandle(id);
                if (handle == null) continue;
                _handles.Add(id, handle);
            }

            if (unbound != null)
            {
                throw new InvalidOperationException(
                    "StoreVariableBagBuilder.Build (FallbackMode.Throw): the following seed variables are not bound: " +
                    string.Join(", ", unbound));
            }
        }
    }

    public readonly struct SliceScope<TState> where TState : State
    {
        readonly StoreVariableBagBuilder _builder;
        readonly Reference _reference;

        internal SliceScope(StoreVariableBagBuilder builder, Reference reference)
        {
            _builder = builder;
            _reference = reference;
        }

        public StoreVariableBagBuilder Bind<T, TPayload>(string variableId, Func<TState, T> project, Func<T, TPayload> toPayload)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (toPayload == null) throw new ArgumentNullException(nameof(toPayload));

            Func<T, object> erased = v => toPayload(v)!;
            var handle = new StoreBackedHandle<TState, T>(variableId, _builder.Store, _reference, project, erased);
            _builder.AddTypedHandle(_reference, handle, typeof(TPayload));
            return _builder;
        }

        public StoreVariableBagBuilder BindReadOnly<T>(string variableId, Func<TState, T> project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            var handle = new ReadOnlyStoreBackedHandle<TState, T>(variableId, _builder.Store, _reference, project);
            _builder.AddReadOnlyHandle(_reference, handle);
            return _builder;
        }
    }

    public readonly struct EntityScope
    {
        readonly StoreVariableBagBuilder _builder;
        readonly Reference _reference;

        internal EntityScope(StoreVariableBagBuilder builder, Reference reference)
        {
            _builder = builder;
            _reference = reference;
        }

        // Writable: reads the base value out of EntityState; writes dispatch a
        // SetBaseValuePayload that rewraps T via the existing VariableValue<T>
        // for that entity variable. Internal CreateWithValue access is the same
        // mechanism StoreVariableStorage already relies on.
        public StoreVariableBagBuilder BindBase<T>(string variableId, Variable entityVariable)
        {
            if (entityVariable == null) throw new ArgumentNullException(nameof(entityVariable));

            Func<EntityState, T> project = state =>
            {
                if (state.TryGetBase(entityVariable, out var bv) && bv is IVariableValue<T> tv)
                    return tv.Get();
                return default!;
            };

            var store = _builder.Store;
            var reference = _reference;

            Func<T, object> toPayload = newVal =>
            {
                var slice = store.Get<EntityState>(reference);
                if (slice.TryGetBase(entityVariable, out var existing) && existing is VariableValue<T> ex)
                {
                    return new SetBaseValuePayload(reference, entityVariable, ex.CreateWithValue(newVal));
                }

                throw new InvalidOperationException(
                    $"EntityScope.BindBase: entity at {reference} has no base value for '{entityVariable.Id}' of type {typeof(T).Name}.");
            };

            var handle = new StoreBackedHandle<EntityState, T>(variableId, store, reference, project, toPayload);
            _builder.AddTypedHandle(reference, handle, typeof(SetBaseValuePayload));
            return _builder;
        }

        // Read-only: applies the modifier stack so the projected value reflects
        // base + modifiers, mirroring StoreVariableStorage's typed handle path.
        public StoreVariableBagBuilder BindComputed<T>(string variableId, Variable entityVariable)
        {
            if (entityVariable == null) throw new ArgumentNullException(nameof(entityVariable));

            Func<EntityState, T> project = state =>
            {
                if (!state.TryGetBase(entityVariable, out var anchor) || anchor is not IVariableValue<T> typedAnchor)
                    return default!;
                T result = typedAnchor.Get();
                foreach (var mod in state.GetModifiers(entityVariable))
                {
                    if (mod.Modifier is VariableModifier<T> typedMod)
                        result = typedMod.Apply(result);
                }
                return result;
            };

            var handle = new ReadOnlyStoreBackedHandle<EntityState, T>(variableId, _builder.Store, _reference, project);
            _builder.AddReadOnlyHandle(_reference, handle);
            return _builder;
        }
    }

    internal interface IBindingGroup
    {
        void Install(Store store, List<Action> cleanups);
    }

    internal sealed class TypedBindingGroup<TState> : IBindingGroup where TState : State
    {
        readonly Reference _reference;
        readonly List<ISliceListener<TState>> _listeners = new();

        public TypedBindingGroup(Reference reference)
        {
            _reference = reference;
        }

        public void AddListener(ISliceListener<TState> listener) => _listeners.Add(listener);

        public void Install(Store store, List<Action> cleanups)
        {
            if (store.TryGet<TState>(_reference, out var initial))
            {
                for (int i = 0; i < _listeners.Count; i++)
                    _listeners[i].Prime(initial);
            }

            Action<TState, StateChangeEvent> handler = OnSliceChanged;
            store.Subscribe<TState>(_reference, handler);
            cleanups.Add(() => store.Unsubscribe<TState>(_reference, handler));
        }

        void OnSliceChanged(TState state, StateChangeEvent change)
        {
            for (int i = 0; i < _listeners.Count; i++)
            {
                _listeners[i].OnSliceChanged(state);
            }
        }
    }
}
