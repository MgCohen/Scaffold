using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.GraphFlow
{
    [Serializable]
    public struct Edge
    {
        public int fromNodeId;
        public string fromPortName;
        public int toNodeId;
        public string toPortName;
    }

    public abstract class GraphAsset<TRunner> : ScriptableObject where TRunner : GraphRunner
    {
        [SerializeReference] public List<RuntimeNode> nodes = new();
        public List<Edge> connections = new();
        public List<Edge> flowEdges = new();
        public int schemaVersion;
    }
}
