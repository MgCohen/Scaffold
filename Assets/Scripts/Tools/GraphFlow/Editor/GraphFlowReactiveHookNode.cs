using System;
using Scaffold.GraphFlow;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Scaffold.GraphFlow.Editor
{
    [Serializable]
    public sealed class GraphFlowReactiveHookNode : Node
    {
        public MiddlewarePhase timing = MiddlewarePhase.Before;
        public string targetDefinitionTypeId = "";
        public RuntimeGraph reactiveGraph;

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
        }
    }
}
