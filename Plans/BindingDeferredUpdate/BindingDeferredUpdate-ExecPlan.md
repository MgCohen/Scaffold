# Deferred binding updates: policy, scheduler, and bind-source defaults

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Repository policy for ExecPlans is defined in `PLANS.md` at the repository root. This document must be maintained in accordance with that file.

## Purpose / Big Picture

After this work, a developer can **delay MVVM binding refresh** (re-run source getters and apply setters) until a **later moment in the frame loop** or the **next frame**, so that many `PropertyChanged` notifications in one logical burst do not each force a full UI pass. The behavior is **opt-in**: default remains **immediate** (today’s behavior). A developer can set policy **once per view** (same ergonomics as `BindConverter` / `RegisterAdapter`) so every bind on that `ViewElement` or `ViewModel` uses deferral without repeating options on each `Bind(...)` call, and can still **override** on a **single bind** via `BindingOptions` when needed.

Someone can see it working when: a small test or sample view registers a **deferred update policy** plus a **scheduler**, triggers multiple `UpdateBinding` / `PropertyChanged` paths in one synchronous call chain, and observes **one** target update per deferral window (or **N** immediate updates when policy is immediate). Running `powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests` from the repository root completes with a clean gate, and any new EditMode tests added for this feature pass.

## Progress

- [x] Author initial ExecPlan at `Plans/BindingDeferredUpdate/BindingDeferredUpdate-ExecPlan.md`.
- [x] Milestone 1 — Core types: `BindingUpdateTiming`, `IDeferredBindingScheduler`, `IBindingDeferredCoordinator`, `BindingOptions.UpdateTiming`, resolved options in `TreeBinding.RegisterBind`.
- [x] Milestone 2 — `TreeBinding` / `BindGroup` / `BindContext`: `OnBindingKeyChanged` + `FlushDeferredUpdates`; deferred dirty set + `RegisterBindingUpdatePolicy`; immediate path still runs every `UpdateBind`.
- [x] Milestone 3 — `IBindings` / `IBindSource` + `MVVMCompositionGenerator` forward `RegisterBindingUpdatePolicy` on generated bind-source partials.
- [x] Milestone 4 — `UnityDeferredBindingScheduler` (`MonoBehaviour` coroutine pump); README notes VContainer `ITickable` pattern.
- [x] Milestone 5 — `Scaffold.MVVM.Tests` `TreeBindingDeferredUpdateTests` (immediate vs deferred vs overrides). Run `validate-changes.ps1` when the environment allows analyzer DLL load and Unity batchmode (local gate may fail with Application Control / open Unity).

## Surprises & Discoveries

Document unexpected behaviors, bugs, optimizations, or insights discovered during implementation. Provide concise evidence.

- Observation: Repository validation on this machine reported `Application Control policy has blocked this file (0x800711C7)` when loading `Scaffold.Mvvm.Analyzers.dll` during analyzer unit tests; Unity batchmode also reported another editor holding the project.
  Evidence: `validate-changes.ps1` output; implementation compiles via `dotnet build` for `MVVMCompositionGenerator` (warnings only, pre-existing SCA on generator file).

## Decision Log

- Decision: **Do not add “cheap coalescing” when deferred mode is off.** When policy is **immediate**, every `UpdateBind` continues to run `BindGroup.Update()` immediately, even if the same bind key is notified multiple times in one frame. Coalescing (dirty set + single flush) applies **only** when that bind path is bound under a **deferred** effective policy.
  Rationale: Stakeholder explicitly wants to preserve the ability to observe every notification chain when not using deferral.
  Author: Stakeholder request, initial ExecPlan draft.

- Decision: **Policy at two levels** — (1) **Bind-source default**: register once on the same object that exposes `Bind` / `BindConverter` (generated `bindings` / `TreeBinding`), e.g. `RegisterBindingUpdatePolicy(...)` in `OnBind` or `Initialize`. (2) **Per-bind override**: optional field on `BindingOptions` (or equivalent) so a single `RegisterBind` can opt into a different mode or `Inherit` the bind-source default.
  Rationale: Matches converter/adapter ergonomics; avoids repeating options on every bind for “defer everything on this view.”
  Author: Stakeholder request, initial ExecPlan draft.

