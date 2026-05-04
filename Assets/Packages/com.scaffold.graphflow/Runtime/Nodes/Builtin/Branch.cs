using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    [GraphNode(Category = "Flow")]
    public sealed partial class Branch : RuntimeNode
    {
        public InputPort<bool> Condition = null!;
        public FlowInPort In = null!;
        public FlowOutPort True = null!;
        public FlowOutPort False = null!;

        public override Task Execute(Flow flow) =>
            flow.GoTo(Condition.Read() ? True : False);
    }
}
