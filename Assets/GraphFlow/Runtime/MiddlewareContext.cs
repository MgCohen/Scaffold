namespace Scaffold.GraphFlow
{
    public readonly struct MiddlewareContext
    {
        public MiddlewareContext(
            GraphRunner runner,
            ExecutableGraph graph,
            ExecutableNode currentNode,
            Flow flow,
            MiddlewarePhase phase,
            object instance)
        {
            Runner = runner;
            Graph = graph;
            CurrentNode = currentNode;
            Flow = flow;
            Phase = phase;
            Instance = instance;
        }

        public GraphRunner Runner { get; }
        public ExecutableGraph Graph { get; }
        public ExecutableNode CurrentNode { get; }
        public Flow Flow { get; }
        public MiddlewarePhase Phase { get; }
        public object Instance { get; }
    }
}
