using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.Nodes
{
    /// <summary>
    /// Typed flow terminator — reads <see cref="Value"/> and writes it to the run's
    /// <see cref="Flow.Result"/> with <see cref="FlowOutcome.Returned"/>.
    /// Runner-agnostic (decision #5).
    /// </summary>
    [Serializable]
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

    /// <summary>
    /// Untyped flow terminator — equivalent to a bare <c>return;</c> in a void method. Used as the
    /// bake fallback for a Return editor node whose <c>ResultType</c> picker is left at
    /// <c>None</c>: the run ends with <see cref="FlowOutcome.Returned"/> and no stored value.
    /// </summary>
    [Serializable]
    public sealed class Return : RuntimeNode
    {
        public FlowInPort In = null!;

        public Return()
        {
            In = new FlowInPort(this);
            Ports.Add(In.Name, In);
        }

        public override Task Execute(Flow flow) => flow.Return();
    }
}
