#nullable enable
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow.Nodes;
using UnityEngine;

namespace Scaffold.GraphFlow.Tests
{
    public sealed class VariableGetSetTests
    {
        sealed class BareRunner  : GraphRunner    { public BareRunner(BakedGraph baked) : base(baked) { } }
        sealed class BareBuilder : GraphBuilder<BareRunner> { protected override BareRunner CreateRunner(BakedGraph baked) => new(baked); }
        sealed class BareAsset   : GraphAsset<BareRunner> { }

        public sealed class EmptyEntry : IGraphEntry { }

        [System.Serializable]
        public sealed class Entry : EntryRuntimeNode<EmptyEntry>
        {
            public FlowOutPort FlowOut;
            public Entry()
            {
                FlowOut = new FlowOutPort(this, nameof(FlowOut));
                Ports.Add(FlowOut.Name, FlowOut);
            }
        }

        [System.Serializable]
        public sealed class IntLiteral : RuntimeNode
        {
            public int Value;
            public OutputPort<int> Out = null!;
            public IntLiteral()
            {
                Out = new OutputPort<int>(_ => Value, cache: false);
                Ports.Add(nameof(Out), Out);
            }
        }

        [Test]
        public async Task GetIntVariableReadsCellValue()
        {
            var asset = ScriptableObject.CreateInstance<BareAsset>();
            var entry = new Entry { nodeId = 1, editorGuid = "a" };
            var get   = new GetIntVariable { nodeId = 2, editorGuid = "b" };
            VariableTestHelpers.SetVariableId(get, "hp");
            asset.nodes.Add(entry);
            asset.nodes.Add(get);
            asset.variables.Add(VariableTestHelpers.Var("hp", new IntDefault { value = 7 }));

            var runner = new BareBuilder().Build(asset);
            var flow = await runner.Run(new EmptyEntry());

            Assert.AreEqual(7, get.Value.Read(flow));

            // Mutating the cell flows through (cache: false on the Get port).
            Assert.IsTrue(runner.Variables.TryGetCell<int>("hp", out var cell));
            cell.Value = 13;
            flow.InvalidateAll();
            Assert.AreEqual(13, get.Value.Read(flow));
        }

        [Test]
        public async Task SetIntVariableWritesCellThroughFlowExecution()
        {
            var asset = ScriptableObject.CreateInstance<BareAsset>();
            var entry  = new Entry          { nodeId = 1, editorGuid = "a" };
            var lit    = new IntLiteral     { nodeId = 2, editorGuid = "b", Value = 99 };
            var setter = new SetIntVariable { nodeId = 3, editorGuid = "c" };
            VariableTestHelpers.SetVariableId(setter, "hp");

            asset.nodes.Add(entry);
            asset.nodes.Add(lit);
            asset.nodes.Add(setter);
            asset.variables.Add(VariableTestHelpers.Var("hp", new IntDefault { value = 0 }));
            asset.connections.Add(new Edge { fromNodeId = 2, fromPortName = nameof(IntLiteral.Out), toNodeId = 3, toPortName = nameof(SetIntVariable.NewValue) });
            asset.flowEdges.Add(new Edge   { fromNodeId = 1, fromPortName = nameof(Entry.FlowOut),  toNodeId = 3, toPortName = nameof(SetIntVariable.In) });

            var runner = new BareBuilder().Build(asset);
            await runner.Run(new EmptyEntry());

            Assert.IsTrue(runner.Variables.TryGetCell<int>("hp", out var cell));
            Assert.AreEqual(99, cell.Value);
        }

        [Test]
        public async Task GetUnsetVariableReturnsTypedDefault()
        {
            var asset = ScriptableObject.CreateInstance<BareAsset>();
            asset.nodes.Add(new Entry { nodeId = 1, editorGuid = "a" });
            var get = new GetIntVariable { nodeId = 2, editorGuid = "b" };
            VariableTestHelpers.SetVariableId(get, "missing");
            asset.nodes.Add(get);

            var runner = new BareBuilder().Build(asset);
            var flow = await runner.Run(new EmptyEntry());

            // No declaration for "missing" → cell resolves to null → port returns default(int).
            Assert.AreEqual(0, get.Value.Read(flow));
        }
    }
}
