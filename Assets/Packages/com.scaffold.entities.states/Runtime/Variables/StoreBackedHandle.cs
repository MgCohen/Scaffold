#nullable enable
using System;
using System.Collections.Generic;
using Scaffold.States;
using Scaffold.Variables;

namespace Scaffold.Entities.States
{
    // Writable handle that reads through the store and dispatches a payload on
    // write. The owning bag installs a single per-slice subscription that calls
    // OnSliceChanged on every handle bound to the slice. _last caches the most
    // recently-fired projected value so identical updates do not re-notify
    // subscribers, and so net-zero churn across a defer scope falls out for
    // free.
    public sealed class StoreBackedHandle<TState, T> : IVariableHandle<T>, ISliceListener<TState>
        where TState : State
    {
        readonly Store _store;
        readonly Reference _reference;
        readonly Func<TState, T> _project;
        readonly Func<T, object> _toPayload;
        T _last = default!;
        bool _hasLast;
        Action<T>? _subscribers;
        bool _applyingFromSubscribe;

        public string Id { get; }
        public Type Type => typeof(T);

        public StoreBackedHandle(string id, Store store, Reference reference,
                                 Func<TState, T> project, Func<T, object> toPayload)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Handle id must be non-empty.", nameof(id));
            Id = id;
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _reference = reference ?? throw new ArgumentNullException(nameof(reference));
            _project = project ?? throw new ArgumentNullException(nameof(project));
            _toPayload = toPayload ?? throw new ArgumentNullException(nameof(toPayload));
        }

        public T Value
        {
            get
            {
                if (_store.TryGet<TState>(_reference, out var state))
                    return _project(state);
                return _hasLast ? _last : default!;
            }
        }

        public void Set(T value)
        {
            if (_applyingFromSubscribe) return;
            if (_hasLast && EqualityComparer<T>.Default.Equals(_last, value)) return;
            _store.Execute(_reference, _toPayload(value));
        }

        public void Subscribe(Action<T> handler)
        {
            if (handler == null) return;
            _subscribers += handler;
        }

        public void Unsubscribe(Action<T> handler)
        {
            if (handler == null) return;
            _subscribers -= handler;
        }

        void ISliceListener<TState>.Prime(TState state)
        {
            _last = _project(state);
            _hasLast = true;
        }

        void ISliceListener<TState>.OnSliceChanged(TState state)
        {
            var next = _project(state);
            if (_hasLast && EqualityComparer<T>.Default.Equals(_last, next))
                return;
            _last = next;
            _hasLast = true;
            var subs = _subscribers;
            if (subs == null) return;
            _applyingFromSubscribe = true;
            try { subs.Invoke(next); }
            finally { _applyingFromSubscribe = false; }
        }
    }
}
