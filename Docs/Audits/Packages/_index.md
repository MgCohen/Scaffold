# Scaffold Packages — Architecture Audit Index

Audit of all `com.scaffold.*` packages under `Assets/Packages/`, run against the architect's stated rubric:

- **Minimum code, maximum extensibility.** Generics over reflection or strings. C# typing pushes errors to compile time.
- **Abstraction at entry points and known-changing seams only.** No interfaces with one impl that won't change.
- **Few guard clauses.** Validate at the entry point, then trust internal invariants. No `null` checks on `readonly` fields.
- **Fail-fast.** Don't return `null` / `default` / `false` to mask errors. Throw.
- **Unity / pure C# at clean boundaries.** Domain types are engine-free where possible.

Each package has its own report in this folder. This index aggregates verdicts and the cross-cutting themes worth addressing project-wide.

---

## Verdict matrix

| Package | Files | Verdict | Headline problem |
|---|---:|---|---|
| `com.scaffold.types` | 10 | Refactor | Unreachable null-on-`typeof(T)` checks; swallowed deserialization exceptions |
| `com.scaffold.events` | 5 | Refactor or replace | Hand-rolled bus; consider MessagePipe. Redundant guards on `readonly` fields |
| `com.scaffold.pooling` | 4 | **Keep** | Strongest small package; sealed generic, real tests |
| `com.scaffold.records` | 2 | **Delete** | Vestigial `IsExternalInit` shim; `autoReferenced: true` actively risky on Unity 6000 |
| `com.scaffold.model` | 1 | Merge or earn its keep | One file. Barely a package |
| `com.scaffold.schemas` | 18 | Refactor | **Bug**: `SchemaObject.RemoveSchema(Type)` uses `IsAssignableFrom` in wrong direction. Cache desync risk |
| `com.scaffold.entities` | 84 | Keep, focused refactor | Stringly-typed `Variable` defeats `GetVariable<T>`; reflection-based registries are source-gen candidates |
| `com.scaffold.entities.states` | 21 | Refactor | Unity leak in pure-C# pipeline; reentrancy via shared scratch buffers; missing `Container/` |
| `com.scaffold.states` | 53 | Refactor | `Store.Get<T>` throws / `Snapshot.Get<T>` returns null — inconsistent. Type-keyed dispatch should be source-generated |
| `com.scaffold.mvvm` | 38 | Keep, focused refactor | Subscription leak in `ViewElement<T>.Bind` (no unsubscribe); `BindedProperty.Update` swallows exceptions |
| `com.scaffold.viewmodel` | 4 | Light cleanup | Unused `using UnityEngine`; `noEngineReferences: false` on a "pure C#" package |
| `com.scaffold.view` | 22 | Refactor | `EventLedger<T>` doesn't sweep dead transforms after scene reload |
| `com.scaffold.appflow` | 37 | Refactor | Not actually a flow — it's a scope stack. `_ = ct;` swallow in `PopAsync`; `async void Start()` |
| `com.scaffold.sceneflow` | 11 | Keep with refactor | Scenes stringly-typed at domain layer; `Tests/` is empty despite README claims |
| `com.scaffold.navigation` | 55 | Refactor | `async void RunTransitions()` can stall the queue; runtime route lookup with `throw new Exception`; `Popup` vs `Screen` are duplicates |
| `com.scaffold.addressables` | 27 | Refactor | **Race**: `AssetHandle<T>.Release()` vs `Complete()`. Concurrent `AcquireAsync` for same key leaks |
| `com.scaffold.autopacker` | 4 | Investigate | DLL-drop package; ~130-byte DLLs are suspicious. No documented build pipeline |
| `com.scaffold.maps` | 9 | Refactor | Indexer predicates are key-only despite README; default-value-hides-error in `Add` overloads; zero tests for the headline feature |
| `com.scaffold.cloudcode` | 18 | Keep, refactor | Public `CallEndpointAsync<T>(string, string, object)` is stringly-typed; `CloudCodeErrorHandler.Handle` is silent no-op |
| `com.scaffold.directpush` | 13 | Refactor | Receive-side handlers have no payload; no `Unsubscribe`; no offline buffer/replay |
| `com.scaffold.liveops` | 63 | Keep, refactor | **Reference design** for the project. `[LiveOpsKey] + KeyOf<T> +` source-gen is the gold pattern. `GetModuleData<T>` returns null silently |
| `com.scaffold.ugs` | 2 | Light cleanup | Two files do the job. Awkward `Scaffold.Ugs.Ugs` namespace; under-declared `package.json` deps |
| `com.scaffold.ads` | 30 | Refactor | `async void` entry points; placement keys still `string` despite `AdPlacementKeySO` existing; no headless provider |
| `com.scaffold.ads.levelplay` | 12 | Refactor | **Threading violation** (LevelPlay calls off main thread); per-impression race on shared `currentShowingPlacement`; reward token forgeable client-side |

