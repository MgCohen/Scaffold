using System;

namespace Scaffold.GraphFlow.Nodes
{
    /// <summary>Pure data node — int → string via <see cref="int.ToString()"/>.</summary>
    [Serializable]
    [GraphNode(Category = "Convert")]
    public sealed partial class IntToString : RuntimeNode
    {
        public InputPort<int> Value = null!;
        public OutputPort<string> Result = null!;

        partial void InitializePorts() =>
            Result = new OutputPort<string>(() => Value.Read().ToString());
    }
}
