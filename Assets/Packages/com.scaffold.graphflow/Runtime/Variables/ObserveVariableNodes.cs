#nullable enable
using System;
using UnityEngine;

namespace Scaffold.GraphFlow.Nodes
{
    // Observe-Variable nodes — fire a flow whenever the bound cell changes.
    //
    // Each node caches its VariableCell<T> in Initialize and subscribes to
    // cell.Changed. On change, it boxes the new value into a
    // VariableChangePayload<T> (one allocation per change, cold path) and asks
    // the runner to drive a flow from this node's FlowOut. The downstream graph
    // reads the new value via NewValue.Read(flow), which pulls the payload from
    // the flow.
    //
    // Lifecycle caveat: subscriptions are never torn down. For runner-owned
    // cells this is fine — the cell is gc'd with the runner. For cells in a
    // consumer-supplied parent bag (CreateParentBag), the handler keeps the
    // runner alive until the parent bag releases the cell. Future polish:
    // explicit teardown / IDisposable on GraphRunner.

    [Serializable, GraphNode(Category = "Variables/Observe")]
    public sealed partial class ObserveIntVariable : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public FlowOutPort FlowOut = null!;
        public OutputPort<int> NewValue = null!;
        VariableCell<int>? _cell;

        partial void InitializePorts() =>
            NewValue = new OutputPort<int>(flow => flow.GetPayload<VariableChangePayload<int>>()?.Value ?? default);

        public override void Initialize(GraphRunner runner)
        {
            if (!runner.Variables.TryGetCell<int>(variableId, out _cell)) return;
            _cell.Changed += v => _ = runner.RunObserver(FlowOut, new VariableChangePayload<int>(v));
        }
    }

    [Serializable, GraphNode(Category = "Variables/Observe")]
    public sealed partial class ObserveFloatVariable : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public FlowOutPort FlowOut = null!;
        public OutputPort<float> NewValue = null!;
        VariableCell<float>? _cell;

        partial void InitializePorts() =>
            NewValue = new OutputPort<float>(flow => flow.GetPayload<VariableChangePayload<float>>()?.Value ?? default);

        public override void Initialize(GraphRunner runner)
        {
            if (!runner.Variables.TryGetCell<float>(variableId, out _cell)) return;
            _cell.Changed += v => _ = runner.RunObserver(FlowOut, new VariableChangePayload<float>(v));
        }
    }

    [Serializable, GraphNode(Category = "Variables/Observe")]
    public sealed partial class ObserveBoolVariable : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public FlowOutPort FlowOut = null!;
        public OutputPort<bool> NewValue = null!;
        VariableCell<bool>? _cell;

        partial void InitializePorts() =>
            NewValue = new OutputPort<bool>(flow => flow.GetPayload<VariableChangePayload<bool>>()?.Value ?? default);

        public override void Initialize(GraphRunner runner)
        {
            if (!runner.Variables.TryGetCell<bool>(variableId, out _cell)) return;
            _cell.Changed += v => _ = runner.RunObserver(FlowOut, new VariableChangePayload<bool>(v));
        }
    }

    [Serializable, GraphNode(Category = "Variables/Observe")]
    public sealed partial class ObserveStringVariable : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public FlowOutPort FlowOut = null!;
        public OutputPort<string> NewValue = null!;
        VariableCell<string>? _cell;

        partial void InitializePorts() =>
            NewValue = new OutputPort<string>(flow => flow.GetPayload<VariableChangePayload<string>>()?.Value ?? string.Empty);

        public override void Initialize(GraphRunner runner)
        {
            if (!runner.Variables.TryGetCell<string>(variableId, out _cell)) return;
            _cell.Changed += v => _ = runner.RunObserver(FlowOut, new VariableChangePayload<string>(v));
        }
    }

    [Serializable, GraphNode(Category = "Variables/Observe")]
    public sealed partial class ObserveObjectVariable : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public FlowOutPort FlowOut = null!;
        public OutputPort<UnityEngine.Object> NewValue = null!;
        VariableCell<UnityEngine.Object>? _cell;

        partial void InitializePorts() =>
            NewValue = new OutputPort<UnityEngine.Object>(flow => flow.GetPayload<VariableChangePayload<UnityEngine.Object>>()?.Value ?? null!);

        public override void Initialize(GraphRunner runner)
        {
            if (!runner.Variables.TryGetCell<UnityEngine.Object>(variableId, out _cell)) return;
            _cell.Changed += v => _ = runner.RunObserver(FlowOut, new VariableChangePayload<UnityEngine.Object>(v));
        }
    }
}
