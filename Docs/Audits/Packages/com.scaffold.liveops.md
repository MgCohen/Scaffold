# Audit: com.scaffold.liveops

## 1. Summary & Verdict

`com.scaffold.liveops` is the largest and most ambitious package in this audit and the one that most clearly *gets* the architect's rubric. It defines a typed RPC envelope (`GameApiEnvelopeRequest/Response`) over Cloud Code, dispatches per-request via a registry built from a source generator (`LiveOpsManifest.Entries`), and supplies a typed key system (`[LiveOpsKey]` + `KeyOf<T>`) that the cache, request router and module bootstrapper all consume. The optimistic-update path forwards through `CloudCodeOptimisticHandlerRegistry` (the same one as `com.scaffold.cloudcode`) without re-implementing it. The backend (`Backend~/Deploy/`) is genuinely well-organized: `Core/LiveOps.DTO`, `Core/LiveOps.Core`, host `LiveOps/`, and per-feature `Scaffold/<Feature>/` trees that get merged into a single deploy solution by `LiveOpsBackendInstall`.

The weak points are concentrated in three places: (a) the `IGameModuleData` payload pipeline relies on Newtonsoft `TypeNameHandling.Auto`, the merge-not-typed `Responses` list on `ModuleResponse`, and `JObject.FromObject` round-trips on the wire; (b) `LiveOpsService.GetModuleData<T>` returns `null` on miss and `data` on `GameClientModuleBase<T>` is a `protected` mutable field set silently to whatever-was-found (fail-loud rule violated); (c) the editor tooling (`LiveOpsBackendInstall`, `LiveOpsBackendDeploy`, `LiveOpsBackendRefresh`) is the largest body of code in the package and could move to its own internal editor-tools assembly to keep the runtime API discoverable.

Verdict: **keep, refactor**. Architecture is right. Tighten the typed contracts at the seams (envelope, module data, response dispatch), make the bootstrap fail-loud, slim editor tooling.

## 2. Structure

```
com.scaffold.liveops/
  package.json                                       ; deps: com.scaffold.cloudcode, com.scaffold.appflow
  README.md                                          ; long, accurate
  LiveOps.ccmr                                       ; UGS Cloud Code module ref pointing at LiveOps.Deploy.sln
  Container/
    LiveOpsInstaller.cs                              ; LiveOpsService -> ILiveOpsService + IAsyncInitializable
    LiveOpsOptimisticRegistrationExtensions.cs       ; helper for IOptimisticCloudCodeHandler registration
  Runtime/
    AssemblyInfo.cs                                  ; InternalsVisibleTo Container, Tests
    ILiveOpsService.cs                               ; CallAsync<TResponse>(ModuleRequest<TResponse>) + GetModuleData<T>
    LiveOpsService.cs                                ; envelope build, optimistic, nested-response dispatch
    ModuleResponseDispatchService.cs                 ; resolves IResponseHandler from ILayerResolver, caches
    IResponseHandler.cs / IResponseHandlerT.cs       ; default-interface-impl bridge
    IGameClientModule.cs                             ; Key marker
    GameClientModuleBase.cs                          ; typed module base + IAsyncInitializable
  Tests/
    LiveOpsServiceOptimisticTests.cs                 ; covers envelope success/exception/validate-throws/nested-dispatch/explicit-vs-DI
  Editor/
    LiveOpsTemplateMenu.cs                           ; menu items
    LiveOpsBackendWindow.cs / LiveOpsBackendWindowMenu.cs
    LiveOpsBackendInstall.cs                         ; Backend~ merge + .sln prune/sync
    LiveOpsBackendInstallContext.cs                  ; paths struct
    LiveOpsBackendRefresh.cs                         ; reverse direction (LiveOps/ -> Backend~)
    LiveOpsBackendDeploy.cs                          ; ugs CLI invocation
    UgsCliDeployContext.cs                           ; project id + env name resolution
  Backend~/
    Deploy/
      Core/
        LiveOps.DTO/
          ModuleRequest/Abstraction/                 ; ModuleRequest, ModuleRequest<T>, ModuleResponse, ResponseType
          GameApi/                                   ; GameApiEnvelopeRequest, GameApiEnvelopeResponse
          GameData/                                  ; GameDataRequest/Response/ModuleError
          GameModule/                                ; GameData (aggregate), IGameModuleData
          Json/                                      ; JsonExtensions, CrossPlatformTypeBinder
          Keys/                                      ; KeyOf, LiveOpsKeyAttribute, LiveOpsKeyResolver, LiveOpsKeyResolution
        LiveOps.Core/
          GameApi/                                   ; GameApiRegistry, GameApiSession, IGameApiHandler, HandlerEntry
          GameData/                                  ; GameDataHandler, ModulePrefetchKeys
          GameModule/                                ; GameModule<T>, IGameModule
          DataCache/Abstraction/                     ; IGameState, IPlayerData, IReadableDataCache, IRemoteConfig, IWriteableDataCache
          DataCache/Implementation/Unity/            ; UnityDataCache, ReadonlyUnityDataCache, UnityGameState/PlayerData/RemoteConfig
          DataCache/                                 ; DataCacheExtensions
          Initialize/                                ; IGameSetup, LiveOpsBootstrapper, LiveOpsManifestEntry
          ServerAuth/                                ; IServerAuth, GameStateServerAuth
          Signal/                                    ; SignalModule (in-server pub/sub)
      LiveOps/
        GameApi/GameApiDispatcher.cs                 ; [CloudCodeFunction("GameApi")] entrypoint
        Initialize/ModuleConfig.cs                   ; ICloudCodeSetup, RegisterScoped<>, IGameSetup discovery
```

