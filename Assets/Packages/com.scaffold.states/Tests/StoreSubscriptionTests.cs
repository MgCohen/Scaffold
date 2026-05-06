#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Tests.Fixtures;

namespace Scaffold.States.Tests
{
    public sealed class StoreSubscriptionTests
    {
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
        public void SubscribeAny_ReceivesNotificationOnAnyStateTypeChange()
        {
            StoreFeaturesDemo demo = SampleStoreFactory.CreateFullDemo();
            Store store = demo.Store;
            var received = new List<BaseState>();

            store.SubscribeAny((_, state, _) => received.Add(state));

            store.Execute(new CombinedTickPayload(1));

            Assert.That(received, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(received, Has.Some.TypeOf<CounterState>());
            Assert.That(received, Has.Some.TypeOf<NotesState>());
        }

        [Test]
        public void Subscribe_TwoArityOverload_ReceivesStateAndEvent()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();
            CounterState? received = null;
            StateChangeEvent? receivedEvent = null;

            store.Subscribe<CounterState>(Reference.Null, (s, e) => { received = s; receivedEvent = e; });
            store.ExecuteMutator<CounterState>(new IncrementCounterMutator(5));

            Assert.That(received, Is.Not.Null);
            Assert.That(received!.Value, Is.EqualTo(5));
            Assert.That(receivedEvent, Is.EqualTo(StateChangeEvent.Updated));
        }

        [Test]
        public void Subscribe_OneArityOverload_ReceivesState()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();
            CounterState? received = null;

            store.Subscribe<CounterState>(Reference.Null, (Action<CounterState>)(s => received = s));
            store.ExecuteMutator<CounterState>(new IncrementCounterMutator(3));

            Assert.That(received, Is.Not.Null);
            Assert.That(received!.Value, Is.EqualTo(3));
        }

        [Test]
        public void Unsubscribe_TwoArityOverload_StopsCallbacks()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();
            int count = 0;
            Action<CounterState, StateChangeEvent> handler = (_, _) => count++;

            store.Subscribe(Reference.Null, handler);
            store.ExecuteMutator<CounterState>(new IncrementCounterMutator(1));
            Assert.That(count, Is.EqualTo(1));

            store.Unsubscribe(Reference.Null, handler);
            store.ExecuteMutator<CounterState>(new IncrementCounterMutator(1));
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void Unsubscribe_OneArityOverload_StopsCallbacks()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();
            int count = 0;
            Action<CounterState> handler = _ => count++;

            store.Subscribe(Reference.Null, handler);
            store.ExecuteMutator<CounterState>(new IncrementCounterMutator(1));
            Assert.That(count, Is.EqualTo(1));

            store.Unsubscribe(Reference.Null, handler);
            store.ExecuteMutator<CounterState>(new IncrementCounterMutator(1));
            Assert.That(count, Is.EqualTo(1));
        }
    }
}
