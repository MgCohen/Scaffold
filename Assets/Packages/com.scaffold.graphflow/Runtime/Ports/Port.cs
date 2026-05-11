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

        // Called by GraphBuilder.Build after the runner is constructed and
        // before nodes are Initialize()'d. OutputPort<T> uses this to size its
        // per-flow Entry[] cache to the runner's MaxConcurrentFlows. Default
        // no-op for input / flow ports.
        internal virtual void Bake(int maxFlows) { }
    }
}
