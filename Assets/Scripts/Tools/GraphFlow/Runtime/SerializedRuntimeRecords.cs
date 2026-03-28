using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.GraphFlow
{
    [Serializable]
    public sealed class SerializedRuntimeNode
    {
        public string id;
        public string definitionTypeId;

        /// <summary>Optional nested graph for invoke-subgraph nodes (Unity serializes asset reference).</summary>
        public RuntimeGraph nestedRuntimeGraph;
    }

    [Serializable]
    public sealed class SerializedRuntimeEdge
    {
        public string fromNodeId;
        public string fromPort;
        public string toNodeId;
        public string toPort;
    }

    [Serializable]
    public sealed class SerializedRuntimeEntry
    {
        public string entryTypeAssemblyQualifiedName;
        public string entryNodeId;
        public List<FlowExitMapping> flowExits = new List<FlowExitMapping>();
    }

    [Serializable]
    public sealed class FlowExitMapping
    {
        public string flowPortName;
        public string nextNodeId;
    }

    [Serializable]
    public sealed class SerializedReactiveHook
    {
        public MiddlewarePhase timing;
        public string targetDefinitionTypeId;
        public RuntimeGraph reactiveGraphAsset;
    }
}
