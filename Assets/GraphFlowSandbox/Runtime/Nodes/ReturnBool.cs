using System.Threading.Tasks;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.Nodes
{
    /// <summary>
    /// Flow terminator — reads a bool input, writes it to <see cref="GraphRunner.ReturnValue"/>
    /// (boxed), and stops the flow walk. M3 introduces a typed return channel keyed by the runner's
    /// declared return type; this is the v1-style boxed slot.
    /// </summary>
    [GraphNode(Category = "Flow")]
    public sealed partial class ReturnBool<TRunner> : RuntimeNode<TRunner> where TRunner : GraphRunner
    {
        public InputPort<bool> Value = null!;

        public override Task<FlowContinuation> Execute(TRunner runner)
        {
            runner.ReturnValue = Value.Read();
            return Task.FromResult(FlowContinuation.Stop);
        }
    }
}
