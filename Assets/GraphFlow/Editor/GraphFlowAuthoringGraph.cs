using System;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor
{
    [Serializable]
    [Graph(AssetExtension)]
    public sealed class GraphFlowAuthoringGraph : Graph
    {
        public const string AssetExtension = "graphflow";
    }
}
