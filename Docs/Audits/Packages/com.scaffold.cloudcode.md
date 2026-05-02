# Audit: com.scaffold.cloudcode

## 1. Summary & Verdict

`com.scaffold.cloudcode` is a thin Unity Cloud Code wrapper that adds (a) a chain-of-responsibility around `ICloudCodeService.CallModuleEndpointAsync`, (b) a generics-driven optimistic-update registry, and (c) a deserialization layer with `Newtonsoft.Json`. The shape is good — handlers are composed, the public surface is one interface, and the optimistic registry is keyed by `(TRequest, TResponse)` types. The interior, however, leaks weakness: the public `CallEndpointAsync<T>` takes `string module, string endpoint, object payload`, so callers re-stringify the module/function on every call site. That is the exact contract you said the architect wants compile-time-safe. Tests prove the optimistic path works, including `IObjectResolver` discovery.

Verdict: **keep, refactor**. The architecture is right. Replace the stringly-typed entry point with a typed-request overload (paralleling `LiveOps.CallAsync<TRequest,TResponse>(ModuleRequest<TResponse>)`), drop the duplicated guard clauses, and trim the `BuildCallHandlerChain` boilerplate to a registered `IEnumerable<ICloudCodeCallHandler>` so the chain composes from DI instead of `new`-statements.

## 2. Structure

```
com.scaffold.cloudcode/
  package.json                                       ; deps: com.unity.services.cloudcode 2.10.2, Newtonsoft.Json
  Container/
    CloudCodeInstaller.cs                            ; VContainer installer, registers handler chain + optimistic registry
  Runtime/
    AssemblyInfo.cs                                  ; InternalsVisibleTo Container, Tests
    CloudCodeService.cs                              ; the service
    CloudCodeSettings.cs                             ; ScriptableObject (retries, timeout, logging)
    CloudCodeErrorHandler.cs                         ; virtual no-op extension point
    ICloudCodeService.cs                             ; public stringly-typed entry
    Handlers/
      ICloudCodeCallHandler.cs                       ; chain link (string -> Task<string>)
      CloudCodeSdkCallHandler.cs                     ; terminal: real Unity SDK
      CloudCodeRetryCallHandler.cs                   ; rate-limit retry, exponential backoff
      CloudCodeTimeoutCallHandler.cs                 ; per-attempt Task.WhenAny timeout
      CloudCodeResponseBodyLoggingCallHandler.cs     ; opt-in raw response logging
      CloudCodeSingleFlightCallHandler.cs            ; per-module SemaphoreSlim
    Optimistic/
      IRequestHandler.cs                             ; IRequestHandler / <T> / <TReq,TRes> default-impl bridges
      IOptimisticCloudCodeHandler.cs                 ; discoverable marker exposing CLR types
      OptimisticHandlerBase.cs                       ; base for typed handlers
      CloudCodeOptimisticHandlerRegistry.cs          ; explicit-register + container-discovery
      OptimisticReconciliationException.cs           ; exception with both responses (unused in code path)
  Tests/
    CloudCodeServiceOptimisticTests.cs               ; covers optimistic path, validate failure, DI discovery
```

No `Backend~/`. The package is purely client-side; backend DTOs live in the consumer feature packages (e.g. `com.scaffold.directpush/Backend~`).

## 3. What's Good

