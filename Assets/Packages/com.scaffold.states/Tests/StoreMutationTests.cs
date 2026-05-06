#nullable enable

using System;
using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Tests.Fixtures;

namespace Scaffold.States.Tests
{
    public sealed class StoreMutationTests
    {
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

            Assert.Multiple(() =>
            {
                Assert.That(store.Get<CounterState>().Value, Is.EqualTo(3));
                Assert.That(store.Get<NotesState>().Text, Is.EqualTo("aaa"));
                Assert.That(store.Get<TotalsDashboardState>().CounterValue, Is.EqualTo(3));
                Assert.That(store.Get<TotalsDashboardState>().NoteCharacterCount, Is.EqualTo(3));
            });
        }

        [Test]
        public void Payload_IPayloadReference_RoutesToKeyedSlice()
        {
            Store store = SampleStoreFactory.CreateKeyedCounterDemo();
            var keyA = new SampleKey("A");
            var keyB = new SampleKey("B");

            store.Execute(new RoutedCounterPayload(keyA, 7));

            Assert.That(store.Get<CounterState>(keyA).Value, Is.EqualTo(7));
            Assert.That(store.Get<CounterState>(keyB).Value, Is.EqualTo(0));

            store.Execute(new RoutedCounterPayload(keyB, 4));

            Assert.That(store.Get<CounterState>(keyB).Value, Is.EqualTo(4));
        }

        [Test]
        public void Execute_WithReference_OverridesPayloadRouting()
        {
            Store store = SampleStoreFactory.CreateKeyedCounterDemo();
            var keyA = new SampleKey("A");
            var keyB = new SampleKey("B");

            store.Execute(keyB, new RoutedCounterPayload(keyA, 5));

            Assert.Multiple(() =>
            {
                Assert.That(store.Get<CounterState>(keyA).Value, Is.EqualTo(0));
                Assert.That(store.Get<CounterState>(keyB).Value, Is.EqualTo(5));
            });
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

            Assert.Multiple(() =>
            {
                Assert.That(store.Get<CounterState>().Value, Is.EqualTo(3));
                Assert.That(store.Get<NotesState>().Text.Length, Is.EqualTo(3));
            });
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
    }
}
