using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Serialized wire record — used for both data edges (hydrated to <see cref="Connection{T}"/>)
    /// and flow edges (hydrated to <see cref="FlowConnection"/>). The role is determined at
    /// hydration time by which list the edge sits on; the on-disk shape is identical.
    /// <para>This is metadata only. After <c>GraphController.Initialize</c> walks the lists once
    /// and builds the runtime <see cref="Connection"/> objects, port handles carry direct refs to
    /// each other and the executor never reads from these lists again.</para>
    /// </summary>
    [Serializable]
    public struct Edge
    {
        public int fromNodeId;
        public string fromPortName;
        public int toNodeId;
        public string toPortName;
    }

    /// <summary>
    /// Baked runtime artifact — reference this from game code (abstract generic).
    /// <para>The node list is typed as <see cref="RuntimeNode"/> (not <c>RuntimeNode&lt;TRunner&gt;</c>)
    /// so pure data nodes — which inherit from <see cref="RuntimeNode"/> directly without a
    /// <c>TRunner</c> — can serialize alongside flow-bearing nodes. The executor only invokes
    /// <c>Execute</c> on the nodes reached through hydrated <see cref="FlowConnection"/> refs;
    /// data nodes are read-only sinks for connections and never participate in flow.</para>
    /// <para>Entry nodes are discovered at hydration by pattern-matching
    /// <see cref="EntryRuntimeNodeBase"/> in <see cref="nodes"/> — no separate index needed.</para>
    /// </summary>
    public abstract class GraphAsset<TRunner> : ScriptableObject where TRunner : GraphRunner
    {
        [SerializeReference] public List<RuntimeNode> nodes = new();
        public List<Edge> connections = new();
        public List<Edge> flowEdges = new();
        public int schemaVersion;
    }
}
