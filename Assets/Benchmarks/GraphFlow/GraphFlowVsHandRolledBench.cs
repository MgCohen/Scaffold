#nullable enable
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow;
using Scaffold.GraphFlow.Nodes;
using Scaffold.Variables;
using Unity.PerformanceTesting;
using UnityEngine;

namespace Scaffold.Benchmarks.GraphFlow
{
    /// <summary>
    /// Six paired scenarios: every <c>*_Graph</c> measures GraphFlow executing a
    /// representative pattern; the corresponding <c>*_HandRolled</c> measures the
    /// equivalent naked-C# implementation. The ratio between the two is the
    /// GraphFlow overhead floor for that pattern.
    ///
    /// Runners are built once per fixture and reused across iterations — Build is
    /// not what we're measuring; <c>Run</c> is.
    /// </summary>
    public sealed class GraphFlowVsHandRolledBench
    {
        PerfRunner _empty = null!;
        PerfRunner _branchTrue = null!;
        PerfRunner _branchFalse = null!;
        PerfRunner _dataPorts = null!;
        PerfRunner _loop1000 = null!;
        PerfRunner _variable = null!;

        readonly EmptyPayload _emptyPayload = EmptyPayload.Instance;
        readonly FlagPayload _flagTrue = new() { Flag = true };
        readonly FlagPayload _flagFalse = new() { Flag = false };
        readonly TripletPayload _triplet = new() { A = 3, B = 4, C = 5 };
        readonly LoopPayload _loopPayload = new() { Count = 1000 };
        readonly WritePayload _writePayload = new() { Value = 42 };

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var builder = new PerfBuilder();
            _empty       = builder.Build(BuildEmpty());
            _branchTrue  = builder.Build(BuildBranch());
            _branchFalse = builder.Build(BuildBranch());
            _dataPorts   = builder.Build(BuildDataPorts());
            _loop1000    = builder.Build(BuildLoop());
            _variable    = builder.Build(BuildVariable());
        }

        [SetUp]
        public void SetUp() => BenchSetup.RearmPerTest();

        // ---- Scenario 1: Empty entry -------------------------------------

