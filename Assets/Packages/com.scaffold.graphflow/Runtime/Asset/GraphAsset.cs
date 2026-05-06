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

    public abstract class GraphAsset : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeReference] public List<RuntimeNode> nodes = new();
        public List<Edge> connections = new();
        public List<Edge> flowEdges = new();
        public int schemaVersion;

        // Strip unresolvable SerializeReference entries on load. Class renames /
        // assembly moves leave behind null managed references in already-baked assets;
        // NaughtyAttributes' inspector walks every property path and logs "target object
        // is null" for each null intermediate, spamming the console on every editor tick.
        // Drop them silently — the bake step is the source of truth, the user can re-bake
        // to recover.
        void ISerializationCallbackReceiver.OnBeforeSerialize() { }
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (nodes != null) nodes.RemoveAll(n => n == null);
        }
    }

    public abstract class GraphAsset<TRunner> : GraphAsset where TRunner : GraphRunner
    {
    }
}
