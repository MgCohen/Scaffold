#nullable enable
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow.Nodes;
using UnityEngine;

namespace Scaffold.GraphFlow.Tests
{
    // End-to-end integration tests — Phase 6 stand-in until a real GT graph
    // asset can be authored in the Unity editor. Combine variable
    // declarations, the three-layer bag chain, variable-bound input ports
    // (Phase 3), Get/Set nodes (Phase 4), and Observe nodes (Phase 5).
    public sealed class VariableEndToEndTests
    {
        sealed class ScopedRunner : GraphRunner
        {
            readonly IVariableBag? _parent;
            public ScopedRunner(BakedGraph baked, IVariableBag? parent) : base(baked) { _parent = parent; }
            protected override IVariableBag? CreateParentBag() => _parent;
        }

        sealed class ScopedBuilder : GraphBuilder<ScopedRunner>
        {
            readonly IVariableBag? _parent;
            public ScopedBuilder(IVariableBag? parent) { _parent = parent; }
            protected override ScopedRunner CreateRunner(BakedGraph baked) => new(baked, _parent);
        }

        sealed class ScopedAsset : GraphAsset<ScopedRunner> { }

        public sealed class StartPayload : IGraphEntry { }

        [System.Serializable]
        public sealed class Start : EntryRuntimeNode<StartPayload>
        {
            public FlowOutPort FlowOut;
            public Start()
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

        // Wires up:
        //   - Two graph variables: hp (int, default 100), name (string, default "hero").
        //   - One global variable in the parent bag: score (int, default 0).
        //   - Variable-bound input port: a Doubler reads `hp` directly, no Get node.
        //   - Set node writes a literal into the graph-level `hp` cell.
        //   - Set node writes into the parent-bag `score` cell — verifies bubble-up.
        //   - Observe node fires when `hp` changes; downstream recorder logs the
        //     new value.
        [Test]
        public async Task FullChain_GraphAndGlobalVariables_VariableEdges_GetSetObserve()
        {
            // Parent bag (consumer-supplied global state).
            var globalBag = new InMemoryVariableBag(new[]
            {
                VariableTestHelpers.Var("score", new IntDefault { value = 0 }),
            });

            var asset = ScriptableObject.CreateInstance<ScopedAsset>();

            var start    = new Start            { nodeId = 1, editorGuid = "start"    };
            var setHp    = new SetIntVariable   { nodeId = 2, editorGuid = "setHp"    };
            var setScore = new SetIntVariable   { nodeId = 3, editorGuid = "setScore" };
            var observe  = new ObserveIntVariable{ nodeId = 4, editorGuid = "observe" };
            var observeRec = new IntRecorder    { nodeId = 5, editorGuid = "obsRec"   };
            var hpLit    = new IntLiteral       { nodeId = 6, editorGuid = "hpLit",    Value = 75 };
            var scoreLit = new IntLiteral       { nodeId = 7, editorGuid = "scoreLit", Value = 250 };
            VariableTestHelpers.SetVariableId(setHp,    "hp");
            VariableTestHelpers.SetVariableId(setScore, "score");
            VariableTestHelpers.SetVariableId(observe,  "hp");

            asset.nodes.Add(start);
            asset.nodes.Add(setHp);
            asset.nodes.Add(setScore);
            asset.nodes.Add(observe);
            asset.nodes.Add(observeRec);
            asset.nodes.Add(hpLit);
            asset.nodes.Add(scoreLit);

            // Graph-layer declarations.
            asset.variables.Add(VariableTestHelpers.Var("hp",   new IntDefault    { value = 100 }));
            asset.variables.Add(VariableTestHelpers.Var("name", new StringDefault { value = "hero" }));

            // Flow: Start → SetHp → SetScore.
            asset.flowEdges.Add(new Edge { fromNodeId = 1, fromPortName = nameof(Start.FlowOut),    toNodeId = 2, toPortName = nameof(SetIntVariable.In)   });
            asset.flowEdges.Add(new Edge { fromNodeId = 2, fromPortName = nameof(SetIntVariable.Done), toNodeId = 3, toPortName = nameof(SetIntVariable.In) });

            // Data: literals into the setters' NewValue ports.
            asset.connections.Add(new Edge { fromNodeId = 6, fromPortName = nameof(IntLiteral.Out), toNodeId = 2, toPortName = nameof(SetIntVariable.NewValue) });
            asset.connections.Add(new Edge { fromNodeId = 7, fromPortName = nameof(IntLiteral.Out), toNodeId = 3, toPortName = nameof(SetIntVariable.NewValue) });

            // Observe → recorder: FlowOut + NewValue.
            asset.flowEdges.Add(new Edge   { fromNodeId = 4, fromPortName = nameof(ObserveIntVariable.FlowOut),  toNodeId = 5, toPortName = nameof(IntRecorder.In)    });
            asset.connections.Add(new Edge { fromNodeId = 4, fromPortName = nameof(ObserveIntVariable.NewValue), toNodeId = 5, toPortName = nameof(IntRecorder.Value) });

            var runner = new ScopedBuilder(globalBag).Build(asset);

            // Pre-conditions: graph defaults seeded, parent bag visible from runner.
            Assert.IsTrue(runner.Variables.TryGetCell<int>("hp", out var hpCell));
            Assert.AreEqual(100, hpCell.Value);
            Assert.IsTrue(runner.Variables.TryGetCell<int>("score", out var scoreCellViaRunner));
            Assert.AreEqual(0, scoreCellViaRunner.Value);
            Assert.AreSame(globalBag, runner.Variables.Parent);

            // Run the graph: SetHp (75) → SetScore (250).
            await runner.Run(new StartPayload());

            // Graph-layer write landed locally.
            Assert.AreEqual(75, hpCell.Value);

            // Global write bubbled up to the parent bag (cached cell ref → owning layer).
            Assert.IsTrue(globalBag.TryGetCell<int>("score", out var scoreInGlobal));
            Assert.AreEqual(250, scoreInGlobal.Value);

            // Observe drove a flow when hp changed: 100 → 75 once.
            Assert.AreEqual(new[] { 75 }, observeRec.Recorded);

            // Subsequent direct cell writes also fan out through Observe.
            hpCell.Value = 60;
            hpCell.Value = 60;   // no-op
            hpCell.Value = 30;
            await Task.Yield();
            Assert.AreEqual(new[] { 75, 60, 30 }, observeRec.Recorded);
        }