- **Chain of responsibility is the right shape.** `BuildCallHandlerChain` (`CloudCodeService.cs:41-48`) wires SingleFlight → Retry → Logging → Timeout → SDK. Each link is a single-method `ICloudCodeCallHandler` (`Handlers/ICloudCodeCallHandler.cs:7-10`); ordering is explicit; it is trivial to insert a new link.
- **Optimistic update support is real and generic.** `IRequestHandler<TRequest,TResponse>` (`Runtime/Optimistic/IRequestHandler.cs:17-38`) uses C# 8 default-interface implementations to bridge the `object`-typed registry and the typed handler — no reflection in the hot path. The dispatch pattern (return optimistic immediately, await server, run `Validate` on a fire-and-forget `async void` with a `try/catch` to a `CloudCodeErrorHandler`) is exactly what an authoritative-server game wants. See `CloudCodeService.cs:50-98`.
- **Discoverable handlers via VContainer.** `CloudCodeOptimisticHandlerRegistry` (`Runtime/Optimistic/CloudCodeOptimisticHandlerRegistry.cs:66-119`) lazily resolves `IEnumerable<IOptimisticCloudCodeHandler>` from the container on first miss, then caches by `(Type Request, Type Response)`. Single allocation per request type. Test in `Tests/CloudCodeServiceOptimisticTests.cs:116-135` proves it.
- **Single-flight gate per module.** `CloudCodeSingleFlightCallHandler.cs:16-30` serializes calls per module via `ConcurrentDictionary<string, SemaphoreSlim>`. Prevents duplicate write storms during reconnect.
- **Settings are a ScriptableObject with a graceful fallback.** `CloudCodeSettings.CreateDefault` (`Runtime/CloudCodeSettings.cs:47-51`) tries `Resources.Load` then falls back to a default instance. No Unity dependency leaks past the boundary.
- **Tests cover the contract.** Optimistic-immediate, validate-throws, no-handler, module mismatch, and DI discovery all have tests (`Tests/CloudCodeServiceOptimisticTests.cs`).

## 4. Issues / Smells

### 4.1 Stringly-typed public API — the central weakness

`ICloudCodeService.CallEndpointAsync<T>(string module, string endpoint, object payload, CancellationToken)` (`Runtime/ICloudCodeService.cs:8`).

Every call site must repeat `"LiveOps", "GameApi"`-style strings; rename refactors are silent breakage; no compile-time match between request type and response type. The optimistic registry is keyed by `(TRequest, TResponse)` (`CloudCodeOptimisticHandlerRegistry.cs:15`), so the type information already exists — it just is not exposed at the call site.

`LiveOpsService` already runs the typed pattern correctly (`Runtime/LiveOpsService.cs:88-91`):

```csharp
GameApiEnvelopeRequest envelope = new GameApiEnvelopeRequest
{
    RequestKey = KeyOf.WireOf(request),
    Payload = JObject.FromObject(request, ...)
};
Task<GameApiEnvelopeResponse> serverTask = cloudCodeService.CallEndpointAsync<GameApiEnvelopeResponse>(
    request.ModuleName, "GameApi", envelope, cancellationToken);
```

The same idea belongs inside CloudCode itself for direct (non-GameApi) endpoints.

### 4.2 Redundant null guards repeated everywhere

The architect's rubric: guard at entry only. The package guards in every constructor:

- `CloudCodeService.cs:14-32` — four `ArgumentNullException` checks in the constructor; the resolver guarantees these.
- `CloudCodeResponseBodyLoggingCallHandler.cs:13-14`, `CloudCodeRetryCallHandler.cs:13-15`, `CloudCodeTimeoutCallHandler.cs:11-13`, `CloudCodeSdkCallHandler.cs:14`, `CloudCodeSingleFlightCallHandler.cs:11`, `CloudCodeInstaller.cs:24-27`. Internal handlers all re-validate. They are constructed in one place (the chain factory) — these guards are guaranteed dead code.
- `CloudCodeService.cs:111-122` — `ValidateModuleEndpoint` checks `IsNullOrWhiteSpace` on every call. If the typed-request API is added, both checks vanish.

### 4.3 Default values that hide errors

`CloudCodeErrorHandler.Handle` (`Runtime/CloudCodeErrorHandler.cs:7-9`) is a `virtual` no-op. The optimistic reconciliation path swallows server exceptions silently unless the consumer remembers to override:

```csharp
catch (Exception ex)
{
    cloudCodeErrorHandler.Handle(ex, module, endpoint, payload, optimisticResponse);
}
```
(`CloudCodeService.cs:94-97`)

The architect "hates default values that hide errors". This is exactly that: a swallowed exception with a default no-op handler. Make it `abstract` or change the default to `Debug.LogException` so a missing override fails loud.

### 4.4 `async void` reconciliation

