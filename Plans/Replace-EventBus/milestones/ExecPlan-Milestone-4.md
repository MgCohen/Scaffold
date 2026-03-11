# Implement Container Switch and Migration for Replace-EventBus

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root and `Plans/Replace-EventBus/ExecPlan.md`.

## Purpose / Big Picture

After this milestone, dependency injection wiring will resolve `IEventBus` (and `IRequestBus`) to the new scalable implementation, while preserving compatibility for existing consumers. Contributors can verify the migration by running Events and dependent-module smoke tests.

## Progress

- [x] (2026-03-10 00:00Z) Created implementation-focused ExecPlan for container switch and migration.
- [ ] Update installer wiring to resolve the new implementation.
- [ ] Register diagnostics default and middleware collection support.
- [ ] Keep/mark legacy `EventController` as not active in DI path.
- [ ] Update usage samples and migration documentation references.
- [ ] Validate Events and dependent smoke tests.

## Surprises & Discoveries

- Observation: Not started yet.
  Evidence: Container wiring has not been updated in this child milestone yet.

## Decision Log

- Decision: Route `IEventBus` and `IRequestBus` to the same scoped scalable instance.
  Rationale: A single runtime instance preserves ordering/diagnostics context and avoids duplicated registration state.
  Date/Author: 2026-03-10 / Codex

- Decision: Keep legacy implementation code available during migration, but remove it from active DI registration.
  Rationale: This supports rollback and debugging without continuing legacy behavior by default.
  Date/Author: 2026-03-10 / Codex

## Outcomes & Retrospective

Not started yet. Update this section after installer switch and smoke tests succeed.

## Context and Orientation

Relevant files for this milestone:

- `Assets/Scripts/Infra/Events/Container/EventsInstaller.cs`
- `Assets/Scripts/Infra/Events/Runtime/Implementation/EventController.cs`
- `Assets/Scripts/Infra/Events/Runtime/Implementation/ScalableEventBus.cs`
- `Assets/Scripts/Infra/Events/Samples/EventsUseCases.cs`
- `Docs/Infra/Events.md`

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

