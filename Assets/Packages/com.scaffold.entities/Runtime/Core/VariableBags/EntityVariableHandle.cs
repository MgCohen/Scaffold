#nullable enable
using System;
using Scaffold.Variables;

namespace Scaffold.Entities
{
    internal sealed class EntityVariableHandle<T> : IVariableHandle<T>
    {
        readonly Func<T> _read;
        readonly Action<T>? _write;

        public string Id { get; }
        public Type Type => typeof(T);
        public T Value => _read();

        internal EntityVariableHandle(string id, Func<T> read, Action<T>? write = null)
        {
            Id = id;
            _read = read;
            _write = write;
        }

        public void Set(T value) => _write?.Invoke(value);
        public void Subscribe(Action<T> handler) { }
        public void Unsubscribe(Action<T> handler) { }
    }

    internal sealed class EntityVariableHandle : IVariableHandle
    {
        public string Id { get; }
        public Type Type { get; }

        internal EntityVariableHandle(string id, Type type)
        {
            Id = id;
            Type = type;
        }

        internal static Type ResolvePayloadType(VariableValue val)
        {
            for (var t = val.GetType(); t != null; t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(VariableValue<>))
                    return t.GetGenericArguments()[0];
            }
            return typeof(object);
        }
    }
}
