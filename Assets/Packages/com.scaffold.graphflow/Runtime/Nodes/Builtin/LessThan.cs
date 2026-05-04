using System;

namespace Scaffold.GraphFlow.Nodes
{
    /// <summary>Pure data node — int &lt; int. Multi-T generic <c>LessThan&lt;T&gt;</c>
    /// deferred (M4 multi-T spec).</summary>
    [Serializable]
    [GraphNode(Category = "Compare")]
    public sealed partial class LessThan : RuntimeNode
    {
        public InputPort<int> A = null!;
        public InputPort<int> B = null!;
        public OutputPort<bool> Result = null!;

        partial void InitializePorts() =>
            Result = new OutputPort<bool>(() => A.Read() < B.Read());
    }
}
