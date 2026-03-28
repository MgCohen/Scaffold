using System.Collections.Generic;

namespace Scaffold.GraphFlow
{
    public sealed class ReactiveHookRuntime
    {
        public ReactiveHookRuntime(
            MiddlewarePhase timing,
            IGraphNodeDefinition targetDefinition,
            ExecutableGraph reactiveSubgraph)
        {
            Timing = timing;
            TargetDefinition = targetDefinition;
            ReactiveSubgraph = reactiveSubgraph;
        }

        public MiddlewarePhase Timing { get; }
        public IGraphNodeDefinition TargetDefinition { get; }
        public ExecutableGraph ReactiveSubgraph { get; }
    }
}
