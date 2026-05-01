#nullable enable

using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Samples;

namespace Scaffold.States.Tests
{
    public sealed class StoreRegisterAggregateTests
    {
        private sealed record TestSourceState(int Value) : State;

        private sealed record TestAggregateState(int Twice) : AggregateState;

        private sealed record BumpPayload(SampleKey Target) : IPayloadReference
        {
            public IReference GetReference() => Target;
        }

        private sealed class BumpMutator : Mutator<TestSourceState, BumpPayload>
        {
            public override TestSourceState Change(TestSourceState state, BumpPayload payload, IStateScope scope)
                => new TestSourceState(state.Value + 1);
        }

        private sealed class DoubleProvider : AggregateProvider<TestAggregateState>
        {
            public DoubleProvider(SampleKey key)
            {
                this.key = key;
            }

            private readonly SampleKey key;

            public override void Wire(IStoreScope scope, IAggregateRebuild rebuild)
            {
                scope.Events.Subscribe<TestSourceState>(key, (_, _, _) => rebuild.RequestRebuild());
            }

            protected override TestAggregateState BuildCore(IStateScope scope)
            {
                var s = scope.Get<TestSourceState>(key);
                return new TestAggregateState(s.Value * 2);
            }
        }

        [Test]
        public void RegisterAggregate_AfterBuild_ReadsDerivedState()
        {
            var key = new SampleKey("e1");
            var builder = new StoreBuilder();
            Store store = builder.Build();

            store.RegisterSlice(key, new TestSourceState(4));
            store.RegisterAggregate(key, new DoubleProvider(key));

            Assert.That(store.Get<TestAggregateState>(key).Twice, Is.EqualTo(8));
        }

        [Test]
        public void RegisterAggregate_AfterBuild_Rebuilds_WhenCanonicalChanges()
        {
            var key = new SampleKey("e2");
            var builder = new StoreBuilder();
            builder.RegisterMutator(new BumpMutator());
            Store store = builder.Build();

            store.RegisterSlice(key, new TestSourceState(5));
            store.RegisterAggregate(key, new DoubleProvider(key));
            Assert.That(store.Get<TestAggregateState>(key).Twice, Is.EqualTo(10));

            store.Execute(key, new BumpPayload(key));
            Assert.That(store.Get<TestAggregateState>(key).Twice, Is.EqualTo(12));
        }

        [Test]
        public void RegisterAggregate_AfterBuild_Rebuilds_AfterLoadSnapshot()
        {
            var key = new SampleKey("e3");
            var builder = new StoreBuilder();
            builder.RegisterMutator(new BumpMutator());
            Store store = builder.Build();

            store.RegisterSlice(key, new TestSourceState(10));
            store.RegisterAggregate(key, new DoubleProvider(key));
            Assert.That(store.Get<TestAggregateState>(key).Twice, Is.EqualTo(20));

            var snap = store.SaveSnapshot();
            store.Execute(key, new BumpPayload(key));
            Assert.That(store.Get<TestSourceState>(key).Value, Is.EqualTo(11));
            Assert.That(store.Get<TestAggregateState>(key).Twice, Is.EqualTo(22));

            store.LoadSnapshot(snap);
            Assert.That(store.Get<TestSourceState>(key).Value, Is.EqualTo(10));
            Assert.That(store.Get<TestAggregateState>(key).Twice, Is.EqualTo(20));
        }
    }
}
