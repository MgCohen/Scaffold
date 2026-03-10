# Implement Container Switch and Migration for Replace-EventBus

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root and `Plans/Replace-EventBus/Replace-EventBus.md`.

## Purpose / Big Picture

After this milestone, dependency injection wiring will resolve `IEventBus` (and `IRequestBus`) to the new scalable implementation, while preserving compatibility for existing consumers. Contributors can verify the migration by running Events and dependent-module smoke tests.

## Progress

- [x] (2026-03-10 00:00Z) Created implementation-focused ExecPlan for container switch and migration.
- [x] (2026-03-10 13:00Z) Updated `EventsInstaller` to register `ScalableEventBus` as scoped runtime, bind both `IEventBus` and `IRequestBus` to the same scoped instance, and register `IEventDiagnosticsSink` default as `NoOpEventDiagnosticsSink`.
- [x] (2026-03-10 13:00Z) Added middleware collection resolution support in installer runtime factory with safe empty fallback when no middleware registrations exist.
- [x] (2026-03-10 13:00Z) Marked `EventController` obsolete and removed it from active DI path while keeping class available for migration fallback.
- [x] (2026-03-10 13:00Z) Updated `EventsUseCases` and `Docs/Events.md` to reflect scalable runtime wiring, request usage, middleware/diagnostics behavior, and migration guidance.
- [x] (2026-03-10 13:00Z) Validation run completed: `dotnet build Scaffold.sln -c Release -p:UseSharedCompilation=false`, focused Events project builds, and Unity batch invocations for Events and Navigation test filters; Unity still does not emit requested XML artifacts in this environment.

## Surprises & Discoveries

- Observation: Unity batch test invocations continue exiting successfully but do not create requested test result XML files.
  Evidence: `Logs/Events-ContainerSwitch.log` and `Logs/Navigation-AfterEventBusSwitch.log` include `Batchmode quit successfully invoked` and `Exiting batchmode successfully now`, while `Logs/Events-ContainerSwitch.xml` and `Logs/Navigation-AfterEventBusSwitch.xml` are absent.
- Observation: A second Unity batch invocation launched too quickly after the first can fail with an active-project lock.
  Evidence: Initial Navigation invocation returned `Multiple Unity instances cannot open the same project`; rerun after completion succeeded.

## Decision Log

- Decision: Route `IEventBus` and `IRequestBus` to the same scoped scalable instance.
  Rationale: A single runtime instance preserves ordering/diagnostics context and avoids duplicated registration state.
  Date/Author: 2026-03-10 / Codex

- Decision: Keep legacy implementation code available during migration, but remove it from active DI registration.
  Rationale: This supports rollback and debugging without continuing legacy behavior by default.
  Date/Author: 2026-03-10 / Codex

- Decision: Resolve middleware collections through container `IEnumerable<T>` and fall back to empty arrays when resolution throws.
  Rationale: This keeps middleware composition extensible while preventing installer failures when no middleware has been registered.
  Date/Author: 2026-03-10 / Codex

## Outcomes & Retrospective

Milestone completed: DI wiring now resolves both `IEventBus` and `IRequestBus` to one scoped `ScalableEventBus`, default diagnostics sink is registered, and legacy `EventController` is no longer in the active DI path.

Migration documentation and sample usage were updated to show scalable bus flows (generic/open listener API and request usage) while preserving a legacy compatibility example.

Validation commands succeeded for solution and focused project builds. Unity batch runs for Events and Navigation filters completed with successful process exit and shutdown logs, but this environment still did not generate requested XML result files.

## Context and Orientation

Relevant files for this milestone:

- `Assets/Scripts/Infra/Events/Container/EventsInstaller.cs`
- `Assets/Scripts/Infra/Events/Runtime/Implementation/EventController.cs`
- `Assets/Scripts/Infra/Events/Runtime/Implementation/ScalableEventBus.cs`
- `Assets/Scripts/Infra/Events/Samples/EventsUseCases.cs`
- `Docs/Events.md`

This milestone should not introduce behavior changes in call sites; it should switch wiring and keep consumer code stable.

## Plan of Work

Update `EventsInstaller` so `IEventBus` resolves to `ScalableEventBus` and `IRequestBus` resolves to the same scoped instance.

Register default diagnostics sink and middleware collection dependencies required by runtime construction.

Keep `EventController` available for rollback but no longer bound as primary implementation.

Update sample usage and docs sections that reference implementation type names or wiring behavior.

Run Events tests plus at least one dependent module smoke test (Navigation).

## Concrete Steps

Run from repo root: `C:/Users/user/Documents/Unity/Scaffold`.

1. Inspect installer and runtime files.

    Get-Content Assets/Scripts/Infra/Events/Container/EventsInstaller.cs
    Get-Content Assets/Scripts/Infra/Events/Runtime/Implementation/ScalableEventBus.cs
    Get-Content Assets/Scripts/Infra/Events/Runtime/Implementation/EventController.cs

2. Update DI registrations in `EventsInstaller`.

3. Update samples/docs for migration visibility.

4. Run build.

    dotnet build Scaffold.sln -c Release

5. Run Events tests.

    Unity.exe -batchmode -quit -projectPath "C:\Users\user\Documents\Unity\Scaffold" -runTests -testPlatform EditMode -testFilter "Scaffold.Events.Tests" -testResults "Logs\Events-ContainerSwitch.xml"

6. Run dependent smoke test.

    Unity.exe -batchmode -quit -projectPath "C:\Users\user\Documents\Unity\Scaffold" -runTests -testPlatform EditMode -testFilter "Scaffold.Navigation.Tests" -testResults "Logs\Navigation-AfterEventBusSwitch.xml"

## Validation and Acceptance

Milestone Gate Requirement: before moving to the next milestone, run build/lint/analyzer checks and the milestone test suite, fix all introduced warnings/errors/failures, re-run until green, and commit the milestone changes before starting the next milestone.
This milestone is accepted when all conditions are true:

1. `EventsInstaller` binds `IEventBus` to scalable implementation.
2. `IRequestBus` resolves to the same scoped runtime instance.
3. Legacy `EventController` is not in active DI path.
4. Events tests pass after wiring switch.
5. Navigation smoke test passes without consumer API changes.

## Idempotence and Recovery

This milestone is safe to rerun.

If dependent modules regress, revert only DI binding in `EventsInstaller` to `EventController` while keeping new implementation and tests in place for continued hardening.

## Artifacts and Notes

Capture concise evidence while implementing:

- Installer diff summary.
- Events test summary after switch.
- Navigation smoke test summary.

Store larger logs under `Logs/`.

## Interfaces and Dependencies

Expected interfaces and runtime wiring at milestone completion:

- `IEventBus -> ScalableEventBus` scoped.
- `IRequestBus -> same ScalableEventBus` scoped.
- Diagnostics default sink and middleware dependencies registered.

Dependencies:

- `Scaffold.Containers` registration APIs.
- Existing Events runtime contracts and implementations.

---

Revision Note (2026-03-10): Initial container-switch/migration implementation plan created as a child plan of `Replace-EventBus`.
Revision Note (2026-03-10): Added explicit milestone gate requiring checks/tests/fixes before advancing.
Revision Note (2026-03-10): Updated milestone gate to require committing changes immediately after successful validation/testing.

