using System.Threading.Tasks;

namespace Scaffold.GraphFlow.Nodes
{
    /// <summary>
    /// Generic flow-control node — reads a bool input and follows one of two flow-output ports.
    /// Hand-authored package built-in: ctor + Ports dict population live in this file (the M3
    /// generator's cross-asm walk for [GraphNode] discovery in package-namespaced types lands in
    /// phase 3 / D6). The editor mirror + registry entry are still emitted by the consumer-side
    /// generator from the [GraphNode] attribute below.
    /// </summary>
    [GraphNode(Category = "Flow")]
    public sealed class Branch<TRunner> : RuntimeNode<TRunner> where TRunner : GraphRunner
    {
        // Port-id literals match the M2 generator's sequential id rules (FlowIn = 0, then 1..N over
        // declared fields in source order, separating data ports from FlowOut handles which now carry
        // their port id directly via the int constants below).
        public const int FlowInPortId    = 0;
        public const int ConditionPortId = unchecked((int)0x00000001u);
        public const int TruePortId      = unchecked((int)0x00000002u);
        public const int FalsePortId     = unchecked((int)0x00000003u);

        public InputPort<bool> Condition = null!;

        public Branch()
        {
            Condition = new InputPort<bool>();
            Ports.Add(ConditionPortId, Condition);
        }

        public override Task Execute(TRunner runner, Flow flow) =>
            flow.GoTo(Condition.Read() ? TruePortId : FalsePortId);
    }
}