`Backend~/` is the compiled-on-server side. The `Backend~/Deploy/Core/LiveOps.DTO/*` assembly is built into `Assets/Plugins/Scaffold.LiveOps.DTO/LiveOps.DTO.dll` and consumed verbatim by both the Unity client (`LiveOpsService.cs:1-13`) and the Cloud Code module (`Backend~/Deploy/LiveOps/GameApi/GameApiDispatcher.cs:6-12`). DTOs are shared, not duplicated.

## 3. What's Good

- **Typed wire contracts.** `ModuleRequest<TResponse> : ModuleRequest where TResponse : ModuleResponse` (`Backend~/Deploy/Core/LiveOps.DTO/ModuleRequest/Abstraction/ModuleRequestT.cs:4-6`) ties request to response at compile time. `ILiveOpsService.CallAsync<TResponse>(ModuleRequest<TResponse>)` (`Runtime/ILiveOpsService.cs:12`) consumes that pairing. The architect's rubric is honored here.
- **`KeyOf<T>` is a beautiful primitive.** A static-cached `LiveOpsKeyResolution` per type (`Backend~/.../Keys/KeyOf.cs:18-26`) populated by a generator-emitted `[ModuleInitializer]` (`LiveOpsKeyResolver.Contribute`, `Backend~/.../Keys/LiveOpsKeyResolver.cs:16-22`). Lookup is one `ConcurrentDictionary<RuntimeTypeHandle, …>` hit. No reflection in the hot path; missing keys fail loud (`LiveOpsKeyResolver.cs:56-60`). This is the reference pattern.
- **Source-generator-backed manifest.** `LiveOpsManifest.Entries` (`Backend~/Deploy/LiveOps/Initialize/ModuleConfig.cs:37`) is generator-emitted; `LiveOpsBootstrapper.InstallFromManifest` (`Backend~/.../Initialize/LiveOpsBootstrapper.cs:13-26`) walks it and registers handlers/modules with the DI container and `GameApiRegistry`. Constant-time, deterministic, no `AppDomain` scan in the request path.
- **Single dispatcher endpoint.** Cloud Code only sees one function: `[CloudCodeFunction("GameApi")]` (`Backend~/Deploy/LiveOps/GameApi/GameApiDispatcher.cs:38`). The wire key in the envelope routes to the right handler. This is the gRPC/MagicOnion pattern adapted to the Cloud Code model where every endpoint costs deployment churn.
- **Batch + flush data semantics.** `UnityDataCache.BeginBatch()` returns an `IAsyncDisposable`; on outermost dispose, `FlushAsync` collects only dirty keys (`Backend~/.../DataCache/Implementation/Unity/UnityDataCache.cs:74-98`) and writes them in a single `SetPrivateCustomItemBatchAsync`. `GameApiDispatcher` wraps the handler in nested `BeginBatch()` (`Backend~/.../GameApi/GameApiDispatcher.cs:78-84`). Persistence is decoupled from handler code.
- **Server auth uses constant-time compare.** `CryptographicOperations.FixedTimeEquals` (`Backend~/.../ServerAuth/GameStateServerAuth.cs:51`) is correct.
- **Optimistic & nested dispatch covered.** Tests in `Tests/LiveOpsServiceOptimisticTests.cs:21-135` cover: optimistic returns immediately, validate runs after envelope completes, envelope-exception path, validate-throws, nested-handler dispatch happens once after reconciliation, and explicit registry register overrides DI discovery.
- **Editor tooling is opinionated and self-healing.** `LiveOpsBackendInstall.PruneMissingProjectsFromSolution` (`Editor/LiveOpsBackendInstall.cs:351-426`) actively re-syncs the solution file when peer feature packages are missing — a practical concern for distributed package layouts. `LiveOpsBackendDeploy` (`Editor/LiveOpsBackendDeploy.cs`) talks to the `ugs` CLI and produces actionable error hints (`ugsCliAuthenticationHint`, `ugsCliProjectRolesHint`).
- **`CrossPlatformTypeBinder` allowlist.** `Backend~/.../Json/CrossPlatformTypeBinder.cs:51-75` only binds `System.*`, `LiveOps*`, `Scaffold.LiveOps*`, `Game.LiveOps*`, `Unity.*`. Newtonsoft's `TypeNameHandling.Auto` is gated by an explicit allowlist — the right defense.

