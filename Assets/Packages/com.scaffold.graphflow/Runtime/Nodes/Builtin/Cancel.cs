using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    [GraphNode(Category = "Flow")]
    public sealed partial class Cancel : RuntimeNode
    {
        public FlowInPort In = null!;

        public override Task Execute(Flow flow) => flow.Cancel();
    }
}
