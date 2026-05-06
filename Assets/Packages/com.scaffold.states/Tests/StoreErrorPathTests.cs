#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Tests.Fixtures;

namespace Scaffold.States.Tests
{
    public sealed class StoreErrorPathTests
    {
        [Test]
        public void Get_UnregisteredStateType_ThrowsKeyNotFoundException()
        {
            var builder = new StoreBuilder();
            Store store = builder.Build();

            Assert.Throws<KeyNotFoundException>(() => store.Get<CounterState>());
        }

        [Test]
        public void Get_WrongReference_ThrowsKeyNotFoundException()
        {
            var builder = new StoreBuilder();
            builder.AddState(new SampleKey("A"), new CounterState(1));
            Store store = builder.Build();

            Assert.Throws<KeyNotFoundException>(() => store.Get<CounterState>(new SampleKey("B")));
        }

        [Test]
        public void Execute_NullPayload_ThrowsArgumentNullException()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();

            Assert.Throws<ArgumentNullException>(() => store.Execute<object>(null!));
        }

        [Test]
        public void ExecuteBatch_NullList_ThrowsArgumentNullException()
        {
            var builder = new StoreBuilder();
            Store store = builder.Build();

            Assert.Throws<ArgumentNullException>(() => store.ExecuteBatch(null!));
        }

        [Test]
        public void ExecuteBatch_EmptyList_DoesNotThrow()
        {
            var builder = new StoreBuilder();
            Store store = builder.Build();

            Assert.DoesNotThrow(() => store.ExecuteBatch(Array.Empty<object>()));
        }

        [Test]
        public void RegisterSlice_NullState_ThrowsArgumentNullException()
        {
            var builder = new StoreBuilder();
            Store store = builder.Build();

            Assert.Throws<ArgumentNullException>(() => store.RegisterSlice(new SampleKey("X"), null!));
        }

        [Test]
        public void Subscribe_NullAction_ThrowsArgumentNullException()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();

            Assert.Throws<ArgumentNullException>(() =>
                store.Subscribe<CounterState>(Reference.Null, (Action<Reference, CounterState, StateChangeEvent>)null!));
        }

        [Test]
        public void SubscribeAny_NullAction_ThrowsArgumentNullException()
        {
            var builder = new StoreBuilder();
            Store store = builder.Build();

            Assert.Throws<ArgumentNullException>(() => store.SubscribeAny(null!));
        }

        [Test]
        public void RegisterMutator_NullMutator_ThrowsArgumentNullException()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();

            Assert.Throws<ArgumentNullException>(() =>
                store.RegisterMutator<CounterState, CombinedTickPayload>(null!));
        }

        [Test]
        public void UnregisterSlice_NullStateType_ThrowsArgumentNullException()
        {
            var builder = new StoreBuilder();
            Store store = builder.Build();

            Assert.Throws<ArgumentNullException>(() => store.UnregisterSlice(new SampleKey("X"), null!));
        }

        [Test]
        public void Unsubscribe_NullAction_ThrowsArgumentNullException()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();

            Assert.Throws<ArgumentNullException>(() =>
                store.Unsubscribe<CounterState>(Reference.Null, (Action<Reference, CounterState, StateChangeEvent>)null!));
        }

        [Test]
        public void UnsubscribeAllReferences_NullAction_ThrowsArgumentNullException()
        {
            var builder = new StoreBuilder();
            Store store = builder.Build();

            Assert.Throws<ArgumentNullException>(() =>
                store.UnsubscribeAllReferences<CounterState>(null!));
        }

        [Test]
        public void SubscribeAllReferences_NullAction_ThrowsArgumentNullException()
        {
            var builder = new StoreBuilder();
            Store store = builder.Build();

            Assert.Throws<ArgumentNullException>(() =>
                store.SubscribeAllReferences<CounterState>(null!));
        }
    }
}