## 4. Issues / Smells

### 4.1 `GetModuleData<T>` returns `null` silently

`LiveOpsService.GetModuleData<T>` (`Runtime/LiveOpsService.cs:59-62`):

```csharp
public T GetModuleData<T>() where T : class, IGameModuleData
{
    return gameData == null ? null : gameData.GetModuleData<T>();
}
```

`GameData.GetModuleData<T>` returns `default` if no entry matches (`Backend~/.../GameModule/GameData.cs:25-35`). Then `GameClientModuleBase<T>.InitializeAsync` (`Runtime/GameClientModuleBase.cs:23-29`) assigns the null straight into `protected T data` and calls `OnInitializedAsync(null)`. Modules silently boot with empty state. This is the textbook failure mode the architect's rubric forbids.

### 4.2 `Responses` is a `List<ModuleResponse>` with `protected set`

`ModuleResponse.Responses` (`Backend~/.../ModuleRequest/Abstraction/ModuleResponse.cs:15-16`) is a mutable list, serialized with `TypeNameHandling.Auto`, and `LiveOpsService.MergeNestedResponsesInto` (`Runtime/LiveOpsService.cs:148-154`) appends server-provided `NestedResponses` into it. The dispatch then iterates and matches by `node.GetType()` (`Runtime/ModuleResponseDispatchService.cs:64-74`).

Two issues:

1. **Tag with type names on the wire** — fragile across rename refactors. Use `[LiveOpsKey]` (already exists!) on every `ModuleResponse` subtype and route via `KeyOf<T>.Wire` instead of CLR type name.
2. **`Responses` is mutable from anywhere.** Nothing prevents handler code from re-adding to a response after dispatch.

### 4.3 The optimistic registry's Discovery + Validation is split between two services

`LiveOpsService.CallGameApiAsync` (`Runtime/LiveOpsService.cs:88-101`) duplicates the optimistic pattern that `CloudCodeService.CallEndpointAsync` (`com.scaffold.cloudcode/Runtime/CloudCodeService.cs:50-62`) already implements. Reasons for the duplication: LiveOps must unwrap the envelope before validating (`UnwrapAndDispatchGameApi`), but the *shape* — try-resolve handler, return optimistic, run validate in background — is identical. Consider extracting an `OptimisticPipeline<TServer, TResult>` so both services share one implementation rather than two.

### 4.4 `JObject.FromObject` round-trip per call

`LiveOpsService.cs:90`:

```csharp
GameApiEnvelopeRequest envelope = new GameApiEnvelopeRequest
{
    RequestKey = KeyOf.WireOf(request),
    Payload = JObject.FromObject(request, JsonSerializer.Create(liveOpsJsonSettings)),
};
```

The request is serialized into a `JObject`, the SDK then re-serializes the envelope to JSON, the server `ToObject(entry.RequestType, _serializer)` (`Backend~/.../GameApi/GameApiDispatcher.cs:58-60`) round-trips again. Three serialization passes for one request. For the LiveOps init load, this happens once; for high-frequency endpoints it matters. Consider `JsonSerializer` directly into a `string Payload` on the envelope, bypassing one pass.

### 4.5 `liveOpsJsonSettings` is not the same as the binder-aware settings

`LiveOpsService.cs:47-51` defines a settings object with `CamelCasePropertyNamesContractResolver` and `NullValueHandling.Ignore` — but **no binder, no `TypeNameHandling.Auto`**. `JsonExtensions.CreateGameApiSerializer` (`Backend~/.../Json/JsonExtensions.cs:17-24`) is the matching one with the binder. The client builds the envelope with `liveOpsJsonSettings` while the server reads with the binder-aware serializer. If a request DTO contains a polymorphic field (`object`, `IGameModuleData`), client send and server receive disagree. Today no DTO does, but the asymmetry is a foot-gun.

### 4.6 `LiveOpsInstaller` doesn't transitively install CloudCode

`Container/LiveOpsInstaller.cs:9-17` only registers `LiveOpsService` as `ILiveOpsService` + `IAsyncInitializable`. The constructor depends on `ICloudCodeService`, `ILayerResolver`, `CloudCodeOptimisticHandlerRegistry`, `CloudCodeErrorHandler` (`Runtime/LiveOpsService.cs:19-44`), so the consumer must remember to call `new CloudCodeInstaller().Install(builder)` first. The README spells this out (`README.md:14`), but the install order is a footgun. Either:

