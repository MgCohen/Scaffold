using System.Threading.Tasks;

namespace Scaffold.GraphFlow.Nodes
{
    /// <summary>
    /// Generic flow-control node — reads a bool input and follows one of two flow-output ports.
    /// Runner-agnostic (decision #5): one registration per package, ever.
    /// </summary>
    [GraphNode(Category = "Flow")]
    public sealed class Branch : RuntimeNode
    {
        public const string FlowInPortName    = "FlowIn";
        public const string ConditionPortName = "Condition";
        public const string TruePortName      = "True";
        public const string FalsePortName     = "False";

        public InputPort<bool> Condition = null!;

        public Branch()
        {
            Condition = new InputPort<bool>();
            Ports.Add(ConditionPortName, Condition);
        }

        public override Task Execute(Flow flow) =>
            flow.GoTo(Condition.Read() ? TruePortName : FalsePortName);
    }
}