        // Variable-bound input port: a Doubler reads `hp` straight from the bag,
        // no intermediate Get node. Verifies Phase 3 wiring + later cell mutation.
        [Test]
        public async Task VariableBoundPort_FollowsCellMutations()
        {
            var asset = ScriptableObject.CreateInstance<ScopedAsset>();
            var start   = new Start       { nodeId = 1, editorGuid = "start" };
            var doubler = new Doubler     { nodeId = 2, editorGuid = "dbl"   };
            var rec     = new IntRecorder { nodeId = 3, editorGuid = "rec"   };
            asset.nodes.Add(start);
            asset.nodes.Add(doubler);
            asset.nodes.Add(rec);
            asset.variables.Add(VariableTestHelpers.Var("speed", new IntDefault { value = 21 }));
            asset.variableEdges.Add(new VariableEdge { variableId = "speed", toNodeId = 2, toPortName = nameof(Doubler.In) });
            asset.connections.Add(new Edge          { fromNodeId = 2, fromPortName = nameof(Doubler.Out),  toNodeId = 3, toPortName = nameof(IntRecorder.Value) });
            asset.flowEdges.Add(new Edge            { fromNodeId = 1, fromPortName = nameof(Start.FlowOut), toNodeId = 3, toPortName = nameof(IntRecorder.In)    });

            var runner = new ScopedBuilder(parent: null).Build(asset);
            await runner.Run(new StartPayload());

            Assert.AreEqual(new[] { 42 }, rec.Recorded);

            // Mutate the cell, run again — bound port reflects the new value.
            Assert.IsTrue(runner.Variables.TryGetCell<int>("speed", out var cell));
            cell.Value = 50;
            await runner.Run(new StartPayload());
            Assert.AreEqual(new[] { 42, 100 }, rec.Recorded);
        }

        [System.Serializable]
        public sealed class Doubler : RuntimeNode
        {
            public InputPort<int> In = null!;
            public OutputPort<int> Out = null!;
            public Doubler()
            {
                In = new InputPort<int>();
                Out = new OutputPort<int>(flow => In.Read(flow) * 2);
                Ports.Add(nameof(In),  In);
                Ports.Add(nameof(Out), Out);
            }
        }
    }
}
