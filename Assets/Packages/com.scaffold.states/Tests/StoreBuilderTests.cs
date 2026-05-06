#nullable enable

using System;
using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Tests.Fixtures;

namespace Scaffold.States.Tests
{
    public sealed class StoreBuilderTests
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

            Assert.Multiple(() =>
            {
                Assert.That(store.Get<TotalsDashboardState>(keyA).CounterValue, Is.EqualTo(0));
                Assert.That(store.Get<TotalsDashboardState>(keyB).CounterValue, Is.EqualTo(0));
            });
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
        public void StoreBuilder_DuplicateCanonicalAtSameReference_Throws()
        {
            var builder = new StoreBuilder();
            var key = new SampleKey("K");
            builder.AddState(key, new CounterState(0));

            Assert.Throws<InvalidOperationException>(() => builder.AddState(key, new CounterState(1)));
        }
    }
}
