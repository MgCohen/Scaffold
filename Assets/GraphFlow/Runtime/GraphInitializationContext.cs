namespace Scaffold.GraphFlow
{
    public sealed class GraphInitializationContext
    {
        public GraphInitializationContext(
            GraphRunner runner,
            ExecutableGraph executableGraph,
            RuntimeGraph asset,
            IGraphTickService tickService)
        {
            Runner = runner;
            ExecutableGraph = executableGraph;
            Asset = asset;
            TickService = tickService;
        }

        public GraphRunner Runner { get; }
        public ExecutableGraph ExecutableGraph { get; }
        public RuntimeGraph Asset { get; }
        public IGraphTickService TickService { get; }
    }
}
