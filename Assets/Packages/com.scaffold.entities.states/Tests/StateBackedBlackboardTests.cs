#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.Entities;
using Scaffold.Entities.States;
using Scaffold.GraphFlow;
using Scaffold.GraphFlow.Nodes;
using Scaffold.States;
using Scaffold.Variables;
using UnityEngine;
using Variable = Scaffold.Variables.Variable;

namespace Scaffold.Entities.States.Tests
{
    // Milestone 6 — end-to-end consumer wire-up. Demonstrates that a graph's
    // declared "hp" variable can be bound to an entity's EntityState.BaseValues
    // entry via StoreVariableBagBuilder, so:
    //   (a) graph reads of "hp" reflect out-of-band store.Execute writes, and
    //   (b) graph reads of "hp" follow store.LoadSnapshot reverts.
    //
    // The runner is constructed exactly the way a real consumer would: subclass
    // GraphRunner, override CreateVariableBag to return a StoreBackedVariableBag
    // built by StoreVariableBagBuilder.
    public sealed class StateBackedBlackboardTests
    {
        static Variable HpVar() => new Variable("hp", "int");

        sealed class StateBackedRunner : GraphRunner
        {
            readonly Store _store;
            readonly Reference _entityRef;
            readonly Variable _hp;
            StoreBackedVariableBag? _bag;

            public StateBackedRunner(BakedGraph baked, Store store, Reference entityRef, Variable hp)
                : base(baked)
            {
                _store = store;
                _entityRef = entityRef;
                _hp = hp;
            }

            protected override IVariableBag CreateVariableBag(IEnumerable<RuntimeVariable> seed)
            {
                _bag = new StoreVariableBagBuilder(_store)
                    .ForEntity(_entityRef)
                    .BindBase<int>("hp", _hp)
                    .Build();
                return _bag;
            }

            public void Teardown() => _bag?.Dispose();
        }

        sealed class StateBackedBuilder : GraphBuilder<StateBackedRunner>
        {
            readonly Store _store;
            readonly Reference _entityRef;
            readonly Variable _hp;

            public StateBackedBuilder(Store store, Reference entityRef, Variable hp)
            {
                _store = store;
                _entityRef = entityRef;
                _hp = hp;
            }

            protected override StateBackedRunner CreateRunner(BakedGraph baked)
                => new(baked, _store, _entityRef, _hp);
        }

        sealed class StateBackedAsset : GraphAsset<StateBackedRunner> { }

        public sealed class StartPayload : IGraphEntry { }

        [Serializable]
        public sealed class Start : EntryRuntimeNode<StartPayload>
        {
            public FlowOutPort FlowOut;
            public Start()
            {
                FlowOut = new FlowOutPort(this, nameof(FlowOut));
                Ports.Add(FlowOut.Name, FlowOut);
            }
        }

        // Records each value seen at its Value input when In fires. Mirrors the
        // graphflow tests' IntRecorder — duplicated here because that one is
        // internal to Scaffold.GraphFlow.Tests.
        [Serializable]
        public sealed class IntRecorder : RuntimeNode
        {
            public FlowInPort In = null!;
            public InputPort<int> Value = null!;
            public readonly List<int> Recorded = new();

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

        static StateBackedAsset BuildAsset()
        {
            var asset = ScriptableObject.CreateInstance<StateBackedAsset>();

            var start  = new Start              { nodeId = 1, editorGuid = "start" };
            var getHp  = new GetVariable<int>   { nodeId = 2, editorGuid = "getHp" };
            var rec    = new IntRecorder        { nodeId = 3, editorGuid = "rec"   };
            SetVariableId(getHp, "hp");

            asset.nodes.Add(start);
            asset.nodes.Add(getHp);
            asset.nodes.Add(rec);

            // Designer-authored graph blackboard: declares "hp" as int default 0.
            // The StateBackedRunner ignores this seed (the bag binds against
            // entity state instead), but the asset still reflects what a designer
            // would have authored on the blackboard panel.
            asset.variables.Add(new RuntimeVariable
            {
                id = "hp",
                name = "hp",
                typeName = typeof(int).AssemblyQualifiedName!,
                defaultValue = new BlackboardInt { value = 0 },
            });

            // Flow: Start → IntRecorder.In
            asset.flowEdges.Add(new Edge
            {
                fromNodeId = 1, fromPortName = nameof(Start.FlowOut),
                toNodeId = 3, toPortName = nameof(IntRecorder.In),
            });

            // Data: GetVariable.Value → IntRecorder.Value
            asset.connections.Add(new Edge
            {
                fromNodeId = 2, fromPortName = "Value",
                toNodeId = 3, toPortName = nameof(IntRecorder.Value),
            });

            return asset;
        }

