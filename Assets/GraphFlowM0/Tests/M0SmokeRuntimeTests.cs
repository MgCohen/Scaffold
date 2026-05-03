using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow.M0;
using Scaffold.GraphFlow.M0.Smoke;
using UnityEngine;

namespace Scaffold.GraphFlow.M0.Tests
{
    public sealed class M0SmokeRuntimeTests
    {
        [Test]
        public async Task Mode1_OnPlay_IntToString_Log()
        {
            var onPlay = new OnPlayRuntime { nodeId = 1, editorGuid = "a" };
            var conv = new IntToStringRuntime { nodeId = 2, editorGuid = "b" };
            var log = new LogDispatcherRuntime { nodeId = 3, editorGuid = "c" };

            var asset = ScriptableObject.CreateInstance<MySmokeGraphAsset>();
            asset.nodes = new List<RuntimeNode<MySmokeRunner>> { onPlay, conv, log };
            asset.entries = new List<EntryIndex>
            {
                new EntryIndex { entryTypeId = typeof(OnPlay).AssemblyQualifiedName!, rootNodeId = 1 },
            };

            asset.flowEdges.Add(new FlowEdge
            {
                fromNodeId = 1,
                fromFlowPortId = OnPlayRuntime.Ports.FlowOut,
                toNodeId = 3,
                toFlowPortId = LogDispatcherRuntime.FlowInSlotId,
            });

            asset.connections.Add(new ConnectionRecord
            {
                fromNodeId = 1,
                fromPortId = OnPlayRuntime.Ports.CardId,
                toNodeId = 2,
                toPortId = IntToStringRuntime.Ports.InValue,
            });

            asset.connections.Add(new ConnectionRecord
            {
                fromNodeId = 2,
                fromPortId = IntToStringRuntime.Ports.OutString,
                toNodeId = 3,
                toPortId = LogDispatcherRuntime.Ports.Message,
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
            var conv = new IntToStringRuntime { nodeId = 3, editorGuid = "c" };
            var log = new LogDispatcherRuntime { nodeId = 4, editorGuid = "d" };

            var asset = ScriptableObject.CreateInstance<MySmokeGraphAsset>();
            asset.nodes = new List<RuntimeNode<MySmokeRunner>> { onPlay, echo, conv, log };
            asset.entries = new List<EntryIndex>
            {
                new EntryIndex { entryTypeId = typeof(OnPlay).AssemblyQualifiedName!, rootNodeId = 1 },
            };

            asset.flowEdges.Add(new FlowEdge
            {
                fromNodeId = 1,
                fromFlowPortId = OnPlayRuntime.Ports.FlowOut,
                toNodeId = 2,
                toFlowPortId = EchoDispatcherRuntime.Ports.FlowIn,
            });

            asset.flowEdges.Add(new FlowEdge
            {
                fromNodeId = 2,
                fromFlowPortId = EchoDispatcherRuntime.Ports.FlowOut,
                toNodeId = 4,
                toFlowPortId = LogDispatcherRuntime.FlowInSlotId,
            });

            asset.connections.Add(new ConnectionRecord
            {
                fromNodeId = 1,
                fromPortId = OnPlayRuntime.Ports.CardId,
                toNodeId = 3,
                toPortId = IntToStringRuntime.Ports.InValue,
            });

            asset.connections.Add(new ConnectionRecord
            {
                fromNodeId = 3,
                fromPortId = IntToStringRuntime.Ports.OutString,
                toNodeId = 2,
                toPortId = EchoDispatcherRuntime.Ports.Magnitude,
            });

            asset.connections.Add(new ConnectionRecord
            {
                fromNodeId = 2,
                fromPortId = EchoDispatcherRuntime.Ports.Summary,
                toNodeId = 4,
                toPortId = LogDispatcherRuntime.Ports.Message,
            });

            var runner = new MySmokeRunner();
            var controller = new GraphController<MySmokeRunner>(asset);
            controller.Initialize(runner);

            await controller.Run(new OnPlay { CardId = 42 });

            Assert.AreEqual("echo:42", runner.LastLogMessage);
        }
    }
}
