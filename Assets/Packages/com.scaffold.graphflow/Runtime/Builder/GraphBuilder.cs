#nullable enable
using System.Runtime.CompilerServices;

namespace Scaffold.GraphFlow
{
    public abstract class GraphBuilder<TRunner> where TRunner : GraphRunner
    {
        readonly ConditionalWeakTable<GraphAsset, BakedGraph> _cache = new();

        public TRunner Build(GraphAsset<TRunner> asset)
        {
            var baked = _cache.GetValue(asset, a => GraphTopology.Bake(a));

            var runner = CreateRunner(baked);
            foreach (var n in baked.Nodes) n.Initialize(runner);
            runner.Initialize();
            return runner;
        }

        protected abstract TRunner CreateRunner(BakedGraph baked);
    }
}
