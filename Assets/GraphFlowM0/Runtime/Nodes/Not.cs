using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.Nodes
{
    /// <summary>
    /// Pure data node — bool → !bool. No <c>TRunner</c>, no <c>Execute</c>: just typed ports + the
    /// <see cref="InitializePorts"/> partial that wires <see cref="Result"/>'s reader to read from
    /// <see cref="Value"/>. Generator-emitted ctor (<c>Not.g.cs</c>) constructs the input port,
    /// calls <see cref="InitializePorts"/>, and populates the <c>Ports</c> dict.
    /// </summary>
    [GraphNode(Category = "Logic")]
    public sealed partial class Not : RuntimeNode
    {
        public InputPort<bool> Value = null!;
        public OutputPort<bool> Result = null!;

        partial void InitializePorts() =>
            Result = new OutputPort<bool>(() => !Value.Read());
    }
}
