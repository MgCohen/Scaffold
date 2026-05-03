using System.Threading.Tasks;

namespace Scaffold.GraphFlow.M0
{
    /// <summary>M0 tree walker — linear flow chain sufficient for OnPlay → Log.</summary>
    public sealed class GraphExecutor<TRunner> where TRunner : GraphRunner
    {
        public async ValueTask RunChain(RuntimeNode<TRunner> start, TRunner runner, GraphAsset<TRunner> asset)
        {
            var current = start;
            while (current != null)
            {
                await current.Execute(runner).ConfigureAwait(false);
                current = TryGetFlowSuccessor(current, asset);
            }
        }

        static RuntimeNode<TRunner>? TryGetFlowSuccessor(RuntimeNode<TRunner> node, GraphAsset<TRunner> asset)
        {
            foreach (var c in asset.connections)
            {
                if (c.fromNodeId != node.nodeId)
                    continue;
                if (!IsFlowPort(c.fromPortId))
                    continue;
                foreach (var n in asset.nodes)
                {
                    if (n.nodeId == c.toNodeId && IsFlowPort(c.toPortId))
                        return n;
                }
            }

            return null;
        }

        static bool IsFlowPort(int portId)
        {
            // Port ids with high nibble 0xE are synthetic flow ports in M0 smoke nodes.
            return (unchecked((uint)portId) >> 28) == 0xE;
        }
    }
}
