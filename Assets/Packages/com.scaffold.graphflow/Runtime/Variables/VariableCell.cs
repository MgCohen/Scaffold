#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.GraphFlow
{
    public abstract class VariableCell
    {
        public string Id { get; }
        public Type Type { get; }

        protected VariableCell(string id, Type type)
        {
            Id = id;
            Type = type;
        }
    }

    public sealed class VariableCell<T> : VariableCell
    {
        T _value;

        public T Value
        {
            get => _value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(_value, value)) return;
                _value = value;
                Changed?.Invoke(value);
            }
        }

        public event Action<T>? Changed;

        public VariableCell(string id, T initial) : base(id, typeof(T))
        {
            _value = initial;
        }
    }
}
