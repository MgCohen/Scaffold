#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    public static class GraphExecutor
    {
        public static async Task<Flow> RunFlow<TRunner>(RuntimeNode start, TRunner runner, GraphAsset<TRunner> asset, object? scope = null, CancellationToken ct = default)
            where TRunner : GraphRunner
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