---

## What to use as the model

**`com.scaffold.liveops`** is the reference design for backend integration:

- `[LiveOpsKey("…")] + KeyOf<T>()` for compile-time key↔type binding.
- A source generator under `Generators/` emits the manifest — no `AppDomain.CurrentDomain.GetAssemblies()` reflection at runtime.
- A single Cloud Code entry point with a typed envelope, batch with `BeginBatch`/`FlushAsync`, and an allowlisted `CrossPlatformTypeBinder` for safe `Newtonsoft.Json` `TypeNameHandling`.
- Backend code shipped as a real shared assembly (`Backend~/Deploy/Core/LiveOps.DTO/*` builds into `Assets/Plugins/Scaffold.LiveOps.DTO/*.dll` consumed by both client and server).

**`com.scaffold.pooling`** is the reference design at the small end: sealed generic, fail-fast at the constructor only, real tests, no Unity surface area.

These two are the only packages that fully match the rubric. Treat them as the templates the others should converge on.

---

## Cross-cutting themes (in order of project-wide impact)

### 1. Stringly-typed dispatch where generics already exist

Recurring across `cloudcode`, `navigation`, `appflow`, `sceneflow`, `entities` (`Variable`), `states` (mutator/payload pairing), and `ads` (placements). The shape is always the same: a typed registry on the inside, a stringly-typed public API on the outside.

Pattern to apply (already used by `liveops`):

```csharp
[LiveOpsKey("inventory.grant")]
public sealed record GrantItemRequest(string ItemId, int Count) : ILiveOpsRequest<GrantItemResponse>;

// caller
var resp = await liveOps.Call<GrantItemRequest, GrantItemResponse>(
    new GrantItemRequest("sword_01", 1), ct);
```

Everywhere a string is keying a type, a generic + attribute + source-generated registry should replace it. The compile-time guarantee removes the "key not registered" runtime branch, the "wrong payload type" runtime branch, and the corresponding tests.

### 2. Reflection at startup that should be source generation

`VariableValueRegistry`, `ModifierTypeIndex` (entities), state mutator dispatch (states), `ModuleConfig` (`liveops`). All scan `AppDomain.CurrentDomain.GetAssemblies()` at first access. The repo already has `Generators/` with `MVVMCompositionGenerator` and `LiveOpsKeyGenerator` showing the pattern. Standardize on it.

Cost: ~one generator per registry. Saves the AppDomain scan, the IL2CPP-fragile `GetTypes()` calls, the silent-conflict resolution paths (`HandleDuplicateId` etc.), and the warm-up tax on first access.

### 3. Redundant guard clauses on `readonly` fields

The single most common rubric violation. Examples:

- `EventController.AddListener` checks `events == null` and `eventLookups == null` even though both are `readonly` and initialized in their declaration.
- `Map<…>`/`BaseMap<…>` (maps): 19+ `if (predicateIndexers == null)` / `if (data == null)` checks across two files.
- `AddressablesAssetReferenceHandler.GuardRuntimeInvariants()` called from 6 places.
- Three-layer null-check chain `EntityComponent → EntityInstance → LocalVariableStorage`.

Mechanical fix: delete every guard against a `readonly` field initialized in declaration or constructor. If the constructor doesn't assert it once, add that one assertion.

### 4. Default-value-hides-error

Direct violations of fail-fast:

- `CloudCodeErrorHandler.Handle` — silent no-op default branch.
- `LiveOpsService.GetModuleData<T>` — returns `null` silently (`Runtime/LiveOpsService.cs:59-62`).
- `Variable.Key` / `PayloadTypeId` — fall back to `""` / `"string"`.
- `Snapshot.Get<TState>` returns `null` while `Store.Get<TState>` throws — inconsistent.
- `Maps.Add` overloads use `default(T)` for the missing key half (throws downstream).
- `Store.cs:264` logs `UnityEngine.Debug.LogWarning` and returns instead of throwing.
- `LevelPlayConfigurationProvider.GetActiveConfiguration` falls back to `EditorConfig` on production platforms (ships editor app keys to the store).

Each of these should throw. Most callers can't recover from the "wrong" path anyway and the silent path adds debug time.

### 5. `async void` and unobserved exceptions

