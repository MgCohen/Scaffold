using Scaffold.GraphFlow;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Scaffold.GraphFlow.Editor
{
    /// <summary>
    /// Placeholder importer: produces an empty <see cref="RuntimeGraph"/> sub-asset.
    /// Replace body with Graph Toolkit graph walk when <c>com.unity.graphtoolkit</c> is added to the project.
    /// </summary>
    [ScriptedImporter(1, "graphflowauthoring")]
    public sealed class GraphFlowAuthoringImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var graph = ScriptableObject.CreateInstance<RuntimeGraph>();
            graph.name = "RuntimeGraph";
            ctx.AddObjectToAsset("RuntimeGraph", graph);
            ctx.SetMainObject(graph);
        }
    }
}
