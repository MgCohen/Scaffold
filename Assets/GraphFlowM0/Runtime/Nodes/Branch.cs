using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.M0.Nodes
{
    /// <summary>
    /// Generic flow-control node — reads a bool and follows one of two flow-output ports.
    /// Hand-written runtime; the editor mirror + registry entry are emitted by the generator
    /// from the [GraphNode]/[FlowIn]/[FlowOut]/[Input] attributes per package whose runner
    /// closes TRunner.
    /// </summary>
    [GraphNode(Category = "Flow")]
    [FlowIn(Ports.FlowIn)]
    [FlowOut(Ports.TrueOut, "True")]
    [FlowOut(Ports.FalseOut, "False")]
    public sealed class Branch<TRunner> : RuntimeNode<TRunner> where TRunner : GraphRunner
    {
        public static class Ports
        {
            public const int FlowIn   = unchecked((int)0xF0F0_0001u);
            public const int TrueOut  = unchecked((int)0xF0F0_0002u);
            public const int FalseOut = unchecked((int)0xF0F0_0003u);
            public const int Condition = unchecked((int)0xC0F0_0001u);
        }

        [Input(Ports.Condition)] [NonSerialized] public Connection<bool>? Condition;

        public override Connection GetOutputConnection(int portId) =>
            throw new ArgumentOutOfRangeException(nameof(portId));

        public override void BindInput(int portId, Connection connection)
        {
            switch (portId)
            {
                case Ports.Condition:
                    Condition = (Connection<bool>)connection;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(portId));
            }
        }

        public override Task<FlowContinuation> Execute(TRunner runner)
        {
            var cond = Condition != null && Condition.Read();
            return Task.FromResult(FlowContinuation.Next(cond ? Ports.TrueOut : Ports.FalseOut));
        }
    }
}
