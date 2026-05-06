#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Tests.Fixtures;

namespace Scaffold.States.Tests
{
    public sealed class StoreQueryTests
    {
        [Test]
        public void TryGet_AbsentSlice_ReturnsFalseAndDoesNotThrow()
        {
            var builder = new StoreBuilder();
            Store store = builder.Build();

            bool found = store.TryGet<CounterState>(new SampleKey("missing"), out CounterState state);

            Assert.That(found, Is.False);
            Assert.That(state, Is.Null);
        }

        [Test]
        public void TryGet_PresentCanonicalSlice_ReturnsTrueAndState()
        {
            var key = new SampleKey("present");
            var builder = new StoreBuilder();
            builder.AddState(key, new CounterState(7));
            Store store = builder.Build();

            bool found = store.TryGet<CounterState>(key, out CounterState state);

            Assert.That(found, Is.True);
            Assert.That(state.Value, Is.EqualTo(7));
        }

        [Test]
        public void TryGet_PresentAggregateSlice_ReturnsCachedState()
        {
            StoreFeaturesDemo demo = SampleStoreFactory.CreateFullDemo();
            Store store = demo.Store;

            bool found = store.TryGet<TotalsDashboardState>(Reference.Null, out TotalsDashboardState state);

            Assert.That(found, Is.True);
            Assert.That(state, Is.Not.Null);
        }

        [Test]
        public void EnumerateAll_KeyedCanonical_YieldsReferenceAndStatePairs()
        {
            var keyA = new SampleKey("A");
            var keyB = new SampleKey("B");
            var builder = new StoreBuilder();
            builder.AddState(keyA, new CounterState(1));
            builder.AddState(keyB, new CounterState(2));
            Store store = builder.Build();

            var pairs = new List<(Reference Reference, CounterState State)>();
            foreach (var pair in store.EnumerateAllPairs<CounterState>())
            {
                pairs.Add(pair);
            }

            Assert.That(pairs.Count, Is.EqualTo(2));
            Assert.That(pairs.Sum(p => p.State.Value), Is.EqualTo(3));
        }
    }
}
