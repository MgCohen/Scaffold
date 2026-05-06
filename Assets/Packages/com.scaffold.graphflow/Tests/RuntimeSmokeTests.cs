using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow.Nodes;
using UnityEngine;

namespace Scaffold.GraphFlow.Tests
{
    public sealed class RuntimeSmokeTests
    {
        const string EntryFlowOut       = "FlowOut";
        const string EntryValuePortName = "Value";

        const string EchoFlowIn   = "FlowIn";
        const string EchoFlowOut  = "FlowOut";
        const string EchoMag      = "Magnitude";
        const string EchoSummary  = "Summary";

        const string LogFlowIn    = "FlowIn";
        const string LogMessage   = "Message";

        const string IntToStrIn   = "Value";
        const string IntToStrOut  = "Result";

        const string BranchFlowIn    = "In";
        const string BranchCondition = "Condition";
        const string BranchTrue      = "True";
        const string BranchFalse     = "False";
        const string NotValue        = "Value";
        const string NotResult       = "Result";
        const string ReturnFlowIn    = "In";
        const string ReturnValue     = "Value";
        const string CancelFlowIn    = "In";

        [Test]
        public async Task Mode1_Entry_IntToString_Log()
        {
            var entry = new TestEntryRuntime          { nodeId = 1, editorGuid = "a" };
            var conv  = new TestIntToStringRuntime    { nodeId = 2, editorGuid = "b" };
            var log   = new TestLogDispatcherRuntime  { nodeId = 3, editorGuid = "c" };

            var asset = ScriptableObject.CreateInstance<TestGraphAsset>();
            asset.nodes = new List<RuntimeNode> { entry, conv, log };
            asset.flowEdges.Add(new Edge { fromNodeId = 1, fromPortName = EntryFlowOut,    toNodeId = 3, toPortName = LogFlowIn });
            asset.connections.Add(new Edge { fromNodeId = 1, fromPortName = EntryValuePortName, toNodeId = 2, toPortName = IntToStrIn });
            asset.connections.Add(new Edge { fromNodeId = 2, fromPortName = IntToStrOut,        toNodeId = 3, toPortName = LogMessage });

            var sink = new CollectingLogSink();
            var runner = new TestBuilder(sink).Build(asset);
            await runner.Run(new TestEntry { Value = 42 });

            Assert.AreEqual("42", sink.Messages[^1]);
        }

        [Test]
        public async Task Mode2_Entry_Echo_Log_BuildPayload_WriteOutputs()
        {
            var entry = new TestEntryRuntime           { nodeId = 1, editorGuid = "a" };
            var echo  = new TestEchoDispatcherRuntime  { nodeId = 2, editorGuid = "b" };
            var log   = new TestLogDispatcherRuntime   { nodeId = 3, editorGuid = "c" };

            var asset = ScriptableObject.CreateInstance<TestGraphAsset>();
            asset.nodes = new List<RuntimeNode> { entry, echo, log };
            asset.flowEdges.Add(new Edge { fromNodeId = 1, fromPortName = EntryFlowOut, toNodeId = 2, toPortName = EchoFlowIn });
            asset.flowEdges.Add(new Edge { fromNodeId = 2, fromPortName = EchoFlowOut,  toNodeId = 3, toPortName = LogFlowIn });
            asset.connections.Add(new Edge { fromNodeId = 1, fromPortName = EntryValuePortName, toNodeId = 2, toPortName = EchoMag });
            asset.connections.Add(new Edge { fromNodeId = 2, fromPortName = EchoSummary,        toNodeId = 3, toPortName = LogMessage });

            var sink = new CollectingLogSink();
            var runner = new TestBuilder(sink).Build(asset);
            await runner.Run(new TestEntry { Value = 42 });

            Assert.AreEqual("echo:42", sink.Messages[^1]);
        }

