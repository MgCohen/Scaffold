using System;
using Scaffold.GraphFlow;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Scaffold.GraphFlow.Editor
{
    [Serializable]
    public sealed class GraphFlowInvokeNode : Node
    {
        public RuntimeGraph nestedRuntimeGraph;

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("In").Build();
            context.AddOutputPort("Out").Build();
        }
    }
}
