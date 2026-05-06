#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Tests.Fixtures;

namespace Scaffold.States.Tests
{
    public sealed class StoreSliceLifecycleTests
    {
        [Test]
        public void RegisterSlice_AfterBuild_AddsRowAndNotifies()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();
            var keys = new List<Reference>();
            store.SubscribeAllReferences<CounterState>((r, _, _) => keys.Add(r));
            var key = new SampleKey("X");
            store.RegisterSlice(key, new CounterState(99));

            Assert.That(store.Get<CounterState>(key).Value, Is.EqualTo(99));
            Assert.That(keys, Does.Contain(key));
        }

        [Test]
        public void RegisterSlice_DuplicateReferenceAndStateType_Throws()
        {
            var builder = new StoreBuilder();
            Store store = builder.Build();
            var key = new SampleKey("X");
            store.RegisterSlice(key, new CounterState(1));
            Assert.Throws<InvalidOperationException>(() => store.RegisterSlice(key, new CounterState(2)));
        }

        [Test]
        public void UnregisterSlice_RemovesRow()
        {
            var builder = new StoreBuilder();
            Store store = builder.Build();
            var key = new SampleKey("X");
            store.RegisterSlice(key, new CounterState(5));
            Assert.That(store.UnregisterSlice<CounterState>(key), Is.True);
            Assert.Throws<KeyNotFoundException>(() => store.Get<CounterState>(key));
        }

        [Test]
        public void UnregisterSlice_WhenMissing_ReturnsFalse()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();
            Assert.That(store.UnregisterSlice(new SampleKey("missing"), typeof(CounterState)), Is.False);
        }
    }
}
