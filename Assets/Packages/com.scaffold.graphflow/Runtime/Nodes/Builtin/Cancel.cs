using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.Nodes
{
    /// <summary>
    /// Flow terminator — sets the run's <see cref="FlowOutcome.Cancelled"/> outcome and stops the walk.
    /// </summary>
    [Serializable]
    [GraphNode(Category = "Flow")]
    public sealed partial class Cancel : RuntimeNode
    {
        public FlowInPort In = null!;

        public override Task Execute(Flow flow) => flow.Cancel();
    }
}
