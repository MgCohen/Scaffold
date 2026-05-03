using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.GraphFlow.M0
{
    [Serializable]
    public struct ConnectionRecord
    {
        public int fromNodeId;
        public int fromPortId;
        public int toNodeId;
        public int toPortId;
    }

    [Serializable]
    public struct EntryIndex
    {
        public string entryTypeId;
        public int rootNodeId;
    }

    /// <summary>Baked runtime artifact — reference this from game code (abstract generic).</summary>
    public abstract class GraphAsset<TRunner> : ScriptableObject where TRunner : GraphRunner
    {
        [SerializeReference] public List<RuntimeNode<TRunner>> nodes = new();
        public List<ConnectionRecord> connections = new();
        public List<EntryIndex> entries = new();
        public int schemaVersion;
    }
}
