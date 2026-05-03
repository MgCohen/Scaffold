using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.M0
{
    public sealed class GraphExecutor<TRunner> where TRunner : GraphRunner
    {
        public async Task RunFlow(RuntimeNode<TRunner> start, TRunner runner, GraphAsset<TRunner> asset)
        {
            RuntimeNode<TRunner>? current = start;
            while (current != null)
            {
                var cont = await current.Execute(runner).ConfigureAwait(false);
                if (!cont.HasNext)
                    break;

                current = TryGetFlowTarget(current.nodeId, cont.OutFlowPortId, asset);
            }
        }

        static RuntimeNode<TRunner>? TryGetFlowTarget(int fromNodeId, int fromFlowPortId, GraphAsset<TRunner> asset)
        {
            var edges = asset.flowEdges;
            if (edges == null)
                return null;

            foreach (var e in edges)
            {
                if (e.fromNodeId != fromNodeId || e.fromFlowPortId != fromFlowPortId)
                    continue;

                foreach (var n in asset.nodes)
                {
                    if (n.nodeId == e.toNodeId)
                        return n;
                }
            }

            return null;
        }
    }
}
