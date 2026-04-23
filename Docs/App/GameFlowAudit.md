# Game Flow Audit

Cross-cutting audit of the Scaffold runtime architecture: execution flow, ownership, dependencies, and per-module critique.

---

## Execution Overview

This repository is a **framework-only** project. There is no concrete game scene or `GameManager` here — only the infrastructure that a game assembly subclasses and wires up. The de facto orchestration chain is:

```
AppFlowRoot (your subclass on root LifetimeScope, Unity Start())
  └─ AppFlowHost.InstallAllAsync(initialLayers)
       └─ For each IScopeLayer: PrepareAsync (optional) → child LifetimeScope → IAsyncInitializable wave
            └─ OnReadyAsync  ← hook to open the first view
```

Everything else (navigation, cloud, ads, scene loading) is a service called by game code after startup completes.

---

## Who Controls What

| Concern | Controller |
|---|---|
| Startup sequencing | `AppFlowRoot` / `AppFlowHost` |
| DI scope lifecycle | VContainer `LifetimeScope` stack (root + pushed child layers) |
| Async init ordering | Per-layer `IAsyncInitializable` wave (`IInLayerScheduler`, default parallel) |
| Top-layer resolve / inject | `ILayerResolver` (`LayerResolverProxy` bound to current top scope) |
| View stack | `NavigationController` |
| View transitions | `NavigationTransitions` |
| View loading | `NavigationProvider` (context views or Addressables) |
| ViewModel injection | `NavigationInjection` (via `ILayerResolver.Top.Inject`) |
| App-global events | `EventController` / `IEventBus` |
| View-local events | `ViewEvents` / `EventLedger<T>` (separate system in `com.scaffold.view`) |
| Additive scene loading | `SceneFlowService` |
| Bootstrap shell visibility | `SceneFlowBootstrapShell` (ref-counted) |
| UGS init + auth | `Ugs` |
| Cloud Code calls | `CloudCodeService` (handler chain) |
| Game data + module calls | `LiveOpsService` |
| Ad initialization | `AdManager.InitializeAds` (manually triggered by game code) |
| Domain state | `Store` (slice/mutator/subscription model) |

---

## Core Dependencies (Module Graph)

```
com.scaffold.appflow
  └─ VContainer

com.scaffold.events
  └─ (no Scaffold dependencies)

com.scaffold.addressables
  └─ Addressables SDK

com.scaffold.navigation
  └─ com.scaffold.events
  └─ com.scaffold.addressables

com.scaffold.sceneflow
  └─ com.scaffold.addressables

com.scaffold.ugs
  └─ Unity.Services.Core / Authentication

com.scaffold.cloudcode
  └─ UGS Cloud Code SDK
  └─ Newtonsoft.Json

com.scaffold.liveops
  └─ com.scaffold.cloudcode
  └─ GameModuleDTO (external assembly)
  └─ VContainer (IObjectResolver — service locator, see critique)

com.scaffold.ads
  └─ AdConfigurationSO (ScriptableObject)
  └─ Ad SDK (provider-specific, e.g. LevelPlay)

com.scaffold.states
  └─ com.scaffold.maps
  └─ com.scaffold.pooling

com.scaffold.mvvm / model / viewmodel / view
  └─ com.scaffold.navigation (INavigation binding)
```

---

## Module Breakdown

---

### `com.scaffold.appflow` — Startup orchestration

**What it does**

Owns stacked VContainer `LifetimeScope` **layers**: subclass `AppFlowRoot` on the root scope, return `IScopeLayer` instances from `GetInitialLayers()`, and use `AppFlowHost` to push/pop child scopes in order. Each layer runs an `IAsyncInitializable` wave after build; optional `ILayerPublisher` replays cross-layer registrations into descendant layers (see package README).

**Entry point:** `AppFlowRoot.Start()` — Unity `async void` lifecycle; constructs `AppFlowHost` and `InstallAllAsync`.

**Key classes**

| Class | Role |
|---|---|
| `AppFlowRoot` | Abstract `LifetimeScope`; registers `LayerResolverProxy` as `ILayerResolver`, `IAppFlowErrorHandler`, `IAppFlowProgress`; exposes `Progress` and `Errors` for bootstrap UI; subclasses supply initial layers |
| `AppFlowHost` | Push/pop API, init and dispose waves, `ILayerResolver` implementation |
| `LayerResolverProxy` | Binds to the current top `IObjectResolver` after each push/pop |
| `ILayerPublisher` | Optional cross-layer asset publishing into child builders |

