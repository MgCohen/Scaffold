using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Samples;
using Unity.PerformanceTesting;

namespace Scaffold.Benchmarks.States
{
    /// <summary>
    /// Phase 0 baseline for <c>Store.Subscribe</c> overhead beyond the unavoidable delegate alloc.
    /// Audit §4.10 / §Benchmark plan — measures only the Ledger.Add path; the lambda capture is
    /// outside Bench.Measure so its delegate alloc is not counted.
    /// </summary>
    public sealed class SubscribeBenchmarks
    {
        [SetUp]
        public void SetUp() => BenchSetup.RearmPerTest();

        [Test, Performance]
        public void Subscribe_PerCall_CachedDelegate()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();

            System.Action<IReference, CounterState, StateChangeEvent> handler =
                static (_, _, _) => { };

            // Each iteration registers a fresh subscription instance.
            // Cached delegate keeps the alloc count to whatever Ledger.Add costs.
            Bench.Measure(() => store.Subscribe(Reference.Null, handler),
                iterationsPer: 10_000);
        }
    }
}
