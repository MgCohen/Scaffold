using System;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using Scaffold.EffectGraph.Editor;
using Scaffold.EffectGraph.Runtime;

namespace Scaffold.EffectGraph.Sample.Editor
{
    [Serializable]
    [Graph(AssetExtension)]
    internal sealed class SampleEffectGraph : Graph<SampleEffectRunner>
    {
        internal const string AssetExtension = "efgraph";

        const string k_graphName = "Sample Effect Graph";

        [MenuItem("Assets/Create/Scaffold/Sample Effect Graph")]
        static void CreateAssetFile() =>
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<SampleEffectGraph>(k_graphName);
    }

    internal sealed class SampleEffectRunner : GraphRunner
    {
    }
}
