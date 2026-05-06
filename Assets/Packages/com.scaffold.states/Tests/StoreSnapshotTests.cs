#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Tests.Fixtures;

namespace Scaffold.States.Tests
{
    public sealed class StoreSnapshotTests
    {
        [Test]
        public void LoadSnapshot_RemovesCanonicalSlicesNotInSnapshot()
        {
            var keyA = new SampleKey("A");
            var keyB = new SampleKey("B");
            var keyC = new SampleKey("C");
            var builder = new StoreBuilder();
            builder.AddState(keyA, new CounterState(1));
            builder.AddState(keyB, new CounterState(2));
            Store store = builder.Build();

            Snapshot snap1 = store.SaveSnapshot();
            store.RegisterSlice(keyC, new CounterState(99));

            Assert.That(store.Get<CounterState>(keyC).Value, Is.EqualTo(99));

            store.LoadSnapshot(snap1);

            Assert.Throws<KeyNotFoundException>(() => store.Get<CounterState>(keyC));
            Assert.That(store.Get<CounterState>(keyA).Value, Is.EqualTo(1));
            Assert.That(store.Get<CounterState>(keyB).Value, Is.EqualTo(2));
        }

        [Test]
        public void LoadSnapshot_NullSnapshot_ThrowsArgumentNullException()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();
            Assert.Throws<ArgumentNullException>(() => store.LoadSnapshot(null!));
        }

        [Test]
        public void LoadSnapshot_RestoresPreviouslyUnregisteredSlice()
        {
            var builder = new StoreBuilder();
            Store store = builder.Build();
            var someRef = new SampleKey("snap-restore");
            store.RegisterSlice(someRef, new CounterState(7));

            Snapshot snapshot = store.SaveSnapshot();

            Assert.That(store.UnregisterSlice<CounterState>(someRef), Is.True);
            Assert.Throws<KeyNotFoundException>(() => _ = store.Get<CounterState>(someRef));

            store.LoadSnapshot(snapshot);

            Assert.That(store.Get<CounterState>(someRef).Value, Is.EqualTo(7));
        }

        [Test]
        public void LoadSnapshot_Prune_RemainingCanonicalRowsMatchGetAll()
        {
            var keyA = new SampleKey("A");
            var keyB = new SampleKey("B");
            var keyC = new SampleKey("C");
            var builder = new StoreBuilder();
            builder.AddState(keyA, new CounterState(1));
            builder.AddState(keyB, new CounterState(2));
            Store store = builder.Build();
            Snapshot snap1 = store.SaveSnapshot();
            store.RegisterSlice(keyC, new CounterState(5));
            store.LoadSnapshot(snap1);

            Assert.That(store.GetAll<CounterState>().Sum(c => c.Value), Is.EqualTo(3));
        }

        [Test]
        public void Snapshot_Get_MissingEntry_ThrowsKeyNotFoundException()
        {
            var snap = new Snapshot();
            Assert.Throws<KeyNotFoundException>(() => snap.Get<CounterState>());
        }
    }
}
