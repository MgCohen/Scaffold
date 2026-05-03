namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Result of <see cref="RuntimeNode{TRunner}.Execute"/> — which flow output to follow, if any.
    /// </summary>
    public readonly struct FlowContinuation
    {
        /// <summary>No outgoing flow edge — stop the flow walk (leaf or pure data node).</summary>
        public static FlowContinuation Stop => default;

        /// <summary>Follow the <see cref="FlowEdge"/> whose <c>fromFlowPortId</c> matches <paramref name="outFlowPortId"/>.</summary>
        public static FlowContinuation Next(int outFlowPortId) => new FlowContinuation(outFlowPortId, true);

        public int OutFlowPortId { get; }
        public bool HasNext { get; }

        FlowContinuation(int outFlowPortId, bool hasNext)
        {
            OutFlowPortId = outFlowPortId;
            HasNext = hasNext;
        }
    }
}
