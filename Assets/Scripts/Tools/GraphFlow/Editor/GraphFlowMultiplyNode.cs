using System;
using Scaffold.GraphFlow.Sample;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor
{
    [Serializable]
    public sealed class GraphFlowMultiplyNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<int>("A").Build();
            context.AddInputPort<int>("B").Build();
            context.AddOutputPort<int>("Product").Build();
            context.AddInputPort("In").Build();
            context.AddOutputPort("Out").Build();
        }

        public string DefinitionTypeId => new MultiplyNumbersDefinition().DefinitionTypeId;
    }
}