**Note:** The legacy `com.scaffold.scope` package (`TwoScopeApplicationHost`, `CrossLayerObjectResolver`, graph-based `AsyncInitializationRunner`) has been removed from this repository.

---

### `com.scaffold.navigation` — View stack and transitions

**What it does**

Manages a push-down stack of views (`NavigationStack`). Each entry is a `NavigationPoint` pairing a `IViewController` (ViewModel) with an `IView` (MonoBehaviour). On `Open`, the provider resolves or Addressables-loads the view, runs middleware, binds the controller, then queues a transition. On `Return`/`Close`, the inverse happens.

**Entry point:** `NavigationController.Open<TController>(...)`.

**Key classes**

| Class | Role |
|---|---|
| `NavigationController` | Public `INavigation`; `Open`, `Close`, `Return` |
| `NavigationProvider` | Resolves `NavigationPoint` from context views or Addressables |
| `NavigationStack` | Ordered list of `NavigationPoint`s |
| `NavigationTransitions` | Queues and sequentially drains transitions; raises `IEventBus` events around each open/close |
| `NavigationMiddleware` | Runs `INavigationOpenHandler` implementations on open |
| `NavigationInjection` | `INavigationOpenHandler`; calls `ILayerResolver.Top.Inject(viewModel)` to wire DI into runtime-loaded ViewModels |

**What is not clear**

- `Open` calls `middleware.OnOpen` (injection) before `point.ViewModel.Bind(this)` (navigation reference). This ordering matters and is not documented.
- `ClosePoint` delegates to `Return()` if the closed point is the current one — closing the active view silently returns to the previous screen. Non-obvious.
- `closeCurrent` (parameter) and `NavigationOptions.CloseAllViews` were overlapping; prefer `NavigationOptions.StackPolicy` (`NavigationStackPolicy`) with legacy flags still supported when `StackPolicy` is `Push`.
- `TransitionHandler.Template` throws a plain `Exception` with a string comment. This is a landmine for anyone adding template-based transitions.
- `ExecuteDefaultTransitionCore` ends with `await Task.CompletedTask` — a no-op await that implies async work is happening when it is not.

**What I would improve**

- Replace `async void RunTransitions()` with a `Channel<ViewTransitionData>` or structured async queue for explicit cancellation and back-pressure.
- ~~Consolidate `closeCurrent` and `CloseAllViews` into a single `NavigationOptions` disposal policy.~~ Implemented as `NavigationStackPolicy` + `NavigationStackResolver`; keep call-site overloads for compatibility.
- Replace `throw new Exception("No handler for template transitions...")` with `throw new NotImplementedException(...)` and mark it as an intentional stub.
- Remove the `await Task.CompletedTask` lines.
- Document the middleware → bind ordering contract.

**What could be optimized**

- `NavigationProvider.FetchContextViews()` scans `GetComponentsInChildren<IView>(true)` once at construction. Views added dynamically after construction are silently missed.
- The `Queue<ViewTransitionData>` + `async void` drain loop is a sequential choke point for rapid navigation. A `Channel` would give better throughput and proper async back-pressure.

---

### `com.scaffold.events` — Global event bus

**What it does**

A type-keyed multicast delegate registry. `EventController` maps `Type → Action<ContextEvent>` and a parallel `Delegate → Action<ContextEvent>` lookup for removal. Used primarily by `NavigationTransitions` to broadcast lifecycle events (`BeforeViewOpenEvent`, `AfterViewOpenEvent`, etc.).

**Key classes**

| Class | Role |
|---|---|
| `IEventBus` | `AddListener<T>`, `RemoveListener<T>`, `Raise`, `Clear` |
| `EventController` | Concrete implementation; two `Dictionary` fields |
| `ContextEvent` | Base class all events must extend |

**What is not clear**

- The `ValidateState` null checks (`if (events == null)`) are dead code — both fields are `readonly` and set in the constructor, they can never be null.
- There are two event systems: `IEventBus` (app-global) and `ViewEvents`/`EventLedger<T>` (view-hierarchy-local, in `com.scaffold.view`). The naming is similar and a new developer can easily reach for the wrong one.
- No thread safety. The dictionaries are mutated from `AddListener`/`RemoveListener` and read from `Raise` with no guard.

