#nullable enable

using System;
using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Tests.Fixtures;

namespace Scaffold.States.Tests
{
    public sealed class ExecuteBatchPoolPoisonTests
    {
        private sealed record AddCounterDeltaBatchPayload(int Delta);

        private sealed class AddCounterDeltaBatchMutator : Mutator<CounterState, AddCounterDeltaBatchPayload>
        {
            public override CounterState Change(CounterState state, AddCounterDeltaBatchPayload payload, IStateScope scope)
            {
                return new CounterState(state.Value + payload.Delta);
            }
        }

        private sealed record PoisonCounterBatchPayload;

        private sealed class PoisonCounterBatchMutator : Mutator<CounterState, PoisonCounterBatchPayload>
        {
            public override CounterState Change(CounterState state, PoisonCounterBatchPayload payload, IStateScope scope)
            {
                throw new InvalidOperationException("intentional poison payload");
            }
        }

        [Test]
        public void ExecuteBatch_AfterMidBatchThrow_LeavesPoolCleanForNextBatch()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();
            store.RegisterMutator(new AddCounterDeltaBatchMutator());
            store.RegisterMutator(new PoisonCounterBatchMutator());

            Assert.Throws<InvalidOperationException>(() =>
                store.ExecuteBatch(new object[]
                {
                    new AddCounterDeltaBatchPayload(5),
                    new PoisonCounterBatchPayload(),
                    new AddCounterDeltaBatchPayload(9),
                }));

            Assert.That(store.Get<CounterState>().Value, Is.EqualTo(0), "Failed batch must not commit.");

            store.ExecuteBatch(new object[]
            {
                new AddCounterDeltaBatchPayload(1),
                new AddCounterDeltaBatchPayload(2),
                new AddCounterDeltaBatchPayload(3),
            });

            Assert.That(store.Get<CounterState>().Value, Is.EqualTo(6));
        }
    }
}
