using System;

namespace Scaffold.GraphFlow.Nodes
{
    // Return<TResult>: hand-authored ctor — single-T generic NOT over a runner is
    // outside the partial generator's eligibility filter, so we own the construction
    // ourselves.
    [Serializable]
    [GraphNode(Category = "Flow")]
    public sealed class Return<TResult> : RuntimeNode
    {
        public FlowInPort In = null!;
        public InputPort<TResult> Value = null!;

        public Return()
        {
            Value = new InputPort<TResult>();
            In = FlowInPort.Sync(this, nameof(In),
                flow => flow.Return(Value.Read(flow)));
            Ports.Add(In.Name, In);
            Ports.Add(nameof(Value), Value);
        }
    }

    [Serializable]
    [GraphNode(Category = "Flow")]
    public sealed partial class Return : RuntimeNode
    {
        public FlowInPort In = null!;

        partial void InitializePorts() =>
            In = FlowInPort.Sync(this, nameof(In), flow => flow.Return());
    }
}