**What I would improve**

- Remove the dead null checks. Replace with a static assertion or just trust the constructor.
- Rename or clearly document the boundary between `IEventBus` and `ViewEvents` — they serve very different scopes.
- Snapshot the delegate before invoking in `Raise` to guard against listeners being mutated during a raise.

**What could be optimized**

- `eventLookups` stores a `Delegate → Action<ContextEvent>` mapping for every listener, used only for removal. If listeners are rarely removed, this second dictionary is pure overhead per add. A typed wrapper with inline identity would reduce allocations.

---

### `com.scaffold.cloudcode` — Cloud Code client

**What it does**

Wraps Unity Cloud Code SDK calls behind a handler chain (decorator pattern). Chain in construction order:

```
SingleFlight → Retry → ResponseBodyLogging → Timeout → SdkCallHandler (actual UGS call)
```

Supports **optimistic responses**: if an `IRequestHandler<TResponse>` is registered for a `(requestType, responseType)` pair, it returns the optimistic result immediately and validates against the real server response in the background via fire-and-forget.

**Key classes**

| Class | Role |
|---|---|
| `ICloudCodeService` | `CallEndpointAsync<T>(module, endpoint, payload, ct)` |
| `CloudCodeService` | Builds handler chain; handles optimistic path |
| `CloudCodeSdkCallHandler` | Leaf; calls UGS SDK |
| `CloudCodeTimeoutCallHandler` | Wraps with a configurable timeout |
| `CloudCodeRetryCallHandler` | Retry with configurable policy |
| `CloudCodeResponseBodyLoggingCallHandler` | Logs raw response body |
| `CloudCodeSingleFlightCallHandler` | Deduplicates in-flight identical requests |
| `CloudCodeOptimisticHandlerRegistry` | Maps `(requestType, responseType) → IRequestHandler` |
| `CloudCodeErrorHandler` | Central error dispatch |

**What is not clear**

- `RunReconciliationInTheBackground` is `async void`. The reconciliation result (including `OptimisticReconciliationException`) is completely unobservable by the caller, which has already received an optimistic response and moved on.
- The payload wrapping `{ "request": payload }` in `WrapPayload` is an implicit convention. Nothing in the type system enforces that Cloud Code modules expect this shape.
- `CloudCodeService` is `internal sealed` — cannot be substituted in tests without going through the installer.

**What I would improve**

- Convert `RunReconciliationInTheBackground` from `async void` to a tracked `Task` surfacing exceptions to telemetry or a global error handler.
- Expose `ICloudCodeService` as the only test seam; make the handler chain constructable without the installer (factory method or builder pattern).
- Add a compile-time contract or at least an XML doc comment that documents the `{ "request": payload }` wrapping convention.

**What could be optimized**

- `Newtonsoft.Json` deserialization happens on every response. `System.Text.Json` (Unity 2021+) produces fewer allocations for common cases.
- `TryGetRegistryHandler` looks up by `GetType()` on the payload at call time. Pre-caching by open generic type at registration time would make the hot path cheaper.

---

### `com.scaffold.liveops` — Game data and module calls

**What it does**

Loads initial `GameData` from Cloud Code on startup (`IAsyncLayerInitializable`), caches it, and exposes it via `GetModuleData<T>()`. Also provides the high-level API for all subsequent server calls (`CallAsync<TResponse>`). After each call, `ModuleResponseDispatchService` walks the nested `ModuleResponse` tree and dispatches each child to registered `IResponseHandler` implementations.

**Key classes**

| Class | Role |
|---|---|
| `ILiveOpsService` | `GetModuleData<T>()`, `CallAsync<TResponse>(request, ct)` |
| `LiveOpsService` | Implements both; owns `GameData` cache |
| `ModuleResponseDispatchService` | Iterates `ModuleResponse.Responses`; matches each node to `IResponseHandler` by type |
| `IResponseHandler` | `HandledResponseType`, `Handle(ModuleResponse)` |

**What is not clear**

- `ModuleResponseDispatchService` resolves `IEnumerable<IResponseHandler>` from the DI container **on every `CallAsync` invocation** — a container lookup per server round-trip.
- `GetModuleData<T>()` returns `null` silently if startup hasn't finished or if the type is absent. Callers cannot distinguish "not initialized" from "no data for this type".
- `LiveOpsService` takes `IObjectResolver` directly — a domain service depending on the container is a service-locator anti-pattern.
- `LoadInitialGameDataAsync` calls `CallAsync` which triggers `DispatchNestedResponses` — response handlers fire during startup initialization, potentially triggering view updates before the app is ready.

