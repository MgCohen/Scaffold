#nullable enable
using System;
using System.Collections.Generic;
using Scaffold.States;
using Scaffold.Variables;

namespace Scaffold.Entities.States
{
    // Read-only counterpart to StoreBackedHandle. Same subscription / dedup
    // semantics; lacks the payload factory because Set is not supported.
    public sealed class ReadOnlyStoreBackedHandle<TState, T> : IReadOnlyVariableHandle<T>, ISliceListener<TState>
        where TState : State
    {
        readonly Store _store;
        readonly Reference _reference;
        readonly Func<TState, T> _project;
        T _last = default!;
        bool _hasLast;
        Action<T>? _subscribers;

        public string Id { get; }
        public Type Type => typeof(T);

        public ReadOnlyStoreBackedHandle(string id, Store store, Reference reference, Func<TState, T> project)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Handle id must be non-empty.", nameof(id));
            Id = id;
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _reference = reference ?? throw new ArgumentNullException(nameof(reference));
            _project = project ?? throw new ArgumentNullException(nameof(project));
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
            _subscribers?.Invoke(next);
        }
    }
}
