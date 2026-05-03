using System;

namespace Scaffold.GraphFlow
{
    /// <summary>Marks an <see cref="IGraphEntry{TRunner}"/> payload and supplies the runtime flow-out port id for generated entry nodes.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class GraphEntryAttribute : Attribute
    {
        /// <summary>Runtime <see cref="FlowContinuation"/> / <see cref="FlowEdge"/> id for the single editor flow output.</summary>
        public int FlowOutPortId { get; set; }
    }
}
