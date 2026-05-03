using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow.M0;
using Scaffold.GraphFlow.M0.Smoke;
using UnityEngine;

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
    }
}
