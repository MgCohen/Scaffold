#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.Variables
{
    public sealed class InMemoryHandle<T> : IVariableHandle<T>
    {
        T _value;
        Action<T>? _subscribers;

        public string Id { get; }
        public Type Type => typeof(T);

        public T Value => _value;

        public InMemoryHandle(string id, T initial)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Handle id must be non-empty.", nameof(id));
            Id = id;
            _value = initial;
        }

        public void Set(T value)
        {
            if (EqualityComparer<T>.Default.Equals(_value, value)) return;
            _value = value;
            _subscribers?.Invoke(value);
        }

        public void Subscribe(Action<T> handler) => _subscribers += handler;
        public void Unsubscribe(Action<T> handler) => _subscribers -= handler;
    }
}