- `AppFlowHost.Start` — `async void`.
- `NavigationController.RunTransitions` — `async void`. An unobserved exception stalls the queue forever.
- `AdManager.InitializeAds`, `RewardedAdManager.ClickShowAdReward`, `InterstitialAdManager.ShowInterstitial` — all `async void`.
- `CloudCodeService` and `LiveOpsService` background reconciliation — `async void`, uncancellable post-optimistic-return.

Replace with `async UniTaskVoid` (UniTask) or `async Task` with explicit `.Forget(ex => …)` propagation. Wire the only legitimate `async void` (Unity event handlers) through a single `Forget` extension that routes to the global error pipeline.

### 6. Unity leaks in pure-C# packages

Several packages declare themselves Unity-free in spirit but leak `UnityEngine`:

- `Store.cs:264` (states) — `UnityEngine.Debug.LogWarning`.
- `EventController.cs` — unused `using UnityEngine`.
- `Schema.cs`, all schema attribute files — unused `using UnityEngine`.
- `BindedProperty.cs`, `BindedCollection.cs` (mvvm) — engine reference.
- `DeferredBindingCoroutineHost.cs` (mvvm) — `MonoBehaviour` coroutine host inside a "pure C#" package.
- `ViewModel.cs:7` (viewmodel) — `using UnityEngine` (unused).

Fix: remove the `using`, set `noEngineReferences: true` in the asmdef, route logging through an `ILogger` injected at the boundary.

### 7. Empty `Tests/` folders ("test theater")

`com.scaffold.types`, `com.scaffold.events`, `com.scaffold.model`, `com.scaffold.schemas`, `com.scaffold.records`, `com.scaffold.ugs`, `com.scaffold.sceneflow` — all ship a `Tests/` folder or asmdef with no `.cs` files. AGENTS.MD permits this when tests aren't ready, but having the empty directory implies coverage that isn't there.

Mechanical fix: either add one happy-path test per package, or delete the empty `Tests/` folder until coverage is actually written. Don't ship empty.

### 8. Missing `Container/Installer` where convention requires it

`com.scaffold.states` and `com.scaffold.entities.states` have no `Container/` despite the project's per-package VContainer convention. Both packages currently rely on the consumer's installer to know the wiring.

### 9. One-impl interfaces

Audit-wide examples: `IInstanceIdGenerator`, `IDefinitionVariableBagProvider`, `INavigationMiddleware` (marker, no impl), `NoView`, `IViewContext`, `IViewContextHost` (all dead). Any interface with one impl and no concrete plan for a second impl is a candidate for inlining. Save the abstraction for entry points and seams that demonstrably change.

### 10. Reentrancy & race bugs to fix in the next pass

These are real defects, not style issues:

- `AssetHandle<T>.Release()` racing `Complete()` — `WhenReady` hangs or release no-ops (`Runtime/Implementation/AssetHandle.cs:50-95`, addressables).
- Concurrent `AcquireAsync` for same key in `AddressablesAssetReferenceHandler` — second result discarded without release (asset leak).
- `Store.EnumerateAll` mutates instance-shared scratch buffers (`mapSliceBuffer` / `sliceBuffer` / `pruneBuffer`) mid-iteration (states).
- `StateEventHandler.NotifyReferenceSubscriptions` invokes without snapshotting the subscriber list.
- `LevelPlayRewardedAdService.currentShowingPlacement` — single mutable string threaded through async SDK callbacks; back-to-back impressions overwrite.
- `LevelPlay` interstitial/rewarded retry path dispatches SDK calls off the main thread via `Task.Run(...).Delay(...).LoadAd()`.
- `ViewElement<T>.Bind` subscribes to `npc.PropertyChanged` but `Unbind` never unsubscribes — old VM keeps a strong delegate reference to the View.

### 11. Documentation reality drift

Several README claims are demonstrably false against the code:

- `com.scaffold.ads` README claims "pure C#" but `AdRewardUIController` is a `MonoBehaviour` in the runtime asmdef.
- `com.scaffold.ads.levelplay` README claims secure server validation; reward token is fabricated client-side from timestamp + instance ID + reward name.
- `com.scaffold.sceneflow` README claims `SceneFlowInstaller` registers a null-object shell; code accepts `null` silently.
- `com.scaffold.ads.levelplay` README claims production safety; `GetActiveConfiguration` falls back to editor keys.

Promote one developer to "README enforcer" or add an analyzer that fails CI when a `<!-- behavior: … -->` block in README is contradicted by code. Easier short-term: trim every README to claims that are verifiably true today.

### 12. `Samples/` should be `Samples~/`

