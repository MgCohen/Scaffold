using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Scaffold.GraphFlow.Tests
{
    /// <summary>
    /// Hand-written synthetic fixtures for the package's runtime smoke tests. Decoupled from any
    /// sample's generator output — every type below is fully hand-authored so the runtime tests
    /// pin the executor / hydration / port-wiring contracts without depending on the M0 sandbox
    /// staying in <c>Samples/</c> (where it auto-compiles) versus <c>Samples~/</c> (where it
    /// doesn't). Generator-emit shapes have their own coverage via the snapshot harness.
    /// </summary>
    public sealed class TestRunner : GraphRunner
    {
        public string LastLogMessage { get; private set; } = "";
        public void RecordLog(string message) => LastLogMessage = message;
    }

    /// <summary>Mode-1 entry payload — carries an int the entry runtime surfaces as an output port.</summary>
    public sealed class TestEntry : IGraphEntry
    {
        public int Value;
    }

    /// <summary>Entry runtime mirror of <see cref="TestEntry"/> — one flow-out + one data-out port.</summary>
    [Serializable]
    public sealed class TestEntryRuntime : EntryRuntimeNode<TestEntry>
    {
        public FlowOutPort FlowOut = null!;
        public OutputPort<int> Value = null!;

        int _valueBacking;

        public TestEntryRuntime()
        {
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            Value   = new OutputPort<int>(() => _valueBacking);
            Ports.Add(FlowOut.Name, FlowOut);
            Ports.Add(nameof(Value), Value);
        }

        public override Task Execute(Flow flow)
        {
            if (Payload != null) _valueBacking = Payload.Value;
            return flow.GoTo(FlowOut);
        }
    }

    /// <summary>Pure data converter — int → string. Mirrors the sandbox's <c>IntToStringRuntime</c>.</summary>
    [Serializable]
    public sealed class TestIntToStringRuntime : RuntimeNode
    {
        public InputPort<int> Value = null!;
        public OutputPort<string> Result = null!;

        public TestIntToStringRuntime()
        {
            Value  = new InputPort<int>();
            Result = new OutputPort<string>(() => Value.Read().ToString());
            Ports.Add(nameof(Value), Value);
            Ports.Add(nameof(Result), Result);
        }
    }

    /// <summary>Mode-1 self-executing log node — consumes a string input, writes to the runner.</summary>
    [Serializable]
    public sealed class TestLogDispatcherRuntime : RuntimeNode<TestRunner>
    {
        public FlowInPort FlowIn = null!;
        public InputPort<string> Message = null!;

        public TestLogDispatcherRuntime()
        {
            FlowIn  = new FlowInPort(this);
            Message = new InputPort<string>();
            Ports.Add(FlowIn.Name, FlowIn);
            Ports.Add(nameof(Message), Message);
        }

        public override Task Execute(TestRunner runner, Flow flow)
        {
            runner.RecordLog(Message.Read() ?? "");
            return flow.Stop();
        }
    }

    /// <summary>
    /// Mode-2 dispatcher — reads <see cref="Magnitude"/> from input port, "dispatches" (no real
    /// pipeline; just formats into <see cref="Summary"/>), exposes Summary as an output port,
    /// continues flow.
    /// </summary>
    [Serializable]
    public sealed class TestEchoDispatcherRuntime : RuntimeNode<TestRunner>
    {
        public FlowInPort FlowIn = null!;
        public FlowOutPort FlowOut = null!;
        public InputPort<int> Magnitude = null!;
        public OutputPort<string> Summary = null!;

        string _summaryBacking = "";

        public TestEchoDispatcherRuntime()
        {
            FlowIn    = new FlowInPort(this);
            FlowOut   = new FlowOutPort(this, nameof(FlowOut));
            Magnitude = new InputPort<int>();
            Summary   = new OutputPort<string>(() => _summaryBacking);
            Ports.Add(FlowIn.Name, FlowIn);
            Ports.Add(FlowOut.Name, FlowOut);
            Ports.Add(nameof(Magnitude), Magnitude);
            Ports.Add(nameof(Summary), Summary);
        }

        public override Task Execute(TestRunner runner, Flow flow)
        {
            _summaryBacking = $"echo:{Magnitude.Read()}";
            return flow.GoTo(FlowOut);
        }
    }

    /// <summary>Concrete graph asset SO — the executor needs a typed
    /// <see cref="GraphAsset{TRunner}"/> shell to drive hydration; nothing test-specific lives on it.</summary>
    public sealed class TestGraphAsset : GraphAsset<TestRunner> { }
}
