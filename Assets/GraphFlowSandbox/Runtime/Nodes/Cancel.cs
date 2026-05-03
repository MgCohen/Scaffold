using System.Threading.Tasks;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.Nodes
{
    /// <summary>
    /// Flow terminator — sets <see cref="GraphRunner.Cancelled"/> on the runner and stops the flow
    /// walk. No data ports, no flow outs. Author surface is bare; the generator emits the default
    /// ctor + registry entry from the empty port set.
    /// </summary>
    [GraphNode(Category = "Flow")]
    public sealed partial class Cancel<TRunner> : RuntimeNode<TRunner> where TRunner : GraphRunner
    {
        public override Task<FlowContinuation> Execute(TRunner runner)
        {
            runner.Cancelled = true;
            return Task.FromResult(FlowContinuation.Stop);
        }
    }
}
