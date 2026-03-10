# Implement Request Routing with Awaitable for Replace-EventBus

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root and `Plans/Replace-EventBus/Replace-EventBus.md`.

## Purpose / Big Picture

After this milestone, the new bus will support request/response messaging using Unity `Awaitable`. Contributors will be able to register request handlers, execute `RequestAsync`, and observe deterministic success and failure behavior (no-handler and handler-throws cases) through tests.

## Progress

- [x] (2026-03-10 00:00Z) Created implementation-focused ExecPlan for request routing with `Awaitable`.
- [ ] Add request contracts and runtime storage for request handlers.
- [ ] Implement `RequestAsync` using `Awaitable<TResponse>` with cancellation support.
- [ ] Add tests for success, no-handler, and handler-failure paths.
- [ ] Validate build and Events test pass status.

## Surprises & Discoveries

- Observation: Not started yet.
  Evidence: No request-routing implementation changes have been made in this child milestone.

## Decision Log

- Decision: Use Unity `Awaitable` instead of `ValueTask` for request flow signatures.
  Rationale: This aligns with Unity-native async patterns in this codebase.
  Date/Author: 2026-03-10 / Codex

- Decision: Route request handlers by exact runtime request type unless explicit hierarchy policy is later added.
  Rationale: Exact routing is simpler, deterministic, and easier to validate for first release.
  Date/Author: 2026-03-10 / Codex

## Outcomes & Retrospective

Not started yet. Update this section after request flow tests pass.

## Context and Orientation

Relevant files for this milestone:

- `Assets/Scripts/Infra/Events/Runtime/Contracts/` (new request contracts if missing)
- `Assets/Scripts/Infra/Events/Runtime/Implementation/ScalableEventBus.cs`
- `Assets/Scripts/Infra/Events/Tests/` (new request-focused tests)
- `Assets/Scripts/Tools/Maps/Runtime/Map.cs`

A request in this plan means an object that expects a typed response asynchronously, for example `LoadProfileRequest -> PlayerProfile`.

## Plan of Work

Ensure `ContextRequest<TResponse>` and `IRequestBus` exist in runtime contracts. `IRequestBus` must expose `RequestAsync<TResponse>(ContextRequest<TResponse> request, CancellationToken cancellationToken = default)` and return `Awaitable<TResponse>`.

In `ScalableEventBus`, add request handler registry support using `Map<Type, long, RequestHandlerEntry>`. Implement generic and open-type register/unregister for handlers.

Implement `RequestAsync` to resolve exactly one handler for a request type (or deterministic policy if multiple handlers are allowed). On no handler or thrown exception, fail the returned `Awaitable` with explicit error messages.

Add tests for request success, missing handler, thrown handler exception, and cancellation behavior.

## Concrete Steps

Run from repo root: `C:/Users/user/Documents/Unity/Scaffold`.

1. Inspect request contracts and runtime implementation.

    Get-Content Assets/Scripts/Infra/Events/Runtime/Contracts/*.cs
    Get-Content Assets/Scripts/Infra/Events/Runtime/Implementation/ScalableEventBus.cs

2. Implement request contracts/runtime with `Awaitable` signatures.

3. Add request tests under `Assets/Scripts/Infra/Events/Tests/`.

4. Run build.

    dotnet build Scaffold.sln -c Release

5. Run Events tests.

    Unity.exe -batchmode -quit -projectPath "C:\Users\user\Documents\Unity\Scaffold" -runTests -testPlatform EditMode -testFilter "Scaffold.Events.Tests" -testResults "Logs\Events-RequestAwaitable.xml"

## Validation and Acceptance

Milestone Gate Requirement: before moving to the next milestone, run build/lint/analyzer checks and the milestone test suite, fix all introduced warnings/errors/failures, re-run until green, and commit the milestone changes before starting the next milestone.
This milestone is accepted when all conditions are true:

1. `IRequestBus.RequestAsync` uses `Awaitable<TResponse>`.
2. Request success path returns expected typed response.
3. No-handler requests fail with deterministic error.
4. Handler-thrown exceptions fail requests deterministically.
5. Cancellation token behavior is tested and deterministic.

## Idempotence and Recovery

This milestone is additive and safe to rerun.

If request routing is unstable, keep listener runtime active and gate request path usage behind wiring or feature flags until tests are stable.

## Artifacts and Notes

Capture concise evidence while implementing:

- Passing request success test output.
- No-handler failure output.
- Handler-throws failure output.
- Cancellation behavior output.

Store larger transcripts in `Logs/`.

## Interfaces and Dependencies

Expected interfaces and types at milestone completion:

- `Scaffold.Events.ContextRequest<TResponse>`.
- `Scaffold.Events.IRequestBus` returning `Awaitable<TResponse>`.
- `RequestHandlerEntry` (or equivalent internal type).
- `Map<Type, long, RequestHandlerEntry>` request storage.

Dependencies:

- Unity async primitive `Awaitable`.
- `Scaffold.Maps` for request handler indexing.

---

Revision Note (2026-03-10): Initial request-routing implementation plan created as a child plan of `Replace-EventBus`.
Revision Note (2026-03-10): Added explicit milestone gate requiring checks/tests/fixes before advancing.
Revision Note (2026-03-10): Updated milestone gate to require committing changes immediately after successful validation/testing.

