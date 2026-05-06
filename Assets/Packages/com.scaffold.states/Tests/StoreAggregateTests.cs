#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Tests.Fixtures;

namespace Scaffold.States.Tests
{
    public sealed class StoreAggregateTests
    {
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
