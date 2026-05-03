using NUnit.Framework;
using Scaffold.States;
using Unity.PerformanceTesting;

namespace Scaffold.Benchmarks.States
{
    /// <summary>
    /// Phase 0 baseline for <see cref="Store.EnumerateAll{TState}"/> over a single state-type bucket.
    /// Today walks <c>Map&lt;Reference, Type, Slice&gt;.GetAll</c> and yields through the shared
    /// <c>sliceBuffer</c> instance. After Phase 3 (audit §7.3 indexed slice store + struct enumerator)
    /// this should drop to zero allocations and ≥3× ns/op.
    /// </summary>
    public sealed class StoreEnumerateAllBenchmarks
    {
        [SetUp]
        public void SetUp() => BenchSetup.RearmPerTest();

        static Store BuildStoreWith(int counterSlices)
        {
            var builder = new StoreBuilder();
            for (int i = 0; i < counterSlices; i++)
            {
                builder.AddState(new SampleKey($"k{i}"), new CounterState(i));
            }

            return builder.Build();
        }

        [Test, Performance]
        public void EnumerateAll_OneTypeBucket_1k()
        {
            Store store = BuildStoreWith(1000);
            Bench.Measure(() =>
            {
                int sum = 0;
                foreach ((Reference _, CounterState s) in store.EnumerateAll<CounterState>())
                {
                    sum += s.Value;
                }

                VolatileSink.Use(sum);
            }, iterationsPer: 100);
        }

        [Test, Performance]
        public void GetAll_OneTypeBucket_1k()
        {
            Store store = BuildStoreWith(1000);
            Bench.Measure(() =>
            {
                int sum = 0;
                foreach (CounterState s in store.GetAll<CounterState>())
                {
                    sum += s.Value;
                }

                VolatileSink.Use(sum);
            }, iterationsPer: 100);
        }
    }

    internal static class VolatileSink
    {
        internal static volatile int Value;

        internal static void Use(int x) => Value = x;
    }
}