- Decision: **Scheduler is injected / registered, not hard-coded to Unity.** The MVVM package defines an abstraction (see Interfaces) that schedules a **continuation** (`System.Action`). Concrete implementations may use Unity `PlayerLoop`, a hidden `MonoBehaviour`, `WaitForEndOfFrame`, or **VContainer** `ITickable` that drains a queue in `Tick()`.
  Rationale: Stay testable and host-agnostic; games choose timing semantics.
  Author: Stakeholder request, initial ExecPlan draft.

- Decision: **Timing semantics are named modes**, not implicit engine behavior. At minimum distinguish **Immediate**, **Next frame** (first opportunity in the next player loop frame after the current stack unwinds), and **End of frame** (after rendering / end-of-frame phase, Unity-specific unless another host maps it). The **scheduler** associated with a policy interprets the mode.
  Rationale: Avoid ambiguity between “next Update” and “after rendering.”
  Author: ExecPlan author, initial draft.

- Decision: **Shipped enum names** — `BindingUpdateTiming` with `Immediate`, `NextFrame`, `EndOfFrame`. Per-bind override uses `BindingOptions.UpdateTiming` (null = inherit) and `BindingOptions.StrictImmediate` for explicit immediate.
  Rationale: Matches ExecPlan interfaces section; keeps `Strict` / `Lazy` as inherit-timing defaults.
  Author: Implementation pass, 2026-04-05.

## Outcomes & Retrospective

Summarize outcomes, gaps, and lessons learned at major milestones or at completion. Compare the result against the original purpose.

- **Achieved:** Bind-source `RegisterBindingUpdatePolicy`, per-bind `BindingOptions`, deferred flush coalescing only on deferred binds, `UnityDeferredBindingScheduler`, MVVM README updates, generator forwarding, EditMode-targeted tests in `Scaffold.MVVM.Tests`.
- **Gaps:** Full `validate-changes.ps1` green not confirmed on hosts where analyzer DLLs are blocked or Unity holds the project lock.

## Context and Orientation

**Binding pipeline today** (relevant files):

- `Assets/Packages/com.scaffold.mvvm/Runtime/Binding/TreeBinding.cs` — owns `BindGroups`, `UpdateBind(string bindKey)` which resolves a group and calls `BindGroup.Update()` immediately.
- `Assets/Packages/com.scaffold.mvvm/Runtime/Binding/BindGroup.cs` / `BindContext.cs` — group update re-gets source values and pushes to registered binds.
- `Assets/Packages/com.scaffold.viewmodel/Runtime/ViewModel.cs` — `OnPropertyChanged` calls `UpdateBinding(propertyName)` (generated).
- `Assets/Packages/com.scaffold.view/Runtime/ViewElement.cs` — `OnViewModelChanged` builds a path and calls `UpdateBinding`.
- `Generators/MVVMCompositionGenerator/MVVMCompositionGenerator.cs` — generates `*_bindsource.g.cs` with `Bind`, `UpdateBinding`, `ClearBindings`, `RegisterConverter`, etc.

**Navigation** (`Assets/Packages/com.scaffold.navigation/`) calls `viewModel.Bind(navigation)`; it does **not** implement binding refresh. All deferred-update work belongs under **MVVM** and the **source generator**, not navigation.

**Term: Effective policy** — For each registered bind, the runtime resolves whether updates are **immediate** or **deferred** by: if the bind’s `BindingOptions` specifies an override, use it; otherwise use the **bind-source default policy** registered on `TreeBinding` (or “immediate” if none).

**Term: Deferred flush** — When in deferred mode, `UpdateBind` does not call `BindGroup.Update()` synchronously; it adds the bind key to a **dirty set** and ensures **one** scheduled callback will flush all dirty keys for this `TreeBinding` instance (coalescing **only** along this deferred path).

## Plan of Work

### 1. Core policy and options

Extend or accompany `BindingOptions` (`Assets/Packages/com.scaffold.mvvm/Runtime/Binding/BindingOptions.cs`) so a bind can specify:

