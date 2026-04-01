# Cloud Code optimistic returns — design notes

**Status:** Implemented (registry + `CloudCodeOptimisticOrchestrator` inside `CloudCodeService` + DI)  
**Related code:** `Assets/Packages/com.scaffold.cloudcode/` (`ICloudCodeService`, `ICloudCodeCallHandler`, `CloudCodeService`)

## Purpose

Explore an optional layer that can **return a client-side inferred response immediately** for some Cloud Code calls, while the **real network call continues in the background**. This improves perceived latency when the response is knowable from the request (or from app rules) before the server responds.

This document captures agreed decisions and remaining implementation work.

---

## Current architecture (snapshot)

- `ICloudCodeService.CallEndpointAsync<T>(module, endpoint, payload, ct)` validates input, converts the **request object** to the wire payload (see **Payload shape** below), starts **`Task<string> serverTask = callHandler.InvokeAsync(...)`** (handler chain **without** optimistic), then **`CloudCodeOptimisticOrchestrator`**: if a registered `IRequestHandler<T>` **accepts** (`TryMatch`), returns deserialized optimistic JSON immediately and **schedules reconciliation** that **awaits the same `serverTask`** and compares JSON; otherwise **awaits `serverTask`** and deserializes.
- Handler order (innermost → outermost): **SDK** → timeout → response logging → **retry** → **single-flight (per module)**. Optimistic logic is **not** an `ICloudCodeCallHandler`.
- `CloudCodeInstaller` registers `CloudCodeOptimisticHandlerRegistry`, **`CloudCodeOptimisticOrchestrator`**, and builds `ICloudCodeService` as a singleton with `CloudCodeSettings`.

Optimistic behavior will be **internal** (callers keep `Task<T>`); it must cooperate with **cancellation**, **retries**, and **single-flight** per module.

---

## Terminology

| Term | Meaning here |
|------|----------------|
| **Optimistic return** | The async method completes with a value **before** the server response is available, using a locally computed guess. |
| **Background / trailing call** | The real `InvokeAsync` continues after the caller already received `T`. |
| **Deterministic inference** | The guessed `T` is **guaranteed** to match what the server would return for that payload (pure function of payload + known rules). **Intended use for now.** |
| **Speculative inference** | A guess that **might** differ from the server. Infrastructure may support it later; reconciliation still applies if used. |
| **Single-flight** | At most one in-flight **real** Cloud Code call per **serialization key** (here: **module name**). |

---

## Decided: design placement

**Orchestration inside `CloudCodeService` (not a call handler)**

- Optimism applies to **whatever is registered** in the handler registry. **Usage is deterministic-only for now**, but the design does not hard-code “deterministic only.”
- The server pipeline stays **`ICloudCodeCallHandler` only** (through single-flight). **`CloudCodeOptimisticOrchestrator`** runs beside it: start `InvokeAsync` first, then decide optimistic vs await server.

---

## Decided: reconciliation when server disagrees

If the trailing call completes with a result that **does not match** the optimistic guess (or handler indicates invalid state):

- Show an **error message** and **restart the game** (hard-fail consistency).

This is stricter than “silent overwrite” and must be wired in app/LiveOps layer when optimistic paths are enabled.

---

## Decided: concurrency

- **Serialization key:** **module name** (`string module` passed to `CallEndpointAsync`). **Single-flight per module** so all endpoints in that module share one queue, avoiding cross-call races on client state tied to that module.
- **Strategy:** **Single-flight** — a second call for the same module **waits** until the previous **real** invocation completes (queue behavior under contention).

---

## Decided: registration

- **Code registration** in DI: **`CloudCodeOptimisticHandlerRegistry.Register<TRequest>(IRequestHandler<TRequest>)`** (singleton registry resolved from the container).
- When dispatching, **match by `payload.GetType()`** (requires **typed object payload**; see **Payload shape**).
- If **no** handler matches, **await** the server task (normal path).
- If a handler is registered for the request type but **`TryMatch` is false**, **await** the server task (no optimistic return).

---

## Decided: API shape (public)

- **Keep `Task<T>`** — no new public result type; optimistic behavior must not change the **observable contract** for callers.
- **Internal only:** call sites that only depend on `ICloudCodeService` do not need new parameters for “optimistic mode” in v1.

