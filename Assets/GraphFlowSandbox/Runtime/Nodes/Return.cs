using System.Threading.Tasks;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.Nodes
{
    /// <summary>
    /// Flow terminator — clears <see cref="GraphRunner.ReturnValue"/> and stops the flow walk.
    /// No data ports, no flow outs. The typed-payload Return ([Return Strike] / Return&lt;TCmd&gt;)
    /// lands with Mode 2 in M3.
    /// </summary>
    [GraphNode(Category = "Flow")]
    public sealed partial class Return<TRunner> : RuntimeNode<TRunner> where TRunner : GraphRunner
    {
        public override Task<FlowContinuation> Execute(TRunner runner)
        {
            runner.ReturnValue = null;
            return Task.FromResult(FlowContinuation.Stop);
        }
    }
}
