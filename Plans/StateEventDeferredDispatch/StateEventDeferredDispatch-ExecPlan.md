# Deferred state event dispatch (coalesce / batch before subscribers run)

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Repository policy for ExecPlans is defined in `PLANS.md` at the repository root. This document must be maintained in accordance with that file.

## Purpose / Big Picture

Today, every time the store commits state (`Store` calls `IStateEventHandler.Notify`), subscribers run immediately. A single logical operation (for example one `Execute` that touches many slices, or aggregate rebuilds that fan out) can produce **many** `Notify` calls in a row. Downstream UI and MVVM layers may then perform redundant work (multiple binding passes, layout thrash) because they observe each notification separately.

After this work, a developer can **wrap the default event handler** in a small **decorator** that **buffers** `Notify` calls and **releases** them on demand (one flush, or a scoped deferral block). The developer can choose whether buffered events are **delivered in full** (every notification still runs, in order) or **folded** so that for the same logical target only the **latest** state instance is delivered before subscribers run. The goal is to **align subscriber work with a single release point** (for example one player-loop frame) so MVVM or view code can refresh once per batch instead of once per intermediate commit.

Someone can see it working when: tests (or a small sample) show **N** canonical updates producing **N** inner notifications in preserve mode but **one** inner notification per `(reference, state type)` key in fold-latest mode after a flush, and **zero** inner notifications while deferral is active and before flush. Running `powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests` from the repository root completes with a clean gate, and new tests in `Scaffold.States.Tests` pass.

**Relationship to MVVM:** `Plans/BindingDeferredUpdate/BindingDeferredUpdate-ExecPlan.md` defers **binding refresh** inside the MVVM package. This plan addresses **state package** notifications **earlier** in the pipeline. Teams may use **either** or **both**: batching at the state layer reduces how often subscribers fire; batching at the binding layer still helps when many property changes occur without going through `Notify` again.

## Progress

- [x] Author initial ExecPlan at `Plans/StateEventDeferredDispatch/StateEventDeferredDispatch-ExecPlan.md` (this file).
- [x] Milestone 1 — Core types: deferral mode enum (preserve-all vs latest-per-key), options object, public decorator `DeferredStateEventHandler` (name final in Decision Log) implementing `IStateEventHandler`, delegating all `Subscribe*` calls to an inner handler, buffering `Notify` when deferral is active.
- [x] Milestone 2 — Control API: explicit flush, scoped deferral (nested-safe), documented ordering and re-entrancy when a flushed subscriber mutates the store again.
- [x] Milestone 3 — Tests in `Assets/Packages/com.scaffold.states/Tests/` proving counts, ordering, fold-latest vs preserve-all, and nested deferral.
- [x] Milestone 4 — Package README (`Assets/Packages/com.scaffold.states/README.md`) and optional `Docs/Tools/States.md` short subsection: how to compose the decorator with `StoreBuilder.AddEventHandler`, when to flush, and pointer to MVVM deferred binding for UI-only batching.
- [x] Milestone 5 — Quality gate: `validate-changes.ps1` (use `-SkipTests` only if the project policy for this branch still skips Unity tests); fix analyzers if any new public API triggers SCA. (Local run: compilation precheck PASS; analyzer step may fail with Application Control / DLL load per environment; not introduced by this feature.)

## Surprises & Discoveries

Document unexpected behaviors, bugs, optimizations, or insights discovered during implementation. Provide concise evidence.

- Observation: `validate-changes.ps1` may report MVVM analyzer test failures (`FileLoadException` / Application Control on analyzer DLL) unrelated to states code; Unity batchmode can abort if another editor holds the project.
  Evidence: Gate output on implementation host; `Scaffold.States` compiles when Unity precheck succeeds.

## Decision Log

- Decision: Use the existing **`IStateEventHandler`** / **`StateEventHandler`** types in `Assets/Packages/com.scaffold.states/` as the extension point. Colloquial “data event handler” in discussions maps to this API; there is no separate `DataEventHandler` type in this repository.
  Rationale: Single source of truth; `Store` already depends on `IStateEventHandler` and `StoreBuilder.AddEventHandler` accepts a custom instance.
  Author: ExecPlan draft, 2026-04-05.

