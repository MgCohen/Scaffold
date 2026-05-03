namespace Scaffold.GraphFlow.Nodes
{
    /// <summary>
    /// Pure data node — bool → !bool. No <c>TRunner</c>, no <c>Execute</c>: just typed ports + the
    /// reader closure on the output port. Hand-authored package built-in (see Branch.cs note re:
    /// generator cross-asm walk landing in phase 3 / D6).
    /// </summary>
    [GraphNode(Category = "Logic")]
    public sealed class Not : RuntimeNode
    {
        public const int ValuePortId  = unchecked((int)0x00000001u);
        public const int ResultPortId = unchecked((int)0x00000002u);

        public InputPort<bool> Value = null!;
        public OutputPort<bool> Result = null!;

        public Not()
        {
            Value = new InputPort<bool>();
            Result = new OutputPort<bool>(() => !Value.Read());
            Ports.Add(ValuePortId, Value);
            Ports.Add(ResultPortId, Result);
        }
    }
}