**What I would improve**

- Replace `IObjectResolver` with `IEnumerable<IResponseHandler>` injected at construction time.
- Cache the resolved `IResponseHandler[]` — do not resolve per call.
- Add an `IsInitialized` / `LoadingState` flag; make `GetModuleData<T>()` throw or return a `Result<T>` rather than silently returning `null`.
- Document (or guard) that response handlers can fire during startup initialization.

**What could be optimized**

- `DispatchForNode` iterates all handlers per node — O(nodes × handlers). Precompute a `Dictionary<Type, IResponseHandler[]>` at construction so dispatch is O(1) per node type.
- The nested response tree walk could be flattened to a single BFS pass to avoid redundant iteration for deep trees.

---

### `com.scaffold.ugs` — Unity Gaming Services init

**What it does**

One class, one job: `UnityServices.InitializeAsync()` + anonymous `SignInAnonymouslyAsync()`. Runs as `IAsyncLayerInitializable` after the main scope's DI graph is fully built.

**Key classes**

| Class | Role |
|---|---|
| `Ugs` | `InitializeAsync` → init UGS + sign in anonymously |

**What is not clear**

- No handling for `AuthenticationException` (network failure, already-signed-in race). A clear failure event on `IEventBus` or retry strategy is absent.
- Anonymous sign-in is hardcoded. No hook to switch to platform auth (Game Center, Google Play, etc.) without replacing the entire class.

**What I would improve**

- Extract an `IAuthenticationStrategy` interface so anonymous sign-in is the default but platform strategies can be injected.
- Add explicit `try/catch` with a retry policy or a startup error signal for auth failures.

---

### `com.scaffold.states` — Immutable slice store

**What it does**

A Redux-inspired state store. State is organized into typed `Slice`s keyed by `(IReference, Type)`. Mutations go through `Mutator<TState>` functions applied via a pooled `MutatorRunner` that works on an in-memory `Scratchpad` (overlay snapshot) and commits atomically. Subscriptions are type/reference filtered and fire on commit.

**Key classes**

| Class | Role |
|---|---|
| `Store` | Top-level API: `Execute`, `ExecuteMutator`, `Get`, `GetAll`, `Subscribe`, `RegisterSlice`, snapshots |
| `Slice` / `AggregateSlice` | Unit of state storage; canonical vs. computed |
| `MutatorRunner` | Applies mutators to `Scratchpad`, then commits to `Store` |
| `Scratchpad` / `IStoreScratchpad` | Ephemeral overlay; lets mutators see mid-batch pending state |
| `MutatorRegistry` | `Dictionary<Type, IPayloadMutatorBinding[]>` dispatch table for `Execute(payload)` |
| `StateEventHandler` | Subscription registry and notification dispatch |
| `Snapshot` | Serializable point-in-time copy of all slice states |

**What is not clear**

- `Store` holds four `List` fields (`mapSliceBuffer`, `aggregateSliceBuffer`, `sliceBuffer`, `pruneBuffer`) used as scratch buffers. Their usage is interspersed across methods and naming does not make their role immediately clear.
- `Store.GetAll<TState>()` uses `sliceBuffer` — a shared instance field. If called recursively from a subscription handler, the buffer is corrupted.
- `AggregateSlice.BuildForScope(IStoreScratchpad)` can be called during a batch mutation's read phase. If the aggregate depends on slices being mutated in the same batch, ordering is non-deterministic and undocumented.
- `Execute<TPayload>` logs `Debug.LogWarning` for unregistered payload types — silent in production builds, easy to miss misconfigured mutators.

**What I would improve**

- Document clearly that `Store` is not thread-safe and must only be called from the main thread.
- Replace `Debug.LogWarning` for unregistered payloads with a `Debug.LogError` or `InvalidOperationException` in non-production builds (e.g. `#if UNITY_EDITOR || DEVELOPMENT_BUILD`).
- Rename the scratch buffer fields with more descriptive names (`_getAllScratchMap`, `_getAllScratchAggregates`, etc.) or inline them as locals in the methods that use them.
- Document the `AggregateSlice.BuildForScope` ordering contract relative to in-batch mutations.