- Decision: The decorator **fully implements** `IStateEventHandler` and **forwards** `Subscribe`, `SubscribeAllReferences`, and `SubscribeAny` to the **inner** handler so subscription lists remain owned by the inner `StateEventHandler` (or any other `IStateEventHandler` implementation). Buffered delivery is implemented by calling **`inner.Notify`** zero or more times when flushing.
  Rationale: `Store` and `AggregateSlice` only call `Notify` on the injected handler; subscribers must remain registered on the same inner object the decorator forwards to.
  Author: ExecPlan draft, 2026-04-05.

- Decision: **Fold-latest** identity key for coalescing is **`(IReference, Type)`** where `Type` is `state.GetType()` for the `BaseState` passed to `Notify`. If the same key is notified again before flush, replace the queued payload with the newer state instance; **preserve insertion order of distinct keys** when flushing so behavior stays deterministic.
  Rationale: Matches how subscriptions are keyed by reference and state type; avoids unbounded queues when one slice updates repeatedly in one defer window.
  Author: ExecPlan draft, 2026-04-05.

- Decision: **Preserve-all** mode keeps a **FIFO list** of every `(IReference, BaseState)` pair in order of **`Notify` calls** and replays them on flush with one `inner.Notify` per entry.
  Rationale: Subscribers that rely on seeing every transition (auditing, animation stepped state) keep today’s semantics within the batch.
  Author: ExecPlan draft, 2026-04-05.

- Decision: **Deferral activation** uses a **depth counter** (push/pop or `BeginDefer`/`EndDefer`) so nested scopes are safe. **`Flush()`** always **drains the entire pending queue** to `inner` immediately, **regardless of deferral depth**, and **does not** change the depth counter. After a flush, if depth is still greater than zero, subsequent `Notify` calls continue to **buffer** until the next flush or until depth returns to zero and pass-through applies (see re-entrancy note in Plan of Work).
  Rationale: “Release” is explicit and total; nested deferral scopes remain predictable (you can flush mid-scope without implicitly ending the outer scope).
  Author: ExecPlan draft, 2026-04-05.

- Decision: Expose flush and scope entry through a **small dedicated interface** (for example `IStateEventDeferralController` with `Flush()` and `IDisposable BeginDeferScope()`) implemented by the decorator class. Callers who inject the decorator via `StoreBuilder.AddEventHandler` **retain a reference** to that instance or **cast** `store.Events` to the controller interface when they know the concrete setup.
  Rationale: `IStateEventHandler` should stay narrow; flush is not a general subscription concern.
  Author: ExecPlan draft, 2026-04-05.

- Decision: Add **`StateEventHandlers.CreateDefault()`** as the public way to obtain the default fan-out handler so README and external composition can wrap the same implementation as `StoreBuilder` without making **`StateEventHandler`** public (avoids exposing mutable subscription internals).
  Rationale: Keeps `StateEventHandler` internal while allowing `DeferredStateEventHandler` examples outside the assembly.
  Author: Implementation, 2026-04-05.

## Outcomes & Retrospective

Summarize outcomes, gaps, and lessons learned at major milestones or at completion. Compare the result against the original purpose.

- **Achieved:** `StateEventMergeMode`, `IStateEventDeferralController`, `DeferredStateEventHandler`, `StateEventHandlers.CreateDefault()` (public factory for the default handler), `InternalsVisibleTo` for tests, `DeferredStateEventHandlerTests`, README and `Docs/Tools/States.md` updates.
- **Gaps:** Full green `validate-changes.ps1` not guaranteed on hosts where analyzer DLLs are blocked or Unity is locked; run EditMode tests in Unity when possible.

## Context and Orientation

**State notifications today**

- `Assets/Packages/com.scaffold.states/Runtime/Abstractions/IStateEventHandler.cs` defines `Notify(IReference reference, BaseState state)` and the subscribe methods.
- `Assets/Packages/com.scaffold.states/Runtime/Events/StateEventHandler.cs` implements immediate fan-out to per-reference, type-wide, and “any” subscriptions.
- `Assets/Packages/com.scaffold.states/Runtime/Store.cs` calls `eventHandler.Notify` after `Set` on a slice (line-region around the private `Set` method) and when registering a new slice; `Assets/Packages/com.scaffold.states/Runtime/State/AggregateSlice.cs` calls `attachedStore.Events.Notify` after rebuilding aggregate state.

**Composition**

