using System;
using Scaffold.GraphFlow.Sample;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor
{
    [Serializable]
    public sealed class GraphFlowLogNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<object>("Message").Build();
            context.AddInputPort("In").Build();
            context.AddOutputPort("Out").Build();
        }

        public string DefinitionTypeId => new LogObjectDefinition().DefinitionTypeId;
    }
}
