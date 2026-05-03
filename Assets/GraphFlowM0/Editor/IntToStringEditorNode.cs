using System;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.M0.Editor.GToolkit
{
    [Serializable]
    public sealed class IntToStringEditorNode : Node
    {
        public const string InValuePortName = "Value";
        public const string OutResultPortName = "Result";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<int>(InValuePortName).Build();
            context.AddOutputPort<string>(OutResultPortName).Build();
        }
    }
}