- `Assets/Packages/com.scaffold.states/Runtime/Builders/Store/StoreBuilder.cs` exposes `AddEventHandler(IStateEventHandler eventHandler)` and defaults to `new StateEventHandler()` when none is supplied.

**Term: Decorator** — A class that implements the same interface as an inner object and forwards calls, adding behavior. Here, the decorator sits where `StoreBuilder` would otherwise pass a plain `StateEventHandler`.

**Term: Inner handler** — The `StateEventHandler` (or future alternative) that actually holds subscription lists. The decorator must forward subscription registration to this object.

**Term: Fold-latest** — On flush, for each distinct `(IReference, Type)` key in the buffer, call `inner.Notify` once with the **last** state instance observed for that key during the defer window.

**Term: Preserve-all** — On flush, call `inner.Notify` once per buffered call, in order, with the exact references and state instances that were passed to `Notify` while deferred.

## Plan of Work

### 1. Public API surface (Scaffold.States)

Add a focused API in the states runtime assembly (exact filenames follow existing `Runtime/Events/` layout):

- An enum or named policy value for **buffer merge mode**: **PreserveAll** vs **LatestPerKey** (names may be adjusted for analyzer and naming consistency).

- A small options type if needed (for example default merge mode, maximum buffer size for diagnostics in debug builds — optional).

- A **public** decorator class, conceptually `DeferredStateEventHandler`, constructed with `(IStateEventHandler inner, … options)`. It implements `IStateEventHandler` by delegating subscribe methods to `inner`. Its `Notify` implementation either forwards immediately to `inner.Notify` when deferral depth is zero and “pass-through” is desired, **or** enqueues according to merge mode. The constructor stores the merge mode default; optional per-call override is only needed if a future requirement demands it — the initial ExecPlan assumes **one mode per decorator instance** to keep behavior obvious.

- A separate interface for **control**: `Flush()`, and **scoped deferral** such as `IDisposable BeginDeferScope()` that increments depth on construction and decrements on dispose, flushing is **not** automatic on dispose unless you explicitly decide that pattern — the recommended default is **decrement depth only on dispose** and require an explicit **`Flush()`** to release buffered notifications, so “release” is always explicit. If stakeholders prefer **flush-on-dispose** for `using` blocks, record that as an additive option on the scope factory (for example `BeginDeferScope(bool flushOnDispose)`).

  Clarification for implementers: the stakeholder asked for an **external** “flush or release”. The ExecPlan therefore prefers **explicit `Flush()`** as the primary release mechanism. Scoped deferral only controls **whether new notifications buffer**; pairing `using` with `Flush()` at the end of the block is an acceptable documented pattern.

### 2. Store and StoreBuilder

- **No strict requirement** to change `Store` or `StoreBuilder` if the decorator is supplied via `AddEventHandler` and the application keeps a reference to the decorator for `Flush()`.

- **Optional ergonomic improvement** (only if it reduces casting): a `StoreBuilder` helper that constructs `inner` + decorator together, or a `Store` property that exposes the deferral controller when the registered handler implements it. This is optional and should not block Milestone 1; add it only if user testing shows casting is painful.

### 3. Semantics and edge cases

- **Re-entrancy:** While executing `inner.Notify` during `Flush`, subscribers may call back into the store and generate more `Notify` calls. Define whether those are **immediate** (depth temporarily treated as zero during inner dispatch) or **buffered** (still deferred). Recommended default: **new notifications during flush go to the inner handler immediately** only if deferral depth is zero at the moment of the nested `Notify`; if still inside an outer defer scope, they **buffer**. Document the rule in code comments and tests.

- **Threading:** Document **single-threaded** assumption (Unity main thread). No requirement for cross-thread safety in the first version unless a clear need appears.

### 4. Tests

Add automated tests alongside `StoreFeaturesSampleTests.cs` under `Assets/Packages/com.scaffold.states/Tests/`, using the same test assembly and patterns. Tests should use a **spy** inner handler or a **counting** wrapper to count how many times `Notify` reaches the inner handler, and list payloads in preserve mode.

### 5. Documentation

Update `Assets/Packages/com.scaffold.states/README.md` with a short subsection: problem, decorator usage, merge modes, flush/scoping, and link to MVVM deferred binding for UI-layer batching. If `Docs/Tools/States.md` is the broader doc entry point, add a matching short pointer there.

