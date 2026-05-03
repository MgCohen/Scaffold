using System;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Pairs a command-shaped <see cref="IGraphAction{TRunner}"/> payload with its result type for Mode 2 dispatcher emission
    /// (<c>DispatcherBase&lt;,&gt;</c> closed with command + result).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class GraphCommandPairAttribute : Attribute
    {
        public Type ResultType { get; set; } = null!;

        public int FlowInPortId { get; set; }

        public int FlowOutPortId { get; set; }
    }
}
