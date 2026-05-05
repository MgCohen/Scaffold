#nullable enable
using System;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.Tests
{
    public sealed class TestRunner : GraphRunner
    {
        public string LastLogMessage { get; private set; } = "";
        public void RecordLog(string message) => LastLogMessage = message;

        public TestRunner(BakedGraph baked) : base(baked) { }
    }

    public sealed class TestBuilder : GraphBuilder<TestRunner>
    {
        protected override TestRunner CreateRunner(BakedGraph baked) => new(baked);
    }

    public sealed class TestEntry : IGraphEntry
    {
        public int Value;
    }

    [Serializable]
    public sealed class TestEntryRuntime : EntryRuntimeNode<TestEntry>
    {
        public FlowOutPort FlowOut;
        public OutputPort<int> Value;

        public TestEntryRuntime()
        {
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            Value = new OutputPort<int>(flow => flow.GetPayload<TestEntry>()!.Value);
            Ports.Add(FlowOut.Name, FlowOut);
            Ports.Add(nameof(Value), Value);
        }
    }

    [Serializable]
    public sealed class TestIntToStringRuntime : RuntimeNode
    {
        public InputPort<int> Value;
        public OutputPort<string> Result;

        public TestIntToStringRuntime()
        {
            Value = new InputPort<int>();
            Result = new OutputPort<string>(flow => Value.Read(flow).ToString());
            Ports.Add(nameof(Value), Value);
            Ports.Add(nameof(Result), Result);
        }
    }

    [Serializable]
    public sealed class TestLogDispatcherRuntime : RuntimeNode<TestRunner>
    {
        public FlowInPort FlowIn;
        public InputPort<string> Message;

        public TestLogDispatcherRuntime()
        {
            Message = new InputPort<string>();
            FlowIn = FlowInPort.Sync(this, nameof(FlowIn), flow =>
            {
                Runner(flow).RecordLog(Message.Read(flow) ?? "");
                return FlowOutPort.End;
            });
            Ports.Add(FlowIn.Name, FlowIn);
            Ports.Add(nameof(Message), Message);
        }
    }

    [Serializable]
    public sealed class TestEchoDispatcherRuntime : RuntimeNode<TestRunner>
    {
        public FlowInPort FlowIn;
        public FlowOutPort FlowOut;
        public InputPort<int> Magnitude;
        public OutputPort<string> Summary;

        public TestEchoDispatcherRuntime()
        {
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            Magnitude = new InputPort<int>();
            Summary = new OutputPort<string>(flow => $"echo:{Magnitude.Read(flow)}");
            FlowIn = FlowInPort.Sync(this, nameof(FlowIn), flow => FlowOut);
            Ports.Add(FlowIn.Name, FlowIn);
            Ports.Add(FlowOut.Name, FlowOut);
            Ports.Add(nameof(Magnitude), Magnitude);
            Ports.Add(nameof(Summary), Summary);
        }
    }

    public sealed class TestGraphAsset : GraphAsset<TestRunner> { }
}
