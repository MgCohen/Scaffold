using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.GraphFlow
{
    /// <summary>Data edges only — typed values via <see cref="Connection{T}"/> at hydration.</summary>
    [Serializable]
    public struct ConnectionRecord
    {
        public int fromNodeId;
        public int fromPortId;
        public int toNodeId;
        public int toPortId;
    }

    /// <summary>Execution ordering — no payload; interpreted only by <see cref="GraphExecutor{TRunner}"/>.</summary>
    [Serializable]
    public struct FlowEdge
    {
        public int fromNodeId;
        public int fromFlowPortId;
        public int toNodeId;
        public int toFlowPortId;
    }

    [Serializable]
    public struct EntryIndex
    {
        public string entryTypeId;
        public int rootNodeId;
    }

    /// <summary>
    /// Baked runtime artifact — reference this from game code (abstract generic).
    /// <para>The node list is typed as <see cref="RuntimeNode"/> (not <c>RuntimeNode&lt;TRunner&gt;</c>)
    /// so pure data nodes — which inherit from <see cref="RuntimeNode"/> directly without a
    /// <c>TRunner</c> — can serialize alongside flow-bearing nodes. The executor only invokes
    /// <c>Execute</c> on <c>RuntimeNode&lt;TRunner&gt;</c> instances reached through <c>flowEdges</c>;
    /// data nodes are read-only sinks for connections and never participate in flow.</para>
    /// </summary>
    public abstract class GraphAsset<TRunner> : ScriptableObject where TRunner : GraphRunner
    {
        [SerializeReference] public List<RuntimeNode> nodes = new();
        public List<ConnectionRecord> connections = new();
        public List<FlowEdge> flowEdges = new();
        public List<EntryIndex> entries = new();
        public int schemaVersion;
    }

}
