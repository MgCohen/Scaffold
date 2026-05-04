using System;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Marker that opts an <see cref="IGraphEntry"/> payload into the generator's entry-emit pipeline.
    /// <para>Post-M3 phase 2 (decision #4): port IDs are field names; this attribute carries no
    /// port-id args. The flow-out port name is fixed to "FlowOut" for entries.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class GraphEntryAttribute : Attribute
    {
    }
}
