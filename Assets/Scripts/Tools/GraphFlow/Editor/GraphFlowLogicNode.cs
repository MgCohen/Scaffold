using System;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor
{
    [Serializable]
    public sealed class GraphFlowLogicNode : Node
    {
        public string definitionTypeId = "";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("In").Build();
            context.AddOutputPort("Out").Build();
        }
    }
}
