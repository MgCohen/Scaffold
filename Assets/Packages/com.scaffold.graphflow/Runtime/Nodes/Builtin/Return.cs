using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    [GraphNode(Category = "Flow")]
    public sealed class Return<TResult> : RuntimeNode
    {
        public FlowInPort In = null!;
        public InputPort<TResult> Value = null!;

        public Return()
        {
            In = new FlowInPort(this);
            Value = new InputPort<TResult>();
            Ports.Add(In.Name, In);
            Ports.Add(nameof(Value), Value);
        }

        public override Task Execute(Flow flow) => flow.Return(Value.Read());
    }

    // Untyped fallback for a Return editor node whose ResultType picker is None.
    [Serializable]
    public sealed class Return : RuntimeNode
    {
        public FlowInPort In = null!;

        public Return()
        {
            In = new FlowInPort(this);
            Ports.Add(In.Name, In);
        }

        public override Task Execute(Flow flow) => flow.Return();
    }
}
