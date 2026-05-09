#nullable enable
using System;
using UnityEngine;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    public sealed class ObserveVariable<T> : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public FlowOutPort FlowOut = null!;
        public OutputPort<T> NewValue = null!;
        VariableCell<T>? _cell;

        public ObserveVariable()
        {
            FlowOut = new FlowOutPort(this, "FlowOut");
            NewValue = new OutputPort<T>(flow => flow.GetPayload<VariableChangePayload<T>>()!.Value);
            Ports.Add(FlowOut.Name, FlowOut);
            Ports.Add("NewValue", NewValue);
        }

        public override void Initialize(GraphRunner runner)
        {
            if (!runner.Variables.TryGetCell<T>(variableId, out _cell)) return;
            _cell.Changed += v => _ = runner.RunObserver(FlowOut, new VariableChangePayload<T>(v));
        }
    }
}
