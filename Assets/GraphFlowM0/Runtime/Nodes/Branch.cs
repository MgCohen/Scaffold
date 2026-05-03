using System.Threading.Tasks;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.Nodes
{
    /// <summary>
    /// Generic flow-control node — reads a bool input and follows one of two flow-output ports.
    /// Hand-written runtime; the editor mirror, registry entry, and default ctor (port construction
    /// + Ports dict population) are emitted by the generator from the typed port-handle fields below.
    /// </summary>
    [GraphNode(Category = "Flow")]
    public sealed partial class Branch<TRunner> : RuntimeNode<TRunner> where TRunner : GraphRunner
    {
        public InputPort<bool> Condition = null!;
        public FlowOut True;
        public FlowOut False;

        public override Task<FlowContinuation> Execute(TRunner runner) =>
            Task.FromResult(Condition.Read() ? True.Continue() : False.Continue());
    }
}
