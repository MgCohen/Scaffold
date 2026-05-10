#nullable enable
using System;
using Scaffold.Variables;

namespace Scaffold.GraphFlow
{
    public abstract class Port
    {
        internal virtual void ConnectFrom(Port output) =>
            throw new InvalidOperationException(
                $"ConnectFrom is only valid on InputPort<T>; got {GetType()}.");

        internal virtual void ConnectFromVariable(IVariableHandle handle) =>
            throw new InvalidOperationException(
                $"ConnectFromVariable is only valid on InputPort<T>; got {GetType()}.");
    }
}
