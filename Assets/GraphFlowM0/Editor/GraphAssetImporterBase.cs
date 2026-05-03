using Scaffold.GraphFlow.M0.Editor.GToolkit;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace Scaffold.GraphFlow.M0.Editor
{
    /// <summary>
    /// ScriptedImporter pipeline for package graphs. Subclasses close type parameters, apply
    /// <c>[ScriptedImporter(version, extension)]</c>, and implement <see cref="Bake"/>.
    /// </summary>
    public abstract class GraphAssetImporterBase<TGraph, TRunner, TAsset> : ScriptedImporter
        where TGraph : Graph<TRunner>
        where TRunner : GraphRunner
        where TAsset : GraphAsset<TRunner>
    {
        public sealed override void OnImportAsset(AssetImportContext ctx)
        {
            var graph = GraphDatabase.LoadGraphForImporter<TGraph>(ctx.assetPath);
            if (graph == null)
            {
                ctx.LogImportError($"Failed to load graph for importer: {typeof(TGraph).Name}", ctx.assetPath);
                return;
            }

            TAsset? previous = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ctx.assetPath))
            {
                if (o is TAsset a)
                {
                    previous = a;
                    break;
                }
            }

            var bake = Bake(graph, previous);
            foreach (var msg in bake.Diagnostics)
                ctx.LogImportError(msg, ctx.assetPath);

            if (bake.HasErrors || bake.Asset == null)
                return;

            bake.Asset.name = "Runtime";
            ctx.AddObjectToAsset("Runtime", bake.Asset);
            ctx.SetMainObject(bake.Asset);
        }

        /// <summary>M0: smoke uses <see cref="GraphBaker"/>. M1: swap for generic baker.</summary>
        protected abstract GraphBakeResult Bake(TGraph graph, TAsset? previousRuntime);
    }
}
