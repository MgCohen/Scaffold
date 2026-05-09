#nullable enable
using System;
using UnityEngine;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    public sealed class SetVariable<T> : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public InputPort<T> NewValue = null!;
        public FlowInPort In = null!;
        public FlowOutPort Done = null!;
        VariableCell<T>? _cell;

        public SetVariable()
        {
            NewValue = new InputPort<T>();
            Done = new FlowOutPort(this, "Done");
            In = FlowInPort.Sync(this, nameof(In), flow =>
            {
                if (_cell != null) _cell.Value = NewValue.Read(flow);
                return Done;
            });
            Ports.Add("NewValue", NewValue);
            Ports.Add(In.Name, In);
            Ports.Add(Done.Name, Done);
        }

        public override void Initialize(GraphRunner runner) =>
            runner.Variables.TryGetCell<T>(variableId, out _cell);
    }
}
