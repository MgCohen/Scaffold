# Add Route-Aware Navigation Flows with Transition-Aware Sequencing

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

After this change, a caller will be able to open a target view controller by intent and let Navigation resolve and execute a route automatically, including intermediate screens and non-screen action steps. The user-visible result is that flows such as `MainMenu -> Inventory -> Forge popup` and `Anywhere -> Settings -> Sound -> scroll to music section` can be declared once in config and then reused consistently.

Success is observable when a single route-open call from a view controller executes all configured flow steps in order, waits for each transition/animation to complete before starting the next step, and ends with the expected final UI state.

## Progress

- [x] (2026-03-15 21:01Z) Authored initial ExecPlan in `Plans/Navigation-Routes/ExecPlan.md`.
- [ ] Implement route schema support on `ViewConfig` and route resolution rules in Navigation runtime.
- [ ] Add async flow execution API that waits for transition completion between route steps.
- [ ] Add view-controller-facing navigation utilities for clear/open/route flows.
- [ ] Add route action step support for in-view actions (for example scrolling to a section).
- [ ] Add regression and behavior tests for route resolution, sequencing, and action steps.
- [ ] Update navigation documentation and samples.

## Surprises & Discoveries

- Observation: Transition buffering already exists in runtime and processes queued transitions sequentially.
  Evidence: `Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationTransitions.cs` stores transitions in `pendingTransitions` and processes them in `RunTransitions()`.

- Observation: `INavigation.Open(...)` is synchronous (`void`) even though transitions are asynchronous (`Awaitable`), so callers cannot currently await transition completion.
  Evidence: `Assets/Scripts/Infra/Navigation/Runtime/Contracts/INavigation.cs` exposes `void Open<TViewController>(...)`; `NavigationTransitions` performs async work internally.

- Observation: `ViewConfig` already accepts schema objects through `SchemaObject`, which is a natural extension point for adding route metadata.
  Evidence: `Assets/Scripts/Infra/Navigation/Runtime/Implementation/ViewConfig.cs` uses `[SchemaFilter(typeof(ViewSchema))]` and existing `NavigationOptionsSchema`/animation schemas follow this pattern.

## Decision Log

- Decision: Route declarations will live in Navigation schemas attached to target `ViewConfig` entries instead of hard-coded in view models.
  Rationale: Keeps route policy centralized and editable without scattering flow logic across multiple controllers.
  Date/Author: 2026-03-15 / Codex

- Decision: Route execution will be asynchronous and transition-aware, using explicit await points between route steps.
  Rationale: Prevents race conditions where consecutive `Open(...)` calls overlap animations and produce non-deterministic final state.
  Date/Author: 2026-03-15 / Codex

- Decision: Navigation utility entry points will be exposed to view controllers through the `IViewController` surface (via contract updates and/or extension methods bound to controller context) rather than only external coordinators.
  Rationale: Satisfies the requirement that flows can begin from view controllers while preserving testability and separation from domain services.
  Date/Author: 2026-03-15 / Codex

- Decision: Route flows will support non-open action steps (for example scroll/focus anchors) as first-class route steps.
  Rationale: Some UX flows finish inside an already-open screen region and should not require fake popup/screen transitions.
  Date/Author: 2026-03-15 / Codex

## Outcomes & Retrospective

Plan drafted. Implementation has not started yet. The current design direction resolves the main risk (transition race conditions during multi-step routing) by introducing awaited route sequencing and explicit action-step support.

## Context and Orientation

The Navigation runtime currently centers on `Assets/Scripts/Infra/Navigation/Runtime/Contracts/INavigation.cs` and `Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationController.cs`. `Open(...)` resolves a `NavigationPoint`, binds the controller, and enqueues transition work. Transition execution lives in `NavigationTransitions`, which already serializes pending transitions but does not expose awaitable completion to callers.

The data model for view/controller binding lives in `Assets/Scripts/Infra/Navigation/Runtime/Implementation/ViewConfig.cs` and `NavigationSettings.cs`. `ViewConfig` already supports extensible schemas through `ViewSchema` descendants, as shown by `NavigationOptionsSchema`.

Definitions used in this plan:

Route means a declarative rule that tells Navigation how to reach a target controller from a specific source (or from anywhere).

Flow step means one ordered operation in a route. A flow step can be a controller-open step (`Open Inventory`) or an action step (`Scroll Sound view to music segment`).

Action step means a route operation that runs inside the currently active view/controller without opening a new view.

Anchor means a named in-view destination such as `music-segment` that a controller can handle (scroll/focus/highlight).

## Milestone Quality Gate

Before marking any milestone complete, execute this loop for that milestone:

