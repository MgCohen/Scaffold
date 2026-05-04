using System.Threading.Tasks;

namespace Scaffold.GraphFlow.Nodes
{
    /// <summary>
    /// Flow terminator — sets the run's <see cref="FlowOutcome.Cancelled"/> outcome and stops the walk.
    /// </summary>
    [GraphNode(Category = "Flow")]
    public sealed class Cancel : RuntimeNode
    {
        public const string FlowInPortName = "FlowIn";

        public Cancel() { }

        public override Task Execute(Flow flow) => flow.Cancel();
    }
}
