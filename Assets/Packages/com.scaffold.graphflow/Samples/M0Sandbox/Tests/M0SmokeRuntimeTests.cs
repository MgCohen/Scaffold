using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow.M0;
using Scaffold.GraphFlow.M0.Smoke;
using Scaffold.GraphFlow.Nodes;
using UnityEngine;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.Tests
{
    /// <summary>
    /// Hand-built (no editor) integration tests for the M0/M2/M3 runtime model — exercises hydration
    /// (Connection.Bind through Ports dict), flow walk (executor against flowEdges), and the
    /// payload-driven runtime emit shape. Port-id literals match the values the generator emits;
    /// since there is no exposed <c>Ports</c> static class on runtime nodes anymore, the tests pin
    /// the ids inline as magic numbers (the registry stamps the same ids per id-derivation rules).
    ///
    /// M3 update: tests read <c>flow.Outcome</c> and <c>flow.ReadResult&lt;T&gt;()</c> from the
    /// <see cref="Flow"/> returned by <c>controller.Run</c> instead of <c>runner.Cancelled</c> /
    /// <c>runner.ReturnValue</c>. The built-in <c>Branch</c>/<c>Cancel</c>/<c>Not</c>/<c>Return</c>
    /// nodes now live in the package's <c>Scaffold.GraphFlow.Nodes</c> namespace.
    /// </summary>
    public sealed class M0SmokeRuntimeTests
    {
        // Mirror of the ids the generator emits — see ExecPlan-v2 "Generic-node emission"
        // (sequential 1..N for [GraphNode] fields without [GraphPort], explicit values for payload
        // fields that opt-in via [GraphPort(Id = ...)]).
        const int OnPlayFlowOut       = unchecked((int)0xF0010001u);
        const int OnPlayCardId        = unchecked((int)0x4F2A8B17u);
        const int EchoFlowIn          = unchecked((int)0xF0030001u);
        const int EchoFlowOut         = unchecked((int)0xF0030002u);
        const int EchoMagnitude       = unchecked((int)0xC0030001u);
        const int EchoSummary         = unchecked((int)0xC0030002u);
        const int LogFlowIn           = 0;                    // IExecutable actions get implicit FlowIn=0
        const int LogMessage          = unchecked((int)0x77E13C20u);
        const int IntToStringValue    = 1;                    // sequential, no [GraphPort]
        const int IntToStringResult   = 2;

        [Test]
        public async Task Mode1_OnPlay_IntToString_Log()
        {
            var onPlay = new OnPlayRuntime { nodeId = 1, editorGuid = "a" };
            var conv = new IntToStringRuntime { nodeId = 2, editorGuid = "b" };
            var log = new LogDispatcherRuntime { nodeId = 3, editorGuid = "c" };

            var asset = ScriptableObject.CreateInstance<MySmokeGraphAsset>();
            asset.nodes = new List<RuntimeNode> { onPlay, conv, log };
            asset.entries = new List<EntryIndex>
            {
                new EntryIndex { entryTypeId = typeof(OnPlay).AssemblyQualifiedName!, rootNodeId = 1 },
            };

            asset.flowEdges.Add(new FlowEdge
            {
                fromNodeId = 1, fromFlowPortId = OnPlayFlowOut,
                toNodeId = 3, toFlowPortId = LogFlowIn,
            });

            asset.connections.Add(new ConnectionRecord
            {
                fromNodeId = 1, fromPortId = OnPlayCardId,
                toNodeId = 2, toPortId = IntToStringValue,
            });

            asset.connections.Add(new ConnectionRecord
            {
                fromNodeId = 2, fromPortId = IntToStringResult,
                toNodeId = 3, toPortId = LogMessage,
            });

            var runner = new MySmokeRunner();
            var controller = new GraphController<MySmokeRunner>(asset);
            controller.Initialize(runner);

            await controller.RunFlow(new OnPlay { CardId = 42 });

            Assert.AreEqual("42", runner.LastLogMessage);
        }

        [Test]
        public async Task Mode2_OnPlay_Echo_Log_BuildPayload_WriteOutputs()
        {
            var onPlay = new OnPlayRuntime { nodeId = 1, editorGuid = "a" };
            var echo = new EchoDispatcherRuntime { nodeId = 2, editorGuid = "b" };
            var log = new LogDispatcherRuntime { nodeId = 3, editorGuid = "c" };

            var asset = ScriptableObject.CreateInstance<MySmokeGraphAsset>();
            asset.nodes = new List<RuntimeNode> { onPlay, echo, log };
            asset.entries = new List<EntryIndex>
            {
                new EntryIndex { entryTypeId = typeof(OnPlay).AssemblyQualifiedName!, rootNodeId = 1 },
            };

            asset.flowEdges.Add(new FlowEdge
            {
                fromNodeId = 1, fromFlowPortId = OnPlayFlowOut,
                toNodeId = 2, toFlowPortId = EchoFlowIn,
            });

            asset.flowEdges.Add(new FlowEdge
            {
                fromNodeId = 2, fromFlowPortId = EchoFlowOut,
                toNodeId = 3, toFlowPortId = LogFlowIn,
            });

            asset.connections.Add(new ConnectionRecord
            {
                fromNodeId = 1, fromPortId = OnPlayCardId,
                toNodeId = 2, toPortId = EchoMagnitude,
            });

            asset.connections.Add(new ConnectionRecord
            {
                fromNodeId = 2, fromPortId = EchoSummary,
                toNodeId = 3, toPortId = LogMessage,
            });

            var runner = new MySmokeRunner();
            var controller = new GraphController<MySmokeRunner>(asset);
            controller.Initialize(runner);

            await controller.RunFlow(new OnPlay { CardId = 42 });

            Assert.AreEqual("echo:42", runner.LastLogMessage);
        }

        // Mirror of the generic-node port ids — the package built-ins now expose these as constants
        // on the node types themselves (Branch.TruePortId etc.). The implicit FlowIn id for any flow
        // node is 0; data port ids start at 1.
        const int BranchFlowIn        = Branch<MySmokeRunner>.FlowInPortId;
        const int BranchCondition     = Branch<MySmokeRunner>.ConditionPortId;
        const int BranchTrue          = Branch<MySmokeRunner>.TruePortId;
        const int BranchFalse         = Branch<MySmokeRunner>.FalsePortId;
        const int NotValue            = Not.ValuePortId;
        const int NotResult           = Not.ResultPortId;
        const int ReturnFlowIn        = Return<MySmokeRunner, bool>.FlowInPortId;
        const int ReturnValuePortId   = Return<MySmokeRunner, bool>.ValuePortId;
        const int CancelFlowIn        = 0;

        [Test]
        public async Task M2_OnPlay_Not_Branch_Return_TruePath()
        {
            // Not.Value is unwired and falls back to default(bool)=false, so Not.Result=true.
            // Branch picks the True path → Return<,bool> terminator with Value unwired (default false).
            // Outcome = Returned, Result = false.
            var onPlay = new OnPlayRuntime { nodeId = 1, editorGuid = "a" };
            var not    = new Not             { nodeId = 2, editorGuid = "b" };
            var branch = new Branch<MySmokeRunner> { nodeId = 3, editorGuid = "c" };
            var ret    = new Return<MySmokeRunner, bool> { nodeId = 4, editorGuid = "d" };
            var cancel = new Cancel<MySmokeRunner> { nodeId = 5, editorGuid = "e" };

            var asset = ScriptableObject.CreateInstance<MySmokeGraphAsset>();
            asset.nodes = new List<RuntimeNode> { onPlay, not, branch, ret, cancel };
            asset.entries = new List<EntryIndex>
            {
                new EntryIndex { entryTypeId = typeof(OnPlay).AssemblyQualifiedName!, rootNodeId = 1 },
            };

            asset.flowEdges.Add(new FlowEdge { fromNodeId = 1, fromFlowPortId = OnPlayFlowOut, toNodeId = 3, toFlowPortId = BranchFlowIn });
            asset.flowEdges.Add(new FlowEdge { fromNodeId = 3, fromFlowPortId = BranchTrue,   toNodeId = 4, toFlowPortId = ReturnFlowIn });
            asset.flowEdges.Add(new FlowEdge { fromNodeId = 3, fromFlowPortId = BranchFalse,  toNodeId = 5, toFlowPortId = CancelFlowIn });

            asset.connections.Add(new ConnectionRecord { fromNodeId = 2, fromPortId = NotResult, toNodeId = 3, toPortId = BranchCondition });

            var runner = new MySmokeRunner();
            var controller = new GraphController<MySmokeRunner>(asset);
            controller.Initialize(runner);

            var flow = await controller.RunFlow(new OnPlay { CardId = 0 });

            Assert.AreEqual(FlowOutcome.Returned, flow.Outcome, "Return path was taken; Outcome should be Returned.");
            Assert.AreEqual(false, flow.ReadResult<bool>(), "Return.Value is unwired → reads default(bool)=false.");
        }

        [Test]
        public async Task M2_Branch_False_Cancel_Path()
        {
            // Hand-wire a true→Not→Branch.Condition flow so Branch picks the False side, hitting Cancel.
            // We don't have a "literal true" data node in the M2 catalog, so we emulate by stacking two
            // Nots: NotA.Value defaults false → Result true → NotB.Value=true → Result false → Branch.Condition=false.
            var onPlay = new OnPlayRuntime { nodeId = 1, editorGuid = "a" };
            var notA   = new Not             { nodeId = 2, editorGuid = "b" };
            var notB   = new Not             { nodeId = 3, editorGuid = "c" };
            var branch = new Branch<MySmokeRunner> { nodeId = 4, editorGuid = "d" };
            var ret    = new Return<MySmokeRunner, bool> { nodeId = 5, editorGuid = "e" };
            var cancel = new Cancel<MySmokeRunner> { nodeId = 6, editorGuid = "f" };

            var asset = ScriptableObject.CreateInstance<MySmokeGraphAsset>();
            asset.nodes = new List<RuntimeNode> { onPlay, notA, notB, branch, ret, cancel };
            asset.entries = new List<EntryIndex>
            {
                new EntryIndex { entryTypeId = typeof(OnPlay).AssemblyQualifiedName!, rootNodeId = 1 },
            };

            asset.flowEdges.Add(new FlowEdge { fromNodeId = 1, fromFlowPortId = OnPlayFlowOut, toNodeId = 4, toFlowPortId = BranchFlowIn });
            asset.flowEdges.Add(new FlowEdge { fromNodeId = 4, fromFlowPortId = BranchTrue,   toNodeId = 5, toFlowPortId = ReturnFlowIn });
            asset.flowEdges.Add(new FlowEdge { fromNodeId = 4, fromFlowPortId = BranchFalse,  toNodeId = 6, toFlowPortId = CancelFlowIn });

            asset.connections.Add(new ConnectionRecord { fromNodeId = 2, fromPortId = NotResult, toNodeId = 3, toPortId = NotValue });
            asset.connections.Add(new ConnectionRecord { fromNodeId = 3, fromPortId = NotResult, toNodeId = 4, toPortId = BranchCondition });

            var runner = new MySmokeRunner();
            var controller = new GraphController<MySmokeRunner>(asset);
            controller.Initialize(runner);

            var flow = await controller.RunFlow(new OnPlay { CardId = 0 });

            Assert.AreEqual(FlowOutcome.Cancelled, flow.Outcome, "False branch reaches Cancel.");
        }

        [Test]
        public async Task M3_Return_Stores_Bool_Value()
        {
            // OnPlay → Return<,bool>, with Value wired from Not.Result (=true since Value defaults false).
            // M3 Return<TRunner, TResult> replaces the M2 ReturnBool.
            var onPlay     = new OnPlayRuntime { nodeId = 1, editorGuid = "a" };
            var not        = new Not             { nodeId = 2, editorGuid = "b" };
            var ret        = new Return<MySmokeRunner, bool> { nodeId = 3, editorGuid = "c" };

            var asset = ScriptableObject.CreateInstance<MySmokeGraphAsset>();
            asset.nodes = new List<RuntimeNode> { onPlay, not, ret };
            asset.entries = new List<EntryIndex>
            {
                new EntryIndex { entryTypeId = typeof(OnPlay).AssemblyQualifiedName!, rootNodeId = 1 },
            };

            asset.flowEdges.Add(new FlowEdge { fromNodeId = 1, fromFlowPortId = OnPlayFlowOut, toNodeId = 3, toFlowPortId = ReturnFlowIn });
            asset.connections.Add(new ConnectionRecord { fromNodeId = 2, fromPortId = NotResult, toNodeId = 3, toPortId = ReturnValuePortId });

            var runner = new MySmokeRunner();
            var controller = new GraphController<MySmokeRunner>(asset);
            controller.Initialize(runner);

            var flow = await controller.RunFlow(new OnPlay { CardId = 0 });

            Assert.AreEqual(FlowOutcome.Returned, flow.Outcome);
            Assert.AreEqual(true, flow.ReadResult<bool>());
        }
    }
}