- Make `LiveOpsInstaller` call `CloudCodeInstaller` itself, or
- Have it throw a clear `InvalidOperationException` at install time when the dependencies aren't registered (VContainer's resolution error is too late).

### 4.7 `ModuleResponseDispatchService` caches handlers forever

`ModuleResponseDispatchService.DispatchNestedResponses` (`Runtime/ModuleResponseDispatchService.cs:24-38`) caches `IResponseHandler[]` on first dispatch and never invalidates. If a child VContainer scope adds handlers after the first dispatch (e.g., a newly-pushed game layer), they are silently ignored. Either:

- Invalidate on every dispatch (cheap; `ToArray` of an enumerable),
- Or expose `Invalidate()` for `ILayerResolver` to call when a new layer pushes.

### 4.8 `EnsureGameApiEnvelope` / `EnsureTypedResult` — fine, but inconsistent style

`LiveOpsService.cs:127-146` has small "Ensure" helpers that throw `InvalidOperationException`. Good style. But the constructor (`Runtime/LiveOpsService.cs:19-44`) has four duplicate `if (x == null) throw new ArgumentNullException(nameof(x));` blocks. Per rubric, drop them. The DI container guarantees non-null.

### 4.9 `IResponseHandler.HandledResponseType` is type-keyed

`Runtime/ModuleResponseDispatchService.cs:67-72`:

```csharp
if (handler != null && handler.HandledResponseType == nodeType) handler.Handle(node);
```

Linear over handlers per-node. With 50+ handlers and 5 nested responses, that's 250 reference equality checks per call. Trivial cost, but a `Dictionary<Type, List<IResponseHandler>>` in `ResolveHandlers` is one line and removes the smell.

### 4.10 `ModuleConfig.DiscoverGameSetupTypes` does AppDomain reflection at startup

`Backend~/Deploy/LiveOps/Initialize/ModuleConfig.cs:59-77` iterates `AppDomain.CurrentDomain.GetAssemblies()` filtering by `AssemblyMetadata("ScaffoldLiveOpsAssembly", "true")`, reads types, filters for `IGameSetup`, sorts. Works, but defeats the manifest pattern that the rest of the bootstrap uses. The generator already knows which assemblies are LiveOps; emit `IGameSetup` impls into the manifest too.

### 4.11 No cancellation through the envelope path

`LiveOpsService.CallAsync` accepts a `CancellationToken` and forwards it to `cloudCodeService.CallEndpointAsync` (`Runtime/LiveOpsService.cs:91`). Good. But once the optimistic path returns, the background `RunGameApiReconciliationInTheBackground` (`:103-115`) has no way to cancel — same fire-and-forget shape as `CloudCodeService` (see audit `com.scaffold.cloudcode.md:4.4`). Consistent across both packages; address in both.

### 4.12 `CrossPlatformTypeBinder.BindToType` returns `null` for empty inputs

`Backend~/.../Json/CrossPlatformTypeBinder.cs:12-15` returns `null` on empty assembly/type name. Newtonsoft will then handle that as "no $type" and fall back to the declared type. That's lenient; combined with the allowlist this is okay, but the code path is implicit. Document.

### 4.13 `LiveOpsBackendInstall` is ~530 lines of editor utility in the runtime package

Files `Editor/LiveOpsBackendInstall.cs`, `Editor/LiveOpsBackendRefresh.cs`, `Editor/LiveOpsBackendDeploy.cs`, `Editor/LiveOpsBackendWindow.cs` together make up a build-tool surface that's ~1.5x the runtime code. Move to `com.scaffold.liveops.editor` (separate package or subdir asmdef) so the runtime package stays focused. It's already partitioned by folder, but as a *package* it dominates the search space.

### 4.14 `LiveOps.ccmr` ships in the package

`com.scaffold.liveops/LiveOps.ccmr` is a Cloud Code module reference — a project-specific config. It probably shouldn't ship inside the package; it's metadata about *this repository's* deployment. If kept, document why.

### 4.15 `GameClientModuleBase<T>.data` field

`Runtime/GameClientModuleBase.cs:19`:

```csharp
protected T data;
```

A protected mutable field, lowercase, with no encapsulation. Convert to `protected T Data { get; private set; }` at minimum. Subclasses still work; future invariant changes (e.g., a `Reload` method) are localizable.

### 4.16 Schema validation / hot-reload of remote config

The architect asked: "Remote config? Schema-validated? Hot-reload?"