        static void SetVariableId(RuntimeNode node, string id)
        {
            var field = node.GetType().GetField("variableId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "Reflection: 'variableId' field not found.");
            field!.SetValue(node, id);
        }

        static (Store store, Ref<EntityState> entityRef) NewStoreWithEntity(int initialHp)
        {
            var builder = new StoreBuilder();
            EntityBridgeContext.RegisterMutators(builder);
            var store = builder.Build();
            var entityRef = new Ref<EntityState>(Guid.NewGuid());
            store.RegisterSlice(entityRef,
                EntityState.Empty.WithBaseValue(HpVar(), new IntVariableValue(initialHp)));
            return (store, entityRef);
        }

        static IntRecorder RecorderFrom(StateBackedRunner runner)
        {
            foreach (var node in runner.Nodes)
            {
                if (node is IntRecorder rec) return rec;
            }
            throw new InvalidOperationException("Recorder not found in baked graph.");
        }

        [Test]
        public async Task GraphRead_ReflectsOutOfBandStoreExecute()
        {
            var (store, entityRef) = NewStoreWithEntity(initialHp: 100);
            var asset = BuildAsset();
            var runner = new StateBackedBuilder(store, entityRef, HpVar()).Build(asset);
            var recorder = RecorderFrom(runner);

            await runner.Run(new StartPayload());
            Assert.AreEqual(new[] { 100 }, recorder.Recorded);

            // Out-of-band write: the producer (game logic, network handler,
            // mutator pipeline) calls store.Execute directly. The graph has no
            // reference to this code path.
            store.Execute(new SetBaseValuePayload(entityRef, HpVar(), new IntVariableValue(50)));

            await runner.Run(new StartPayload());
            Assert.AreEqual(new[] { 100, 50 }, recorder.Recorded);

            runner.Teardown();
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public async Task GraphRead_FollowsSnapshotRestore()
        {
            var (store, entityRef) = NewStoreWithEntity(initialHp: 100);
            var asset = BuildAsset();
            var runner = new StateBackedBuilder(store, entityRef, HpVar()).Build(asset);
            var recorder = RecorderFrom(runner);

            await runner.Run(new StartPayload());
            Assert.AreEqual(new[] { 100 }, recorder.Recorded);

            var snap = store.SaveSnapshot();
            store.Execute(new SetBaseValuePayload(entityRef, HpVar(), new IntVariableValue(50)));

            await runner.Run(new StartPayload());
            Assert.AreEqual(new[] { 100, 50 }, recorder.Recorded);

            store.LoadSnapshot(snap);

            await runner.Run(new StartPayload());
            Assert.AreEqual(new[] { 100, 50, 100 }, recorder.Recorded);

            runner.Teardown();
            UnityEngine.Object.DestroyImmediate(asset);
        }

        // Writing through the graph's SetVariable<int> node should dispatch a
        // SetBaseValuePayload that lands in EntityState — the same path
        // out-of-band consumers use. Verifies the round-trip in the other
        // direction.
        [Test]
        public async Task GraphWrite_DispatchesPayloadToStore()
        {
            var (store, entityRef) = NewStoreWithEntity(initialHp: 100);
            var asset = ScriptableObject.CreateInstance<StateBackedAsset>();

            var start  = new Start              { nodeId = 1, editorGuid = "start"  };
            var setHp  = new SetVariable<int>   { nodeId = 2, editorGuid = "setHp"  };
            var hpLit  = new IntLiteral         { nodeId = 3, editorGuid = "hpLit", Value = 42 };
            SetVariableId(setHp, "hp");

            asset.nodes.Add(start);
            asset.nodes.Add(setHp);
            asset.nodes.Add(hpLit);
            asset.variables.Add(new RuntimeVariable
            {
                id = "hp", name = "hp",
                typeName = typeof(int).AssemblyQualifiedName!,
                defaultValue = new BlackboardInt { value = 0 },
            });

            asset.flowEdges.Add(new Edge { fromNodeId = 1, fromPortName = nameof(Start.FlowOut), toNodeId = 2, toPortName = nameof(SetVariable<int>.In) });
            asset.connections.Add(new Edge { fromNodeId = 3, fromPortName = nameof(IntLiteral.Out), toNodeId = 2, toPortName = nameof(SetVariable<int>.NewValue) });

            var runner = new StateBackedBuilder(store, entityRef, HpVar()).Build(asset);

            await runner.Run(new StartPayload());

            // EntityState slice now has hp=42 — written via the graph's
            // SetVariable<int> node → StoreBackedHandle.Set → store.Execute(SetBaseValuePayload).
            Assert.That(store.Get<EntityState>(entityRef).TryGetBase(HpVar(), out var bv), Is.True);
            Assert.That(((IntVariableValue)bv).Value, Is.EqualTo(42));

            runner.Teardown();
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Serializable]
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
    }
}
