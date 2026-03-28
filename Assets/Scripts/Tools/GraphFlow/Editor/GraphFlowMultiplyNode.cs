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
            context.AddInputPort("In").Build();
            context.AddOutputPort("Out").Build();
        }

        public string DefinitionTypeId => new MultiplyAddInputsDefinition().DefinitionTypeId;
    }
}