1. Check complexity first. If the milestone is complex (multiple modules, unknown API shape, non-trivial refactor), write a mini milestone plan with concrete steps and sample inputs/outputs.
2. If the milestone includes bug-fix behavior, add/update a regression test proving fail-before and pass-after.
3. Implement the milestone scope.
4. If a regression test was added for a bug-fix path, re-run it and confirm pass-after.
5. Run `.agents/scripts/run-editmode-tests.ps1`.
6. Run `.agents/scripts/check-analyzers.ps1`.
7. Fix all failures and diagnostics.
8. Re-run both scripts until clean.
9. Commit milestone changes.

## Plan of Work

### Milestone 1: Route schema model and resolution policy

Add route metadata as a new `ViewSchema` descendant in `Assets/Scripts/Infra/Navigation/Runtime/Implementation/` (for example `RouteViewSchema.cs` with supporting serializable types). The schema attaches to the target `ViewConfig` and declares route entries containing optional source type and ordered flow steps.

Each route entry must support:

- Optional source controller type (`null` means wildcard/anywhere).
- Ordered flow steps before target.
- Route step kind (`OpenController` or `Action`).
- Open behavior per step (`ReplaceCurrent` or `Push`).
- Optional payload string for action steps (anchor id such as `music-segment`).

Resolution precedence:

1. Exact source match first (current source controller type equals route source).
2. Then wildcard source route.
3. If none found, open target directly without route expansion.

Milestone 1 is complex; include a mini plan in commit notes with sample route entries and expected resolved step arrays.

### Milestone 2: Awaitable navigation flow API

Add an async flow API in Navigation contracts and implementation so multi-step route execution can await transition completion for each step.

Add an awaitable open path (for example `Awaitable<IViewController> OpenAsync<TViewController>(...)`) without breaking existing `Open(...)` callers. `Open(...)` may remain as compatibility wrapper.

Wire completion from `NavigationController` to transition completion by observing `NavigationTransitions.TransitionFinished` and correlating completion to the opened `NavigationPoint`. Ensure this works with buffered transitions and does not complete early when unrelated transitions finish.

Define timeout/error behavior for missing completion (for example destroyed point mid-transition) and cover with tests.

### Milestone 3: Route executor and controller utility surface

Implement a route executor in Navigation runtime (for example `NavigationRouter` or integrated `NavigationController.OpenRoutedAsync(...)`) that:

1. Resolves route by source+target.
2. Expands to ordered steps (`Flow + Target`).
3. Executes each step sequentially with await.
4. Runs action steps at the correct point in sequence.

Expose this capability to view controllers through `IViewController` utility access. The final interface must let a bound controller initiate route flows and clear/open flows directly, while preserving existing `Bind(INavigation)` and `Close()` behavior.

Required utility capabilities:

- `OpenRoutedAsync<TTargetController>(...)`
- `ClearAndOpenAsync<TTargetController>(...)` for explicit stack reset flows
- Optional direct `OpenAsync<TTargetController>(...)` wrapper for consistency

If an interface-breaking change is needed, update all internal implementations/samples/tests in the same milestone.

### Milestone 4: Action-step support for in-view intro behavior

Introduce a route action handler contract (for example `IRouteActionHandler` or `IRouteAnchorHandler`) implemented by target controllers that need in-view post-open behavior.

Suggested contract:

- `Awaitable ExecuteRouteActionAsync(string actionId, string payload)`

For the music flow example, route config should support:

- Source: wildcard
- Flow:
  - Open `SettingsViewController`
  - Open `SoundViewController`
  - Action on current controller: `ScrollToAnchor`, payload `music-segment`

Ensure action step execution happens after the prior open transition completes.

### Milestone 5: Tests and regression coverage

Add/extend tests in:

- `Assets/Scripts/Infra/Navigation/Tests/NavigationTests.cs`
- `Assets/Scripts/Infra/MVVM/Tests/MVVMTests.cs` (only if controller utility surface affects MVVM base behavior)

Required coverage:

- Exact-source route match chosen over wildcard.
- Wildcard route used when no exact-source route exists.
- No-route fallback opens target directly.
- Multi-step route runs in deterministic order.
- Route step N+1 starts only after step N transition finished.
- Clear-and-open utility resets stack as defined.
- Action step executes after required opens and receives payload.
- Loop/cycle guard prevents recursive route expansion (`A -> B -> A`).

If any bug is discovered during this work, add fail-before regression tests before implementing the fix.

### Milestone 6: Documentation and samples

Update:

- `Docs/Infra/Navigation.md` with route schema format, resolution precedence, and async route execution semantics.
- `Docs/Core/MVVM.md` with view-controller navigation utility guidance (where to route from, where not to route from).
- `Assets/Scripts/Infra/Navigation/Samples/NavigationUseCases.cs` with routed examples including the music anchor action step.

