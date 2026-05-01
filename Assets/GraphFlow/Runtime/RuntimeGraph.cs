using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scaffold.GraphFlow
{
    public sealed class RuntimeGraph : ScriptableObject
    {
        [SerializeField] List<SerializedRuntimeNode> serializedNodes = new List<SerializedRuntimeNode>();
        [SerializeField] List<SerializedRuntimeEdge> serializedEdges = new List<SerializedRuntimeEdge>();
        [SerializeField] List<SerializedRuntimeEntry> serializedEntries = new List<SerializedRuntimeEntry>();

        public IReadOnlyList<SerializedRuntimeNode> SerializedNodes => serializedNodes;
        public IReadOnlyList<SerializedRuntimeEdge> SerializedEdges => serializedEdges;

        public IReadOnlyList<Type> EntryPointTypes =>
            serializedEntries
                .Select(e => Type.GetType(e.entryTypeAssemblyQualifiedName))
                .Where(t => t != null)
                .ToList();

        public bool TryGetSerializedEntry(Type entryType, out SerializedRuntimeEntry entry)
        {
            foreach (var e in serializedEntries)
            {
                var t = Type.GetType(e.entryTypeAssemblyQualifiedName);
                if (t == entryType)
                {
                    entry = e;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        public ExecutableGraph BuildExecutable(INodeExecutorRegistry registry)
        {
            var idToNode = new Dictionary<string, ExecutableNode>(StringComparer.Ordinal);
            foreach (var sn in serializedNodes)
            {
                var def = registry.Resolve(sn.definitionTypeId);
                if (def == null)
                    throw new InvalidOperationException($"Unknown DefinitionTypeId: {sn.definitionTypeId}");
                idToNode[sn.id] = new ExecutableNode(ParseNodeId(sn.id), def, sn.nestedRuntimeGraph);
            }

            foreach (var edge in serializedEdges)
            {
                if (!idToNode.TryGetValue(edge.fromNodeId, out var from) ||
                    !idToNode.TryGetValue(edge.toNodeId, out var to))
                    continue;

                if (IsFlowPort(edge.fromPort) && IsFlowPort(edge.toPort) &&
                    string.Equals(edge.fromPort, "Out", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(edge.toPort, "In", StringComparison.OrdinalIgnoreCase))
                {
                    from.FlowSuccessors["Out"] = to;
                }
                else if (!IsFlowPort(edge.fromPort) || !IsFlowPort(edge.toPort))
                {
                    to.DataEdgesIn.Add(new DataEdgeRuntime(from, edge.fromPort, edge.toPort));
                }
            }

            var entryRoots = new Dictionary<Type, ExecutableNode>();
            foreach (var se in serializedEntries)
            {
                var entryType = Type.GetType(se.entryTypeAssemblyQualifiedName);
                if (entryType == null || !idToNode.TryGetValue(se.entryNodeId, out var entryNode))
                    continue;
                entryRoots[entryType] = entryNode;
            }

            return new ExecutableGraph(idToNode.Values.ToList(), entryRoots);
        }

        static NodeId ParseNodeId(string s) =>
            Guid.TryParse(s, out var g) ? new NodeId(g) : new NodeId(Guid.NewGuid());

        static bool IsFlowPort(string port) =>
            string.Equals(port, "Out", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(port, "In", StringComparison.OrdinalIgnoreCase);

        /// <summary>For EditMode tests and tooling; not for production graph authoring.</summary>
        public void AppendSerializedNode(string id, string definitionTypeId, RuntimeGraph nested = null) =>
            serializedNodes.Add(new SerializedRuntimeNode { id = id, definitionTypeId = definitionTypeId, nestedRuntimeGraph = nested });

        public void AppendSerializedEdge(string fromNodeId, string fromPort, string toNodeId, string toPort) =>
            serializedEdges.Add(new SerializedRuntimeEdge
            {
                fromNodeId = fromNodeId,
                fromPort = fromPort,
                toNodeId = toNodeId,
                toPort = toPort
            });

        public void AppendSerializedEntry(string entryTypeAssemblyQualifiedName, string entryNodeId) =>
            serializedEntries.Add(new SerializedRuntimeEntry
            {
                entryTypeAssemblyQualifiedName = entryTypeAssemblyQualifiedName,
                entryNodeId = entryNodeId
            });

        public void ClearSerializedForTests() => ResetSerializedRuntime();

        public void ResetSerializedRuntime()
        {
            serializedNodes.Clear();
            serializedEdges.Clear();
            serializedEntries.Clear();
        }
    }
}
