using Scaffold.GraphFlow.M0.Editor.GToolkit;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace Scaffold.GraphFlow.M0.Editor
{
    /// <summary>
    /// ScriptedImporter pipeline for package graphs. Subclasses close type parameters, apply
    /// <c>[ScriptedImporter(version, extension)]</c>, and supply <see cref="Registry"/>.
    /// The bake itself is generic — driven entirely by the registry.
    /// </summary>
    public abstract class GraphAssetImporterBase<TGraph, TRunner, TAsset> : ScriptedImporter
        where TGraph : Graph<TRunner>
        where TRunner : GraphRunner
        where TAsset : GraphAsset<TRunner>
    {
        protected abstract GraphPackageRegistry<TRunner> Registry { get; }

        public sealed override void OnImportAsset(AssetImportContext ctx)
        {
            var graph = GraphDatabase.LoadGraphForImporter<TGraph>(ctx.assetPath);
            if (graph == null)
            {
                ctx.LogImportError($"Failed to load graph for importer: {typeof(TGraph).Name} ({ctx.assetPath})", null);
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

            var bake = GraphBakerCore.Bake<TRunner, TAsset>(graph, previous, Registry);
            foreach (var msg in bake.Diagnostics)
                ctx.LogImportError($"{msg} ({ctx.assetPath})", null);

            if (bake.HasErrors || bake.Asset == null)
                return;

            bake.Asset.name = "Runtime";
            ctx.AddObjectToAsset("Runtime", bake.Asset);
            ctx.SetMainObject(bake.Asset);
        }
    }
}
