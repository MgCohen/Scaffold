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
        public FlowInPort In = null!;
        public InputPort<TResult> Value = null!;

        public Return()
        {
            In = new FlowInPort(this);
            Value = new InputPort<TResult>();
            Ports.Add(In.Name, In);
            Ports.Add(nameof(Value), Value);
        }

        public override Task Execute(Flow flow) => flow.Return(Value.Read());
    }
}
