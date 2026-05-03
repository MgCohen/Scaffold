using Scaffold.GraphFlow.M0.Editor.GToolkit;
using Scaffold.GraphFlow.M0.Smoke;
using UnityEditor.AssetImporters;

namespace Scaffold.GraphFlow.M0.Editor
{
    [ScriptedImporter(1, MySmokeGraph.AssetExtension)]
    public sealed class MySmokeGraphImporter : GraphAssetImporterBase<MySmokeGraph, MySmokeRunner, MySmokeGraphAsset>
    {
        protected override GraphBakeResult Bake(MySmokeGraph graph, MySmokeGraphAsset? previousRuntime) =>
            GraphBaker.Bake(graph, previousRuntime);
    }
}
