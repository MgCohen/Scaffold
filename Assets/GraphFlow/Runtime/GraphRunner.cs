using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    public sealed partial class GraphRunner
    {
        readonly IReadOnlyList<IGraphMiddleware> middlewarePipeline;
        readonly INodeExecutorRegistry registry;

        public GraphRunner(IReadOnlyList<IGraphMiddleware> middlewarePipeline, INodeExecutorRegistry registry = null)
        {
            this.middlewarePipeline = middlewarePipeline ?? Array.Empty<IGraphMiddleware>();
            this.registry = registry;
        }

        public ValueTask<GraphRunResult> RunAsync<TEntry>(ExecutableGraph graph, CancellationToken cancellationToken = default)
            where TEntry : GraphEntryPoint, new()
            => RunAsync(graph, new TEntry(), null, cancellationToken);

        public ValueTask<GraphRunResult> RunAsync<TEntry>(
            ExecutableGraph graph,
            Flow flow,
            CancellationToken cancellationToken = default)
            where TEntry : GraphEntryPoint, new()
            => RunAsync(graph, new TEntry(), flow, cancellationToken);

        public async ValueTask<GraphRunResult> RunAsync<TEntry>(
            ExecutableGraph graph,
            TEntry entryPayload,
            Flow flow,
            CancellationToken cancellationToken = default)
            where TEntry : GraphEntryPoint
        {
            flow ??= new Flow(cancellationToken);
            if (registry != null)
            {
                flow.Registry = registry;
                flow.ActiveRunner = this;
            }

            if (!graph.EntryRoots.TryGetValue(typeof(TEntry), out var start))
                return new GraphRunResult(false, true, null);

            return await RunFromNode(graph, start, flow, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask<GraphRunResult> RunChildGraphAsync(
            ExecutableGraph childGraph,
            GraphEntryPoint entryPayload,
            Flow childFlow,
            CancellationToken cancellationToken = default)
        {
            var entryType = entryPayload.GetType();
            if (!childGraph.EntryRoots.TryGetValue(entryType, out var start))
                return new GraphRunResult(false, true, null);

            if (registry != null && childFlow.Registry == null)
            {
                childFlow.Registry = registry;
                childFlow.ActiveRunner = this;
            }

            return await RunFromNode(childGraph, start, childFlow, cancellationToken).ConfigureAwait(false);
        }

        async ValueTask<GraphRunResult> RunFromNode(
            ExecutableGraph graph,
            ExecutableNode start,
            Flow flow,
            CancellationToken cancellationToken)
        {
            var node = start;
            while (node != null)
            {
                flow.CurrentNode = node;
                var instance = node.Definition.CreateInstance();
                WireInstance(node, instance, flow);

                await InvokeMiddlewarePipeline(graph, node, flow, MiddlewarePhase.Before, instance, cancellationToken)
                    .ConfigureAwait(false);

                await node.Definition.ExecuteAsync(instance, flow, cancellationToken).ConfigureAwait(false);
                flow.LastInstanceByNode[node] = instance;

                await InvokeMiddlewarePipeline(graph, node, flow, MiddlewarePhase.After, instance, cancellationToken)
                    .ConfigureAwait(false);

                node = node.GetFlowSuccessor("Out");
            }

            return new GraphRunResult(false, true, null);
        }

        async ValueTask InvokeMiddlewarePipeline(
            ExecutableGraph graph,
            ExecutableNode node,
            Flow flow,
            MiddlewarePhase phase,
            object instance,
            CancellationToken cancellationToken)
        {
            if (middlewarePipeline.Count == 0)
                return;

            var ctx = new MiddlewareContext(this, graph, node, flow, phase, instance);

            async ValueTask ChainAsync(int index)
            {
                if (index >= middlewarePipeline.Count)
                    return;
                await middlewarePipeline[index]
                    .InvokeAsync(ctx, () => ChainAsync(index + 1))
                    .ConfigureAwait(false);
            }

            await ChainAsync(0).ConfigureAwait(false);
        }
    }
}
