using System.Threading.Tasks;

namespace Scaffold.GraphFlow.Nodes
{
    /// <summary>
    /// Flow terminator — sets the run's <see cref="FlowOutcome.Cancelled"/> outcome and stops the
    /// walk. No data ports, no flow outs.
    /// </summary>
    [GraphNode(Category = "Flow")]
    public sealed class Cancel<TRunner> : RuntimeNode<TRunner> where TRunner : GraphRunner
    {
        public Cancel() { }

        public override Task Execute(TRunner runner, Flow flow) => flow.Cancel();
    }
}
