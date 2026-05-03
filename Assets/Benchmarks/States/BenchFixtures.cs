#nullable enable
using Scaffold.States;

namespace Scaffold.Benchmarks.States
{
    /// <summary>
    /// Self-contained benchmark fixtures so this assembly does not depend on
    /// <c>Scaffold.States.Samples</c>. The samples assembly will be renamed to
    /// <c>Samples~/</c> in Phase 6 (Unity convention — tilde-suffixed folders are imported via
    /// Package Manager only and do not compile into the project), at which point any benchmark
    /// that referenced sample types would stop compiling. Inline fixtures sidestep that entirely.
    /// </summary>
    public sealed record CounterState(int Value) : State;

    public sealed record SampleKey(string Name) : Reference;

    /// <summary>Reference-type payload (record class). Mirrors the sample <c>CombinedTickPayload</c>.</summary>
    public sealed record CombinedTickPayload(int Delta);

    /// <summary>Value-type payload — used by the Phase 5 baseline (audit §7.1) so the source-gen
    /// dispatcher's "≥3× ns/op for value-type payloads" target has a like-for-like floor in Phase 0.</summary>
    public readonly record struct ValueCombinedTickPayload(int Delta);

    public sealed class ApplyCombinedTickToCounter : Mutator<CounterState, CombinedTickPayload>
    {
        public override CounterState Change(CounterState state, CombinedTickPayload payload, IStateScope scope)
            => new(state.Value + payload.Delta);
    }

    public sealed class ApplyValueCombinedTickToCounter : Mutator<CounterState, ValueCombinedTickPayload>
    {
        public override CounterState Change(CounterState state, ValueCombinedTickPayload payload, IStateScope scope)
            => new(state.Value + payload.Delta);
    }
}
