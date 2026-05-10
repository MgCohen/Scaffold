#nullable enable
using System;
using NUnit.Framework;
using Scaffold.Variables;

namespace Scaffold.Variables.Tests
{
    public class InMemoryVariableBagTests
    {
        [Serializable]
        sealed class TestIntDefault : VariableDefault<int> { }

        [Serializable]
        sealed class TestStringDefault : VariableDefault<string>
        {
            public TestStringDefault() { value = ""; }
        }

        [Test]
        public void SeedsTypedHandlesFromDefaults()
        {
            var intDef = new TestIntDefault { value = 42 };
            var strDef = new TestStringDefault { value = "hello" };
            var bag = new InMemoryVariableBag(new[]
            {
                ("hp", (VariableDefault?)intDef),
                ("name", (VariableDefault?)strDef),
            });

            Assert.IsTrue(bag.TryGet<int>("hp", out var hp));
            Assert.AreEqual(42, hp.Value);
            Assert.AreEqual(typeof(int), hp.Type);
            Assert.AreEqual("hp", hp.Id);

            Assert.IsTrue(bag.TryGet<string>("name", out var name));
            Assert.AreEqual("hello", name.Value);
        }

        [Test]
        public void TypeMismatchReturnsFalse()
        {
            var bag = new InMemoryVariableBag(new[]
            {
                ("hp", (VariableDefault?)new TestIntDefault { value = 100 }),
            });

            Assert.IsFalse(bag.TryGet<float>("hp", out var hp));
            Assert.IsNull(hp);
        }

        [Test]
        public void MissingKeyReturnsFalseAtRoot()
        {
            var bag = new InMemoryVariableBag();
            Assert.IsFalse(bag.TryGet<int>("missing", out var handle));
            Assert.IsNull(handle);
        }

        [Test]
        public void SetWritesValueAndReadGetsIt()
        {
            var bag = new InMemoryVariableBag(new[]
            {
                ("hp", (VariableDefault?)new TestIntDefault { value = 0 }),
            });

            Assert.IsTrue(bag.TryGet<int>("hp", out var hp));
            hp.Set(75);
            Assert.AreEqual(75, hp.Value);
        }

        [Test]
        public void SubscribeFiresOnDistinctValueOnly()
        {
            var bag = new InMemoryVariableBag(new[]
            {
                ("hp", (VariableDefault?)new TestIntDefault { value = 0 }),
            });

            Assert.IsTrue(bag.TryGet<int>("hp", out var hp));

            int callCount = 0;
            int lastValue = -1;
            hp.Subscribe(v => { callCount++; lastValue = v; });

            hp.Set(50);
            Assert.AreEqual(1, callCount);
            Assert.AreEqual(50, lastValue);

            hp.Set(50); // same value, should not fire
            Assert.AreEqual(1, callCount);

            hp.Set(75);
            Assert.AreEqual(2, callCount);
            Assert.AreEqual(75, lastValue);
        }

        [Test]
        public void UnsubscribeStopsCallbacks()
        {
            var bag = new InMemoryVariableBag(new[]
            {
                ("hp", (VariableDefault?)new TestIntDefault { value = 0 }),
            });

            Assert.IsTrue(bag.TryGet<int>("hp", out var hp));

            int callCount = 0;
            Action<int> handler = _ => callCount++;
            hp.Subscribe(handler);
            hp.Set(10);
            Assert.AreEqual(1, callCount);

            hp.Unsubscribe(handler);
            hp.Set(20);
            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void LookupCascadesThroughParents()
        {
            var global = new InMemoryVariableBag(new[]
            {
                ("hp", (VariableDefault?)new TestIntDefault { value = 100 }),
            });
            var graph = new InMemoryVariableBag(parent: global);
            var flow = new InMemoryVariableBag(parent: graph);

            Assert.IsTrue(flow.TryGet<int>("hp", out var hp));
            Assert.AreEqual(100, hp.Value);
        }

        [Test]
        public void WriteHitsOwningLayerImplicitly()
        {
            var global = new InMemoryVariableBag(new[]
            {
                ("hp", (VariableDefault?)new TestIntDefault { value = 100 }),
            });
            var graph = new InMemoryVariableBag(parent: global);
            var flow = new InMemoryVariableBag(parent: graph);

            Assert.IsTrue(flow.TryGet<int>("hp", out var fromFlow));
            Assert.IsTrue(graph.TryGet<int>("hp", out var fromGraph));
            Assert.IsTrue(global.TryGet<int>("hp", out var fromGlobal));

            // All three layers return the same handle instance — write through any
            // and it lands in the owning bag (global).
            Assert.AreSame(fromGlobal, fromGraph);
            Assert.AreSame(fromGlobal, fromFlow);

            fromFlow.Set(42);
            Assert.AreEqual(42, fromGlobal.Value);
        }

        [Test]
        public void NonGenericTryGetReturnsBaseHandle()
        {
            var bag = new InMemoryVariableBag(new[]
            {
                ("hp", (VariableDefault?)new TestIntDefault { value = 99 }),
            });

            Assert.IsTrue(bag.TryGet("hp", out var handle));
            Assert.AreEqual("hp", handle.Id);
            Assert.AreEqual(typeof(int), handle.Type);
        }

        [Test]
        public void LocalHandlesEnumeratesOwnedHandles()
        {
            var bag = new InMemoryVariableBag(new[]
            {
                ("a", (VariableDefault?)new TestIntDefault { value = 1 }),
                ("b", (VariableDefault?)new TestIntDefault { value = 2 }),
            });

            int count = 0;
            foreach (var _ in bag.LocalHandles) count++;
            Assert.AreEqual(2, count);
        }

        [Test]
        public void CyclicParentChainDoesNotHang()
        {
            // The shared InMemoryVariableBag's Parent is constructor-only, so
            // a cycle isn't reachable via the public API. Force one via
            // reflection on the auto-property's backing field to exercise the
            // TryGetGuarded recursion termination.
            var a = new InMemoryVariableBag();
            var b = new InMemoryVariableBag(parent: a);
            var backingField = typeof(InMemoryVariableBag).GetField(
                "<Parent>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            backingField.SetValue(a, b);

            Assert.IsFalse(a.TryGet<int>("anything", out _));
            Assert.IsFalse(b.TryGet<int>("anything", out _));
            Assert.IsFalse(a.TryGet("anything", out _));
            Assert.IsFalse(b.TryGet("anything", out _));
        }
    }
}
