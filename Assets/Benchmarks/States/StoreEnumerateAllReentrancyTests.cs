using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.States;

namespace Scaffold.Benchmarks.States
{
    /// <summary>
    /// Audit §4.5 — Store reuses an instance-shared <c>sliceBuffer</c> across
    /// <see cref="Store.EnumerateAll{TState}"/>, <see cref="Store.GetAll{TState}"/>, and the
    /// scratchpad's <c>FillSlices</c>. A subscriber that calls back into the store from inside
    /// <c>Notify</c> while the outer caller is still iterating <c>EnumerateAll</c> will trash
    /// the buffer mid-iteration.
    /// This test reproduces that reentrancy and asserts the outer iterator delivers exactly
    /// the expected slice count regardless of subscriber re-entry. Pre-Phase-2 it fails (bad
    /// counts or duplicate yields); Phase 2's rented per-call buffers turn it green.
    /// </summary>
    [Ignore("Expected red until com.scaffold.states-refactor Phase 2 lands.")]
    public sealed class StoreEnumerateAllReentrancyTests
    {
        [Test]
        public void EnumerateAll_Subscriber_CallsGetAll_DoesNotCorruptOuterIteration()
        {
            var builder = new StoreBuilder();
            for (int i = 0; i < 20; i++)
            {
                builder.AddState(new SampleKey($"k{i}"), new CounterState(i));
            }

            Store store = builder.Build();

            // Subscriber re-enters the store inside Notify and walks GetAll<CounterState>,
            // which internally fills the same sliceBuffer the outer EnumerateAll is yielding from.
            store.SubscribeAllReferences<CounterState>((_, _, _) =>
            {
                int innerCount = 0;
                foreach (CounterState _inner in store.GetAll<CounterState>())
                {
                    innerCount++;
                }

                VolatileSink.Use(innerCount);
            });

            List<int> seen = new();
            foreach ((IReference _, CounterState s) in store.EnumerateAll<CounterState>())
            {
                // Trigger a Notify inside iteration to fire the subscriber's GetAll re-entry.
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
    }
}
