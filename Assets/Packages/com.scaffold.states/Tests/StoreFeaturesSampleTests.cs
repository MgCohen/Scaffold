#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Tests.Fixtures;

namespace Scaffold.States.Tests
{
    public sealed class StoreFeaturesSampleTests
    {
        [Test]
        public void StoreBuilder_SameAggregateStateType_DifferentReferences_Builds()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            builder.AddState(new NotesState(""));
            var keyA = new SampleKey("A");
            var keyB = new SampleKey("B");
            builder.RegisterAggregate(keyA, new TotalsAggregateProvider());
            builder.RegisterAggregate(keyB, new TotalsAggregateProvider());

            Store store = builder.Build();

            Assert.That(store.Get<TotalsDashboardState>(keyA).CounterValue, Is.EqualTo(0));
            Assert.That(store.Get<TotalsDashboardState>(keyB).CounterValue, Is.EqualTo(0));
        }

        [Test]
        public void StoreBuilder_SameAggregateStateType_SameReference_SecondRegister_Throws()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            builder.AddState(new NotesState(""));
            var keyA = new SampleKey("A");
            builder.RegisterAggregate(keyA, new TotalsAggregateProvider());

            Assert.Throws<InvalidOperationException>(() => builder.RegisterAggregate(keyA, new TotalsAggregateProvider()));
        }

        [Test]
        public void DirectMutator_Execute_UpdatesCanonicalSlice()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(1));
            Store store = builder.Build();

            store.ExecuteMutator<CounterState>(new IncrementCounterMutator(4));

            Assert.That(store.Get<CounterState>().Value, Is.EqualTo(5));
        }

        [Test]
        public void Payload_TwoMutatorsSamePayload_BothApplyInOneExecute()
        {
            StoreFeaturesDemo demo = SampleStoreFactory.CreateFullDemo();
            Store store = demo.Store;

            store.Execute(new CombinedTickPayload(3));

            Assert.That(store.Get<CounterState>().Value, Is.EqualTo(3));
            Assert.That(store.Get<NotesState>().Text, Is.EqualTo("aaa"));
            Assert.That(store.Get<TotalsDashboardState>().CounterValue, Is.EqualTo(3));
            Assert.That(store.Get<TotalsDashboardState>().NoteCharacterCount, Is.EqualTo(3));
        }

        [Test]
        public void Aggregate_Subscribe_ReceivesRebuiltStateAfterCanonicalCommit()
        {
            StoreFeaturesDemo demo = SampleStoreFactory.CreateFullDemo();
            Store store = demo.Store;
            var seen = new List<TotalsDashboardState>();
            store.Subscribe<TotalsDashboardState>((_, s, _) => seen.Add(s));

            store.Execute(new CombinedTickPayload(2));

            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(1));
            TotalsDashboardState last = seen[^1];
            Assert.That(last.CounterValue, Is.EqualTo(2));
            Assert.That(last.NoteCharacterCount, Is.EqualTo(2));
        }

        [Test]
        public void Aggregate_OptionalWireSubscriptions_ReceiveCommittedCanonicalNotifications()
        {
            StoreFeaturesDemo demo = SampleStoreFactory.CreateFullDemo();
            Store store = demo.Store;

            store.Execute(new CombinedTickPayload(1));

            Assert.That(demo.TotalsProvider.CanonicalSubscriptionNotifications, Is.EqualTo(2));
        }

        [Test]
        public void Payload_IPayloadReference_RoutesToKeyedSlice()
        {
            Store store = SampleStoreFactory.CreateKeyedCounterDemo();
            var keyA = new SampleKey("A");
            var keyB = new SampleKey("B");

            store.Execute(new RoutedCounterPayload(keyA, 7));
            store.Execute(new RoutedCounterPayload(keyB, 4));

            Assert.That(store.Get<CounterState>(keyA).Value, Is.EqualTo(7));
            Assert.That(store.Get<CounterState>(keyB).Value, Is.EqualTo(4));
        }

        [Test]
        public void Execute_WithReference_OverridesPayloadRouting()
        {
            Store store = SampleStoreFactory.CreateKeyedCounterDemo();
            var keyA = new SampleKey("A");
            var keyB = new SampleKey("B");

            // Payload says A, but explicit execute reference targets slice B.
            store.Execute(keyB, new RoutedCounterPayload(keyA, 5));

            Assert.That(store.Get<CounterState>(keyA).Value, Is.EqualTo(0));
            Assert.That(store.Get<CounterState>(keyB).Value, Is.EqualTo(5));
        }

        [Test]
        public void SubscribeAllReferences_KeyedCounters_NotifiesPerKey()
        {
            Store store = SampleStoreFactory.CreateKeyedCounterDemo();
            var keyA = new SampleKey("A");
            var keyB = new SampleKey("B");
            var keys = new List<Reference>();

            store.SubscribeAllReferences<CounterState>((r, _, _) => keys.Add(r));

            store.Execute(new RoutedCounterPayload(keyA, 1));
            store.Execute(new RoutedCounterPayload(keyB, 1));

            Assert.That(keys, Is.EqualTo(new Reference[] { keyA, keyB }));
        }

        [Test]
        public void ExecuteBatch_AppliesMultiplePayloadsBeforeAggregateNotificationsComplete()
        {
            StoreFeaturesDemo demo = SampleStoreFactory.CreateFullDemo();
            Store store = demo.Store;

            store.ExecuteBatch(new object[]
            {
                new CombinedTickPayload(1),
                new CombinedTickPayload(2),
            });

            Assert.That(store.Get<CounterState>().Value, Is.EqualTo(3));
            Assert.That(store.Get<NotesState>().Text.Length, Is.EqualTo(3));
        }

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

        [Test]
        public void RegisterMutator_OnStore_AfterBuild_ExecutesPayload()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();
            store.RegisterMutator(new ApplyCombinedTickToCounter());
            store.Execute(new CombinedTickPayload(2));
            Assert.That(store.Get<CounterState>().Value, Is.EqualTo(2));
        }

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
        public void EnumerateAll_KeyedCanonical_YieldsReferenceAndStatePairs()
        {
            var keyA = new SampleKey("A");
            var keyB = new SampleKey("B");
            var builder = new StoreBuilder();
            builder.AddState(keyA, new CounterState(1));
            builder.AddState(keyB, new CounterState(2));
            Store store = builder.Build();

            var pairs = store.EnumerateAll<CounterState>().ToList();
            Assert.That(pairs.Count, Is.EqualTo(2));
            Assert.That(pairs.Sum(p => p.State.Value), Is.EqualTo(3));
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
        public void Execute_StillMergesPartialOverlay_WithoutPruningOtherKeyedSlices()
        {
            Store store = SampleStoreFactory.CreateKeyedCounterDemo();
            var keyA = new SampleKey("A");
            var keyB = new SampleKey("B");

            store.Execute(new RoutedCounterPayload(keyA, 7));

            Assert.That(store.Get<CounterState>(keyA).Value, Is.EqualTo(7));
            Assert.That(store.Get<CounterState>(keyB).Value, Is.EqualTo(0));
        }

        [Test]
        public void Subscribe_WithStateChangeEvent_ReceivesCreatedAndRemoved()
        {
            var builder = new StoreBuilder();
            Store store = builder.Build();
            var key = new SampleKey("K");
            var events = new List<StateChangeEvent>();

            store.SubscribeAllReferences<CounterState>((_, _, e) => events.Add(e));
            store.RegisterSlice(key, new CounterState(1));
            store.UnregisterSlice<CounterState>(key);

            Assert.That(events, Is.EqualTo(new[] { StateChangeEvent.Created, StateChangeEvent.Removed }));
        }

        [Test]
        public void Subscribe_Unsubscribe_KeyedSlice_StopsCallbacks()
        {
            Store store = SampleStoreFactory.CreateKeyedCounterDemo();
            var keyA = new SampleKey("A");
            int count = 0;
            Action<Reference, CounterState, StateChangeEvent> handler = (_, _, _) => count++;
            store.Subscribe<CounterState>(keyA, handler);
            store.Execute(new RoutedCounterPayload(keyA, 1));
            Assert.That(count, Is.EqualTo(1));
            store.Unsubscribe<CounterState>(keyA, handler);
            store.Execute(new RoutedCounterPayload(keyA, 2));
            Assert.That(count, Is.EqualTo(1));
        }

        private sealed record OrphanPayload;

        [Test]
        public void Execute_UnregisteredPayload_ThrowsMutatorNotRegisteredException()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();

            var ex = Assert.Throws<MutatorNotRegisteredException>(() => store.Execute(new OrphanPayload()));
            Assert.That(ex!.PayloadType, Is.EqualTo(typeof(OrphanPayload)));
        }

        [Test]
        public void StoreBuilder_DuplicateCanonicalAtSameReference_Throws()
        {
            var builder = new StoreBuilder();
            var key = new SampleKey("K");
            builder.AddState(key, new CounterState(0));

            Assert.Throws<InvalidOperationException>(() => builder.AddState(key, new CounterState(1)));
        }

        [Test]
        public void Snapshot_Get_MissingEntry_ThrowsKeyNotFoundException()
        {
            var snap = new Snapshot();
            Assert.Throws<KeyNotFoundException>(() => snap.Get<CounterState>());
        }

        [Test]
        public void UnregisterAggregate_DisposesWire_StopsAggregateCanonicalSubscriptions()
        {
            StoreFeaturesDemo demo = SampleStoreFactory.CreateFullDemo();
            Store store = demo.Store;

            store.Execute(new CombinedTickPayload(1));
            Assert.That(demo.TotalsProvider.CanonicalSubscriptionNotifications, Is.EqualTo(2));

            Assert.That(store.UnregisterAggregate<TotalsDashboardState>(null), Is.True);

            store.Execute(new CombinedTickPayload(1));
            Assert.That(demo.TotalsProvider.CanonicalSubscriptionNotifications, Is.EqualTo(2));
            Assert.That(store.Get<CounterState>().Value, Is.EqualTo(2));
        }
    }
}
