#nullable enable
using System;
using UnityEngine;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    public sealed class GetVariable<T> : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public OutputPort<T> Value = null!;
        VariableCell<T>? _cell;

        public GetVariable()
        {
            Value = new OutputPort<T>(_ => _cell != null ? _cell.Value : default!, cache: false);
            Ports.Add("Value", Value);
        }

        public override void Initialize(GraphRunner runner) =>
            runner.Variables.TryGetCell<T>(variableId, out _cell);
    }
}
