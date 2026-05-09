using System.Collections.Generic;
using NUnit.Framework;

namespace Scaffold.GraphFlow.Tests
{
    public sealed class VariableBagTests
    {
        static IEnumerable<RuntimeVariable> Seed(params (string id, BlackboardVariable def)[] entries)
        {
            foreach (var (id, def) in entries)
                yield return new RuntimeVariable { id = id, name = id, typeName = def.ValueType.AssemblyQualifiedName, defaultValue = def };
        }

        [Test]
        public void SeedsTypedCellsFromDefaults()
        {
            var bag = new InMemoryVariableBag(Seed(
                ("hp",   new BlackboardInt   { value = 7 }),
                ("name", new BlackboardString{ value = "alice" })));

            Assert.IsTrue(bag.TryGetCell<int>("hp", out var hp));
            Assert.AreEqual(7, hp.Value);

            Assert.IsTrue(bag.TryGetCell<string>("name", out var name));
            Assert.AreEqual("alice", name.Value);
        }

        [Test]
        public void TypeMismatchReturnsFalse()
        {
            var bag = new InMemoryVariableBag(Seed(("hp", new BlackboardInt { value = 1 })));
            Assert.IsFalse(bag.TryGetCell<float>("hp", out _));
        }

        [Test]
        public void MissingKeyReturnsFalseAtRoot()
        {
            var bag = new InMemoryVariableBag(System.Array.Empty<RuntimeVariable>());
            Assert.IsFalse(bag.TryGetCell<int>("missing", out _));
        }

        [Test]
        public void LookupCascadesThroughParents()
        {
            var global = new InMemoryVariableBag(Seed(("score", new BlackboardInt { value = 100 })));
            var graph  = new InMemoryVariableBag(Seed(("hp",    new BlackboardInt { value = 5 })),   global);
            var flow   = new InMemoryVariableBag(System.Array.Empty<RuntimeVariable>(),          graph);

            Assert.IsTrue(flow.TryGetCell<int>("hp",    out var hp));     Assert.AreEqual(5,   hp.Value);
            Assert.IsTrue(flow.TryGetCell<int>("score", out var score));  Assert.AreEqual(100, score.Value);
        }

        [Test]
        public void WriteHitsOwningLayerImplicitly()
        {
            // Cached cell ref means writes target the bag that owns the id —
            // no explicit "find owner" walk on the hot path.
            var global = new InMemoryVariableBag(Seed(("score", new BlackboardInt { value = 100 })));
            var graph  = new InMemoryVariableBag(Seed(("hp",    new BlackboardInt { value = 5 })),   global);
            var flow   = new InMemoryVariableBag(System.Array.Empty<RuntimeVariable>(),          graph);

            Assert.IsTrue(flow.TryGetCell<int>("score", out var scoreFromFlow));
            scoreFromFlow.Value = 999;

            // Reading from the global bag sees the write.
            Assert.IsTrue(global.TryGetCell<int>("score", out var scoreFromGlobal));
            Assert.AreEqual(999, scoreFromGlobal.Value);

            // Graph layer never declared "score", so it doesn't shadow.
            Assert.IsTrue(graph.TryGetCell<int>("score", out var scoreFromGraph));
            Assert.AreSame(scoreFromGlobal, scoreFromGraph);
        }

        [Test]
        public void ChangedFiresOnDistinctValueOnly()
        {
            var bag = new InMemoryVariableBag(Seed(("hp", new BlackboardInt { value = 0 })));
            Assert.IsTrue(bag.TryGetCell<int>("hp", out var hp));

            int fires = 0;
            int last = -1;
            hp.Changed += v => { fires++; last = v; };

            hp.Value = 0;   // no-op
            hp.Value = 5;   // change
            hp.Value = 5;   // no-op
            hp.Value = 7;   // change

            Assert.AreEqual(2, fires);
            Assert.AreEqual(7, last);
        }

        [Test]
        public void NonGenericTryGetCellReturnsBaseCell()
        {
            var bag = new InMemoryVariableBag(Seed(("hp", new BlackboardInt { value = 3 })));
            Assert.IsTrue(bag.TryGetCell("hp", out var raw));
            Assert.AreEqual(typeof(int), raw.Type);
            Assert.AreEqual("hp", raw.Id);
        }
    }
}
