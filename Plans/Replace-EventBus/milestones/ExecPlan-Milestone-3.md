# Implement Middleware and Diagnostics for Replace-EventBus

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root and `Plans/Replace-EventBus/ExecPlan.md`.

## Purpose / Big Picture

After this milestone, event and request flows will support middleware hooks and diagnostics sinks for tracing, metrics, and policy checks without changing business handlers. Contributors can verify deterministic middleware ordering and diagnostics invocation through Events tests.

## Progress

- [x] (2026-03-10 00:00Z) Created implementation-focused ExecPlan for middleware and diagnostics.
- [ ] Add/confirm middleware contracts for event and request paths.
- [ ] Add/confirm diagnostics sink contracts and no-op default implementation.
- [ ] Integrate middleware and diagnostics into publish/request pipelines with deterministic ordering.
- [ ] Add tests proving middleware order and diagnostics callbacks.
- [ ] Validate build and Events test pass status.

## Surprises & Discoveries

- Observation: Not started yet.
  Evidence: No middleware/diagnostics changes have been implemented in this child milestone.

## Decision Log

- Decision: Middleware execution order will be deterministic and documented.
  Rationale: Cross-cutting behavior must be predictable to avoid hidden regressions.
  Date/Author: 2026-03-10 / Codex

- Decision: Diagnostics defaults to no-op sink.
  Rationale: Instrumentation should be opt-in and low overhead when not configured.
  Date/Author: 2026-03-10 / Codex

## Outcomes & Retrospective

Not started yet. Update this section after middleware order and diagnostics tests are passing.

## Context and Orientation

Relevant files for this milestone:

- `Assets/Scripts/Infra/Events/Runtime/Contracts/` (middleware and diagnostics contracts)
- `Assets/Scripts/Infra/Events/Runtime/Implementation/ScalableEventBus.cs`
- `Assets/Scripts/Infra/Events/Container/EventsInstaller.cs` (for default sink and middleware collection wiring)
- `Assets/Scripts/Infra/Events/Tests/`

Middleware in this plan means code that wraps event or request execution before and after handlers run.

## Plan of Work

Ensure contracts exist for `IEventMiddleware`, `IRequestMiddleware`, and `IEventDiagnosticsSink`.

Implement middleware pipeline execution in `ScalableEventBus` for both `Raise` and `RequestAsync`. Preserve deterministic order. For example, if middlewares are `[A, B]`, execution trace should be `before-A`, `before-B`, `handler`, `after-B`, `after-A`.

Integrate diagnostics callbacks for event publish, listener invocation timing, listener failure, and request completion success/failure.

Create a no-op diagnostics sink implementation and wire it as default in container registration.

Add tests for middleware order and diagnostics callback invocation counts.

## Concrete Steps

Run from repo root: `C:/Users/user/Documents/Unity/Scaffold`.

1. Inspect contracts/runtime/tests.

    Get-Content Assets/Scripts/Infra/Events/Runtime/Contracts/*.cs
    Get-Content Assets/Scripts/Infra/Events/Runtime/Implementation/ScalableEventBus.cs
    Get-Content Assets/Scripts/Infra/Events/Tests/*.cs

2. Implement middleware pipeline and diagnostics sink support.

3. Add tests for middleware order and diagnostics callbacks.

4. Run build.

    dotnet build Scaffold.sln -c Release

5. Run Events tests.

    Unity.exe -batchmode -quit -projectPath "C:\Users\user\Documents\Unity\Scaffold" -runTests -testPlatform EditMode -testFilter "Scaffold.Events.Tests" -testResults "Logs\Events-MiddlewareDiagnostics.xml"

## Validation and Acceptance

Milestone Gate Requirement: before moving to the next milestone, run build/lint/analyzer checks and the milestone test suite, fix all introduced warnings/errors/failures, re-run until green, and commit the milestone changes before starting the next milestone.
This milestone is accepted when all conditions are true:

1. Middleware runs in deterministic before/after order for event and request paths.
2. Diagnostics hooks are invoked for publish, listener invoke/failure, and request completion.
3. No-op diagnostics sink allows runtime to execute with zero required external setup.
4. Middleware and diagnostics tests pass in Events suite.

## Idempotence and Recovery

This milestone is additive and safe to rerun.

If middleware integration causes regressions, keep diagnostics contract additions and temporarily bypass middleware execution path while retaining tests to drive fixes.

## Artifacts and Notes

Capture concise evidence while implementing:

- Middleware order trace.
- Diagnostics callback trace for publish and failure cases.
- Events test summary for middleware/diagnostics cases.

Store larger logs in `Logs/`.

## Interfaces and Dependencies

Expected interfaces and types at milestone completion:

- `Scaffold.Events.IEventMiddleware`.
- `Scaffold.Events.IRequestMiddleware`.
- `Scaffold.Events.IEventDiagnosticsSink` and no-op implementation.

Dependencies:

- Existing Events runtime and container abstractions.
- Existing test module in `Assets/Scripts/Infra/Events/Tests/`.

---

Revision Note (2026-03-10): Initial middleware/diagnostics implementation plan created as a child plan of `Replace-EventBus`.
Revision Note (2026-03-10): Added explicit milestone gate requiring checks/tests/fixes before advancing.
Revision Note (2026-03-10): Updated milestone gate to require committing changes immediately after successful validation/testing.

