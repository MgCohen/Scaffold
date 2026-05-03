using System;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.M0.Editor.GToolkit
{
    /// <summary>Entry node — OnPlay with Run flow + CardId output.</summary>
    [Serializable]
    public sealed class OnPlayEditorNode : Node
    {
        public const string FlowOutPortName = "FlowOut";
        public const string CardIdPortName = "CardId";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddOutputPort(FlowOutPortName)
                .WithDisplayName(string.Empty)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            context.AddOutputPort<int>(CardIdPortName).Build();
        }
    }

    /// <summary>Mode 2 smoke — Echo command + fake result ports.</summary>
    [Serializable]
    public sealed class EchoDispatcherEditorNode : Node
    {
        public const string FlowInPortName = "FlowIn";
        public const string FlowOutPortName = "FlowOut";
        public const string MagnitudePortName = "Magnitude";
        public const string SummaryPortName = "Summary";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort(FlowInPortName)
                .WithDisplayName(string.Empty)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            context.AddOutputPort(FlowOutPortName)
                .WithDisplayName(string.Empty)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            context.AddInputPort<int>(MagnitudePortName).Build();
            context.AddOutputPort<string>(SummaryPortName).Build();
        }
    }

    /// <summary>Action node — Log dispatcher with flow ordering + Message input.</summary>
    [Serializable]
    public sealed class LogDispatcherEditorNode : Node
    {
        public const string FlowInPortName = "FlowIn";
        public const string MessagePortName = "Message";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort(FlowInPortName)
                .WithDisplayName(string.Empty)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            context.AddInputPort<string>(MessagePortName).Build();
        }
    }
}
