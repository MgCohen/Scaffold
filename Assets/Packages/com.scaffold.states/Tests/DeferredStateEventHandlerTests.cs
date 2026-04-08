#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.States.Samples;

namespace Scaffold.States.Tests
{
    public sealed class DeferredStateEventHandlerTests
    {
        private sealed class CountingStateEventHandler : IStateEventHandler
        {
            private readonly IStateEventHandler inner;

            public CountingStateEventHandler(IStateEventHandler inner)
            {
                this.inner = inner;
            }

            public int NotifyCount { get; private set; }

            public void Notify(IReference reference, BaseState state, StateChangeEvent changeEvent)
            {
                NotifyCount++;
                inner.Notify(reference, state, changeEvent);
            }

            public void Subscribe<TState>(IReference reference, Action<IReference, TState, StateChangeEvent> action) where TState : BaseState
            {
                inner.Subscribe(reference, action);
            }

            public void SubscribeAllReferences<TState>(Action<IReference, TState, StateChangeEvent> action) where TState : BaseState
            {
                inner.SubscribeAllReferences(action);
            }

            public void SubscribeAny(Action<IReference, BaseState, StateChangeEvent> action)
            {
                inner.SubscribeAny(action);
            }
        }

        [Test]
        public void Notify_WhenNotDeferring_ForwardsImmediately()
        {
            var core = new StateEventHandler();
            var counting = new CountingStateEventHandler(core);
            var deferred = new DeferredStateEventHandler(counting, StateEventMergeMode.PreserveAll);

            deferred.Notify(Reference.Null, new CounterState(1));

            Assert.That(counting.NotifyCount, Is.EqualTo(1));
        }

        [Test]
        public void Notify_WhenDeferring_DoesNotReachInnerUntilFlush_PreserveAll()
        {
            var core = new StateEventHandler();
            var counting = new CountingStateEventHandler(core);
            var deferred = new DeferredStateEventHandler(counting, StateEventMergeMode.PreserveAll);

            using (deferred.BeginDeferScope())
            {
                deferred.Notify(Reference.Null, new CounterState(1));
                deferred.Notify(Reference.Null, new CounterState(2));
                Assert.That(counting.NotifyCount, Is.EqualTo(0));
            }

            Assert.That(counting.NotifyCount, Is.EqualTo(0));

            deferred.Flush();

            Assert.That(counting.NotifyCount, Is.EqualTo(2));
        }

