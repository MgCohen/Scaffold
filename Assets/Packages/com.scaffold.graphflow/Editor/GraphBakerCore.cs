using System;
using System.Collections.Generic;
using System.Linq;
using Scaffold.GraphFlow.Editor.GToolkit;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Scaffold.GraphFlow.Editor
{
    public sealed class GraphBakeResult<TAsset> where TAsset : ScriptableObject
    {
        public TAsset? Asset { get; set; }
        public bool HasErrors { get; set; }
        public IReadOnlyList<string> Diagnostics => _diagnostics;
        readonly List<string> _diagnostics = new();

        public void LogError(string msg)
        {
            _diagnostics.Add(msg);
            HasErrors = true;
        }
    }

    /// <summary>
    /// Generic, registry-driven baker. One implementation translates any GraphFlow package's editor graph
    /// into its typed runtime asset — the generator emits <c>&lt;Stem&gt;GraphRegistry</c>, which supplies
    /// per-editor-node factories + port-name lookups so this code stays free of per-payload switches.
    /// </summary>
    public static class GraphBakerCore
    {
        public const int SchemaVersion = 3;

        public static GraphBakeResult<TAsset> Bake<TRunner, TAsset>(
            Graph<TRunner> editorGraph,
            TAsset? previousRuntime,
            GraphPackageRegistry<TRunner> registry)
            where TRunner : GraphRunner
            where TAsset : GraphAsset<TRunner>
        {
            var result = new GraphBakeResult<TAsset>();

            if (editorGraph == null)
            {
                result.LogError("Editor graph is null.");
                return result;
            }

            var editorNodes = editorGraph.GetNodes().ToList();
            if (editorNodes.Count == 0)
            {
                // Newly-created graph: emit an empty asset so the importer succeeds and the user can author.
                result.Asset = ScriptableObject.CreateInstance<TAsset>();
                return result;
            }

            var guidToNodeId = RecoverGuidMap<TRunner, TAsset>(previousRuntime);
            var nextId = NextFreeNodeId(guidToNodeId);

            var editorToRuntime = new Dictionary<INode, RuntimeNode>();
            var editorToRegistration = new Dictionary<INode, GraphPackageRegistry<TRunner>.NodeRegistration>();

            foreach (var n in editorNodes)
            {
                if (n is IConstantNode || n is IVariableNode)
                    continue;

                var reg = registry.Lookup(n.GetType());
                if (reg == null)
                {
                    result.LogError($"Unsupported editor node: {n.GetType().FullName}");
                    return result;
                }

                var guid = EditorNodeIdentity.GetStableGuid(n);

                if (!guidToNodeId.TryGetValue(guid, out var nodeId))
                {
                    nodeId = nextId++;
                    guidToNodeId[guid] = nodeId;
                }

                var runtime = reg.Factory(n);
                runtime.nodeId = nodeId;
                runtime.editorGuid = guid;
                editorToRuntime[n] = runtime;
                editorToRegistration[n] = reg;
            }

            var dataConnections = new List<Edge>();
            var flowEdges = new List<Edge>();

            foreach (var pair in editorToRuntime.OrderBy(p => p.Value.nodeId))
            {
                var editorNode = pair.Key;
                var fromRuntime = pair.Value;
                var fromReg = editorToRegistration[editorNode];

                foreach (var port in editorNode.GetOutputPorts())
                {
                    var connected = new List<IPort>();
                    port.GetConnectedPorts(connected);
                    foreach (var other in connected)
                    {
                        var toEditor = other.GetNode();
                        if (toEditor == null || !editorToRuntime.TryGetValue(toEditor, out var toRuntime))
                            continue;

                        var toReg = editorToRegistration[toEditor];
                        if (TryRecordFlowEdge(fromReg, port.name, fromRuntime.nodeId, toReg, other.name, toRuntime.nodeId, flowEdges))
                            continue;

                        if (TryRecordDataEdge(fromReg, port.name, fromRuntime.nodeId, toReg, other.name, toRuntime.nodeId, dataConnections))
                            continue;

                        result.LogError($"Edge from {editorNode.GetType().Name}.{port.name} to {toEditor.GetType().Name}.{other.name} matches neither flow nor data ports.");
                        return result;
                    }
                }
            }

            var asset = ScriptableObject.CreateInstance<TAsset>();
            asset.nodes = editorToRuntime.Values.OrderBy(n => n.nodeId).ToList();
            asset.connections = SortEdges(dataConnections);
            asset.flowEdges = SortEdges(flowEdges);
            asset.schemaVersion = SchemaVersion;

            result.Asset = asset;
            return result;
        }

        static bool TryRecordFlowEdge<TRunner>(
            GraphPackageRegistry<TRunner>.NodeRegistration fromReg, string fromPortName, int fromNodeId,
            GraphPackageRegistry<TRunner>.NodeRegistration toReg, string toPortName, int toNodeId,
            List<Edge> into) where TRunner : GraphRunner
        {
            if (!fromReg.FlowOutputPortNames.Contains(fromPortName))
                return false;
            if (!toReg.FlowInputPortNames.Contains(toPortName))
                return false;
            into.Add(new Edge { fromNodeId = fromNodeId, fromPortName = fromPortName, toNodeId = toNodeId, toPortName = toPortName });
            return true;
        }

        static bool TryRecordDataEdge<TRunner>(
            GraphPackageRegistry<TRunner>.NodeRegistration fromReg, string fromPortName, int fromNodeId,
            GraphPackageRegistry<TRunner>.NodeRegistration toReg, string toPortName, int toNodeId,
            List<Edge> into) where TRunner : GraphRunner
        {
            if (!fromReg.DataOutputPortNames.Contains(fromPortName))
                return false;
            if (!toReg.DataInputPortNames.Contains(toPortName))
                return false;
            into.Add(new Edge { fromNodeId = fromNodeId, fromPortName = fromPortName, toNodeId = toNodeId, toPortName = toPortName });
            return true;
        }

        static Dictionary<string, int> RecoverGuidMap<TRunner, TAsset>(TAsset? previous)
            where TRunner : GraphRunner
            where TAsset : GraphAsset<TRunner>
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            if (previous?.nodes == null)
                return map;

            foreach (var n in previous.nodes)
            {
                if (!string.IsNullOrEmpty(n.editorGuid))
                    map[n.editorGuid] = n.nodeId;
            }

            return map;
        }

        static int NextFreeNodeId(Dictionary<string, int> guidToNodeId)
        {
            var max = 0;
            foreach (var id in guidToNodeId.Values)
                if (id > max)
                    max = id;
            return max + 1;
        }

        static List<Edge> SortEdges(List<Edge> edges) => edges
            .OrderBy(e => e.fromNodeId)
            .ThenBy(e => e.fromPortName, StringComparer.Ordinal)
            .ThenBy(e => e.toNodeId)
            .ThenBy(e => e.toPortName, StringComparer.Ordinal)
            .ToList();
    }
}
