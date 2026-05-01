using System;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor
{
    [Serializable]
    public sealed class GraphFlowEntryNode : Node
    {
        public string entryTypeAssemblyQualifiedName = "";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("In").Build();
            context.AddOutputPort("Out").Build();
        }
    }
}
