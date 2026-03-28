using Scaffold.GraphFlow;
using Unity.GraphToolkit.Editor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Scaffold.GraphFlow.Editor
{
    [ScriptedImporter(1, GraphFlowAuthoringGraph.AssetExtension)]
    public sealed class GraphFlowAuthoringImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var graph = GraphDatabase.LoadGraphForImporter<GraphFlowAuthoringGraph>(ctx.assetPath);
            var runtime = ScriptableObject.CreateInstance<RuntimeGraph>();
            runtime.name = "RuntimeGraph";
            GraphFlowRuntimeBaker.BakeInto(runtime, graph);
            ctx.AddObjectToAsset("RuntimeGraph", runtime);
            ctx.SetMainObject(runtime);
        }
    }
}