        static PerfGraphAsset BuildEmpty()
        {
            var entry = new EmptyEntry();
            return PerfAsset.Make(entry); // FlowOut unwired -> graph runs no nodes
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void Empty_Graph()
        {
            Bench.Measure(() => _empty.Run(_emptyPayload).GetAwaiter().GetResult(),
                iterationsPer: 1000);
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void Empty_HandRolled()
        {
            Bench.Measure(() => HandRolled.Empty(_emptyPayload), iterationsPer: 1000);
        }

        // ---- Scenario 2: Sync branch -------------------------------------

        static PerfGraphAsset BuildBranch()
        {
            var entry  = new FlagEntry();
            var branch = new Branch();
            var ret    = new Return<bool>();
            var cancel = new Cancel();
            return PerfAsset.Make(entry, branch, ret, cancel)
                .Flow(entry,  "FlowOut", branch, "In")
                .Flow(branch, "True",    ret,    "In")
                .Flow(branch, "False",   cancel, "In")
                .Data(entry,  "Flag",    branch, "Condition");
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void SyncBranch_True_Graph()
        {
            Bench.Measure(() => _branchTrue.Run<FlagPayload, bool>(_flagTrue).GetAwaiter().GetResult(),
                iterationsPer: 1000);
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void SyncBranch_True_HandRolled()
        {
            Bench.Measure(() => HandRolled.Branch(_flagTrue), iterationsPer: 1000);
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void SyncBranch_False_Graph()
        {
            Bench.Measure(() => _branchFalse.Run<FlagPayload, bool>(_flagFalse).GetAwaiter().GetResult(),
                iterationsPer: 1000);
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void SyncBranch_False_HandRolled()
        {
            Bench.Measure(() => HandRolled.Branch(_flagFalse), iterationsPer: 1000);
        }

        // ---- Scenario 3: Data port chain (Add + Multiply) ----------------

        static PerfGraphAsset BuildDataPorts()
        {
            var entry = new TripletEntry();
            var add   = new Add();
            var mul   = new Multiply();
            var ret   = new Return<int>();
            return PerfAsset.Make(entry, add, mul, ret)
                .Flow(entry, "FlowOut", ret, "In")
                .Data(entry, "A",       add, "A")
                .Data(entry, "B",       add, "B")
                .Data(add,   "Result",  mul, "A")
                .Data(entry, "C",       mul, "B")
                .Data(mul,   "Result",  ret, "Value");
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void DataPorts_Graph()
        {
            Bench.Measure(() => _dataPorts.Run<TripletPayload, int>(_triplet).GetAwaiter().GetResult(),
                iterationsPer: 1000);
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void DataPorts_HandRolled()
        {
            Bench.Measure(() => HandRolled.DataPorts(_triplet), iterationsPer: 1000);
        }

        // ---- Scenario 4: Loop of N iterations ----------------------------

        static PerfGraphAsset BuildLoop()
        {
            var entry = new LoopEntry();
            var loop  = new Loop();
            var ret   = new Return();
            return PerfAsset.Make(entry, loop, ret)
                .Flow(entry, "FlowOut",  loop, "Begin")
                .Flow(loop,  "Body",     loop, "Continue") // empty body — measure pure loop overhead
                .Flow(loop,  "Done",     ret,  "In")
                .Data(entry, "Count",    loop, "Count");
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void Loop1000_Graph()
        {
            Bench.Measure(() => _loop1000.Run(_loopPayload).GetAwaiter().GetResult(),
                iterationsPer: 50);
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void Loop1000_HandRolled()
        {
            Bench.Measure(() => HandRolled.Loop(_loopPayload), iterationsPer: 50);
        }

        // ---- Scenario 5: Variable set + read ----------------------------

        static PerfGraphAsset BuildVariable()
        {
            var entry = new WriteEntry();
            var set   = new SetVariableInt();
            var get   = new GetVariableInt();
            var ret   = new Return<int>();
            return PerfAsset.Make(entry, set, get, ret)
                .Var<int>("v", 0)
                .Flow(entry, "FlowOut",  set, "In")
                .Flow(set,   "Done",     ret, "In")
                .Data(entry, "Value",    set, "NewValue")
                .Data(get,   "Value",    ret, "Value");
            // Set/Get nodes resolve the "v" handle themselves in Initialize;
            // no VariableEdge needed (those are for binding InputPort directly).
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void Variable_Graph()
        {
            Bench.Measure(() => _variable.Run<WritePayload, int>(_writePayload).GetAwaiter().GetResult(),
                iterationsPer: 1000);
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void Variable_HandRolled()
        {
            Bench.Measure(() => HandRolled.Variable(_writePayload), iterationsPer: 1000);
        }
    }

    // ---- Hand-rolled equivalents -----------------------------------------
    //
    // Each method does the same observable work as the matching graph: read
    // payload fields, perform the computation, return. No Task wrapping — the
    // point is to show what a developer would write *without* the graph
    // machinery, so the ratio surfaces total GraphFlow overhead.

    static class HandRolled
    {
        public static void Empty(EmptyPayload _) { }

        public static bool Branch(FlagPayload p) => p.Flag;

        public static int DataPorts(TripletPayload p) => (p.A + p.B) * p.C;

        public static int Loop(LoopPayload p)
        {
            int i = 0;
            while (i < p.Count) i++;
            return i;
        }

        // Mirrors the graph: write the value to a field, then read it back.
        static int _store;
        public static int Variable(WritePayload p)
        {
            _store = p.Value;
            return _store;
        }
    }

    // Local typed variable nodes for the Variable scenario. We can't use
    // GetVariable<int> / SetVariable<int> directly because their variableId is
    // serialized; we need to set it programmatically. Subclasses expose the
    // id and pre-set it at construction.

    [System.Serializable]
    public sealed class SetVariableInt : RuntimeNode
    {
        public InputPort<int> NewValue;
        public FlowInPort In;
        public FlowOutPort Done;
        IVariableHandle<int>? _handle;

        public SetVariableInt()
        {
            NewValue = new InputPort<int>();
            Done = new FlowOutPort(this, "Done");
            In = FlowInPort.Sync(this, "In", flow =>
            {
                _handle?.Set(NewValue.Read(flow));
                return Done;
            });
            Ports.Add(nameof(NewValue), NewValue);
            Ports.Add(In.Name, In);
            Ports.Add(Done.Name, Done);
        }

        public override void Initialize(global::Scaffold.GraphFlow.GraphRunner runner) =>
            runner.Variables.TryGet<int>("v", out _handle);
    }

    [System.Serializable]
    public sealed class GetVariableInt : RuntimeNode
    {
        public OutputPort<int> Value;
        IVariableHandle<int>? _handle;

        public GetVariableInt()
        {
            Value = new OutputPort<int>(_ => _handle != null ? _handle.Value : 0, cache: false);
            Ports.Add(nameof(Value), Value);
        }

        public override void Initialize(global::Scaffold.GraphFlow.GraphRunner runner) =>
            runner.Variables.TryGet<int>("v", out _handle);
    }
}