        [Test]
        public async Task M2_Entry_Not_Branch_Return_TruePath()
        {
            var entry  = new TestEntryRuntime { nodeId = 1, editorGuid = "a" };
            var not    = new Not              { nodeId = 2, editorGuid = "b" };
            var branch = new Branch           { nodeId = 3, editorGuid = "c" };
            var ret    = new Return<bool>     { nodeId = 4, editorGuid = "d" };
            var cancel = new Cancel           { nodeId = 5, editorGuid = "e" };

            var asset = ScriptableObject.CreateInstance<TestGraphAsset>();
            asset.nodes = new List<RuntimeNode> { entry, not, branch, ret, cancel };
            asset.flowEdges.Add(new Edge { fromNodeId = 1, fromPortName = EntryFlowOut, toNodeId = 3, toPortName = BranchFlowIn });
            asset.flowEdges.Add(new Edge { fromNodeId = 3, fromPortName = BranchTrue,   toNodeId = 4, toPortName = ReturnFlowIn });
            asset.flowEdges.Add(new Edge { fromNodeId = 3, fromPortName = BranchFalse,  toNodeId = 5, toPortName = CancelFlowIn });
            asset.connections.Add(new Edge { fromNodeId = 2, fromPortName = NotResult, toNodeId = 3, toPortName = BranchCondition });

            var runner = new TestBuilder(new CollectingLogSink()).Build(asset);
            var flow = await runner.Run(new TestEntry { Value = 0 });

            Assert.AreEqual(Outcome.Returned, flow.Outcome, "Return path was taken; Outcome should be Returned.");
            Assert.AreEqual(false, flow.ReadResult<bool>(), "Return.Value is unwired → reads default(bool)=false.");
        }

        [Test]
        public async Task M2_Branch_False_Cancel_Path()
        {
            var entry  = new TestEntryRuntime { nodeId = 1, editorGuid = "a" };
            var notA   = new Not              { nodeId = 2, editorGuid = "b" };
            var notB   = new Not              { nodeId = 3, editorGuid = "c" };
            var branch = new Branch           { nodeId = 4, editorGuid = "d" };
            var ret    = new Return<bool>     { nodeId = 5, editorGuid = "e" };
            var cancel = new Cancel           { nodeId = 6, editorGuid = "f" };

            var asset = ScriptableObject.CreateInstance<TestGraphAsset>();
            asset.nodes = new List<RuntimeNode> { entry, notA, notB, branch, ret, cancel };
            asset.flowEdges.Add(new Edge { fromNodeId = 1, fromPortName = EntryFlowOut, toNodeId = 4, toPortName = BranchFlowIn });
            asset.flowEdges.Add(new Edge { fromNodeId = 4, fromPortName = BranchTrue,   toNodeId = 5, toPortName = ReturnFlowIn });
            asset.flowEdges.Add(new Edge { fromNodeId = 4, fromPortName = BranchFalse,  toNodeId = 6, toPortName = CancelFlowIn });
            asset.connections.Add(new Edge { fromNodeId = 2, fromPortName = NotResult, toNodeId = 3, toPortName = NotValue });
            asset.connections.Add(new Edge { fromNodeId = 3, fromPortName = NotResult, toNodeId = 4, toPortName = BranchCondition });

            var runner = new TestBuilder(new CollectingLogSink()).Build(asset);
            var flow = await runner.Run(new TestEntry { Value = 0 });

            Assert.AreEqual(Outcome.Cancelled, flow.Outcome, "False branch reaches Cancel.");
        }

        [Test]
        public async Task M3_Return_Stores_Bool_Value()
        {
            var entry = new TestEntryRuntime { nodeId = 1, editorGuid = "a" };
            var not   = new Not              { nodeId = 2, editorGuid = "b" };
            var ret   = new Return<bool>     { nodeId = 3, editorGuid = "c" };

            var asset = ScriptableObject.CreateInstance<TestGraphAsset>();
            asset.nodes = new List<RuntimeNode> { entry, not, ret };
            asset.flowEdges.Add(new Edge { fromNodeId = 1, fromPortName = EntryFlowOut, toNodeId = 3, toPortName = ReturnFlowIn });
            asset.connections.Add(new Edge { fromNodeId = 2, fromPortName = NotResult, toNodeId = 3, toPortName = ReturnValue });

            var runner = new TestBuilder(new CollectingLogSink()).Build(asset);
            var flow = await runner.Run(new TestEntry { Value = 0 });

            Assert.AreEqual(Outcome.Returned, flow.Outcome);
            Assert.AreEqual(true, flow.ReadResult<bool>());
        }
    }
}