- **Remote config** is consumed server-side via `IRemoteConfig` (`Backend~/.../DataCache/Abstraction/IRemoteConfig.cs`) which is `IReadableDataCache`. Implementation `UnityRemoteConfig` (`Backend~/.../DataCache/Implementation/Unity/UnityRemoteConfig.cs`) fetches via `_gameApiClient.RemoteConfigSettings.AssignSettingsGetAsync` and caches as `Dictionary<string, string>`. Each value is JSON-deserialized lazily on `Get<T>`.
- **Schema validation**: none. There's no schema for what a config key *should* deserialize to; a bad JSON value silently throws inside `value.FromJson<T>()` (`ReadonlyUnityDataCache.cs:90`).
- **Hot reload**: only on access if `context.AccessToken` changed (`ReadonlyUnityDataCache.cs:47-55`); per-token, not per-deployment. Within a session, config is cached.
- **Keying**: by `[LiveOpsKey]` via `DataCacheExtensions.Get<T>` → `KeyOf<T>.Module` (`Backend~/.../DataCache/DataCacheExtensions.cs:14-17`). One DTO type ↔ one config key. Excellent.

The keying design is solid; schema validation and hot-reload are missing and worth adding.

## 5. Suggested Before/After Snippets

### 5.1 Fail-loud module data

Before (`Runtime/LiveOpsService.cs:59-62`, `Runtime/GameClientModuleBase.cs:23-29`):

```csharp
public T GetModuleData<T>() where T : class, IGameModuleData
    => gameData == null ? null : gameData.GetModuleData<T>();

public Task InitializeAsync(CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();
    T moduleData = liveOps.GetModuleData<T>();
    data = moduleData;
    return OnInitializedAsync(moduleData);
}
```

After:

```csharp
public T GetModuleData<T>() where T : class, IGameModuleData
{
    if (gameData is null)
        throw new InvalidOperationException(
            $"LiveOps not initialized; cannot resolve {typeof(T).Name}. Ensure LiveOpsService runs before {typeof(T).DeclaringType?.Name ?? "the module"}.");
    return gameData.GetModuleData<T>()
        ?? throw new InvalidOperationException(
            $"GameData has no entry for {typeof(T).Name} (key='{KeyOf<T>.Module}').");
}
```

### 5.2 Typed nested-response routing

Before: `node.GetType() == handler.HandledResponseType` linear scan.

After: route by `[LiveOpsKey]` wire key — `Dictionary<string, IReadOnlyList<IResponseHandler>>` indexed by `KeyOf.WireOf(node.GetType())`. Constant-time per node.

### 5.3 Fail-fast install order

```csharp
public sealed class LiveOpsInstaller : IInstaller
{
    public void Install(IContainerBuilder builder)
    {
        builder.RegisterBuildCallback(resolver =>
        {
            // Verify CloudCode dependencies are present; throw early with a clear message.
            _ = resolver.Resolve<ICloudCodeService>();
            _ = resolver.Resolve<CloudCodeOptimisticHandlerRegistry>();
        });
        builder.Register<LiveOpsService>(Lifetime.Singleton)
            .As<ILiveOpsService>()
            .As<IAsyncInitializable>();
    }
}
```

### 5.4 Drop boilerplate constructor guards

`Runtime/LiveOpsService.cs:19-44`: 25 lines collapse to ~5.

```csharp
public LiveOpsService(
    ICloudCodeService cloudCodeService,
    ILayerResolver layerResolver,
    CloudCodeOptimisticHandlerRegistry optimisticRegistry,
    CloudCodeErrorHandler cloudCodeErrorHandler)
{
    this.cloudCodeService = cloudCodeService;
    this.optimisticRegistry = optimisticRegistry;
    this.cloudCodeErrorHandler = cloudCodeErrorHandler;
    this.moduleResponseDispatchService = new ModuleResponseDispatchService(layerResolver);
}
```

## 6. Easy Wins

1. Throw on null `GetModuleData<T>` (`Runtime/LiveOpsService.cs:59-62`).
2. Drop the four constructor null guards in `LiveOpsService` (`Runtime/LiveOpsService.cs:19-44`).
3. Convert `GameClientModuleBase<T>.data` field to a property (`Runtime/GameClientModuleBase.cs:19`).
4. Build the dispatch handler index by-type at `ResolveHandlers` time instead of linear scan in `DispatchForNode` (`Runtime/ModuleResponseDispatchService.cs:64-74`).
5. Use `JsonExtensions.CreateGameApiSerializer()` (already in DTO assembly) on the client side instead of an ad-hoc `liveOpsJsonSettings` (`Runtime/LiveOpsService.cs:47-51`).
6. Have `LiveOpsInstaller` validate `ICloudCodeService` is registered (build callback).
7. Move `LiveOps.ccmr` out of the package or document why it ships.
8. Add an `Invalidate()` to `ModuleResponseDispatchService` for layer-aware re-resolve.

## 7. Bigger Refactors

