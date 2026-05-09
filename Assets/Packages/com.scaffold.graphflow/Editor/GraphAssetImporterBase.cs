using System;
using Scaffold.GraphFlow.Editor.GToolkit;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Scaffold.GraphFlow.Editor
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
                var placeholder = ScriptableObject.CreateInstance<TAsset>();
                placeholder.name = "Runtime";
                ctx.AddObjectToAsset("Runtime", placeholder);
                ctx.SetMainObject(placeholder);
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

            GraphBakeResult<TAsset> bake;
            try
            {
                bake = GraphBakerCore.Bake<TRunner, TAsset>(graph, previous, Registry);
            }
            catch (InvalidOperationException ex)
            {
                ctx.LogImportError($"{ex.Message} ({ctx.assetPath})", null);
                EmitPlaceholder(ctx);
                return;
            }

            foreach (var msg in bake.Diagnostics)
                ctx.LogImportError($"{msg} ({ctx.assetPath})", null);

            if (bake.HasErrors)
            {
                EmitPlaceholder(ctx);
                return;
            }

            bake.Asset!.name = "Runtime";
            ctx.AddObjectToAsset("Runtime", bake.Asset);
            ctx.SetMainObject(bake.Asset);
        }

        void EmitPlaceholder(AssetImportContext ctx)
        {
            var placeholder = ScriptableObject.CreateInstance<TAsset>();
            placeholder.name = "Runtime";
            ctx.AddObjectToAsset("Runtime", placeholder);
            ctx.SetMainObject(placeholder);
        }
    }
}