Document stack behavior clearly for each step mode (`ReplaceCurrent` vs `Push`) and include one end-to-end sample narrative from source to final anchor.

## Concrete Steps

Run all commands from repository root: `C:\Unity\Scaffold`.

1. Inspect current navigation contracts and transition internals.

    Get-Content Assets/Scripts/Infra/Navigation/Runtime/Contracts/INavigation.cs
    Get-Content Assets/Scripts/Infra/Navigation/Runtime/Contracts/IViewController.cs
    Get-Content Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationController.cs
    Get-Content Assets/Scripts/Infra/Navigation/Runtime/Implementation/NavigationTransitions.cs
    Get-Content Assets/Scripts/Infra/Navigation/Runtime/Implementation/ViewConfig.cs

2. Implement Milestones 1-4 iteratively, running tests after each milestone.

    powershell -ExecutionPolicy Bypass -File ".\.agents\scripts\run-editmode-tests.ps1" -AssemblyNames "Scaffold.Navigation.Tests"

3. Run full quality scripts after each milestone gate.

    powershell -ExecutionPolicy Bypass -File ".\.agents\scripts\run-editmode-tests.ps1"
    powershell -ExecutionPolicy Bypass -File ".\.agents\scripts\check-analyzers.ps1"

4. Update docs and samples, then re-run full quality scripts.

Expected command outcomes:

- Navigation test suite passes with new route and sequencing tests.
- EditMode suite passes.
- Analyzer diagnostics introduced by this feature are zero.

## Validation and Acceptance

This work is accepted only when all conditions are true:

1. Opening a routed target from a source expands and executes configured flow steps in order.
2. Route execution waits for each transition/animation to finish before next step begins.
3. Route resolution prioritizes exact source over wildcard and falls back to direct open when no route matches.
4. View controllers can initiate clear/open/routed flows through the new utility surface.
5. Clear/open utility behavior is deterministic and covered by tests.
6. Action route steps run after the relevant view opens and can drive in-view behavior (for example scroll to music section).
7. Cycle protection prevents recursive route loops.
8. `Docs/Infra/Navigation.md` and `Docs/Core/MVVM.md` are updated with route and utility guidance.
9. `.agents/scripts/run-editmode-tests.ps1` passes.
10. `.agents/scripts/check-analyzers.ps1` reports zero new diagnostics for this change.

## Idempotence and Recovery

Schema and runtime updates are additive and can be iterated safely.

If a route change breaks navigation unexpectedly, disable route expansion through a feature flag or temporary bypass path that falls back to direct `Open(...)` while preserving schema data. Keep tests to lock behavior before re-enabling.

If async completion wiring fails (for example missed completion correlation), keep legacy `Open(...)` behavior intact and gate routed async execution until correlation tests pass.

Do not remove existing public APIs until compatibility tests prove no regressions.

## Artifacts and Notes

Capture concise evidence during implementation:

- Route resolution trace for exact-source, wildcard, and fallback cases.
- Sequencing trace proving awaited order (`step 1 open done`, `step 2 open done`, `action done`).
- Test output snippet showing action-step payload reached target handler (`music-segment`).
- Final test/analyzer summary lines.

Store larger logs under `Logs/` and keep this document focused on short proof snippets.

## Interfaces and Dependencies

Interfaces and types expected at completion (names may vary slightly, behavior must match):

- Route schema types in Navigation runtime:
  - `RouteViewSchema : ViewSchema`
  - `RouteEntry` with `SourceControllerType` and `FlowSteps`
  - `RouteStep` with step kind (`OpenController` or `Action`), open mode, and optional payload

- Navigation async flow contracts:
  - Awaitable open method on navigation service (`OpenAsync`)
  - Routed open method (`OpenRoutedAsync`)
  - Clear/open helper (`ClearAndOpenAsync`)

- View-controller utility surface:
  - Utility methods available from bound view controllers to start open/routed/clear flows
  - Backward compatibility for existing `Bind(INavigation)` and `Close()`

- Optional action handler contract:
  - `IRouteActionHandler` (or equivalent) implemented by controllers that accept route actions such as anchor scrolling

Dependencies and boundaries:

- Keep route and flow execution inside `Assets/Scripts/Infra/Navigation/`.
- Keep domain services free of navigation and controller type knowledge.
- MVVM usage remains in `Assets/Scripts/Infra/MVVM/` and should consume navigation utilities, not reimplement routing.

---

Revision Note (2026-03-15): Initial plan created to add route-aware navigation with source-based flow expansion, transition-aware awaited sequencing, view-controller navigation utilities, and in-view action steps (for example music section scroll).
