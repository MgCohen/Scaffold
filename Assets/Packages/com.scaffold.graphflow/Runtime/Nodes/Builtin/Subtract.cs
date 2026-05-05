using System;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    [GraphNode(Category = "Math")]
    public sealed partial class Subtract : RuntimeNode
    {
        public InputPort<int> A = null!;
        public InputPort<int> B = null!;
        public OutputPort<int> Result = null!;

        partial void InitializePorts() =>
            Result = new OutputPort<int>(() => A.Read() - B.Read());
    }
}
