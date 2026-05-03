using System;
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
                toFlowPortId = LogDispatcherRuntime.Ports.FlowIn,
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
            var log = new LogDispatcherRuntime { nodeId = 3, editorGuid = "c" };

            var asset = ScriptableObject.CreateInstance<MySmokeGraphAsset>();
            asset.nodes = new List<RuntimeNode<MySmokeRunner>> { onPlay, echo, log };
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
                toNodeId = 3,
                toFlowPortId = LogDispatcherRuntime.Ports.FlowIn,
            });

            asset.connections.Add(new ConnectionRecord
            {
                fromNodeId = 1,
                fromPortId = OnPlayRuntime.Ports.CardId,
                toNodeId = 2,
                toPortId = EchoDispatcherRuntime.Ports.Magnitude,
            });

            asset.connections.Add(new ConnectionRecord
            {
                fromNodeId = 2,
                fromPortId = EchoDispatcherRuntime.Ports.Summary,
                toNodeId = 3,
                toPortId = LogDispatcherRuntime.Ports.Message,
            });

            var runner = new MySmokeRunner();
            var controller = new GraphController<MySmokeRunner>(asset);
            controller.Initialize(runner);

            await controller.Run(new OnPlay { CardId = 42 });

            Assert.AreEqual("echo:42", runner.LastLogMessage);
        }

        [Test]
        public void Map_AppliesConversionLazily()
        {
            var onPlay = new OnPlayRuntime { nodeId = 1, editorGuid = "z" };
            var ok = new Connection<string>(onPlay, 0, () => "42");
            var coerced = Connection.Map(ok, s => int.TryParse(s, out var v) ? v : -1);
            Assert.AreEqual(42, coerced.Read());

            var bad = new Connection<string>(onPlay, 0, () => "x");
            var coerced2 = Connection.Map(bad, s => int.TryParse(s, out var v) ? v : 7);
            Assert.AreEqual(7, coerced2.Read());
        }
    }
}