---

## Payload shape (request object → wire dictionary)

**Decided refactor:**

- Public API accepts a **single object** `payload` (the request DTO), not `Dictionary<string, object>`.
- **Internally** the service builds the dictionary passed to Unity Cloud Code. The existing game convention is preserved: **`{ "request", payload }`** (same as previous `LiveOpsService` usage).

This enables **`payload.GetType()`** for optimistic handler lookup without exposing dictionaries at call sites.

**Consumer update:** `LiveOpsService` passes **`request`** directly instead of building a dictionary.

---

## Cross-cutting policies (answered)

| # | Topic | Decision |
|---|--------|-----------|
| 1 | Handler mismatch / ineligible request | **Await server** (no optimistic shortcut); reconciliation N/A for that call. |
| 2 | Deterministic vs speculative | **Indifferent** for plumbing; **behavior** will be **deterministic** for now. |
| 3 | Single-flight key | **Per module name**. |
| 4 | Registry key | **By request type** (`System.Type` from payload). Requires object payload refactor above. |
| 5 | Return type | **Keep `Task<T>`.** |
| 6 | Retry vs optimistic | **Independent** — no special coupling in v1 (each layer keeps its own policy). |
| 7 | Cancellation after optimistic return | Treat as **error** (same severity family as mismatch; exact UX TBD with product). |

---

## Implementation notes (as built)

- **`IRequestHandler<TRequest>`** — `TryMatch(module, endpoint, request)`, `GetOptimisticJsonResponse(request)` (JSON string for the wire response).
- **`CloudCodeOptimisticHandlerRegistry`** — public `Register<TRequest>(IRequestHandler<TRequest>)` only; internal dispatch uses typed slots.
- **`CloudCodeOptimisticOrchestrator`** — `TryGetOptimisticResponse` / `ScheduleReconciliation`: same lookup and `JToken.DeepEquals` comparison; mismatch or cancellation after optimistic return → **`Debug.LogError`** (customize by editing this type or subclassing if needed).
- **`CloudCodeSingleFlightCallHandler`** — one in-flight inner invocation per **module** name.
- **Game wiring** — resolve `CloudCodeOptimisticHandlerRegistry` and call `Register<MyRequest>(handler)` during composition (or from a `RegisterBuildCallback` after `IRequestHandler<MyRequest>` is registered).

---

## Testing considerations

- Unit tests: fake baseline + optional fake optimistic handler; assert ordering and single-flight per module.
- Regression: object payload serializes to the same dictionary shape as before for LiveOps.

---

## Decision log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-03-31 | Orchestrator in `CloudCodeService`; deterministic use first | Start server `InvokeAsync` first; optimistic is not an `ICloudCodeCallHandler`. |
| 2026-03-31 | Mismatch → error + restart | Hard consistency guarantee. |
| 2026-03-31 | Single-flight per **module** | Avoid races; queue all calls in a module. |
| 2026-03-31 | `IRequestHandler<T>` + type match | Code registration; `GetType()` on payload. |
| 2026-03-31 | Keep `Task<T>` | No public surface change for optimism. |
| 2026-03-31 | Payload = `object`; internal `{ "request", payload }` | Enables type-based routing; keeps wire format. |
| 2026-03-31 | Retry independent of optimism | Simpler v1 policies. |
| 2026-03-31 | Post-return cancel → error | Fail-safe vs silent background. |

---

## Next steps (optional)

1. Add concrete **`IRequestHandler<T>`** implementations for specific DTOs in feature assemblies (or extend **`CloudCodeOptimisticOrchestrator`** for restart-on-mismatch behavior).

---

## References (local)

- `Assets/Packages/com.scaffold.cloudcode/Runtime/CloudCodeService.cs` — `CallEndpointAsync` and optimistic orchestration.
- `Assets/Packages/com.scaffold.cloudcode/Runtime/CloudCodeOptimisticOrchestrator.cs` — optimistic resolution and reconciliation.
- `Assets/Packages/com.scaffold.cloudcode/Runtime/ICloudCodeCallHandler.cs` — decorator contract.
- `Assets/Packages/com.scaffold.liveops/Runtime/LiveOpsService.cs` — typed module requests.
