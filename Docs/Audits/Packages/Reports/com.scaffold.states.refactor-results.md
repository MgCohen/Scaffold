# `com.scaffold.states` refactor — results summary

This report accompanies [`Plans/com.scaffold.states-refactor-ExecPlan.md`](../../../../Plans/com.scaffold.states-refactor-ExecPlan.md) Phase 7.

## Baselines

- Pre-refactor medians were copied to [`Assets/Benchmarks/States/baselines.phase0.json`](../../../../Assets/Benchmarks/States/baselines.phase0.json) from `baselines.json` at the end of this refactor pass so historical comparison stays possible.
- After you re-run the perf harness (`run-unity-tests.ps1` with `-PerformanceTestResultsPath`), replace [`Assets/Benchmarks/States/baselines.json`](../../../../Assets/Benchmarks/States/baselines.json) with fresh medians and refresh this section with ns/op, bytes/op, AllocCount, and Gen0 deltas vs `baselines.phase0.json`.

## Code outcomes (Phases 4–6)

- **Snapshot** is a sealed wrapper over the slice map (composition over inheritance).
- **Aggregate wiring** returns `IDisposable`; `UnregisterAggregate` disposes wire subscriptions.
- **Subscriptions**: narrowed `Subscribe` / `Unsubscribe` overloads plus `UnsubscribeAllReferences`; `Notify(..., Updated)` for simple updates lives in `StateEventHandlerNotifyExtensions`.
- **`IMutatorDispatcher`**: `Execute<TPayload>` tries `TryDispatch` before the registry; **`EntityBridgeContext`** registers both the generated dispatcher and the mutators so **`ExecuteBatch`** (registry path) remains correct for entity payloads.
- **Phase 6**: [`StatesInstaller`](../../../../Assets/Packages/com.scaffold.states/Runtime/Container/StatesInstaller.cs) in assembly **`Scaffold.States.Container`** (VContainer + VContainer.Unity; runtime `Scaffold.States` stays `noEngineReferences: true`).
- **Tests**: EditMode fixtures duplicated under `Tests/TestFixtures.cs` so the suite does not depend on the non-imported samples assembly.

## Follow-up

- Re-run EditMode + perf suites and paste the median table here.
- Optional: Roslyn **MutatorDispatcher** generator under `Generators/` to replace hand-maintained `GeneratedMutatorDispatcher.cs` in `com.scaffold.entities.states`.