- **Inherit bind-source default** (for backward compatibility, `null` or a dedicated sentinel means inherit).
- **Explicit mode**: Immediate vs deferred family.

Keep existing `LazyEvaluation` behavior; document interaction (lazy only affects **initial** wire-up, not ongoing updates).

Introduce an enum such as `BindingUpdateTiming` with values at least: **Immediate**, **NextFrame**, **EndOfFrame** (exact names are implementation details; document mapping in the Decision Log when finalized).

### 2. Bind-source default policy registration

Add to `IBindings` and `TreeBinding` a registration method analogous to `RegisterConverter` / `RegisterAdapter`, for example:

- `RegisterBindingUpdatePolicy(BindingUpdatePolicy policy)` or split into `SetDefaultBindingUpdateTiming(BindingUpdateTiming)` plus `SetDeferredBindingScheduler(IDeferredBindingScheduler scheduler)`.

The **policy** object should combine:

- The **timing** mode (when not overridden per bind).
- The **scheduler** reference used when timing is not Immediate (may be null if mode is Immediate).

Validation: if default mode is deferred, a scheduler must be registered before any deferred update runs; fail fast with a clear exception or log in development.

### 3. TreeBinding behavior

In `TreeBinding.UpdateBind`:

- Resolve **effective timing** for each affected `BindGroup` / bind registration. Because one `bindKey` can fan into a `BindGroup` shared by path segments (`BindGroups.Register` walks path hierarchy), the implementation must define whether policy is stored **per `BindContext`** (recommended: each `BindRegistration` in `BindContext` already carries `BindingOptions`) or aggregated at group level. **Recommended approach:** store **effective policy** on each **bind registration** at registration time (computed from inherit + default), so `BindGroup.Update` can still iterate contexts but each bind knows whether it is deferred.

- **Immediate** effective policy: call existing synchronous update path (no dirty set, no coalescing).

- **Deferred** effective policy: mark the minimal set of **contexts** or **keys** needed to flush correctly, enqueue **one** scheduler callback per `TreeBinding` per “wave” if not already pending, and in the callback run the same `BindContext.Update` / group logic as today for dirty entries only.

**Important:** Do **not** merge multiple synchronous `UpdateBind` calls into one when mode is Immediate (stakeholder requirement).

### 4. Scheduler abstraction (host-agnostic)

Define in `Scaffold.MVVM` (no Unity reference in the core contract if the MVVM assembly is Unity-free; if it already references Unity, keep the interface in the same assembly as `TreeBinding`):

    public interface IDeferredBindingScheduler
    {
        void Schedule(Action continuation);
    }

Semantics: **Schedule** means “invoke **continuation** exactly once at the time described by the **policy** + **implementation**.” Implementations are responsible for main-thread marshaling if required.

**Suggested implementations** (can live in the same package under `Runtime/Scheduling/` or a small optional Unity-specific helper assembly if you split):

- **Unity end of frame** — A small `MonoBehaviour` or `PlayerLoop` registration that runs continuations after the appropriate phase (e.g. coroutine with `WaitForEndOfFrame`, or `PlayerLoop` system inserted after `PostLateUpdate` / project-appropriate phase). Document which Unity versions and phases you target.

- **Unity next frame** — Run continuations at the start of the next `Update` or via `PlayerLoop` before/after specific systems; avoid allocating per call where possible.

- **VContainer `ITickable`** — A class registered in the DI container that implements `ITickable` and drains a queue of continuations at `Tick()`. This yields “next tick” semantics (order relative to other tickables depends on VContainer registration order; document that games may register this tickable last if they want “after everything else in this tick”). This is **not** identical to Unity “end of frame” unless you combine with Unity’s player loop; treat it as a **separate** scheduler implementation for apps that already center timing on VContainer.

Games inject the chosen `IDeferredBindingScheduler` when building the view or in `OnBind` by passing it into `RegisterBindingUpdatePolicy`.

### 5. ViewElement / ViewModel developer workflow

Document the pattern: in `OnBind` (or `Initialize` for view models), call **once**:

