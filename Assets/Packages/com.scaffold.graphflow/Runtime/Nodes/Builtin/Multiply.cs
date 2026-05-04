using System;

namespace Scaffold.GraphFlow.Nodes
{
    /// <summary>Pure data node — int * int.</summary>
    [Serializable]
    [GraphNode(Category = "Math")]
    public sealed partial class Multiply : RuntimeNode
    {
        public InputPort<int> A = null!;
        public InputPort<int> B = null!;
        public OutputPort<int> Result = null!;

        partial void InitializePorts() =>
            Result = new OutputPort<int>(() => A.Read() * B.Read());
    }
}