**What could be optimized**

- The `MutatorRunner` pool has `initialSize: 2`. For `ExecuteBatch` called in a single-threaded loop, only one runner is ever live at a time — the second slot is unused. The pool is still valuable for avoiding allocation; the initial size can stay at 1.
- `Snapshot` (overlay in `Scratchpad`) uses a full dictionary-backed structure. For typical mutations that touch 1–2 slices, a small fixed-size inline buffer (e.g. struct array up to 8 entries, spilling to dictionary) would reduce allocation in the common case.
- `FillSlices` populates `sliceBuffer` completely before `EnumerateSliceStates` yields — defeating lazy enumeration. A two-pass `yield` over both `map` and `aggregates` would avoid the intermediate list allocation.

---

### `com.scaffold.sceneflow` — Additive scene loading

**What it does**

Wraps Addressables scene operations for additive load/unload. Tracks active loads by `Guid`. Reference-counts a `SceneFlowBootstrapShell` (hides bootstrap camera/AudioListener/EventSystem) so the shell deactivates only when all managed loads are unloaded.

**Key classes**

| Class | Role |
|---|---|
| `ISceneFlowService` | `LoadAdditiveAsync`, `UnloadAsync` |
| `SceneFlowService` | Implementation with `Guid`-keyed `Dictionary<Guid, SceneFlowLoadRecord>` |
| `SceneFlowBootstrapShell` | MonoBehaviour; `SetAdditiveContentActive(bool)` |
| `SceneFlowLoadResult` | Token returned from load; used to call `UnloadAsync` |
| `SceneFlowLoadOptions` | `ManageBootstrapShell` flag |

**What is not clear**

- `shellManagedLoadCount` is a plain `int` with no thread safety. Concurrent calls to `LoadAdditiveAsync` (valid in a game that loads multiple additive scenes simultaneously) could corrupt the count.
- `UnloadAsync` throws `InvalidOperationException` with the message "unload may have already completed" for an unknown `LoadId`. This is misleading — the real cause is equally likely to be a programming error (wrong token).
- Nothing prevents calling `UnloadAsync` twice with the same `SceneFlowLoadResult` token. The second call throws, but the error message is confusing.

**What I would improve**

- Remove the load record from `activeLoads` at the start of `UnloadAsync` (before the async operation) to make double-unload idempotent rather than exception-throwing.
- Use `Interlocked.Increment`/`Decrement` on `shellManagedLoadCount` if concurrent loads are expected, or add a single-threaded assertion.
- Improve the `InvalidOperationException` message: distinguish "already unloaded" from "unknown token / programming error".

**What could be optimized**

- The error-path `ReleaseHandleIfNeeded` calls `Addressables.Release` directly — bypassing `IAddressablesSceneOperations`. If the operations interface is replaced in tests, this path escapes the abstraction.
- For games tracking many simultaneous scenes, `Guid` hashing is fast but always boxes the struct. A custom `IEqualityComparer<Guid>` avoids boxing entirely.

---

### `com.scaffold.ads` / `com.scaffold.ads.levelplay` — Ad management

**What it does**

Wraps ad network SDKs (LevelPlay via `LevelPlayInstaller`) behind three typed managers for rewarded, interstitial, and banner ads. `AdManager` is the facade that creates an `IAdProvider` from `AdConfigurationSO` and wires services to managers. Ad placements are identified by `AdPlacementKeySO` ScriptableObjects. Reward validation can be done via HTTP or `ILiveOpsService`.

**Key classes**

| Class | Role |
|---|---|
| `AdManager` | Facade; `InitializeAds(userId, rewardClient)` bootstraps provider and wires managers |
| `RewardedAdManager` | Shows rewarded ads; calls `IRewardEndpointClient` for server-side reward validation |
| `InterstitialAdManager` | Shows interstitial ads |
| `BannerAdManager` | Shows/hides banner ads |
| `IAdProvider` | SDK abstraction; created from `AdConfigurationSO.CreateProvider()` |
| `AdConfigurationSO` | ScriptableObject; placement configs + provider factory |
| `IRewardEndpointClient` | Reward validation — two impls: `HttpRewardEndpointClient`, `LiveOpsRewardEndpointClient` |

**What is not clear**

