using System.Threading.Tasks;

namespace Scaffold.GraphFlow.Nodes
{
    /// <summary>
    /// Typed flow terminator — reads <see cref="Value"/> and writes it to the run's
    /// <see cref="Flow.Result"/> with <see cref="FlowOutcome.Returned"/>. Replaces the M2 untyped
    /// <c>Return</c> + <c>ReturnBool</c>; one TResult per graph (M3 validation EFG-V07).
    /// </summary>
    [GraphNode(Category = "Flow")]
    public sealed class Return<TRunner, TResult> : RuntimeNode<TRunner> where TRunner : GraphRunner
    {
        public const int FlowInPortId = 0;
        public const int ValuePortId  = unchecked((int)0x00000001u);

        public InputPort<TResult> Value = null!;

        public Return()
        {
            Value = new InputPort<TResult>();
            Ports.Add(ValuePortId, Value);
        }

        public override Task Execute(TRunner runner, Flow flow) => flow.Return(Value.Read());
    }
}
