using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow.M0.Smoke;
using UnityEngine;

namespace Scaffold.GraphFlow.M0.Tests
{
    /// <summary>
    /// Validates hydration + linear executor without Graph Toolkit — pure runtime slice.
    /// </summary>
    public sealed class M0SmokeRuntimeTests
    {
        [Test]
        public async Task Run_OnPlayToLog_ChainsDataThroughIntToString()
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

            // Flow: OnPlay -> Log
            asset.connections.Add(new ConnectionRecord
            {
                fromNodeId = 1,
                fromPortId = OnPlayRuntime.Ports.FlowOut,
                toNodeId = 3,
                toPortId = LogDispatcherRuntime.Ports.FlowIn,
            });

            // Data: CardId -> IntToString.In
            asset.connections.Add(new ConnectionRecord
            {
                fromNodeId = 1,
                fromPortId = OnPlayRuntime.Ports.CardId,
                toNodeId = 2,
                toPortId = IntToStringRuntime.Ports.InValue,
            });

            // Data: IntToString.Out -> Log.Message
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
    }
}
