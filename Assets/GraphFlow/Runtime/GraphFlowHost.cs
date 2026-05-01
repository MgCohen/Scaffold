using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Scaffold.GraphFlow
{
    public sealed class GraphFlowHost : MonoBehaviour, IGraphFlowObject
    {
        [SerializeField] RuntimeGraph graphAsset;
        ExecutableGraph executable;
        GraphRunner runner;

        public void Initialize(
            IReadOnlyList<IGraphMiddleware> middlewares,
            INodeExecutorRegistry registry,
            IGraphTickService tickService)
        {
            executable = graphAsset != null ? graphAsset.BuildExecutable(registry) : null;
            runner = new GraphRunner(middlewares, registry);
            if (executable == null)
                return;

            var ctx = new GraphInitializationContext(runner, executable, graphAsset, tickService);
            foreach (var def in CollectDefinitions(registry))
                def.Initialize(ctx);
        }

        public ValueTask<GraphRunResult> RunAsync<TEntry>(CancellationToken cancellationToken = default)
            where TEntry : GraphEntryPoint, new()
        {
            if (runner == null || executable == null)
                return new ValueTask<GraphRunResult>(new GraphRunResult(false, true, null));
            return runner.RunAsync<TEntry>(executable, cancellationToken);
        }

        static IEnumerable<IGraphNodeDefinition> CollectDefinitions(INodeExecutorRegistry registry)
        {
            if (registry is GraphFlowRegistry r)
            {
                foreach (var d in r.AllDefinitions)
                    yield return d;
            }
        }
    }
}
