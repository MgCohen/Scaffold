using System;
using System.Collections.Generic;
using System.Linq;
using Scaffold.GraphFlow.M0.Editor.GToolkit;
using Scaffold.GraphFlow.M0.Smoke;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Scaffold.GraphFlow.M0.Editor
{
    public sealed class GraphBakeResult
    {
        public MySmokeGraphAsset? Asset { get; set; }
        public bool HasErrors { get; set; }
        public IReadOnlyList<string> Diagnostics => _diagnostics;
        readonly List<string> _diagnostics = new List<string>();

        public void LogError(string msg) => _diagnostics.Add(msg);
    }

    /// <summary>M0 bake — editor graph → typed runtime asset (hand-written registry).</summary>
    public static class GraphBaker
    {
        const int SchemaVersion = 1;

        public static GraphBakeResult Bake(MySmokeGraph editorGraph, MySmokeGraphAsset? previousRuntime)
        {
            var result = new GraphBakeResult();

            if (editorGraph == null)
            {
                result.LogError("Editor graph is null.");
                result.HasErrors = true;
                return result;
            }

            var editorNodes = editorGraph.GetNodes().ToList();
            if (editorNodes.Count == 0)
            {
                result.LogError("Graph has no nodes.");
                result.HasErrors = true;
                return result;
            }

            var guidToNodeId = RecoverGuidMap(previousRuntime);
            var nextId = NextFreeNodeId(guidToNodeId);

            var editorToRuntime = new Dictionary<INode, RuntimeNode<MySmokeRunner>>();

            foreach (var n in editorNodes)
            {
                var guid = TryGetEditorGuid(n);
                if (string.IsNullOrEmpty(guid))
                {
                    result.LogError($"Node {n.GetType().Name} has no stable editor guid — cannot bake.");
                    result.HasErrors = true;
                    return result;
                }

                if (!guidToNodeId.TryGetValue(guid, out var nodeId))
                {
                    nodeId = nextId++;
                    guidToNodeId[guid] = nodeId;
                }

                switch (n)
                {
                    case OnPlayEditorNode:
                        editorToRuntime[n] = new OnPlayRuntime { nodeId = nodeId, editorGuid = guid };
                        break;
                    case LogDispatcherEditorNode logEd:
                        editorToRuntime[n] = new LogDispatcherRuntime
                        {
                            nodeId = nodeId,
                            editorGuid = guid,
                            Message = GetStringInput(logEd, LogDispatcherEditorNode.MessagePortName),
                        };
                        break;
                    case IntToStringEditorNode:
                        editorToRuntime[n] = new IntToStringRuntime { nodeId = nodeId, editorGuid = guid };
                        break;
                    default:
                        result.LogError($"Unsupported editor node: {n.GetType().FullName}");
                        result.HasErrors = true;
                        return result;
                }
            }

            var connections = new List<ConnectionRecord>();
            foreach (var pair in editorToRuntime)
            {
                var editorNode = pair.Key;
                var fromRuntime = pair.Value;

                foreach (var port in editorNode.GetOutputPorts())
                {
                    var list = new List<IPort>();
                    port.GetConnectedPorts(list);
                    foreach (var other in list)
                    {
                        var toEditor = other.GetNode();
                        if (toEditor == null || !editorToRuntime.TryGetValue(toEditor, out var toRuntime))
                            continue;

                        var fromPortId = MapOutputPortId(editorNode, port.name);
                        var toPortId = MapInputPortId(toEditor, other.name);

                        connections.Add(new ConnectionRecord
                        {
                            fromNodeId = fromRuntime.nodeId,
                            fromPortId = fromPortId,
                            toNodeId = toRuntime.nodeId,
                            toPortId = toPortId,
                        });
                    }
                }
            }

            var entries = new List<EntryIndex>();
            foreach (var pair in editorToRuntime)
            {
                if (pair.Key is OnPlayEditorNode)
                {
                    entries.Add(new EntryIndex
                    {
                        entryTypeId = typeof(OnPlay).AssemblyQualifiedName!,
                        rootNodeId = pair.Value.nodeId,
                    });
                    break;
                }
            }

            if (entries.Count == 0)
            {
                result.LogError("Graph must contain exactly one OnPlay entry node for M0.");
                result.HasErrors = true;
                return result;
            }

            var asset = ScriptableObject.CreateInstance<MySmokeGraphAsset>();
            asset.nodes = new List<RuntimeNode<MySmokeRunner>>(editorToRuntime.Values);
            asset.connections = connections;
            asset.entries = entries;
            asset.schemaVersion = SchemaVersion;

            result.Asset = asset;
            result.HasErrors = false;
            return result;
        }

        static Dictionary<string, int> RecoverGuidMap(MySmokeGraphAsset? previous)
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

        static string? TryGetEditorGuid(INode node)
        {
            try
            {
                return EditorNodeIdentity.GetStableGuid(node);
            }
            catch
            {
                return null;
            }
        }

        static int MapOutputPortId(INode editorNode, string portName)
        {
            switch (editorNode)
            {
                case OnPlayEditorNode when portName == OnPlayEditorNode.FlowOutPortName:
                    return OnPlayRuntime.Ports.FlowOut;
                case OnPlayEditorNode when portName == OnPlayEditorNode.CardIdPortName:
                    return OnPlayRuntime.Ports.CardId;
                case IntToStringEditorNode when portName == IntToStringEditorNode.OutResultPortName:
                    return IntToStringRuntime.Ports.OutString;
                default:
                    throw new InvalidOperationException($"Unknown output port {portName} on {editorNode.GetType().Name}");
            }
        }

        static int MapInputPortId(INode editorNode, string portName)
        {
            switch (editorNode)
            {
                case LogDispatcherEditorNode when portName == LogDispatcherEditorNode.FlowInPortName:
                    return LogDispatcherRuntime.Ports.FlowIn;
                case LogDispatcherEditorNode when portName == LogDispatcherEditorNode.MessagePortName:
                    return LogDispatcherRuntime.Ports.Message;
                case IntToStringEditorNode when portName == IntToStringEditorNode.InValuePortName:
                    return IntToStringRuntime.Ports.InValue;
                default:
                    throw new InvalidOperationException($"Unknown input port {portName} on {editorNode.GetType().Name}");
            }
        }

        static string GetStringInput(LogDispatcherEditorNode node, string portName)
        {
            var port = node.GetInputPortByName(portName);
            if (port == null)
                return "";

            if (port.isConnected && port.firstConnectedPort != null)
                return "";

            if (port.TryGetValue(out string embedded))
                return embedded ?? "";

            return "";
        }
    }
}