- `AdManager.InitializeAds` is `async void`. If `adProvider.Initialize(userId)` throws, the exception is unobserved.
- `AdManager` is registered as a VContainer singleton but `InitializeAds` must be called manually — it is the only major service not integrated with the startup initialization graph. Nothing enforces that it has been called before the sub-managers are used.
- `SetRewardEndpointClient` is public and can be called after initialization, replacing the reward client mid-session. The intended use case is undocumented.
- `adProvider = adConfiguration.CreateProvider()` runs in the constructor — a side effect at DI container build time.

**What I would improve**

- Make `AdManager` implement `IAsyncInitializable` and move initialization into `InitializeAsync`, integrating it with the startup graph.
- Replace `async void InitializeAds` with a tracked `Task InitializeAdsAsync`.
- Add a guard in the sub-managers that throws a clear error if used before `Initialize` is called.
- Move `adProvider = adConfiguration.CreateProvider()` out of the constructor and into the initializer to avoid side effects at DI build time.

**What could be optimized**

- The initialization ceremony (`Initialize(service, config)` called per-manager) is imperative wiring that could be replaced by constructor injection once `IAdProvider` services are resolved asynchronously.

---

### `com.scaffold.mvvm` / `model` / `viewmodel` / `view` — MVVM stack

**What it does**

Standard MVVM binding stack. `Model` is the data layer base. `ViewModel` / `IViewModel` is the binding surface. `View<T>` is a MonoBehaviour that binds to a `IViewModel`. Navigation integrates through `IViewController` (extended by `ViewModel`) and `Bind(INavigation)`.

**What is not clear**

- The split across four packages (`mvvm`, `model`, `viewmodel`, `view`) is architecturally clean but operationally fragmented — understanding the full binding contract requires reading four READMEs.
- `View<T>` lifecycle (`Open`, `Close`, `Hide`, `Focus`) is driven by `NavigationTransitions`, not by the view itself. Views must never self-close without going through `INavigation`, but this constraint is not enforced.

**What I would improve**

- Add a single "MVVM in Scaffold" overview document synthesizing all four packages.
- Add an analyzer rule or a clear assertion that prevents `View<T>` subclasses from calling `Close()` directly (bypassing navigation).

---

### `com.scaffold.entities` — Entity/component model

**What it does**

Flyweight entity model. `EntityInstance<TDefinition>` pairs a runtime entity with a `ScriptableObject`-based definition. `EntityBehaviour` and `EntityBehaviorRunner` are MonoBehaviours that attach entity components to GameObjects.

**What is not clear**

- No declared dependency on other Scaffold assemblies — how entities interact with the state store or LiveOps data is entirely up to the game assembly. The integration boundary is implicit.
- The distinction between `EntityBehaviour` and `EntityBehaviorRunner` is not immediately clear from names alone.

**What I would improve**

- Add a usage example in the README showing the full lifecycle: definition SO → `EntityInstance` creation → `EntityBehaviour` on a GameObject → state integration.

---

## Systemic Issues

These cross-cut multiple modules and are worth addressing as a group.

### 1. Two async init contracts with no clear guidance

`IAsyncInitializable` (topo-graph, per-scope) and `IAsyncLayerInitializable` (legacy parallel pass, post-main-scope) exist side by side with no documentation at the call site explaining which to use and when. New developers will pick randomly. The legacy path bypasses dependency ordering entirely.

### 2. No startup failure recovery

If any initializer throws (UGS network failure, Cloud Code timeout), the game lands in an indeterminate state: `startupCompleted` stays `false`, the exception is logged, nothing more happens. There is no retry screen, error state, or fallback. An `IStartupErrorHandler` or at minimum a `StartupFailed` event on `IEventBus` is missing.

### 3. `AdManager.InitializeAds` is manually triggered

`AdManager` is the only major service not integrated with the async initialization graph. It is easy to call it too early, forget it entirely, or call it before `Ugs` has signed in.

### 4. Service-locator in `LiveOpsService`

`IObjectResolver` is injected into a domain service and used to resolve `IResponseHandler[]` at call time. This is unnecessary — the handlers can be injected as `IEnumerable<IResponseHandler>` at construction and cached.

### 5. `async void` in hot paths

`AppFlowRoot.Start`, `AdManager.InitializeAds`, and `NavigationTransitions.RunTransitions` are all `async void`. Unobserved exceptions in these methods are swallowed or only surface via `Debug.LogException`. A global `IAsyncExceptionHandler` or structured exception surfacing strategy would make failures visible and actionable.