- `bindings.RegisterBindingUpdatePolicy(...)` with timing + scheduler,

then register binds as today. Per-bind override: `Bind(..., new BindingOptions(..., timing: BindingUpdateTiming.NextFrame))` when needed.

### 6. Source generator

Update `MVVMCompositionGenerator` so generated `*_bindsource.g.cs` exposes the new `RegisterBindingUpdatePolicy` (or named equivalent) on the same generated partial as `RegisterConverter`, keeping a single `_bindSourceBindings` field.

### 7. Tests

Add tests in the appropriate test assembly for `com.scaffold.mvvm` (or a small test double `IDeferredBindingScheduler` that runs continuations synchronously or on demand) to verify:

- Immediate mode: **N** calls to `UpdateBind` → **N** group updates (assert with a test double counter on `BindContext` or mock).
- Deferred mode with manual flush: **N** calls in one “frame” before scheduler runs → **1** flush.
- Per-bind override beats bind-source default.

Use `Docs/AutomatedTesting.md` and existing MVVM test patterns if present.

## Concrete Steps

From repository root `C:\Unity\Scaffold` (or your clone), after implementation:

    powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests

If EditMode tests are added:

    powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\run-editmode-tests.ps1"

Expected: validation reports success; analyzer total blockers as per project policy; new tests pass.

## Validation and Acceptance

- **Unit-level:** A test scheduler records how many times `BindContext.Update` runs for repeated `UpdateBind` on the same key — immediate: multiple; deferred before flush: one.
- **Manual Unity-level:** A sample or temporary logging view registers **EndOfFrame** policy, toggles a property in a loop in `Update()`, and logs show one binding apply after the burst.
- **Regression:** Existing views without calling `RegisterBindingUpdatePolicy` behave as today (all immediate).

## Idempotence and Recovery

Registering policy multiple times should replace the previous default policy deterministically (document whether first or last wins; recommend **last registration wins** for `OnBind` re-entry). `ClearBindings` / `Unbind` must cancel any pending scheduled callback for that `TreeBinding` instance to avoid leaks or updates after teardown.

## Artifacts and Notes

Indented examples only (no nested code fences in this file per `PLANS.md`):

    // Pseudocode: bind source default + per-bind override
    RegisterBindingUpdatePolicy(BindingUpdateTiming.NextFrame, myScheduler);
    Bind(() => vm.Title, t => label.text = t); // inherits deferred
    Bind(() => vm.Cursor, SetCursor, BindingOptions.Immediate); // override

## Interfaces and Dependencies

**Must exist after implementation (names may be refined but roles are fixed):**

- `BindingUpdateTiming` enum — Immediate, NextFrame, EndOfFrame (or subset).
- `IDeferredBindingScheduler` — `void Schedule(Action continuation)`.
- `BindingOptions` — way to specify inherit vs explicit timing (nullable field or dedicated static `Inherit`).
- `IBindings` — `RegisterBindingUpdatePolicy(...)` (or equivalent split API).
- `TreeBinding` — implements registration, dirty tracking for deferred binds, flush on scheduler callback.

**Optional / sample:**

- `UnityEndOfFrameBindingScheduler` — requires UnityEngine.
- `VContainerTickableBindingScheduler` — depends on VContainer; lives outside core MVVM or in an optional integration assembly to avoid hard dependency from `Scaffold.MVVM` to VContainer.

**Assembly boundaries:** Follow existing `Scaffold.MVVM` asmdef; do not add VContainer reference to core MVVM unless the repo already does—prefer optional integration package or sample code in Docs.

---

Revision note: Initial version created from stakeholder request (deferred binding, no coalescing when immediate, scheduler agnostic, policy on bind source + per-bind override).

Revision note (2026-04-05): Implementation landed in `Scaffold.MVVM` (`BindingUpdateTiming`, `IDeferredBindingScheduler`, `TreeBinding.RegisterBindingUpdatePolicy`, `BindingOptions.UpdateTiming`, `UnityDeferredBindingScheduler`, generator + tests). Re-run full validation when analyzer DLLs and Unity batchmode are available.
