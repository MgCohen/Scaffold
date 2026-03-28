using System;
using System.Collections.Generic;
using System.Linq;
using Scaffold.GraphFlow;
using Scaffold.GraphFlow.Sample;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Scaffold.GraphFlow.Editor
{
    public static class GraphFlowRuntimeBaker
    {
        public static void BakeInto(RuntimeGraph target, GraphFlowAuthoringGraph graph)
        {
            target.ResetSerializedRuntime();
            var nodes = graph.GetNodes().ToList();
            var nodeToId = new Dictionary<INode, string>();
            for (var i = 0; i < nodes.Count; i++)
                nodeToId[nodes[i]] = Guid.NewGuid().ToString("N");

            foreach (var node in nodes)
            {
                switch (node)
                {
                    case GraphFlowReactiveHookNode hook:
                        if (hook.reactiveGraph != null && !string.IsNullOrEmpty(hook.targetDefinitionTypeId))
                            target.AppendSerializedReactiveHook(hook.timing, hook.targetDefinitionTypeId, hook.reactiveGraph);
                        break;
                    case GraphFlowAddNode add:
                        target.AppendSerializedNode(nodeToId[node], add.DefinitionTypeId);
                        break;
                    case GraphFlowLogNode log:
                        target.AppendSerializedNode(nodeToId[node], log.DefinitionTypeId);
                        break;
                    case GraphFlowMultiplyNode mul:
                        target.AppendSerializedNode(nodeToId[node], mul.DefinitionTypeId);
                        break;
                    case GraphFlowLogicNode logic when !string.IsNullOrEmpty(logic.definitionTypeId):
                        target.AppendSerializedNode(nodeToId[node], logic.definitionTypeId);
                        break;
                    case GraphFlowEntryNode entry:
                        target.AppendSerializedNode(
                            nodeToId[node],
                            new GraphFlowEntryPassThroughDefinition().DefinitionTypeId);
                        break;
                    case GraphFlowInvokeNode inv:
                        target.AppendSerializedNode(
                            nodeToId[node],
                            new InvokeSubGraphDefinition().DefinitionTypeId,
                            inv.nestedRuntimeGraph);
                        break;
                }
            }

            foreach (var node in nodes)
            {
                foreach (var port in node.GetOutputPorts())
                {
                    var list = new List<IPort>();
                    port.GetConnectedPorts(list);
                    foreach (var other in list)
                    {
                        var fromNode = port.GetNode();
                        var toNode = other.GetNode();
                        if (fromNode == null || toNode == null)
                            continue;
                        if (!nodeToId.ContainsKey(fromNode) || !nodeToId.ContainsKey(toNode))
                            continue;
                        target.AppendSerializedEdge(nodeToId[fromNode], port.name, nodeToId[toNode], other.name);
                    }
                }
            }

            foreach (var node in nodes)
            {
                if (node is GraphFlowEntryNode entry && !string.IsNullOrEmpty(entry.entryTypeAssemblyQualifiedName))
                    target.AppendSerializedEntry(entry.entryTypeAssemblyQualifiedName, nodeToId[node]);
            }

        }

    }
}
