using System.Collections.Generic;

namespace Scaffold.GraphFlow
{
    public sealed class DataEdgeRuntime
    {
        public DataEdgeRuntime(ExecutableNode sourceNode, string sourcePortName, string targetPortName)
        {
            SourceNode = sourceNode;
            SourcePortName = sourcePortName;
            TargetPortName = targetPortName;
        }

        public ExecutableNode SourceNode { get; }
        public string SourcePortName { get; }
        public string TargetPortName { get; }
    }

    public sealed class ExecutableNode
    {
        public ExecutableNode(NodeId serializedId, IGraphNodeDefinition definition)
        {
            SerializedId = serializedId;
            Definition = definition;
        }

        public NodeId SerializedId { get; }
        public IGraphNodeDefinition Definition { get; }

        public Dictionary<string, ExecutableNode> FlowSuccessors { get; } = new Dictionary<string, ExecutableNode>();

        public List<DataEdgeRuntime> DataEdgesIn { get; } = new List<DataEdgeRuntime>();

        public ExecutableNode GetFlowSuccessor(string portName)
        {
            return FlowSuccessors.TryGetValue(portName, out var n) ? n : null;
        }
    }
}
