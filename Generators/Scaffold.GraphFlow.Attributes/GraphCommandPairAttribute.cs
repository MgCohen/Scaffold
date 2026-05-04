using System;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Pairs a command-shaped payload with its result type for Mode 2 dispatcher emission.
    /// <para>Post-M3 phase 2 (decision #4): port IDs are field names; flow port-id args removed.
    /// The attribute is scheduled for full removal in phase 4 (decision #2).</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class GraphCommandPairAttribute : Attribute
    {
        public Type ResultType { get; set; } = null!;
    }
}
