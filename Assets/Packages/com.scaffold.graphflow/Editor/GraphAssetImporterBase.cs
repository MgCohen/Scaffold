using System;
using Scaffold.GraphFlow.Editor.GToolkit;
using Unity.GraphToolkit.Editor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Scaffold.GraphFlow.Editor
{
    /// <summary>
    /// ScriptedImporter pipeline for package graphs. Subclasses close type parameters, apply
    /// <c>[ScriptedImporter(version, extension)]</c>, and supply <see cref="Registry"/>.
    /// The bake itself is generic — driven entirely by the registry.
    ///
    /// Follows the same pattern as Unity's official GT sample importers
    /// (VisualNovelDirectorImporter, TextureMakerImporter): when the graph can't be loaded
    /// or has errors, return early without adding a sub-asset — Unity shows a DefaultAsset
    /// with import settings, which is the expected empty-graph state.
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
                Debug.LogError($"Failed to load graph asset: {ctx.assetPath}");
                return;
            }

            GraphBakeResult<TAsset> bake;
            try
            {
                bake = GraphBakerCore.Bake<TRunner, TAsset>(graph, null, Registry);
            }
            catch (InvalidOperationException ex)
            {
                ctx.LogImportError($"{ex.Message} ({ctx.assetPath})", null);
                return;
            }

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
