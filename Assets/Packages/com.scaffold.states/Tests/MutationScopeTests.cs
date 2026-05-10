#nullable enable

using System;
using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Tests.Fixtures;

namespace Scaffold.States.Tests
{
    public sealed class MutationScopeTests
    {
        [Test]
        public void Scope_ExecuteAndCommit_AppliesStateToStore()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            builder.RegisterMutator(new ApplyCombinedTickToCounter());
            Store store = builder.Build();

            using (var scope = store.BeginScope())
            {
                scope.Execute(new CombinedTickPayload(5));
                scope.Commit();
            }

            Assert.That(store.Get<CounterState>().Value, Is.EqualTo(5));
        }

        [Test]
        public void Scope_ReadsPendingState()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            builder.RegisterMutator(new ApplyCombinedTickToCounter());
            Store store = builder.Build();

            using var scope = store.BeginScope();
            scope.Execute(new CombinedTickPayload(3));

            Assert.That(scope.Get<CounterState>().Value, Is.EqualTo(3));
            Assert.That(store.Get<CounterState>().Value, Is.EqualTo(0));
        }

        [Test]
        public void Scope_SequentialMutations_EachSeesLatest()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(10));
            builder.RegisterMutator(new ApplyCombinedTickToCounter());
            Store store = builder.Build();

            using var scope = store.BeginScope();
            scope.Execute(new CombinedTickPayload(5));
            Assert.That(scope.Get<CounterState>().Value, Is.EqualTo(15));

            scope.Execute(new CombinedTickPayload(3));
            Assert.That(scope.Get<CounterState>().Value, Is.EqualTo(18));

            scope.Execute(new CombinedTickPayload(-8));
            Assert.That(scope.Get<CounterState>().Value, Is.EqualTo(10));

            scope.Commit();
            Assert.That(store.Get<CounterState>().Value, Is.EqualTo(10));
        }

        [Test]
        public void Scope_DisposeWithoutCommit_DiscardsChanges()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            builder.RegisterMutator(new ApplyCombinedTickToCounter());
            Store store = builder.Build();

            using (var scope = store.BeginScope())
            {
                scope.Execute(new CombinedTickPayload(99));
            }

            Assert.That(store.Get<CounterState>().Value, Is.EqualTo(0));
        }

        [Test]
        public void Scope_CommitTwice_Throws()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();

            using var scope = store.BeginScope();
            scope.Commit();

            Assert.Throws<InvalidOperationException>(() => scope.Commit());
        }

        [Test]
        public void Scope_ExecuteAfterCommit_Throws()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            builder.RegisterMutator(new ApplyCombinedTickToCounter());
            Store store = builder.Build();

            using var scope = store.BeginScope();
            scope.Commit();

            Assert.Throws<InvalidOperationException>(() => scope.Execute(new CombinedTickPayload(1)));
        }

        [Test]
        public void Scope_ReadAfterCommit_Throws()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();

            using var scope = store.BeginScope();
            scope.Commit();

            Assert.Throws<InvalidOperationException>(() => scope.Get<CounterState>());
        }

        [Test]
        public void Scope_UseAfterDispose_Throws()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            builder.RegisterMutator(new ApplyCombinedTickToCounter());
            Store store = builder.Build();

            var scope = store.BeginScope();
            scope.Dispose();

            Assert.Throws<ObjectDisposedException>(() => scope.Execute(new CombinedTickPayload(1)));
            Assert.Throws<ObjectDisposedException>(() => scope.Get<CounterState>());
            Assert.Throws<ObjectDisposedException>(() => scope.Commit());
        }

        [Test]
        public void Scope_DirectMutator_Works()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(5));
            Store store = builder.Build();

            using var scope = store.BeginScope();
            scope.ExecuteMutator(new IncrementCounterMutator(3));
            Assert.That(scope.Get<CounterState>().Value, Is.EqualTo(8));

            scope.ExecuteMutator(new IncrementCounterMutator(2));
            Assert.That(scope.Get<CounterState>().Value, Is.EqualTo(10));

            scope.Commit();
            Assert.That(store.Get<CounterState>().Value, Is.EqualTo(10));
        }

        [Test]
        public void Scope_MultipleStateTypes_AllCommitAtomically()
        {
            StoreFeaturesDemo demo = SampleStoreFactory.CreateFullDemo();
            Store store = demo.Store;

            using var scope = store.BeginScope();
            scope.Execute(new CombinedTickPayload(2));
            scope.Execute(new CombinedTickPayload(3));
            scope.Commit();

            Assert.That(store.Get<CounterState>().Value, Is.EqualTo(5));
            Assert.That(store.Get<NotesState>().Text.Length, Is.EqualTo(5));
        }

        [Test]
        public void Scope_KeyedReferences_RoutesCorrectly()
        {
            Store store = SampleStoreFactory.CreateKeyedCounterDemo();
            var keyA = new SampleKey("A");
            var keyB = new SampleKey("B");

            using var scope = store.BeginScope();
            scope.Execute(new RoutedCounterPayload(keyA, 7));
            scope.Execute(new RoutedCounterPayload(keyB, 3));

            Assert.That(scope.Get<CounterState>(keyA).Value, Is.EqualTo(7));
            Assert.That(scope.Get<CounterState>(keyB).Value, Is.EqualTo(3));
            Assert.That(store.Get<CounterState>(keyA).Value, Is.EqualTo(0));

            scope.Commit();
            Assert.That(store.Get<CounterState>(keyA).Value, Is.EqualTo(7));
            Assert.That(store.Get<CounterState>(keyB).Value, Is.EqualTo(3));
        }

        [Test]
        public void Scope_DoubleDispose_DoesNotThrow()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();

            var scope = store.BeginScope();
            scope.Dispose();
            Assert.DoesNotThrow(() => scope.Dispose());
        }

        [Test]
        public void Scope_StoreRemainsUnchangedUntilCommit()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            builder.RegisterMutator(new ApplyCombinedTickToCounter());
            Store store = builder.Build();

            using var scope = store.BeginScope();

            for (int i = 1; i <= 10; i++)
            {
                scope.Execute(new CombinedTickPayload(1));
                Assert.That(scope.Get<CounterState>().Value, Is.EqualTo(i));
                Assert.That(store.Get<CounterState>().Value, Is.EqualTo(0));
            }

            scope.Commit();
            Assert.That(store.Get<CounterState>().Value, Is.EqualTo(10));
        }
    }
}
