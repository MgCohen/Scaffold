using System.Collections.Generic;

namespace Scaffold.GraphFlow
{
    public sealed class GraphFlowCentralService
    {
        readonly List<IGraphMiddleware> middlewares = new List<IGraphMiddleware>();
        readonly List<IGraphFlowObject> graphs = new List<IGraphFlowObject>();

        public void RegisterMiddleware(IGraphMiddleware middleware) => middlewares.Add(middleware);

        public void RegisterGraph(IGraphFlowObject graph) => graphs.Add(graph);

        public void Bootstrap(INodeExecutorRegistry registry, IGraphTickService tickService = null)
        {
            tickService ??= new NullGraphTickService();
            var shared = middlewares.ToArray();
            foreach (var g in graphs)
                g.Initialize(shared, registry, tickService);
        }
    }

    public interface IGraphFlowObject
    {
        void Initialize(IReadOnlyList<IGraphMiddleware> middlewares, INodeExecutorRegistry registry, IGraphTickService tickService);
    }
}
