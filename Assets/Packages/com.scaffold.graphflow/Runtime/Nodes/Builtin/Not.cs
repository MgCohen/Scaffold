namespace Scaffold.GraphFlow.Nodes
{
    /// <summary>
    /// Pure data node — bool → !bool.
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