## Concrete Steps

From the repository root `C:\Unity\Scaffold` (or your clone), implementation order:

1. Add the new types under `Assets/Packages/com.scaffold.states/Runtime/Events/` (or `Runtime/Abstractions/` for interfaces), with `.meta` files generated by Unity on import or copied from sibling files per project convention.

2. Implement the decorator and controller interface; wire manual `Flush` and defer depth.

3. Add tests; run EditMode tests for the states assembly via `.agents/scripts/run-editmode-tests.ps1` when Unity is available, or rely on compilation + validate gate per project policy.

4. Run the quality gate from the repo root:

    powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests

   If analyzers are configured and the environment loads `Analyzers/Output` DLLs, run:

    powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\check-analyzers.ps1"

5. Update README (and optional Docs) as in Milestone 4.

Expected success: scripts exit zero; new tests pass in Unity test runner; no new analyzer errors.

## Validation and Acceptance

**Acceptance A — Preserve-all**

- With merge mode preserve-all, while deferral is active, perform **K** distinct `Notify` calls (via store updates or direct handler calls in tests). Before flush, inner `Notify` count is **0**. After `Flush`, inner count is **K** and order matches call order.

**Acceptance B — Fold-latest**

- With merge mode latest-per-key, notify the same `(IReference, Type)` **K** times with different state instances. After flush, inner `Notify` count is **1** and the state instance is the **last** one.

**Acceptance C — No deferral**

- With deferral depth **0** and policy chosen so pass-through applies (or a “disabled” configuration if implemented), `Notify` immediately reaches inner handler without manual flush — **same as today** for default `StateEventHandler` behavior.

**Acceptance D — Aggregate integration**

- Optional but valuable: one test with a real `Store` + `AggregateSlice` from samples showing reduced inner notification count under fold-latest across a burst of canonical commits (if the sample can produce duplicate keys in one defer window).

## Idempotence and Recovery

Adding new files under the states package is additive. Rolling back is removing the new types and tests and reverting README changes. No migrations or asset destruction.

## Artifacts and Notes

Indented examples below are illustrative pseudocode for implementers; final names must match the Decision Log and analyzer rules.

    // Construct inner + decorator
    var inner = StateEventHandlers.CreateDefault();
    var deferred = new DeferredStateEventHandler(inner, StateEventMergeMode.LatestPerKey);
    builder.AddEventHandler(deferred);
    Store store = builder.Build();

    deferred.BeginDeferScope(); // or using (deferred.BeginDeferScope()) { ... }
    store.Execute(...); // many Notifys buffer
    deferred.Flush();

## Interfaces and Dependencies

By the end of implementation, the following capabilities must exist (exact type names are allowed to differ if the Decision Log is updated in the same revision):

- **`StateEventMergeMode`** (enum): at least **PreserveAll** and **LatestPerKey**.

- **`DeferredStateEventHandler`** (class): constructor taking **`IStateEventHandler inner`** and merge mode (and optional future options). Implements **`IStateEventHandler`**; subscription methods delegate to **`inner`**. **`Notify`** enqueues or passes through per defer rules.

- **`IStateEventDeferralController`** (interface): **`void Flush()`**, and **`IDisposable BeginDeferScope()`** (or equivalent nested-safe defer API). Implemented by **`DeferredStateEventHandler`**.

- **No new assembly references** required beyond what `Scaffold.States` already uses, unless optional Unity scheduling is added later (not required for this ExecPlan’s core).

Optional future extension (not required for closure of this plan): a **scheduler** that calls **`Flush()`** on end-of-frame, similar in spirit to `UnityDeferredBindingScheduler` in MVVM, living in an engine-specific assembly or a small optional module so **`Scaffold.States` remains engine-agnostic**.

## Revision history

- 2026-04-05: Initial ExecPlan authored from stakeholder request (decorator around state event dispatch, batch flush, preserve vs fold-latest, frame-aligned UI optimization goal).
- 2026-04-05: Marked initial authoring Progress complete; specified **`Flush()`** semantics (always drains full queue; does not alter deferral depth).
- 2026-04-05: Implementation landed: `DeferredStateEventHandler`, `StateEventHandlers.CreateDefault()`, tests, README/Docs, `AssemblyInfo` for test visibility.
