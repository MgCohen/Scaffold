#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Samples;

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

            store.Execute<CounterState>(new IncrementCounterMutator(4));

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
            store.Subscribe<TotalsDashboardState>((_, s) => seen.Add(s));

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
        public void Execute_WithReference_OverridesPayloadAndRegistrationKey()
        {
            Store store = SampleStoreFactory.CreateKeyedCounterDemo();
            var keyA = new SampleKey("A");
            var keyB = new SampleKey("B");

            // Payload says A, but explicit reference forces slice B.
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
            var keys = new List<IReference>();

            store.SubscribeAllReferences<CounterState>((r, _) => keys.Add(r));

            store.Execute(new RoutedCounterPayload(keyA, 1));
            store.Execute(new RoutedCounterPayload(keyB, 1));

            Assert.That(keys, Is.EqualTo(new IReference[] { keyA, keyB }));
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
    }
}
