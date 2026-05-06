using System;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    [GraphNode(Category = "Flow")]
    public sealed partial class Loop : RuntimeNode
    {
        public InputPort<int> Count = null!;
        public FlowInPort Begin = null!;
        public FlowInPort Continue = null!;
        public FlowOutPort Body = null!;
        public FlowOutPort Done = null!;
        public OutputPort<int> Iteration = null!;

        partial void InitializePorts()
        {
            Iteration = new OutputPort<int>(flow => flow.GetSlot<int>(this), cache: false);

            Begin = FlowInPort.Sync(this, nameof(Begin), flow =>
            {
                flow.SetSlot(this, 0);
                return Count.Read(flow) > 0 ? Body : Done;
            });

            Continue = FlowInPort.Sync(this, nameof(Continue), flow =>
            {
                flow.InvalidateAll();
                var i = flow.GetSlot<int>(this) + 1;
                flow.SetSlot(this, i);
                return i < Count.Read(flow) ? Body : Done;
            });
        }
    }
}
