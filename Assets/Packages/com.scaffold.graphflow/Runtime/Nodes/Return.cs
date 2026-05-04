using System.Threading.Tasks;

namespace Scaffold.GraphFlow.Nodes
{
    /// <summary>
    /// Typed flow terminator — reads <see cref="Value"/> and writes it to the run's
    /// <see cref="Flow.Result"/> with <see cref="FlowOutcome.Returned"/>.
    /// Runner-agnostic (decision #5).
    /// </summary>
    [GraphNode(Category = "Flow")]
    public sealed class Return<TResult> : RuntimeNode
    {
        public const string FlowInPortName = "FlowIn";
        public const string ValuePortName  = "Value";

        public InputPort<TResult> Value = null!;

        public Return()
        {
            Value = new InputPort<TResult>();
            Ports.Add(ValuePortName, Value);
        }

        public override Task Execute(Flow flow) => flow.Return(Value.Read());
    }
}