- **Schema-validated remote config.** Add `[LiveOpsKey]` + a generated `RemoteConfigSchema` table that records `(key, expected-type, default-supplier)`; on first access, validate; on missing/malformed value, log + fall back to default. Patterns to crib: Firebase Remote Config defaults, AWS AppConfig schema validators, GameAnalytics Configs (server-side validation).
- **Hot-reload of remote config.** Today the cache invalidates per-`AccessToken` (`Backend~/.../DataCache/Implementation/Unity/ReadonlyUnityDataCache.cs:47-55`). Add a `Refresh()` on `IRemoteConfig` for explicit re-fetch (called from a Cloud Code-driven `RemoteConfigChangedSignal`).
- **Unify optimistic pipeline.** Extract a generic `OptimisticPipeline<TWire, TResult>(Task<TWire> server, Func<TWire, TResult> unwrap, IRequestHandler<TResult> handler, TResult optimistic, errorSink)` shared by `CloudCodeService` and `LiveOpsService`. Today the same pattern lives twice.
- **Manifest-emitted `IGameSetup`.** Replace `AppDomain` reflection in `ModuleConfig.DiscoverGameSetupTypes` (`Backend~/Deploy/LiveOps/Initialize/ModuleConfig.cs:59-77`) with a generator-emitted entry list, matching `LiveOpsManifest.Entries`.
- **Wire-string envelope payload.** Drop `JObject Payload` for `string Payload` on the envelope to remove one serialization pass; document that the server is responsible for `JObject.Parse` / `JsonConvert.DeserializeObject(payload, requestType)` only when needed.
- **Editor sub-package.** Move the entire `Editor/` tree to `com.scaffold.liveops.editor` (separate package) or at minimum its own asmdef + assembly. Today it dwarfs the runtime in line count.

## 8. Organization & Docs

- **`README.md`** is the most thorough doc in the repo and reflects the implementation accurately, including the Backend~/Deploy/Game/Scaffold layout and `LiveOpsManifest.Entries` flow. Keep — but trim some emphasis-bold formatting that obscures the prose.
- **Tests cover the critical paths** (`Tests/LiveOpsServiceOptimisticTests.cs:21-303`): envelope success, exception, validate-throws, nested dispatch, explicit-vs-DI override. Missing: `GetModuleData<T>` not-initialized, missing key, partial `GameDataResponse` (`isPartial=true` with errors).
- **Backend tests** (`LiveOps/Tests/LiveOps.Tests`) live outside the package per README. Per-feature `Backend~/.../Tests` would be cleaner, but the centralized test project is fine for a Cloud Code module.
- **`AssemblyInfo.cs`** is the right pattern (`Runtime/AssemblyInfo.cs:1-5`); `internal sealed` types throughout.
- **Newtonsoft on the client** is acceptable; the binder allowlist (`CrossPlatformTypeBinder`) is good defensive code. Validate that `Scaffold.LiveOps.*` and `LiveOps.Modules.*` allowlist entries (`Backend~/.../Json/CrossPlatformTypeBinder.cs:62-69`) match the actual emitted assemblies after the generator runs.

### References

- Unity Cloud Code Modules — `[CloudCodeFunction]` and the dispatcher pattern: https://docs.unity.com/ugs/manual/cloud-code/manual/modules
- gRPC-Web — single endpoint + wire key dispatch: https://grpc.io/docs/platforms/web/
- MagicOnion `IService<T>` — request-typed RPC for Unity: https://github.com/Cysharp/MagicOnion
- Firebase Remote Config — schema + defaults + change listeners: https://firebase.google.com/docs/remote-config
- AWS AppConfig — JSON Schema validation on deploy: https://docs.aws.amazon.com/appconfig/latest/userguide/appconfig-creating-configuration-and-profile-validators.html
- GameAnalytics Configs — server-side validation, hot reload: https://docs.gameanalytics.com/integrations/sdk/unity/event-tracking#remote-configs
- The `[LiveOpsKey]` + `KeyOf<T>` + source generator pattern (this repo) is the gold standard inside the codebase — extend it to nested `ModuleResponse` routing and `IGameSetup` manifesting.

## 9. Consumers

The typed-RPC pattern is **defined cleanly but barely adopted at the consumer side** — a critical finding. There are exactly **two** production call sites of `ILiveOpsService.CallAsync<TResponse>(ModuleRequest<TResponse>)` outside the package itself, and **zero** subclasses of `GameClientModuleBase<T>` and **zero** consumers of `GetModuleData<T>` outside the package's own runtime. The "gold pattern" exists in the abstract; the project hasn't yet had to live with it at scale.

