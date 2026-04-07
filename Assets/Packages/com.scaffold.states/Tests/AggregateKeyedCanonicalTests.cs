#nullable enable

using System.Linq;
using NUnit.Framework;
using Scaffold.States.Samples;

namespace Scaffold.States.Tests
{
    /// <summary>
    /// Aggregate over all <see cref="CounterState"/> rows via <see cref="IStateScope.GetAll{TState}"/>,
    /// wired with <see cref="IStateEventHandler.SubscribeAllReferences{TState}"/> so keyed runtime rows participate.
    /// </summary>
    public sealed class KeyedCountersSumAggregateProvider : AggregateProvider<KeyedCountersSumState>
    {
        public override void Wire(IStoreScope scope, IAggregateRebuild rebuild)
        {
            scope.Events.SubscribeAllReferences<CounterState>((_, _, _) => rebuild.RequestRebuild());
        }

        protected override KeyedCountersSumState BuildCore(IStateScope scope)
        {
            int sum = scope.GetAll<CounterState>().Sum(c => c.Value);
            return new KeyedCountersSumState(sum);
        }
    }

    public sealed record KeyedCountersSumState(int Sum) : AggregateState;

    public sealed class AggregateKeyedCanonicalTests
    {
        [Test]
        public void Aggregate_Rebuilds_When_KeyedCanonical_Created()
        {
            var keyA = new SampleKey("A");
            var keyB = new SampleKey("B");
            var keyC = new SampleKey("C");
            var builder = new StoreBuilder();
            builder.AddState(keyA, new CounterState(3));
            builder.AddState(keyB, new CounterState(4));
            builder.RegisterAggregate(new KeyedCountersSumAggregateProvider());
            builder.RegisterMutator(new AddDeltaToKeyedCounter());
            Store store = builder.Build();

            Assert.That(store.Get<KeyedCountersSumState>().Sum, Is.EqualTo(7));

            store.RegisterSlice(keyC, new CounterState(10));

            Assert.That(store.Get<KeyedCountersSumState>().Sum, Is.EqualTo(17));
        }

        [Test]
        public void Aggregate_Rebuilds_When_KeyedCanonical_Removed()
        {
            var keyA = new SampleKey("A");
            var keyB = new SampleKey("B");
            var keyC = new SampleKey("C");
            var builder = new StoreBuilder();
            builder.AddState(keyA, new CounterState(3));
            builder.AddState(keyB, new CounterState(4));
            builder.RegisterAggregate(new KeyedCountersSumAggregateProvider());
            Store store = builder.Build();

            Assert.That(store.Get<KeyedCountersSumState>().Sum, Is.EqualTo(7));

            store.RegisterSlice(keyC, new CounterState(10));
            Assert.That(store.Get<KeyedCountersSumState>().Sum, Is.EqualTo(17));

            Assert.That(store.UnregisterSlice<CounterState>(keyC), Is.True);
            Assert.That(store.Get<KeyedCountersSumState>().Sum, Is.EqualTo(7));
        }

        [Test]
        public void Aggregate_Rebuilds_After_LoadSnapshot_Prune_RemovesKeyedCanonical()
        {
            var keyA = new SampleKey("A");
            var keyB = new SampleKey("B");
            var keyC = new SampleKey("C");
            var builder = new StoreBuilder();
            builder.AddState(keyA, new CounterState(3));
            builder.AddState(keyB, new CounterState(4));
            builder.RegisterAggregate(new KeyedCountersSumAggregateProvider());
            Store store = builder.Build();
            Snapshot snap1 = store.SaveSnapshot();
            store.RegisterSlice(keyC, new CounterState(10));
            Assert.That(store.Get<KeyedCountersSumState>().Sum, Is.EqualTo(17));
            store.LoadSnapshot(snap1);
            Assert.That(store.Get<KeyedCountersSumState>().Sum, Is.EqualTo(7));
        }
    }
}
