using NUnit.Framework;
using Scaffold.States;
using Unity.PerformanceTesting;

namespace Scaffold.Benchmarks.States
{
    /// <summary>
    /// Phase 0 baseline for <see cref="Store.Execute{TPayload}(TPayload)"/> with a single registered
    /// <c>Mutator&lt;TState, TPayload&gt;</c> against a single canonical slice. Two payload shapes
    /// are measured: a record class (the audit's stated baseline) and a <c>readonly record struct</c>
    /// (the floor Phase 5's source-gen dispatcher must beat by ≥3× ns/op for an apples-to-apples win).
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

        static Store BuildStoreWithCounter_ValuePayload()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            builder.RegisterMutator(new ApplyValueCombinedTickToCounter());
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
            // for the Phase 5 source-gen comparison on the reference-type payload path.
            Store store = BuildStoreWithCounter();
            ApplyCombinedTickToCounter mutator = new();
            CombinedTickPayload payload = new(1);
            Bench.Measure(() => store.ExecuteMutator(mutator, payload), iterationsPer: 10_000);
        }

        [Test, Performance]
        public void Execute_ValuePayload_OneSlice()
        {
            // Value-type payload: today's path boxes via Execute<TPayload>(TPayload) → object payload.
            // This benchmark anchors the Phase 5 source-gen "≥3× ns/op for value-type payloads"
            // acceptance to a like-for-like Phase 0 number.
            Store store = BuildStoreWithCounter_ValuePayload();
            ValueCombinedTickPayload payload = new(1);
            Bench.Measure(() => store.Execute(payload), iterationsPer: 10_000);
        }

        [Test, Performance]
        public void Execute_TypedMutator_ValuePayload_OneSlice_NoRegistry()
        {
            // Direct ExecuteMutator on the value-type payload — the floor Phase 5's dispatcher
            // is allowed to converge toward (no Pool overhead, no registry lookup, no boxing).
            Store store = BuildStoreWithCounter_ValuePayload();
            ApplyValueCombinedTickToCounter mutator = new();
            ValueCombinedTickPayload payload = new(1);
            Bench.Measure(() => store.ExecuteMutator(mutator, payload), iterationsPer: 10_000);
        }
    }
}
