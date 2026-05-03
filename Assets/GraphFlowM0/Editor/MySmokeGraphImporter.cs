using Scaffold.GraphFlow.M0.Editor.GToolkit;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Scaffold.GraphFlow.M0.Editor
{
    [ScriptedImporter(1, MySmokeGraph.AssetExtension)]
    public sealed class MySmokeGraphImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var graph = GraphDatabase.LoadGraphForImporter<MySmokeGraph>(ctx.assetPath);
            MySmokeGraphAsset? previous = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ctx.assetPath))
            {
                if (o is MySmokeGraphAsset a)
                {
                    previous = a;
                    break;
                }
            }

            var bake = GraphBaker.Bake(graph!, previous);
            foreach (var msg in bake.Diagnostics)
                ctx.LogImportError(msg, ctx.assetPath);

            if (bake.HasErrors || bake.Asset == null)
                return;

            bake.Asset.name = "Runtime";
            ctx.AddObjectToAsset("Runtime", bake.Asset);
            ctx.SetMainObject(bake.Asset);
        }
    }
}
