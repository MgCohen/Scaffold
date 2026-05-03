using NUnit.Framework;
using Scaffold.States;
using Scaffold.States.Samples;
using Unity.PerformanceTesting;

namespace Scaffold.Benchmarks.States
{
    /// <summary>
    /// Phase 0 baseline for <see cref="Store.Execute{TPayload}(TPayload)"/> with a single registered
    /// <c>Mutator&lt;TState, TPayload&gt;</c> against a single canonical slice.
    /// Audit reference: Docs/Audits/Packages/com.scaffold.states.md §Benchmark plan, first row.
    /// </summary>
    public sealed class StoreExecuteBenchmarks
    {
        [SetUp]
        public void SetUp() => BenchSetup.RearmPerTest();

        static Store BuildStoreWithCounter()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            builder.RegisterMutator(new ApplyCombinedTickToCounter());
            return builder.Build();
        }

        [Test, Performance]
        public void Execute_SingleMutator_OneSlice()
        {
            Store store = BuildStoreWithCounter();
            CombinedTickPayload payload = new(1);
            Bench.Measure(() => store.Execute(payload), iterationsPer: 10_000);
        }

        [Test, Performance]
        public void Execute_TypedMutator_OneSlice_NoRegistry()
        {
            // Direct ExecuteMutator path skips MutatorRegistry lookup entirely; useful as a floor
            // for the Phase 5 source-gen comparison.
            Store store = BuildStoreWithCounter();
            ApplyCombinedTickToCounter mutator = new();
            CombinedTickPayload payload = new(1);
            Bench.Measure(() => store.ExecuteMutator(mutator, payload), iterationsPer: 10_000);
        }
    }
}