        [Test]
        public void Flush_PreserveAll_PreservesOrder()
        {
            var core = new StateEventHandler();
            var counting = new CountingStateEventHandler(core);
            var deferred = new DeferredStateEventHandler(counting, StateEventMergeMode.PreserveAll);
            var values = new List<int>();

            core.Subscribe<CounterState>(Reference.Null, (_, s, _) => values.Add(s.Value));

            using (deferred.BeginDeferScope())
            {
                deferred.Notify(Reference.Null, new CounterState(1));
                deferred.Notify(Reference.Null, new CounterState(2));
                deferred.Notify(Reference.Null, new CounterState(3));
            }

            deferred.Flush();

            Assert.That(values, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void Flush_LatestPerKey_OneInnerNotifyPerKey_KeepsLastState()
        {
            var core = new StateEventHandler();
            var counting = new CountingStateEventHandler(core);
            var deferred = new DeferredStateEventHandler(counting, StateEventMergeMode.LatestPerKey);
            CounterState? last = null;

            core.Subscribe<CounterState>(Reference.Null, (_, s, _) => last = s);

            using (deferred.BeginDeferScope())
            {
                deferred.Notify(Reference.Null, new CounterState(1));
                deferred.Notify(Reference.Null, new CounterState(2));
                deferred.Notify(Reference.Null, new CounterState(3));
            }

            deferred.Flush();

            Assert.That(counting.NotifyCount, Is.EqualTo(1));
            Assert.That(last, Is.Not.Null);
            Assert.That(last!.Value, Is.EqualTo(3));
        }

        [Test]
        public void NestedDeferScope_RequiresMatchingDispose()
        {
            var core = new StateEventHandler();
            var counting = new CountingStateEventHandler(core);
            var deferred = new DeferredStateEventHandler(counting, StateEventMergeMode.PreserveAll);

            using (deferred.BeginDeferScope())
            {
                using (deferred.BeginDeferScope())
                {
                    deferred.Notify(Reference.Null, new CounterState(1));
                    Assert.That(counting.NotifyCount, Is.EqualTo(0));
                }

                Assert.That(counting.NotifyCount, Is.EqualTo(0));
                deferred.Notify(Reference.Null, new CounterState(2));
            }

            deferred.Flush();

            Assert.That(counting.NotifyCount, Is.EqualTo(2));
        }

        [Test]
        public void Flush_ReentrantNotifyWhileDeferringBuffersForNextFlushIteration()
        {
            var core = new StateEventHandler();
            var counting = new CountingStateEventHandler(core);
            var deferred = new DeferredStateEventHandler(counting, StateEventMergeMode.PreserveAll);
            var reEntered = false;

            core.Subscribe<CounterState>(Reference.Null, (_, _, _) =>
            {
                if (!reEntered)
                {
                    reEntered = true;
                    deferred.Notify(Reference.Null, new CounterState(99));
                }
            });

            using (deferred.BeginDeferScope())
            {
                deferred.Notify(Reference.Null, new CounterState(1));
                Assert.That(counting.NotifyCount, Is.EqualTo(0));
                deferred.Flush();
                Assert.That(counting.NotifyCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void Store_LatestPerKey_TwoCommitsWhileDeferred_CoalescesInnerNotifies()
        {
            var core = new StateEventHandler();
            var counting = new CountingStateEventHandler(core);
            var deferred = new DeferredStateEventHandler(counting, StateEventMergeMode.LatestPerKey);
            var builder = new StoreBuilder();
            builder.AddEventHandler(deferred);
            builder.AddState(new CounterState(0));
            Store store = builder.Build();

            using (deferred.BeginDeferScope())
            {
                store.ExecuteMutator<CounterState>(new IncrementCounterMutator(1));
                store.ExecuteMutator<CounterState>(new IncrementCounterMutator(1));
                Assert.That(counting.NotifyCount, Is.EqualTo(0));
            }

            deferred.Flush();

            Assert.That(counting.NotifyCount, Is.EqualTo(1));
            Assert.That(store.Get<CounterState>().Value, Is.EqualTo(2));
        }

        [Test]
        public void Store_PreserveAll_TwoCommitsWhileDeferred_TwoInnerNotifies()
        {
            var core = new StateEventHandler();
            var counting = new CountingStateEventHandler(core);
            var deferred = new DeferredStateEventHandler(counting, StateEventMergeMode.PreserveAll);
            var builder = new StoreBuilder();
            builder.AddEventHandler(deferred);
            builder.AddState(new CounterState(0));
            Store store = builder.Build();

            using (deferred.BeginDeferScope())
            {
                store.ExecuteMutator<CounterState>(new IncrementCounterMutator(1));
                store.ExecuteMutator<CounterState>(new IncrementCounterMutator(1));
            }

            deferred.Flush();

            Assert.That(counting.NotifyCount, Is.EqualTo(2));
            Assert.That(store.Get<CounterState>().Value, Is.EqualTo(2));
        }

        [Test]
        public void BeginDeferScope_DoubleDispose_DoesNotThrowAndDepthStaysConsistent()
        {
            var core = new StateEventHandler();
            var counting = new CountingStateEventHandler(core);
            var deferred = new DeferredStateEventHandler(counting, StateEventMergeMode.PreserveAll);

            var scope = deferred.BeginDeferScope();
            scope.Dispose();
            scope.Dispose();

            deferred.Notify(Reference.Null, new CounterState(1));
            Assert.That(counting.NotifyCount, Is.EqualTo(1));
        }
    }
}
