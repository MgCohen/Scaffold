#nullable enable
using System;
using UnityEngine;

namespace Scaffold.GraphFlow.Nodes
{
    // Set-Variable nodes — one concrete class per supported VariableDefault<T>.
    //
    // Each node caches its VariableCell<T> in Initialize. The In handler writes
    // through cell.Value directly (no dictionary lookup, no boxing) and forwards
    // execution to Done.

    [Serializable, GraphNode(Category = "Variables/Set")]
    public sealed partial class SetIntVariable : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public InputPort<int> NewValue = null!;
        public FlowInPort In = null!;
        public FlowOutPort Done = null!;
        VariableCell<int>? _cell;
        public override void Initialize(GraphRunner runner) =>
            runner.Variables.TryGetCell<int>(variableId, out _cell!);
        partial void InitializePorts() =>
            In = FlowInPort.Sync(this, nameof(In), flow =>
            {
                if (_cell != null) _cell.Value = NewValue.Read(flow);
                return Done;
            });
    }

    [Serializable, GraphNode(Category = "Variables/Set")]
    public sealed partial class SetFloatVariable : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public InputPort<float> NewValue = null!;
        public FlowInPort In = null!;
        public FlowOutPort Done = null!;
        VariableCell<float>? _cell;
        public override void Initialize(GraphRunner runner) =>
            runner.Variables.TryGetCell<float>(variableId, out _cell!);
        partial void InitializePorts() =>
            In = FlowInPort.Sync(this, nameof(In), flow =>
            {
                if (_cell != null) _cell.Value = NewValue.Read(flow);
                return Done;
            });
    }

    [Serializable, GraphNode(Category = "Variables/Set")]
    public sealed partial class SetBoolVariable : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public InputPort<bool> NewValue = null!;
        public FlowInPort In = null!;
        public FlowOutPort Done = null!;
        VariableCell<bool>? _cell;
        public override void Initialize(GraphRunner runner) =>
            runner.Variables.TryGetCell<bool>(variableId, out _cell!);
        partial void InitializePorts() =>
            In = FlowInPort.Sync(this, nameof(In), flow =>
            {
                if (_cell != null) _cell.Value = NewValue.Read(flow);
                return Done;
            });
    }

    [Serializable, GraphNode(Category = "Variables/Set")]
    public sealed partial class SetStringVariable : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public InputPort<string> NewValue = null!;
        public FlowInPort In = null!;
        public FlowOutPort Done = null!;
        VariableCell<string>? _cell;
        public override void Initialize(GraphRunner runner) =>
            runner.Variables.TryGetCell<string>(variableId, out _cell!);
        partial void InitializePorts() =>
            In = FlowInPort.Sync(this, nameof(In), flow =>
            {
                if (_cell != null) _cell.Value = NewValue.Read(flow);
                return Done;
            });
    }

    [Serializable, GraphNode(Category = "Variables/Set")]
    public sealed partial class SetObjectVariable : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public InputPort<UnityEngine.Object> NewValue = null!;
        public FlowInPort In = null!;
        public FlowOutPort Done = null!;
        VariableCell<UnityEngine.Object>? _cell;
        public override void Initialize(GraphRunner runner) =>
            runner.Variables.TryGetCell<UnityEngine.Object>(variableId, out _cell!);
        partial void InitializePorts() =>
            In = FlowInPort.Sync(this, nameof(In), flow =>
            {
                if (_cell != null) _cell.Value = NewValue.Read(flow);
                return Done;
            });
    }
}
