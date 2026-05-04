#nullable enable
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    public sealed class GraphExecutor<TRunner> where TRunner : GraphRunner
    {
        public async Task<Flow> RunFlow(RuntimeNode start, TRunner runner, GraphAsset<TRunner> asset, IEffectScope? scope = null, CancellationToken ct = default)
        {
            var flow = new Flow(ct) { Scope = scope, Runner = runner };
            RuntimeNode? current = start;
            while (current != null)
            {
                await current.Execute(flow).ConfigureAwait(false);
                var nextPortName = flow.ConsumeNext();
                if (nextPortName == null)
                    break;

                current = TryGetFlowTarget(current.nodeId, nextPortName, asset);
            }

            return flow;
        }

        static RuntimeNode? TryGetFlowTarget(int fromNodeId, string fromFlowPortName, GraphAsset<TRunner> asset)
        {
            var edges = asset.flowEdges;
            if (edges == null)
                return null;

            foreach (var e in edges)
            {
                if (e.fromNodeId != fromNodeId || e.fromFlowPortName != fromFlowPortName)
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
