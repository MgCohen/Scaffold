using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.Smoke
{
    /// <summary>
    /// Pure data node — converts int input to string output. No <c>TRunner</c>, no <c>Execute</c>.
    /// The generator emits the default ctor (<c>IntToStringRuntime.g.cs</c>), the editor mirror, and
    /// the registry entry from the <c>[GraphNode]</c> attribute and the typed port-handle fields below.
    /// </summary>
    [GraphNode(Category = "Convert")]
    public sealed partial class IntToStringRuntime : RuntimeNode
    {
        public InputPort<int> Value = null!;
        public OutputPort<string> Result = null!;

        partial void InitializePorts() =>
            Result = new OutputPort<string>(() => Value.Read().ToString());
    }
}