- `Assets/Packages/com.scaffold.ads/Runtime/Implementation/Rewarded/LiveOpsRewardEndpointClient.cs:21-26` — `liveOpsService.CallAsync(new WatchAdRequest { PlacementId = placementId })`. Clean adoption: typed request, typed response, no strings. This is what the rest of the project should look like.
- `Assets/Packages/com.scaffold.directpush/Runtime/DirectPushClient.cs:26,39,51` — three `liveOpsService.CallAsync(request, ct)` sites for self/player/project pushes. Also clean. Note: `DirectPushClient` is itself a thin wrapper that consumers call instead of `ILiveOpsService` directly — i.e., even within Scaffold, `ILiveOpsService` is hidden behind feature-specific façades.
- `Assets/Packages/com.scaffold.ads.levelplay/Runtime/Test/UnityAdsTester.cs:19-20` — `[Inject] private ILiveOpsService liveOpsService;`. Consumed only to construct `LiveOpsRewardEndpointClient`; the `ILiveOpsService` is not the consumer-facing API here.
- **`[LiveOpsKey]` adoption tally** (production DTOs, excluding tests): 7 — `WatchAdRequest`, `AdData`, `AdsConfig`, `AdsPersistence`, `SendSelfPushRequest`, `SendPlayerPushRequest`, `SendProjectPushRequest`. Plus `GameDataRequest` in core. Not a single non-Scaffold module yet.
- **`KeyOf<T>` use sites** (non-test, non-package-internal): zero. Every `KeyOf<T>.Module` / `KeyOf.WireOf(...)` reference lives inside `LiveOps.DTO`, `LiveOps.Core`, or the LiveOps client runtime. Consumers never write `KeyOf<>` themselves; they put `[LiveOpsKey]` on the DTO and the registry/ extensions do the lookup. That is the pattern's success — the verbosity is centralized.
- **`GameClientModuleBase<T>` subclasses**: zero. The base class advertises a strong pattern (constructor-inject `ILiveOpsService`, auto-populate `protected T data`) but no module yet uses it. The audit's §4.1 fail-loud concern (silent null `data`) cannot bite until the first subclass lands; when it does, it will bite immediately.
- **`GetModuleData<T>` callers**: zero outside `GameClientModuleBase<T>` itself. The function is reachable only through that base class.
- No GameModule-side code exists (`/home/user/Scaffold/GameModule/` contains only `obj/` build outputs). All consumers live under `Assets/Packages/com.scaffold.ads*` and `com.scaffold.directpush`.

Net: the typed-RPC pattern is the project's reference design but has only two production call sites, both inside the framework's own peer feature packages. The architecture is provably right (clean adoption where it does exist) but **not yet stress-tested by gameplay code**.

## 10. Alternatives & prior art

- **Unity Remote Config.** The official UGS service for runtime config; fetched on demand, not schema-validated, no hot reload signal. https://docs.unity.com/ugs/manual/remote-config/manual. **Wrap.** Already wrapped via `UnityRemoteConfig` (`Backend~/.../DataCache/Implementation/Unity/UnityRemoteConfig.cs`). Adopt the schema validation ideas from below.
- **LaunchDarkly / GrowthBook feature-flag client SDKs.** Typed flag accessors with default values, schema, real-time updates. https://launchdarkly.com/docs, https://docs.growthbook.io. **Steal pattern.** The "default + schema + change-listener" trio is exactly the gap in `IRemoteConfig` (audit §4.16). Implement as `IRemoteConfig.Get<T>(default)` + `IRemoteConfig.OnChanged<T>` keyed by `[LiveOpsKey]`.
- **AutoMapper.** Convention-based DTO↔domain mapping for .NET. https://docs.automapper.org. **Build (don't adopt).** The architect mentioned AutoMapper. The right move here is *not* to introduce AutoMapper — the `[LiveOpsKey]` source-generator pattern already gives compile-time-typed mapping with zero reflection. AutoMapper is reflective; the existing pattern is strictly better. Worth keeping the option in mind for the *server-side* `Game.Domain ↔ LiveOps.DTO` mapping if/when Game code lands.
- **Newtonsoft.Json `TypeNameHandling.Auto` allowlist.** Already in use via `CrossPlatformTypeBinder`. https://www.newtonsoft.com/json/help/html/SerializeTypeNameHandling.htm. **Adopt (already done) + steal pattern.** Replace CLR-type-name routing with `[LiveOpsKey]` wire-key routing for nested `ModuleResponse` (audit §4.2) — same allowlist discipline, but compile-time-stable.
- **MessagePack source-generated formatters (Cysharp).** Generator-emitted, allocation-free, schema-explicit serialization. https://github.com/MessagePack-CSharp/MessagePack-CSharp. **Steal pattern.** When the JSON triple-pass becomes a real cost (audit §4.4), the gen-formatter pattern is the upgrade target without ditching JSON on the wire — generate JSON writers/readers per `[LiveOpsKey]` type instead of relying on Newtonsoft reflection.
- **Firebase Remote Config (defaults, change-listener, schema-on-deploy).** https://firebase.google.com/docs/remote-config. **Steal pattern.** The defaults-bundle + change-listener UX is the model for the missing hot-reload story.

## 11. Benchmark plan

