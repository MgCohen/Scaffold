using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    public sealed class GraphExecutor<TRunner> where TRunner : GraphRunner
    {
        public async Task<Flow> RunFlow(RuntimeNode<TRunner> start, TRunner runner, GraphAsset<TRunner> asset, CancellationToken ct = default)
        {
            var flow = new Flow(ct);
            RuntimeNode<TRunner>? current = start;
            while (current != null)
            {
                await current.Execute(runner, flow).ConfigureAwait(false);
                var nextPortId = flow.ConsumeNext();
                if (nextPortId == null)
                    break;

                current = TryGetFlowTarget(current.nodeId, nextPortId.Value, asset);
            }

            return flow;
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
                    // Data nodes (RuntimeNode without TRunner) cannot be flow targets — they have no
                    // FlowIn port. A flow edge pointing at one is a bake error; the cast returns null
                    // and the executor stops.
                    if (n.nodeId == e.toNodeId)
                        return n as RuntimeNode<TRunner>;
                }
            }

            return null;
        }
    }
}
