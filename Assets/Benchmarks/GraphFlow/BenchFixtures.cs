#nullable enable
using System;
using System.Collections.Generic;
using Scaffold.GraphFlow;
using Scaffold.GraphFlow.Nodes;
using Scaffold.Variables;
using UnityEngine;

namespace Scaffold.Benchmarks.GraphFlow
{
    public sealed class PerfRunner : global::Scaffold.GraphFlow.GraphRunner
    {
        public PerfRunner(BakedGraph baked) : base(baked) { }
    }

    public sealed class PerfBuilder : GraphBuilder<PerfRunner>
    {
        protected override PerfRunner CreateRunner(BakedGraph baked) => new(baked);
    }

    public sealed class PerfGraphAsset : GraphAsset<PerfRunner> { }

    public static class PerfAsset
    {
        public static PerfGraphAsset Make(params RuntimeNode[] nodes)
        {
            var asset = ScriptableObject.CreateInstance<PerfGraphAsset>();
            asset.nodes = new List<RuntimeNode>(nodes);
            for (int i = 0; i < nodes.Length; i++) nodes[i].nodeId = i + 1;
            return asset;
        }

        public static PerfGraphAsset Flow(this PerfGraphAsset a, RuntimeNode from, string fromPort, RuntimeNode to, string toPort)
        {
            a.flowEdges.Add(new Edge { fromNodeId = from.nodeId, fromPortName = fromPort, toNodeId = to.nodeId, toPortName = toPort });
            return a;
        }

        public static PerfGraphAsset Data(this PerfGraphAsset a, RuntimeNode from, string fromPort, RuntimeNode to, string toPort)
        {
            a.connections.Add(new Edge { fromNodeId = from.nodeId, fromPortName = fromPort, toNodeId = to.nodeId, toPortName = toPort });
            return a;
        }

        public static PerfGraphAsset Var<T>(this PerfGraphAsset a, string id, T value) where T : notnull
        {
            VariableDefault def = value switch
            {
                int i    => new BlackboardInt    { value = i },
                float f  => new BlackboardFloat  { value = f },
                bool b   => new BlackboardBool   { value = b },
                string s => new BlackboardString { value = s },
                _ => throw new NotSupportedException($"No BlackboardVariable for {typeof(T)}.")
            };
            a.variables.Add(new RuntimeVariable { id = id, name = id, typeName = typeof(T).FullName!, defaultValue = def });
            return a;
        }

        public static PerfGraphAsset VarEdge(this PerfGraphAsset a, string variableId, RuntimeNode to, string toPort)
        {
            a.variableEdges.Add(new VariableEdge { variableId = variableId, toNodeId = to.nodeId, toPortName = toPort });
            return a;
        }
    }

    // ---- Payloads --------------------------------------------------------

    public sealed class EmptyPayload { public static readonly EmptyPayload Instance = new(); }
    public sealed class FlagPayload  { public bool Flag; }
    public sealed class TripletPayload { public int A; public int B; public int C; }
    public sealed class LoopPayload  { public int Count; }
    public sealed class WritePayload { public int Value; }

    // ---- Entries ---------------------------------------------------------

    [Serializable]
    public sealed class EmptyEntry : EntryRuntimeNode<EmptyPayload>
    {
        public FlowOutPort FlowOut;
        public EmptyEntry()
        {
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            Ports.Add(FlowOut.Name, FlowOut);
        }
    }

    [Serializable]
    public sealed class FlagEntry : EntryRuntimeNode<FlagPayload>
    {
        public FlowOutPort FlowOut;
        public OutputPort<bool> Flag;
        public FlagEntry()
        {
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            Flag = new OutputPort<bool>(flow => ((Flow<FlagPayload>)flow).Payload.Flag);
            Ports.Add(FlowOut.Name, FlowOut);
            Ports.Add(nameof(Flag), Flag);
        }
    }

    [Serializable]
    public sealed class TripletEntry : EntryRuntimeNode<TripletPayload>
    {
        public FlowOutPort FlowOut;
        public OutputPort<int> A, B, C;
        public TripletEntry()
        {
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            A = new OutputPort<int>(flow => ((Flow<TripletPayload>)flow).Payload.A);
            B = new OutputPort<int>(flow => ((Flow<TripletPayload>)flow).Payload.B);
            C = new OutputPort<int>(flow => ((Flow<TripletPayload>)flow).Payload.C);
            Ports.Add(FlowOut.Name, FlowOut);
            Ports.Add(nameof(A), A);
            Ports.Add(nameof(B), B);
            Ports.Add(nameof(C), C);
        }
    }

    [Serializable]
    public sealed class LoopEntry : EntryRuntimeNode<LoopPayload>
    {
        public FlowOutPort FlowOut;
        public OutputPort<int> Count;
        public LoopEntry()
        {
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            Count = new OutputPort<int>(flow => ((Flow<LoopPayload>)flow).Payload.Count);
            Ports.Add(FlowOut.Name, FlowOut);
            Ports.Add(nameof(Count), Count);
        }
    }

    [Serializable]
    public sealed class WriteEntry : EntryRuntimeNode<WritePayload>
    {
        public FlowOutPort FlowOut;
        public OutputPort<int> Value;
        public WriteEntry()
        {
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            Value = new OutputPort<int>(flow => ((Flow<WritePayload>)flow).Payload.Value);
            Ports.Add(FlowOut.Name, FlowOut);
            Ports.Add(nameof(Value), Value);
        }
    }
}
