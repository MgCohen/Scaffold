namespace Scaffold.GraphFlow.Nodes
{
    /// <summary>
    /// Pure data node — bool → !bool.
    /// </summary>
    [GraphNode(Category = "Logic")]
    public sealed class Not : RuntimeNode
    {
        public const string ValuePortName  = "Value";
        public const string ResultPortName = "Result";

        public InputPort<bool> Value = null!;
        public OutputPort<bool> Result = null!;

        public Not()
        {
            Value = new InputPort<bool>();
            Result = new OutputPort<bool>(() => !Value.Read());
            Ports.Add(ValuePortName, Value);
            Ports.Add(ResultPortName, Result);
        }
    }
}
