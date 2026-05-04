#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Per-run flow walker. Reads no edge metadata at runtime — every step follows the destination
    /// stored on the <see cref="FlowOutPort"/> the previous node selected via
    /// <see cref="Flow.GoTo"/>. <c>asset.flowEdges</c> is consumed once at hydration by
    /// <see cref="GraphController{TRunner}.Initialize"/> to populate those refs.
    /// </summary>
    public sealed class GraphExecutor<TRunner> where TRunner : GraphRunner
    {
        public async Task<Flow> RunFlow(RuntimeNode start, TRunner runner, GraphAsset<TRunner> asset, IEffectScope? scope = null, CancellationToken ct = default)
        {
            var flow = new Flow(ct) { Scope = scope, Runner = runner };
            RuntimeNode? current = start;
            while (current != null)
            {
                await current.Execute(flow).ConfigureAwait(false);
                var nextOut = flow.ConsumeNext();
                current = nextOut?.Connection?.Destination.Owner;
            }

            return flow;
        }
    }
}
