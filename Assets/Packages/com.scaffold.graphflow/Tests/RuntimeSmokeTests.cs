using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow.Nodes;
using UnityEngine;

namespace Scaffold.GraphFlow.Tests
{
    /// <summary>
    /// Hand-built (no editor / no bake) integration tests for the runtime model — exercises
    /// hydration (<c>Connection.Bind</c> through the <c>Ports</c> dict + <c>FlowConnection</c>
    /// through flow ports), the executor's flow walk, and the entry-bridge dispatch contract.
    /// Port names are field names (post-M3 phase 2 / decision #4).
    ///
    /// <para>Fixtures live in <see cref="Scaffold.GraphFlow.Tests.TestRunner"/> and friends —
    /// fully hand-authored so these tests don't depend on any sample's generator output.</para>
    /// </summary>
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

        const string BranchFlowIn    = "FlowIn";
        const string BranchCondition = "Condition";
        const string BranchTrue      = "True";
        const string BranchFalse     = "False";
        const string NotValue        = "Value";
        const string NotResult       = "Result";
        const string ReturnFlowIn    = "FlowIn";
        const string ReturnValue     = "Value";
        const string CancelFlowIn    = "FlowIn";

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

            var runner = new TestRunner();
            var controller = new GraphController<TestRunner>(asset);
            controller.Initialize(runner);

            await controller.Run(new TestEntry { Value = 42 });

            Assert.AreEqual("42", runner.LastLogMessage);
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

            var runner = new TestRunner();
            var controller = new GraphController<TestRunner>(asset);
            controller.Initialize(runner);

            await controller.Run(new TestEntry { Value = 42 });

            Assert.AreEqual("echo:42", runner.LastLogMessage);
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

            var runner = new TestRunner();
            var controller = new GraphController<TestRunner>(asset);
            controller.Initialize(runner);

            var flow = await controller.Run(new TestEntry { Value = 0 });

            Assert.AreEqual(FlowOutcome.Returned, flow.Outcome, "Return path was taken; Outcome should be Returned.");
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

            var runner = new TestRunner();
            var controller = new GraphController<TestRunner>(asset);
            controller.Initialize(runner);

            var flow = await controller.Run(new TestEntry { Value = 0 });

            Assert.AreEqual(FlowOutcome.Cancelled, flow.Outcome, "False branch reaches Cancel.");
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

            var runner = new TestRunner();
            var controller = new GraphController<TestRunner>(asset);
            controller.Initialize(runner);

            var flow = await controller.Run(new TestEntry { Value = 0 });

            Assert.AreEqual(FlowOutcome.Returned, flow.Outcome);
            Assert.AreEqual(true, flow.ReadResult<bool>());
        }
    }
}
