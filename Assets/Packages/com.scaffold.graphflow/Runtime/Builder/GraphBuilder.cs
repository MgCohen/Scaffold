#nullable enable
using UnityEngine;

namespace Scaffold.GraphFlow
{
    public abstract class GraphBuilder<TRunner> where TRunner : GraphRunner
    {
        public TRunner Build(GraphAsset<TRunner> asset)
        {
            var baked = GraphTopology.Bake(asset);
            var runner = CreateRunner(baked);
            runner.SeedVariables(baked.Variables);
            WireVariableEdges(baked, runner);
            foreach (var n in baked.Nodes) n.Initialize(runner);
            runner.Initialize();
            return runner;
        }

        protected abstract TRunner CreateRunner(BakedGraph baked);

        // Wires each VariableEdge by setting the destination input port's source to a
        // closure that reads the runner's typed VariableCell. Note: BakedGraph (and the
        // RuntimeNode instances inside it) is cached per-asset on the builder, so building
        // the same asset twice with different runners would have the second wire-pass
        // overwrite the first. Same constraint already applies to Get/Set node Initialize
        // caching — assumes one-runner-per-asset within a builder lifetime.
        static void WireVariableEdges(BakedGraph baked, GraphRunner runner)
        {
            foreach (var ve in baked.VariableEdges)
            {
                if (string.IsNullOrEmpty(ve.variableId)) continue;
                if (!baked.NodesById.TryGetValue(ve.toNodeId, out var node)) continue;
                if (!node.Ports.TryGetValue(ve.toPortName, out var port)) continue;
                if (!runner.Variables.TryGetCell(ve.variableId, out var cell))
                {
                    Debug.LogWarning($"GraphFlow: variable '{ve.variableId}' not declared; port {node.GetType().Name}.{ve.toPortName} left unconnected.");
                    continue;
                }
                port.ConnectFromVariable(cell);
            }
        }
    }
}