A handful of packages (`autopacker`, `maps`, others) ship sample code under `Samples/`, which always compiles into consumer builds. Unity convention is `Samples~/` (tilde-suffixed) so the samples are imported via Package Manager only. Easy bulk fix.

---

## Suggested next-step plan

If the team wants concrete waves of work, prioritized by ROI:

**Wave 1 — bug-killers (1–2 days)**

1. Fix the addressables `Release()` race (`AssetHandle<T>`) and the AcquireAsync key race.
2. Snapshot subscriber list in `StateEventHandler.NotifyReferenceSubscriptions`; snapshot scratch buffers in `Store.EnumerateAll`.
3. Patch `SchemaObject.RemoveSchema(Type)` direction bug.
4. Move LevelPlay retry path back to main thread; `TaskCompletionSource`-gate `OnInitSuccess`; switch placement key to `LevelPlayAdInfo.Placement`.
5. Add `Unbind` unsubscription in `ViewElement<T>`.

**Wave 2 — fail-fast cleanup (2–3 days)**

1. Delete every guard against a `readonly` field initialized at declaration. Add one constructor assertion if missing.
2. Replace silent default returns (`GetModuleData<T>`, `CloudCodeErrorHandler.Handle`, `Variable.Key`, `RemoveModifier`, `Snapshot.Get<TState>`) with throws.
3. Remove `_ = ct;` swallows. Honor `CancellationToken` end to end or take it out of the signature.

**Wave 3 — generics & source-gen (1–2 weeks)**

1. Apply the `[LiveOpsKey] + KeyOf<T>` pattern to `cloudcode` (typed `Call<TReq,TResp>`), `directpush` (typed receive payloads), `entities.states` (typed mutator dispatch), `navigation` (typed routes), `sceneflow` (`SceneKey<TScene>`), `ads` (`AdPlacementKey<TFormat>`).
2. Replace `VariableValueRegistry`/`ModifierTypeIndex`/`ModuleConfig` AppDomain scans with source-generated registries.

**Wave 4 — scope cleanup (~1 week)**

1. Delete dead packages/types: `com.scaffold.records`; in navigation, `ServerNavigationController`, `NoView`, `IViewContext`, `IViewContextHost`, marker `INavigationMiddleware`.
2. Convert one-impl interfaces to concrete classes unless a second impl is planned.
3. Move `Samples/` → `Samples~/` across affected packages.
4. Trim or correct the misleading READMEs.

**Wave 5 — testing (continuous)**

1. Either add one happy-path test per package or delete the empty `Tests/` folders.
2. Add PlayMode coverage for the addressables refcount/race scenarios.
3. Add a generator-presence sanity test for `autopacker` and verify the shipped DLLs aren't stubs.

---

## Per-package reports

See the individual files in this directory:

- [`com.scaffold.addressables.md`](com.scaffold.addressables.md)
- [`com.scaffold.ads.md`](com.scaffold.ads.md)
- [`com.scaffold.ads.levelplay.md`](com.scaffold.ads.levelplay.md)
- [`com.scaffold.appflow.md`](com.scaffold.appflow.md)
- [`com.scaffold.autopacker.md`](com.scaffold.autopacker.md)
- [`com.scaffold.cloudcode.md`](com.scaffold.cloudcode.md)
- [`com.scaffold.directpush.md`](com.scaffold.directpush.md)
- [`com.scaffold.entities.md`](com.scaffold.entities.md)
- [`com.scaffold.entities.states.md`](com.scaffold.entities.states.md)
- [`com.scaffold.events.md`](com.scaffold.events.md)
- [`com.scaffold.liveops.md`](com.scaffold.liveops.md)
- [`com.scaffold.maps.md`](com.scaffold.maps.md)
- [`com.scaffold.model.md`](com.scaffold.model.md)
- [`com.scaffold.mvvm.md`](com.scaffold.mvvm.md)
- [`com.scaffold.navigation.md`](com.scaffold.navigation.md)
- [`com.scaffold.pooling.md`](com.scaffold.pooling.md)
- [`com.scaffold.records.md`](com.scaffold.records.md)
- [`com.scaffold.sceneflow.md`](com.scaffold.sceneflow.md)
- [`com.scaffold.schemas.md`](com.scaffold.schemas.md)
- [`com.scaffold.states.md`](com.scaffold.states.md)
- [`com.scaffold.types.md`](com.scaffold.types.md)
- [`com.scaffold.ugs.md`](com.scaffold.ugs.md)
- [`com.scaffold.view.md`](com.scaffold.view.md)
- [`com.scaffold.viewmodel.md`](com.scaffold.viewmodel.md)
