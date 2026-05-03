using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.States;

namespace Scaffold.Benchmarks.States
{
    public sealed class StoreEnumerateAllReentrancyTests
    {
        [Test]
        public void EnumerateAll_Subscriber_CallsGetAll_DoesNotCorruptOuterIteration()
        {
            Store store = CreateKeyedCounterStore(20);
            WireReentrantGetAllSubscriber(store);
            AssertOuterEnumerateMatchesExpected(store);
        }

        private void WireReentrantGetAllSubscriber(Store store)
        {
            store.SubscribeAllReferences<CounterState>((_, _, _) =>
            {
                int innerCount = 0;
                foreach (CounterState _inner in store.GetAll<CounterState>())
                {
                    innerCount++;
                }

                VolatileSink.Use(innerCount);
            });
        }

        private void AssertOuterEnumerateMatchesExpected(Store store)
        {
            List<int> seen = new();
            foreach ((Reference _, CounterState s) in store.EnumerateAllPairs<CounterState>())
            {
                store.Events.Notify(Reference.Null, s, StateChangeEvent.Updated);
                seen.Add(s.Value);
            }

            Assert.That(seen, Has.Count.EqualTo(20));
            Assert.That(seen, Is.EquivalentTo(new[]
            {
                0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
                10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
            }));
        }

        private static Store CreateKeyedCounterStore(int count)
        {
            var builder = new StoreBuilder();
            for (int i = 0; i < count; i++)
            {
                builder.AddState(new SampleKey($"k{i}"), new CounterState(i));
            }

            return builder.Build();
        }
    }
}
