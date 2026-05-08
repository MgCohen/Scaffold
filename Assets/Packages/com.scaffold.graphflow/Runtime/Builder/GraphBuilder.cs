#nullable enable
using System.Collections.Generic;

namespace Scaffold.GraphFlow
{
    public abstract class GraphBuilder<TRunner> where TRunner : GraphRunner
    {
        readonly Dictionary<GraphAsset, BakedGraph> _cache = new();

        public TRunner Build(GraphAsset<TRunner> asset)
        {
            if (!_cache.TryGetValue(asset, out var baked))
                _cache[asset] = baked = GraphTopology.Bake(asset);

            var runner = CreateRunner(baked);
            runner.SeedVariables(baked.Variables);
            foreach (var n in baked.Nodes) n.Initialize(runner);
            runner.Initialize();
            return runner;
        }

        protected abstract TRunner CreateRunner(BakedGraph baked);
    }
}