- **Source-generator throughput.** What to measure: incremental compile time for `LiveOps.Deploy` solution as `[LiveOpsKey]` count scales 10 → 100 → 1000. Tool: `dotnet build -bl` + binlog analyzer; `Scaffold.LiveOps.Bootstrap.Generators` is the target. Test location: `LiveOps/Tests/Generators.Benchmarks/` (new). Scenario: synthetic DTO assemblies with N types each tagged `[LiveOpsKey("Key{i}")]`. Baseline: warm incremental rebuild < 2s for 100 keys. Success: linear scaling, no re-emission of unchanged manifests (incremental gen contract).
- **`[LiveOpsKey]` registry warm-up.** What to measure: AppDomain-startup cost from `[ModuleInitializer]` `LiveOpsKeyResolver.Contribute` calls (`/home/user/Scaffold/LiveOps/Deploy/Core/LiveOps.DTO/Keys/LiveOpsKeyResolver.cs:16-22`). Tool: Unity.PerformanceTesting `[OneTimeSetUp]`. Test location: `com.scaffold.liveops/Tests/KeyResolverWarmupBenchmarks.cs`. Scenario: count entries × measured ms. Baseline: < 1 ms for the current 7-key ship; project a budget for 100/1000 keys. Success: warm-up cost per key ≤ 10 µs (ConcurrentDictionary insert + RuntimeTypeHandle compare).
- **Triple `JObject.FromObject` overhead.** What to measure: per-call CPU and bytes-allocated of `LiveOpsService.CallGameApiAsync` end-to-end (`/home/user/Scaffold/Assets/Packages/com.scaffold.liveops/Runtime/LiveOpsService.cs:88-101`). Tool: Unity.PerformanceTesting. Test location: `com.scaffold.liveops/Tests/EnvelopeSerializationBenchmarks.cs`. Scenario: fake `ICloudCodeService` returns a canned `GameApiEnvelopeResponse`; 10k iterations across 5/50/500-property requests. Baseline: today, three serialization passes (audit §4.4). Success: switching `JObject Payload` → `string Payload` cuts allocs ≥ 60% on the 50-property case.
- **`BeginBatch` / `FlushAsync` size scaling.** What to measure: `UnityDataCache.FlushAsync` write batching cost as dirty-key count grows 1 → 100 → 10k. Tool: Unity.PerformanceTesting + a fake `IGameApiClient`. Test location: `LiveOps/Tests/LiveOps.Tests/UnityDataCacheBatchBenchmarks.cs`. Scenario: nested `BeginBatch` x 3 with M dirty keys per inner; assert exactly one outer `SetPrivateCustomItemBatchAsync` call. Baseline: linear in dirty-key count; constant in batch nesting depth. Success: matches; documents the contract so a future refactor doesn't accidentally fan out per-key.
- **No-AppDomain-scan in steady state (correctness).** What to measure: that after the first `LiveOpsBootstrapper.InstallFromManifest` call, no `AppDomain.CurrentDomain.GetAssemblies()` is invoked on subsequent requests (audit §4.10 vs the rest of the bootstrap). Tool: NUnit EditMode + a custom `AssemblyLoadContext` probe (or wrap `GetAssemblies` in a counter via the test seam). Test location: `LiveOps/Tests/LiveOps.Tests/ModuleConfigReflectionTests.cs`. Scenario: warm up the bootstrapper, run 100 dispatch cycles, assert assembly-scan counter == 1 (the initial `DiscoverGameSetupTypes`). Baseline: today, `DiscoverGameSetupTypes` runs once at install; the test pins it. Success: failing if anyone introduces post-warmup reflection.
- **Optimistic + nested-dispatch latency.** What to measure: time from `CallAsync` invocation to optimistic-response return, and from server-completion to nested-response dispatch. Tool: Unity.PerformanceTesting + `TaskCompletionSource` injection (mirrors existing test pattern in `Tests/LiveOpsServiceOptimisticTests.cs:38`). Test location: `com.scaffold.liveops/Tests/OptimisticPipelineBenchmarks.cs`. Scenario: 10k iterations with 0/1/5 nested responses. Baseline: optimistic path returns within 1 µs of registry hit; nested dispatch is O(handlers × responses) (audit §4.9). Success: switching to a `Dictionary<Type, IResponseHandler[]>` index removes the linear scan.
- **Partial `GameDataResponse` correctness.** What to measure: that a `GameDataResponse` with `IsPartial = true` and `Errors != empty` is surfaced explicitly, not swallowed. Tool: NUnit EditMode. Test location: `com.scaffold.liveops/Tests/PartialGameDataTests.cs`. Scenario: stub envelope yields a partial response; assert `LiveOpsService` rejects-or-flags rather than silently storing partial `gameData`. Baseline: today's behavior is undocumented (audit §8); the test pins observed behavior, then drives a fail-loud fix.

