#nullable enable
using System;
using Scaffold.Variables;
using UnityEngine;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    public sealed class ObserveVariable<T> : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public FlowOutPort FlowOut = null!;
        public OutputPort<T> NewValue = null!;
        IVariableHandle<T>? _handle;

        public ObserveVariable()
        {
            FlowOut = new FlowOutPort(this, "FlowOut");
            NewValue = new OutputPort<T>(flow => flow.GetPayload<VariableChangePayload<T>>()!.Value);
            Ports.Add(FlowOut.Name, FlowOut);
            Ports.Add("NewValue", NewValue);
        }

        public override void Initialize(GraphRunner runner)
        {
            if (!runner.Variables.TryGet<T>(variableId, out _handle)) return;
            _handle.Subscribe(v => _ = runner.RunObserver(FlowOut, new VariableChangePayload<T>(v)));
        }
    }
}
