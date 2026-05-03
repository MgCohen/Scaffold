using System;
using System.Collections.Generic;
using System.Linq;
using Scaffold.GraphFlow.M0;
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

    /// <summary>M0 bake — editor graph → typed runtime asset (hand-written switches until M1 registry).</summary>
    public static class GraphBaker
    {
        const int SchemaVersion = 2;

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
                    case EchoDispatcherEditorNode echoEd:
                        editorToRuntime[n] = new EchoDispatcherRuntime
                        {
                            nodeId = nodeId,
                            editorGuid = guid,
                            Magnitude = GetIntEmbeddedOrDefault(echoEd, EchoDispatcherEditorNode.MagnitudePortName),
                        };
                        break;
                    case LogDispatcherEditorNode logEd:
                        editorToRuntime[n] = new LogDispatcherRuntime
                        {
                            nodeId = nodeId,
                            editorGuid = guid,
                            Message = GetStringEmbeddedOrDefault(logEd, LogDispatcherEditorNode.MessagePortName),
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

            var dataConnections = new List<ConnectionRecord>();
            var flowEdges = new List<FlowEdge>();

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

                        if (IsFlowEdge(editorNode, port.name, toEditor, other.name))
                        {
                            flowEdges.Add(new FlowEdge
                            {
                                fromNodeId = fromRuntime.nodeId,
                                fromFlowPortId = MapFlowOutputPortId(editorNode, port.name),
                                toNodeId = toRuntime.nodeId,
                                toFlowPortId = MapFlowInputPortId(toEditor, other.name),
                            });
                            continue;
                        }

                        dataConnections.Add(new ConnectionRecord
                        {
                            fromNodeId = fromRuntime.nodeId,
                            fromPortId = MapDataOutputPortId(editorNode, port.name),
                            toNodeId = toRuntime.nodeId,
                            toPortId = MapDataInputPortId(toEditor, other.name),
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
            asset.connections = dataConnections;
            asset.flowEdges = flowEdges;
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

        static bool IsFlowEdge(INode fromEditor, string fromPortName, INode toEditor, string toPortName)
        {
            var fromFlow = IsFlowOutputPort(fromEditor, fromPortName);
            var toFlow = IsFlowInputPort(toEditor, toPortName);
            return fromFlow && toFlow;
        }

        static bool IsFlowOutputPort(INode editorNode, string portName)
        {
            return editorNode switch
            {
                OnPlayEditorNode when portName == OnPlayEditorNode.FlowOutPortName => true,
                EchoDispatcherEditorNode when portName == EchoDispatcherEditorNode.FlowOutPortName => true,
                _ => false,
            };
        }

        static bool IsFlowInputPort(INode editorNode, string portName)
        {
            return editorNode switch
            {
                EchoDispatcherEditorNode when portName == EchoDispatcherEditorNode.FlowInPortName => true,
                LogDispatcherEditorNode when portName == LogDispatcherEditorNode.FlowInPortName => true,
                _ => false,
            };
        }

        static int MapFlowOutputPortId(INode editorNode, string portName)
        {
            switch (editorNode)
            {
                case OnPlayEditorNode when portName == OnPlayEditorNode.FlowOutPortName:
                    return OnPlayRuntime.Ports.FlowOut;
                case EchoDispatcherEditorNode when portName == EchoDispatcherEditorNode.FlowOutPortName:
                    return EchoDispatcherRuntime.Ports.FlowOut;
                default:
                    throw new InvalidOperationException($"Unknown flow output port {portName} on {editorNode.GetType().Name}");
            }
        }

        static int MapFlowInputPortId(INode editorNode, string portName)
        {
            switch (editorNode)
            {
                case EchoDispatcherEditorNode when portName == EchoDispatcherEditorNode.FlowInPortName:
                    return EchoDispatcherRuntime.Ports.FlowIn;
                case LogDispatcherEditorNode when portName == LogDispatcherEditorNode.FlowInPortName:
                    return LogDispatcherRuntime.FlowInSlotId;
                default:
                    throw new InvalidOperationException($"Unknown flow input port {portName} on {editorNode.GetType().Name}");
            }
        }

        static int MapDataOutputPortId(INode editorNode, string portName)
        {
            switch (editorNode)
            {
                case OnPlayEditorNode when portName == OnPlayEditorNode.CardIdPortName:
                    return OnPlayRuntime.Ports.CardId;
                case EchoDispatcherEditorNode when portName == EchoDispatcherEditorNode.SummaryPortName:
                    return EchoDispatcherRuntime.Ports.Summary;
                case IntToStringEditorNode when portName == IntToStringEditorNode.OutResultPortName:
                    return IntToStringRuntime.Ports.OutString;
                default:
                    throw new InvalidOperationException($"Unknown data output port {portName} on {editorNode.GetType().Name}");
            }
        }

        static int MapDataInputPortId(INode editorNode, string portName)
        {
            switch (editorNode)
            {
                case EchoDispatcherEditorNode when portName == EchoDispatcherEditorNode.MagnitudePortName:
                    return EchoDispatcherRuntime.Ports.Magnitude;
                case LogDispatcherEditorNode when portName == LogDispatcherEditorNode.MessagePortName:
                    return LogDispatcherRuntime.Ports.Message;
                case IntToStringEditorNode when portName == IntToStringEditorNode.InValuePortName:
                    return IntToStringRuntime.Ports.InValue;
                default:
                    throw new InvalidOperationException($"Unknown data input port {portName} on {editorNode.GetType().Name}");
            }
        }

        static int GetIntEmbeddedOrDefault(EchoDispatcherEditorNode node, string portName)
        {
            var port = node.GetInputPortByName(portName);
            if (port == null)
                return 0;

            if (port.isConnected)
                return 0;

            if (port.TryGetValue(out int embedded))
                return embedded;

            return 0;
        }

        static string GetStringEmbeddedOrDefault(LogDispatcherEditorNode node, string portName)
        {
            var port = node.GetInputPortByName(portName);
            if (port == null)
                return "";

            if (port.isConnected)
                return "";

            if (port.TryGetValue(out string embedded))
                return embedded ?? "";

            return "";
        }
    }
}
