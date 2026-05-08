#nullable enable
using System;
using UnityEngine;

namespace Scaffold.GraphFlow.Nodes
{
    // Get-Variable nodes — one concrete class per supported VariableDefault<T>.
    //
    // Each node caches its VariableCell<T> in Initialize and exposes a single
    // OutputPort<T>. Hot path is cache: false so consumers see live cell writes
    // without per-flow staleness.
    //
    // Adding a new type means: add a VariableDefault<T> subclass + a Get/Set pair
    // here. No generator changes needed. The package generator picks up these
    // [GraphNode] classes for the editor menu via the per-package registry.

    [Serializable, GraphNode(Category = "Variables/Get")]
    public sealed partial class GetIntVariable : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public OutputPort<int> Value = null!;
        VariableCell<int>? _cell;
        public override void Initialize(GraphRunner runner) =>
            runner.Variables.TryGetCell<int>(variableId, out _cell!);
        partial void InitializePorts() =>
            Value = new OutputPort<int>(_ => _cell != null ? _cell.Value : default, cache: false);
    }

    [Serializable, GraphNode(Category = "Variables/Get")]
    public sealed partial class GetFloatVariable : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public OutputPort<float> Value = null!;
        VariableCell<float>? _cell;
        public override void Initialize(GraphRunner runner) =>
            runner.Variables.TryGetCell<float>(variableId, out _cell!);
        partial void InitializePorts() =>
            Value = new OutputPort<float>(_ => _cell != null ? _cell.Value : default, cache: false);
    }

    [Serializable, GraphNode(Category = "Variables/Get")]
    public sealed partial class GetBoolVariable : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public OutputPort<bool> Value = null!;
        VariableCell<bool>? _cell;
        public override void Initialize(GraphRunner runner) =>
            runner.Variables.TryGetCell<bool>(variableId, out _cell!);
        partial void InitializePorts() =>
            Value = new OutputPort<bool>(_ => _cell != null && _cell.Value, cache: false);
    }

    [Serializable, GraphNode(Category = "Variables/Get")]
    public sealed partial class GetStringVariable : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public OutputPort<string> Value = null!;
        VariableCell<string>? _cell;
        public override void Initialize(GraphRunner runner) =>
            runner.Variables.TryGetCell<string>(variableId, out _cell!);
        partial void InitializePorts() =>
            Value = new OutputPort<string>(_ => _cell != null ? _cell.Value : string.Empty, cache: false);
    }

    [Serializable, GraphNode(Category = "Variables/Get")]
    public sealed partial class GetObjectVariable : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public OutputPort<UnityEngine.Object> Value = null!;
        VariableCell<UnityEngine.Object>? _cell;
        public override void Initialize(GraphRunner runner) =>
            runner.Variables.TryGetCell<UnityEngine.Object>(variableId, out _cell!);
        partial void InitializePorts() =>
            Value = new OutputPort<UnityEngine.Object>(_ => _cell != null ? _cell.Value : null!, cache: false);
    }
}