`RunReconciliationInTheBackground<T>` (`CloudCodeService.cs:87-98`) is `async void`. Acceptable here because the body is wrapped in `try/catch` and the only side effect is calling the error handler — but `async void` makes the optimistic call non-cancellable from the caller's CTS once the optimistic response is returned. Consider a `Task` returned through a fire-and-forget hook the consumer can await in tests; today, tests rely on `TaskCompletionSource` injected through the validate callback (`Tests/CloudCodeServiceOptimisticTests.cs:25-34`).

### 4.5 `WrapPayload` is an unconditional `Dictionary` allocation

`WrapPayload` (`CloudCodeService.cs:77-80`):

```csharp
return payload == null ? new() : new() { { "request", payload } };
```

Every call allocates a new `Dictionary<string, object>`. Cheap, but the wrapping convention `{ "request": ... }` is hard-coded and hidden. It is part of the wire contract and should be either documented or pushed down to the SDK handler.

### 4.6 `CloudCodeSettings` mixes Unity inspector with pure-C# config

`Runtime/CloudCodeSettings.cs` is a `ScriptableObject` with `[SerializeField]`/`[Tooltip]` plus a static factory. Settings are read on every `InvokeAsync` (`CloudCodeRetryCallHandler.cs:23,51,52`, `CloudCodeTimeoutCallHandler.cs:21`, `CloudCodeResponseBodyLoggingCallHandler.cs:23`). Per the rubric (Unity vs pure C# at boundaries), the runtime should depend on a record/POCO (`CloudCodeOptions`) and the `ScriptableObject` should map to it once at install time.

### 4.7 `OptimisticReconciliationException` is dead

Defined in `Runtime/Optimistic/OptimisticReconciliationException.cs` but never thrown by the package. Either thread it through `Validate` (rename `Validate` to `Reconcile` and have the convention be `throw new OptimisticReconciliationException(...)` on mismatch), or delete it.

### 4.8 `ICloudCodeCallHandler` chain is hand-built

`BuildCallHandlerChain` (`CloudCodeService.cs:41-48`) news up four wrappers in a fixed order. To insert a metric/tracing handler from a consumer, you have to fork the package. The chain should compose from DI:

```csharp
builder.Register<CloudCodeRetryCallHandler>(Lifetime.Singleton).As<ICloudCodeCallHandler>();
// + ordered enumeration
```

This trades two lines of boilerplate for a real extension point — and "main entry points where we know things will keep changing" matches your abstraction rule.

### 4.9 Module-level lock is per-module, not per-endpoint

`CloudCodeSingleFlightCallHandler.cs:20` keys the gate on `module` only. Two unrelated endpoints in the same module serialize. For the LiveOps module that fans through `GameApi`, that may be intentional, but the comment is missing. Document or change to `(module, endpoint)`.

### 4.10 Retry handler swallows the last attempt's exception type

`CloudCodeRetryCallHandler.cs:32-38` only catches `CloudCodeRateLimitedException`. After max attempts, the loop falls out and throws `InvalidOperationException("Cloud Code retry loop exited without a successful response.")`. That hides the original rate-limit cause. Throw the last `CloudCodeRateLimitedException` (or wrap it) so callers can reason about the failure.

### 4.11 Newtonsoft `TypeNameHandling.Auto` is risky without a binder

`CloudCodeService.cs:24-28` uses `TypeNameHandling.Auto` but no `SerializationBinder`. The LiveOps DTO layer ships exactly such a binder (`Backend~/Deploy/Core/LiveOps.DTO/Json/CrossPlatformTypeBinder.cs`); this package should reuse it (or expose a settings hook). `TypeNameHandling.Auto` without an allowlist is a well-known deserialization risk vector.

## 5. Suggested Before/After Snippets

### 5.1 Typed entry point

Before (`Runtime/ICloudCodeService.cs`):

```csharp
public interface ICloudCodeService
{
    Task<T> CallEndpointAsync<T>(
        string module,
        string endpoint,
        object payload = null,
        CancellationToken cancellationToken = default);
}
```

After:

```csharp
public interface ICloudCodeRequest<TResponse> { string Module { get; } string Endpoint { get; } }

public interface ICloudCodeService
{
    Task<TResponse> CallAsync<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class, ICloudCodeRequest<TResponse>;

    // String overload preserved as internal/legacy for the LiveOps envelope path only.
    internal Task<TResponse> CallRawAsync<TResponse>(
        string module, string endpoint, object payload, CancellationToken ct);
}
```

This compiles a request/response pair to one symbol, removes `ValidateModuleEndpoint` entirely, and lets the optimistic registry key on `TRequest` directly without `request.GetType()`.

### 5.2 Drop redundant constructor guards

Before (`CloudCodeService.cs:12-33`, ~20 lines of guards). After: keep one guard at the public entry, remove the rest. VContainer will throw on missing dependencies anyway.

```csharp
internal CloudCodeService(
    CloudCodeSettings settings,
    CloudCodeSdkCallHandler sdkCallHandler,
    CloudCodeOptimisticHandlerRegistry optimisticRegistry,
    CloudCodeErrorHandler cloudCodeErrorHandler)
{
    this.settings = settings;
    this.optimisticRegistry = optimisticRegistry;
    this.cloudCodeErrorHandler = cloudCodeErrorHandler;
    callHandler = BuildCallHandlerChain(sdkCallHandler);
    jsonSettings = BuildJsonSettings();
}
```

### 5.3 Fail-loud default error handler

Before (`CloudCodeErrorHandler.cs`):

```csharp
public virtual void Handle(...) { }
```

After:

```csharp
public virtual void Handle(Exception exception, string module, string endpoint, object requestPayload, object optimisticResponseOrNull)
{
    UnityEngine.Debug.LogException(new InvalidOperationException(
        $"[CloudCode] Unhandled error on {module}/{endpoint} (optimistic={optimisticResponseOrNull?.GetType().Name ?? "none"}).",
        exception));
}
```

### 5.4 DI-composed handler chain

```csharp
public sealed class CloudCodeInstaller : IInstaller
{
    public void Install(IContainerBuilder builder)
    {
        builder.RegisterInstance(CloudCodeSettings.CreateDefault());
        builder.Register<CloudCodeSdkCallHandler>(Lifetime.Singleton);
        builder.Register<CloudCodeOptimisticHandlerRegistry>(Lifetime.Singleton);
        builder.Register<CloudCodeErrorHandler>(Lifetime.Singleton);
        // ordered chain, last wins (innermost = first registered after the SDK)
        builder.Register<CloudCodeTimeoutCallHandler>(Lifetime.Singleton).As<ICloudCodeCallHandler>();
        builder.Register<CloudCodeResponseBodyLoggingCallHandler>(Lifetime.Singleton).As<ICloudCodeCallHandler>();
        builder.Register<CloudCodeRetryCallHandler>(Lifetime.Singleton).As<ICloudCodeCallHandler>();
        builder.Register<CloudCodeSingleFlightCallHandler>(Lifetime.Singleton).As<ICloudCodeCallHandler>();
        builder.Register<ICloudCodeService, CloudCodeService>(Lifetime.Singleton);
    }
}
```

`CloudCodeService` then receives `IEnumerable<ICloudCodeCallHandler>` and folds them, with `CloudCodeSdkCallHandler` as the terminal.

## 6. Easy Wins

1. Make `CloudCodeErrorHandler.Handle` log by default (`Runtime/CloudCodeErrorHandler.cs:7`).
2. Delete duplicated `ArgumentNullException`s in handlers (`Handlers/CloudCodeRetryCallHandler.cs:13-15`, `CloudCodeTimeoutCallHandler.cs:11-13`, `CloudCodeResponseBodyLoggingCallHandler.cs:13-14`, `CloudCodeSdkCallHandler.cs:14`, `CloudCodeSingleFlightCallHandler.cs:11`, `CloudCodeInstaller.cs:24-27`).
3. Remove `ValidateModuleEndpoint` once a typed-request API exists (`CloudCodeService.cs:111-122`).
4. Throw the original `CloudCodeRateLimitedException` after retries, not a fresh `InvalidOperationException` (`CloudCodeRetryCallHandler.cs:38`).
5. Either delete `OptimisticReconciliationException` or have `OptimisticHandlerBase.Validate` throw it on mismatch by convention.
6. Add a `SerializationBinder` to the JSON settings (`CloudCodeService.cs:24-28`) — reuse `LiveOps.DTO.Json.CrossPlatformTypeBinder`.
7. Document the `{ "request": ... }` payload-wrap convention or fold it into the SDK handler (`CloudCodeService.cs:77-80`).
8. Make `CloudCodeSingleFlightCallHandler` keying explicit: `(module, endpoint)` or comment why module-only is intentional.

## 7. Bigger Refactors

- **Typed RPC.** Generate handler stubs from a DTO assembly attribute (mirroring how LiveOps already does `[LiveOpsKey]`/`KeyOf<T>` and a source generator). The CloudCode package should consume the same `Backend~`-shipped DTO assembly the server compiles, so request/response types are literally identical bytes on both sides. Patterns to crib: gRPC-Web typed clients, MagicOnion `IService<T>`, and Firebase Functions Callable typed wrappers.
- **Cancellation.** All public methods accept `CancellationToken`, and the chain mostly threads it. But `CloudCodeSdkCallHandler.InvokeAsync` (`Handlers/CloudCodeSdkCallHandler.cs:20-24`) passes the token only via the early `ThrowIfCancellationRequested`; `Unity.Services.CloudCode.ICloudCodeService.CallModuleEndpointAsync` does not take a CT. Wrap with a `Task.WhenAny(call, Task.Delay(-1, ct))` like the timeout handler already does. Today, a cancelled token after the SDK call has started will not interrupt the call.
- **Settings as POCO.** Promote a `CloudCodeOptions` record and let `CloudCodeSettings : ScriptableObject` map to it. Runtime should never see a Unity object.
- **Source-generated registration.** A `[CloudCodeRequest(module, endpoint)]` attribute could feed a generator that registers `(TRequest, TResponse) -> (module, endpoint)` mappings, eliminating `request.ModuleName/FunctionName` virtuals on every type and matching the LiveOps `[LiveOpsKey]` pattern.

## 8. Organization & Docs

- No `README.md` for `com.scaffold.cloudcode`. Every other audited package has one. Add a TL;DR + the wire contract (`{ "request": ... }`) + the optimistic-handler example.
- `Tests/` only covers `CloudCodeService` paths. There are no tests for `CloudCodeRetryCallHandler` retry/backoff math, `CloudCodeTimeoutCallHandler` timeout, or `CloudCodeSingleFlightCallHandler` ordering. These are deterministic and trivially testable.
- `OptimisticHandlerBase.cs:6` and `IOptimisticCloudCodeHandler.cs:6` are tagged `// sample:` — looks like leftover scaffolding markers; either turn into `<summary>` XML docs or remove.
- `package.json` is missing an explicit dependency on VContainer despite the installer using it directly (`Container/CloudCodeInstaller.cs:3-4`). Add `jp.hadashikick.vcontainer`.
- `Runtime/Handlers/` and `Runtime/Optimistic/` partition is clean. Keep.

### References (Unity Cloud Code patterns)

- Unity Cloud Code C# Modules — `ICloudCodeService.CallModuleEndpointAsync` (typed `<T>` overload exists in 2.10+; the package uses the string overload and deserializes itself, which is fine, but typed overload would remove `DeserializeResponse`): https://docs.unity.com/ugs/manual/cloud-code/manual/modules
- gRPC-Web typed client pattern (request-typed method per RPC, code-generated): https://grpc.io/docs/platforms/web/
- MagicOnion `IService<T>` typed RPC for Unity: https://github.com/Cysharp/MagicOnion
- Firebase Functions Callable — typed payload/response with shared TS types: https://firebase.google.com/docs/functions/callable
- PlayFab Cloud Script — older pattern; `HandlerName` is a string. The Scaffold package pattern (typed via attribute + generator) is strictly better than PlayFab's, and parity with MagicOnion/gRPC-Web is achievable with the source generator already used by LiveOps.
