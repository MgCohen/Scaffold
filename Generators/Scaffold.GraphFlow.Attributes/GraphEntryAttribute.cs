using System;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Marks an <see cref="IGraphEntry{TRunner}"/> payload and supplies the runtime flow-out port id(s)
    /// for generated entry nodes.
    /// <para>M2 introduces an optional <see cref="ValidateFlowOutPortId"/> alongside the existing
    /// <see cref="FlowOutPortId"/> (which is the Run flow). When set, the generator emits a second
    /// arrowhead output port labeled "Validate" on the entry editor mirror; the bake step records it
    /// as a separate flow edge. Controller-side dispatch through Validate (running it before Run and
    /// short-circuiting on a <c>[ReturnBool]</c>-false) lands in M3+ — for M2 the Validate port is
    /// editor-authoring-and-bake-record only.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class GraphEntryAttribute : Attribute
    {
        /// <summary>Runtime <see cref="FlowContinuation"/> / <see cref="FlowEdge"/> id for the Run flow output (the existing single editor flow output).</summary>
        public int FlowOutPortId { get; set; }

        /// <summary>Optional Validate flow-out port id. When non-zero, the entry's editor mirror declares a second arrowhead output port "Validate". Default 0 = no Validate port.</summary>
        public int ValidateFlowOutPortId { get; set; }
    }
}
