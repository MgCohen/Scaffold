#nullable enable
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow.Nodes;
using UnityEngine;

namespace Scaffold.GraphFlow.Tests
{
    public sealed class VariableObserveTests
    {
        sealed class BareRunner  : GraphRunner    { public BareRunner(BakedGraph baked) : base(baked) { } }
        sealed class BareBuilder : GraphBuilder<BareRunner> { protected override BareRunner CreateRunner(BakedGraph baked) => new(baked); }
        sealed class BareAsset   : GraphAsset<BareRunner> { }

        public sealed class EmptyEntry : IGraphEntry { }

        // Records every value that arrives at its In port on the flow that ran it.
        [System.Serializable]
        public sealed class IntRecorder : RuntimeNode
        {
            public FlowInPort In = null!;
            public InputPort<int> Value = null!;
            public readonly System.Collections.Generic.List<int> Recorded = new();

            public IntRecorder()
            {
                Value = new InputPort<int>();
                In = FlowInPort.Sync(this, nameof(In), flow =>
                {
                    Recorded.Add(Value.Read(flow));
                    return null;
                });
                Ports.Add(In.Name, In);
                Ports.Add(nameof(Value), Value);
            }
        }

        static void SetVariableId(RuntimeNode node, string id)
        {
            var field = node.GetType().GetField("variableId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field!.SetValue(node, id);
        }

        [Test]
        public async Task CellChangeFiresObserverFlowWithNewValue()
        {
            var asset = ScriptableObject.CreateInstance<BareAsset>();
            var observe  = new ObserveIntVariable { nodeId = 1, editorGuid = "a" };
            var recorder = new IntRecorder       { nodeId = 2, editorGuid = "b" };
            SetVariableId(observe, "hp");

            asset.nodes.Add(observe);
            asset.nodes.Add(recorder);
            asset.variables.Add(new RuntimeVariable
            {
                id = "hp",
                name = "hp",
                typeName = typeof(int).AssemblyQualifiedName,
                defaultValue = new IntDefault { value = 0 },
            });
            asset.flowEdges.Add(new Edge   { fromNodeId = 1, fromPortName = nameof(ObserveIntVariable.FlowOut),  toNodeId = 2, toPortName = nameof(IntRecorder.In) });
            asset.connections.Add(new Edge { fromNodeId = 1, fromPortName = nameof(ObserveIntVariable.NewValue), toNodeId = 2, toPortName = nameof(IntRecorder.Value) });

            var runner = new BareBuilder().Build(asset);
            Assert.IsTrue(runner.Variables.TryGetCell<int>("hp", out var cell));

            cell.Value = 7;
            await Task.Yield();   // let any continuations land

            cell.Value = 7;       // same — Changed not raised, no record
            cell.Value = 11;
            await Task.Yield();

            Assert.AreEqual(new[] { 7, 11 }, recorder.Recorded);
        }
    }
}
