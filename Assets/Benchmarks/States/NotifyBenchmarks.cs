using NUnit.Framework;
using Scaffold.States;
using Unity.PerformanceTesting;

namespace Scaffold.Benchmarks.States
{
    /// <summary>
    /// Phase 0 baseline for <c>StateEventHandler.NotifyReferenceSubscriptions</c>.
    /// Audit §4.11 — current implementation iterates the subscription list while holding a live
    /// reference to it; subscribers can mutate it mid-notify (covered separately by
    /// <see cref="StateEventHandlerInlineUnsubscribeTests"/>). This benchmark measures the
    /// non-mutating fanout cost as the post-Phase-2 floor.
    /// </summary>
    public sealed class NotifyBenchmarks
    {
        [SetUp]
        public void SetUp() => BenchSetup.RearmPerTest();

        [Test, Performance]
        public void Notify_50Subs_NoMutation()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();

            int sum = 0;
            for (int i = 0; i < 50; i++)
            {
                store.Subscribe<CounterState>((_, s, _) => sum += s.Value);
            }

            CounterState payload = new(1);
            Bench.Measure(() => store.Events.Notify(Reference.Null, payload, StateChangeEvent.Updated),
                iterationsPer: 1_000);

            VolatileSink.Use(sum);
        }
    }
}
