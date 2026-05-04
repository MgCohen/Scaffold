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
    /// payload-driven runtime emit shape. Port names are field names (post-M3 phase 2 / decision #4).
    /// </summary>
    public sealed class M0SmokeRuntimeTests
    {
        const string OnPlayFlowOut       = "FlowOut";
        const string OnPlayCardId        = "CardId";
        const string EchoFlowIn          = "FlowIn";
        const string EchoFlowOut         = "FlowOut";
        const string EchoMagnitude       = "Magnitude";
        const string EchoSummary         = "Summary";
        const string LogFlowIn           = "FlowIn";
        const string LogMessage          = "Message";
        const string IntToStringValue    = "Value";
        const string IntToStringResult   = "Result";

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
                fromNodeId = 1, fromFlowPortName = OnPlayFlowOut,
                toNodeId = 3, toFlowPortName = LogFlowIn,
            });

            asset.connections.Add(new ConnectionRecord
            {
                fromNodeId = 1, fromPortName = OnPlayCardId,
                toNodeId = 2, toPortName = IntToStringValue,
            });

            asset.connections.Add(new ConnectionRecord
            {
                fromNodeId = 2, fromPortName = IntToStringResult,
                toNodeId = 3, toPortName = LogMessage,
            });

            var runner = new MySmokeRunner();
            var controller = new GraphController<MySmokeRunner>(asset);
            controller.Initialize(runner);

            await controller.Run(new OnPlay { CardId = 42 });

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
                fromNodeId = 1, fromFlowPortName = OnPlayFlowOut,
                toNodeId = 2, toFlowPortName = EchoFlowIn,
            });

            asset.flowEdges.Add(new FlowEdge
            {
                fromNodeId = 2, fromFlowPortName = EchoFlowOut,
                toNodeId = 3, toFlowPortName = LogFlowIn,
            });

            asset.connections.Add(new ConnectionRecord
            {
                fromNodeId = 1, fromPortName = OnPlayCardId,
                toNodeId = 2, toPortName = EchoMagnitude,
            });

            asset.connections.Add(new ConnectionRecord
            {
                fromNodeId = 2, fromPortName = EchoSummary,
                toNodeId = 3, toPortName = LogMessage,
            });

            var runner = new MySmokeRunner();
            var controller = new GraphController<MySmokeRunner>(asset);
            controller.Initialize(runner);

            await controller.Run(new OnPlay { CardId = 42 });

            Assert.AreEqual("echo:42", runner.LastLogMessage);
        }

        const string BranchFlowIn        = Branch.FlowInPortName;
        const string BranchCondition     = Branch.ConditionPortName;
        const string BranchTrue          = Branch.TruePortName;
        const string BranchFalse         = Branch.FalsePortName;
        const string NotValue            = Not.ValuePortName;
        const string NotResult           = Not.ResultPortName;
        const string ReturnFlowIn        = Return<bool>.FlowInPortName;
        const string ReturnValuePortName = Return<bool>.ValuePortName;
        const string CancelFlowIn        = Cancel.FlowInPortName;

        [Test]
        public async Task M2_OnPlay_Not_Branch_Return_TruePath()
        {
            var onPlay = new OnPlayRuntime { nodeId = 1, editorGuid = "a" };
            var not    = new Not          { nodeId = 2, editorGuid = "b" };
            var branch = new Branch       { nodeId = 3, editorGuid = "c" };
            var ret    = new Return<bool> { nodeId = 4, editorGuid = "d" };
            var cancel = new Cancel       { nodeId = 5, editorGuid = "e" };

            var asset = ScriptableObject.CreateInstance<MySmokeGraphAsset>();
            asset.nodes = new List<RuntimeNode> { onPlay, not, branch, ret, cancel };
            asset.entries = new List<EntryIndex>
            {
                new EntryIndex { entryTypeId = typeof(OnPlay).AssemblyQualifiedName!, rootNodeId = 1 },
            };

            asset.flowEdges.Add(new FlowEdge { fromNodeId = 1, fromFlowPortName = OnPlayFlowOut, toNodeId = 3, toFlowPortName = BranchFlowIn });
            asset.flowEdges.Add(new FlowEdge { fromNodeId = 3, fromFlowPortName = BranchTrue,   toNodeId = 4, toFlowPortName = ReturnFlowIn });
            asset.flowEdges.Add(new FlowEdge { fromNodeId = 3, fromFlowPortName = BranchFalse,  toNodeId = 5, toFlowPortName = CancelFlowIn });

            asset.connections.Add(new ConnectionRecord { fromNodeId = 2, fromPortName = NotResult, toNodeId = 3, toPortName = BranchCondition });

            var runner = new MySmokeRunner();
            var controller = new GraphController<MySmokeRunner>(asset);
            controller.Initialize(runner);

            var flow = await controller.Run(new OnPlay { CardId = 0 });

            Assert.AreEqual(FlowOutcome.Returned, flow.Outcome, "Return path was taken; Outcome should be Returned.");
            Assert.AreEqual(false, flow.ReadResult<bool>(), "Return.Value is unwired → reads default(bool)=false.");
        }

        [Test]
        public async Task M2_Branch_False_Cancel_Path()
        {
            var onPlay = new OnPlayRuntime { nodeId = 1, editorGuid = "a" };
            var notA   = new Not          { nodeId = 2, editorGuid = "b" };
            var notB   = new Not          { nodeId = 3, editorGuid = "c" };
            var branch = new Branch       { nodeId = 4, editorGuid = "d" };
            var ret    = new Return<bool> { nodeId = 5, editorGuid = "e" };
            var cancel = new Cancel       { nodeId = 6, editorGuid = "f" };

            var asset = ScriptableObject.CreateInstance<MySmokeGraphAsset>();
            asset.nodes = new List<RuntimeNode> { onPlay, notA, notB, branch, ret, cancel };
            asset.entries = new List<EntryIndex>
            {
                new EntryIndex { entryTypeId = typeof(OnPlay).AssemblyQualifiedName!, rootNodeId = 1 },
            };

            asset.flowEdges.Add(new FlowEdge { fromNodeId = 1, fromFlowPortName = OnPlayFlowOut, toNodeId = 4, toFlowPortName = BranchFlowIn });
            asset.flowEdges.Add(new FlowEdge { fromNodeId = 4, fromFlowPortName = BranchTrue,   toNodeId = 5, toFlowPortName = ReturnFlowIn });
            asset.flowEdges.Add(new FlowEdge { fromNodeId = 4, fromFlowPortName = BranchFalse,  toNodeId = 6, toFlowPortName = CancelFlowIn });

            asset.connections.Add(new ConnectionRecord { fromNodeId = 2, fromPortName = NotResult, toNodeId = 3, toPortName = NotValue });
            asset.connections.Add(new ConnectionRecord { fromNodeId = 3, fromPortName = NotResult, toNodeId = 4, toPortName = BranchCondition });

            var runner = new MySmokeRunner();
            var controller = new GraphController<MySmokeRunner>(asset);
            controller.Initialize(runner);

            var flow = await controller.Run(new OnPlay { CardId = 0 });

            Assert.AreEqual(FlowOutcome.Cancelled, flow.Outcome, "False branch reaches Cancel.");
        }

        [Test]
        public async Task M3_Return_Stores_Bool_Value()
        {
            var onPlay = new OnPlayRuntime { nodeId = 1, editorGuid = "a" };
            var not    = new Not          { nodeId = 2, editorGuid = "b" };
            var ret    = new Return<bool> { nodeId = 3, editorGuid = "c" };

            var asset = ScriptableObject.CreateInstance<MySmokeGraphAsset>();
            asset.nodes = new List<RuntimeNode> { onPlay, not, ret };
            asset.entries = new List<EntryIndex>
            {
                new EntryIndex { entryTypeId = typeof(OnPlay).AssemblyQualifiedName!, rootNodeId = 1 },
            };

            asset.flowEdges.Add(new FlowEdge { fromNodeId = 1, fromFlowPortName = OnPlayFlowOut, toNodeId = 3, toFlowPortName = ReturnFlowIn });
            asset.connections.Add(new ConnectionRecord { fromNodeId = 2, fromPortName = NotResult, toNodeId = 3, toPortName = ReturnValuePortName });

            var runner = new MySmokeRunner();
            var controller = new GraphController<MySmokeRunner>(asset);
            controller.Initialize(runner);

            var flow = await controller.Run(new OnPlay { CardId = 0 });

            Assert.AreEqual(FlowOutcome.Returned, flow.Outcome);
            Assert.AreEqual(true, flow.ReadResult<bool>());
        }
    }
}
