#nullable enable
using System;

namespace Scaffold.Variables
{
    public interface IVariableHandle
    {
        string Id { get; }
        Type Type { get; }
    }

    public interface IReadOnlyVariableHandle<T> : IVariableHandle
    {
        T Value { get; }
        void Subscribe(Action<T> handler);
        void Unsubscribe(Action<T> handler);
    }

    public interface IVariableHandle<T> : IReadOnlyVariableHandle<T>
    {
        void Set(T value);
    }
}
