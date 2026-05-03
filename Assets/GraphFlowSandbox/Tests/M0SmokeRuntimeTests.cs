using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow.M0;
using Scaffold.GraphFlow.M0.Nodes;
using Scaffold.GraphFlow.M0.Smoke;
using UnityEngine;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.Tests
{
    /// <summary>
    /// Hand-built (no editor) integration tests for the M0/M2 runtime model — exercises hydration
    /// (Connection.Bind through Ports dict), flow walk (executor against flowEdges), and the
    /// payload-driven runtime emit shape. Port-id literals match the values the generator emits;
    /// since there is no exposed <c>Ports</c> static class on runtime nodes anymore, the tests pin
    /// the ids inline as magic numbers (the registry stamps the same ids per id-derivation rules).
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

            await controller.Run(new OnPlay { CardId = 42 });

            Assert.AreEqual("echo:42", runner.LastLogMessage);
        }

        // The previous Connection.Map adapter was a v1 type-coercion helper used at hydration.
        // M2 replaced the conversion seam with Connection.Bind; converters land in M4. The Map
        // test is removed alongside the type-erased Connection<TFrom>→<TTo> bridge.

        // Mirror of the generic-node port ids — see GenericNodeParser (sequential 1..N for fields,
        // 0 for the implicit FlowIn). These shapes are also pinned in the snapshot expectations.
        const int BranchFlowIn        = 0;
        const int BranchCondition     = 1;
        const int BranchTrue          = 2;
        const int BranchFalse         = 3;
        const int NotValue            = 1;
        const int NotResult           = 2;
        const int ReturnFlowIn        = 0;
        const int CancelFlowIn        = 0;
        const int ReturnBoolFlowIn    = 0;
        const int ReturnBoolValue     = 1;

        [Test]
        public async Task M2_OnPlay_Not_Branch_Return_TruePath()
        {
            // Not.Value is unwired and falls back to default(bool)=false, so Not.Result=true.
            // Branch picks the True path → Return terminator. ReturnValue stays null (Return clears it),
            // Cancelled stays false.
            var onPlay = new OnPlayRuntime { nodeId = 1, editorGuid = "a" };
            var not    = new Not             { nodeId = 2, editorGuid = "b" };
            var branch = new Branch<MySmokeRunner> { nodeId = 3, editorGuid = "c" };
            var ret    = new Return<MySmokeRunner> { nodeId = 4, editorGuid = "d" };
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

            var runner = new MySmokeRunner { ReturnValue = "untouched" };
            var controller = new GraphController<MySmokeRunner>(asset);
            controller.Initialize(runner);

            await controller.Run(new OnPlay { CardId = 0 });

            Assert.IsFalse(runner.Cancelled, "Return path was taken; Cancel.Execute should never run.");
            Assert.IsNull(runner.ReturnValue, "Return.Execute clears ReturnValue.");
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
            var ret    = new Return<MySmokeRunner> { nodeId = 5, editorGuid = "e" };
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

            await controller.Run(new OnPlay { CardId = 0 });

            Assert.IsTrue(runner.Cancelled, "False branch reaches Cancel.");
        }

        [Test]
        public async Task M2_ReturnBool_Stores_Value()
        {
            // OnPlay → ReturnBool, with ReturnBool.Value wired from Not.Result (=true since Value defaults false).
            var onPlay     = new OnPlayRuntime { nodeId = 1, editorGuid = "a" };
            var not        = new Not             { nodeId = 2, editorGuid = "b" };
            var returnBool = new ReturnBool<MySmokeRunner> { nodeId = 3, editorGuid = "c" };

            var asset = ScriptableObject.CreateInstance<MySmokeGraphAsset>();
            asset.nodes = new List<RuntimeNode> { onPlay, not, returnBool };
            asset.entries = new List<EntryIndex>
            {
                new EntryIndex { entryTypeId = typeof(OnPlay).AssemblyQualifiedName!, rootNodeId = 1 },
            };

            asset.flowEdges.Add(new FlowEdge { fromNodeId = 1, fromFlowPortId = OnPlayFlowOut, toNodeId = 3, toFlowPortId = ReturnBoolFlowIn });
            asset.connections.Add(new ConnectionRecord { fromNodeId = 2, fromPortId = NotResult, toNodeId = 3, toPortId = ReturnBoolValue });

            var runner = new MySmokeRunner();
            var controller = new GraphController<MySmokeRunner>(asset);
            controller.Initialize(runner);

            await controller.Run(new OnPlay { CardId = 0 });

            Assert.AreEqual(true, runner.ReturnValue);
            Assert.IsFalse(runner.Cancelled);
        }
    }
}
