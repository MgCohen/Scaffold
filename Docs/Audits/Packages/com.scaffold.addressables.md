# Audit: `com.scaffold.addressables`

Audit date: 2026-05-02. Reviewer: senior architect.

## 1. Summary

A reasonable-sized Addressables wrapper (27 .cs files) that gets the big picture right: a `IAddressablesGateway` facade returning typed `IAssetHandle<T>` / `IAssetGroupHandle<T>`; a separate low-level `IAddressablesAssetClient` that wraps `UnityEngine.AddressableAssets.Addressables`; reference counting in a centralized `AddressablesAssetReferenceHandler`; and provider/registrar contracts for module-local preload. The contracts don't leak `AsyncOperationHandle`, which is the most important thing to get right.

But the implementation has measurable problems against the rubric:

- **Concurrency bugs** in `AssetHandle<T>` (race between `Release()` and `Complete()`) and **double-release possibility** in `AddressablesAssetReferenceHandler` (ref-count vs. failure).
- **Leftover `Debug.Log`s** in `AddressablesGateway.InitializeCoreAsync` (instance hash codes printed on every startup).
- **Sync API exists in the contract but is implemented on top of async** — `Load<T>(...)` returns a "deferred" handle that fires-and-forgets a task. This is misnamed; it isn't sync, it's async-with-out-parameter-pretending-to-be-sync.
- **Defensive guards everywhere**, in violation of the rubric: `GuardRuntimeInvariants()` in every public method on the gateway checks for fields that are assigned in the constructor and can never be null.
- **Dead/orphan contracts** (`IAssetPreloader`, `PreloadMode`, `IAssetGroupProvider<T>` references shapes the README says were removed).
- **`Map<Type, string, ...>`** for the loaded-asset registry is a bizarre choice when a `Dictionary<(Type, string), Entry>` would do the same in 1/10th the code.
- Reference counting is **mixed with a "NeverDie" mode** that can be flipped by any caller — there's no single owner, no protected-from-clients invariant.

**Verdict: Solid skeleton, leaky middle.** The contracts are clean enough to ship; the implementation needs a focused pass to fix the two real concurrency bugs, strip debug logs, kill the dead "deferred sync" overloads, and consolidate the ref counter. Then it's done.

## 2. Structure

```text
com.scaffold.addressables/
  Container/
    AddressablesInstaller.cs                       VContainer registrations
  Editor/                                          empty (only asmdef)
  Runtime/
    AssemblyInfo.cs                                InternalsVisibleTo
    AddressablesGatewayComponentExtensions.cs      Component<T> overloads
    ComponentAddressableHandles.cs                 ComponentAssetHandle<T>
    ComponentGroupHandle.cs                        ComponentGroupHandle<T>
    Contracts/
      IAddressablesGateway.cs                      Public surface
      IAddressablesAssetClient.cs                  Low-level Addressables wrapper contract
      IAssetHandle.cs / IAssetHandle<T>.cs         Typed handle
      IAssetGroupHandle.cs                         Typed group handle (IDisposable)
      IAssetProvider.cs / IAssetProvider<T>.cs     Provider contracts
      IAssetGroupProviderT.cs                      Group provider
      IAssetRegistrar.cs                           Bootstrap registrar
      IAssetPreloader.cs                           PreloadAsync contract
      AssetHandleState.cs                          Loading/Ready/Faulted/Released
      PreloadMode.cs                               Normal/NeverDie
    Implementation/
      AddressablesGateway.cs                       Public gateway impl
      AddressablesAssetClient.cs                   Low-level client (Addressables calls)
      AddressablesAssetReferenceHandler.cs         Refcount + cache
      AddressablesLoadedEntry.cs                   Cache entry POCO
      AssetHandle.cs                               Default handle impl
      AssetGroupHandle.cs                          Default group handle
      AssetProvider.cs / AssetGroupProvider.cs     Provider base classes
    Internal/
      IAssetReferenceHandler.cs                    InternalsVisibleTo only
  Tests/
    AddressablesInstallerTests.cs                  3 tests (DI wiring + visibility)
    PlayMode/.gitkeep                              empty
  package.json, README.md, asmdefs
```

The split across `Contracts` / `Implementation` / `Internal` is correct and well-disciplined. `IAssetReferenceHandler` is genuinely internal (the test at `AddressablesInstallerTests.cs:37-46` verifies this); good.

## 3. What's good

- **Contract types do not leak Addressables.** `IAssetHandle<T>` and `IAssetGroupHandle<T>` deliberately hide `AsyncOperationHandle` behind `T Asset` / `IReadOnlyList<T> Assets`. This is the single most important architectural property of an Addressables wrapper and they got it. Reference: `Runtime/Contracts/IAssetHandle.cs:7-16`, `IAssetHandleT.cs:5-9`, `IAssetGroupHandle.cs:8-15`.
- **Generic typed handles.** `IAssetHandle<out T>` is properly covariant and constrained `where T : UnityEngine.Object`. Same for `IAssetGroupHandle<out T>`. Compile-time safety as the rubric demands.
- **`IDisposable` on group handle.** `IAssetGroupHandle<T> : IDisposable` (`IAssetGroupHandle.cs:8`) lines up with `using` / `IDisposable` patterns, and `AssetGroupHandle.Dispose()` forwards to `Release()` (`AssetGroupHandle.cs:58-61`). Good.
- **`AssetReference` and `AssetReferenceT<T>` overloads** exist (`IAddressablesGateway.cs:12-13`), avoiding `string` keys at the call site. The `AssetReferenceT<T>` overload casts to `AssetReference` (`AddressablesGateway.cs:32-37`); minor, keeps the surface compact.
- **Component extension methods** layered on top (`AddressablesGatewayComponentExtensions.cs`) — `LoadComponentAsync<TComponent>` loads a `GameObject` then resolves a component. Right place for that, since you don't want it in the gateway itself.
- **Labels return groups, references return single handles.** Right model for Addressables. The naming and overload choices match Addressables semantics (`IAddressablesGateway.cs:11-16`).
- **Catalog sync at startup is best-effort, not blocking** (`AddressablesGateway.RunCatalogSyncAsync` swallows non-cancellation errors and logs a warning, `AddressablesGateway.cs:147-159`). This is the right call for shipping titles where forcing CCD sync to succeed at boot is asking for crashes in airports and on cellular.
- **Internal handler is genuinely hidden.** `IAssetReferenceHandler` lives under `Scaffold.Addressables.Internal` and is `internal`, exposed only via `InternalsVisibleTo` to `Container` and `Tests`.
- **Provider/registrar split is appropriate.** `IAssetProvider<T>` (single asset, `TryGet`) vs `IAssetGroupProvider<T>` (collection, `TryGet` + list) vs `IAssetRegistrar` (writes typed registrations into a child VContainer builder) — three single-purpose contracts. The `AssetProvider<T>`/`AssetGroupProvider<T>` base classes are minimal and don't try to be clever.
- **Tests assert DI contract.** `AddressablesInstallerTests` proves `IAddressablesGateway` and `IAsyncInitializable` resolve to the same singleton (`Tests/AddressablesInstallerTests.cs:13-22`) — this matters; a regression there would silently double-init.

## 4. Issues / smells

### 4.1 Real concurrency bug: race between `Release()` and `Complete()` in `AssetHandle<T>`

`Runtime/Implementation/AssetHandle.cs`. The deferred `Load<T>(...)` path constructs an empty `AssetHandle<T>` (`AssetHandle.cs:20-23`), returns it to the caller, and races `CompleteLoadAsync` (`AddressablesGateway.cs:175-186`) on a background task. If the caller calls `handle.Release()` before `Complete` runs:

```text
AssetHandle.cs:83-95   Release()
  releasedFlag := 1                           // sets flag
  if state == Loading: return                 // bails — DOESN'T release the inner handle
  ReleaseReadyHandle(); state = Released

AssetHandle.cs:50-67   Complete(loaded)
  if state != Loading: return
  inner = loaded; asset = loaded.Asset
  state = IsReleased ? Released : Ready       // reads releasedFlag — OK
  completion.TrySetResult(true)
  if (IsReleased) loadedHandle.Release()      // late release path
```

The intent is clear: if `Release()` lost the race, mark released; when `Complete` arrives, release the inner. But there are two holes:

1. **Non-atomic state machine.** `state`, `inner`, `asset` are written without any lock or memory barrier. A reader of `IsReady`/`Asset` on another thread could see `state == Ready` before seeing `asset` (under weak memory; in practice on x86 you'll get away with it, on ARM you may not). These are runtime objects and Unity's main thread will dominate, but the gateway's `CompleteLoadAsync` runs on the task continuation pool, not the main thread.
2. **`Complete` ignores the case where the handle was released *and* the load failed in flight** — fine here because that path runs through `Fail`, but `Fail` (`AssetHandle.cs:69-81`) sets state to `Released` if `IsReleased` then calls `completion.TrySetException(exception)`. If `Release()` already called `TrySetResult(true)` — wait, it doesn't; `Release()` never completes the TCS. So awaiters of `WhenReady` in the racing-release case **block forever** if the inner load also faults after release: `Fail` sets state to `Released` and tries to set the exception, and the released-load-throw path is fine; but `Release()` itself never resolves `completion`. Anyone awaiting `WhenReady` after calling `Release()` waits until the load finishes (or, on cancellation propagation, faults).

Fix: the simplest correct shape is "complete on Release if Loading":

```csharp
public void Release()
{
    if (Interlocked.Exchange(ref releasedFlag, 1) != 0) return;
    if (state == AssetHandleState.Loading)
    {
        state = AssetHandleState.Released;
        completion.TrySetCanceled();   // or TrySetResult(true), or TrySetException(new ObjectDisposedException(...))
        return;
    }
    ReleaseReadyHandle();
    state = AssetHandleState.Released;
}
```

Then `Complete` checks `IsReleased` first and releases the inner immediately. `Fail` becomes a no-op when released.

### 4.2 `AssetHandle<T>` formatting is broken

`AssetHandle.cs:31-34`, `AssetHandle.cs:52-55`, `AssetHandle.cs:71-78`, `AssetHandle.cs:89-92`, `AssetHandle.cs:99-106`, `AssetHandle.cs:111-119`. The whole file uses inconsistent indentation — the `if` body de-dents to column 0, looking like:

```csharp
            get
            {
                if (!IsReady)
{
    throw new InvalidOperationException("Asset handle is not ready.");
}
                return asset;
            }
```

This is auto-format damage that nobody fixed. It's the only file in the package that's like this. The class also mixes "Guard" helpers (`GuardConstructor`, `AssetHandle.cs:110-120`) that the rubric says you shouldn't have (entry-point only). Reformat and inline.

### 4.3 Defensive guard explosion in `AddressablesGateway`

`AddressablesGateway.cs:200-206`:

```csharp
private void GuardRuntimeInvariants()
{
    if (client == null || assetReferenceHandler == null)
        throw new InvalidOperationException("Addressables gateway is not properly initialized.");
}
```

Both fields are `readonly` and assigned in the constructor with `?? throw new ArgumentNullException(...)` (`AddressablesGateway.cs:17-18`). They cannot be null. `GuardRuntimeInvariants()` is called at the top of every public `LoadAsync`/`Load` overload (`AddressablesGateway.cs:34, 41, 49, 58, 65, 75`). Six redundant invariant checks. Delete them.

`GuardLabel` (`AddressablesGateway.cs:208-214`) and `GuardReference` (`AddressablesGateway.cs:216-222`) are used at the entry boundary (label) and inside `ResolveReferenceKey` (reference). The reference one is fine. The label one is also fine **at the gateway entry point**; however, `AddressablesAssetClient.LoadAssetsByLabelAsync` at `Runtime/Implementation/AddressablesAssetClient.cs:52` re-checks the label, and so does `ResolveLabelAsync` at line 72. That's three places guarding the same invariant. The rubric says entry point only — keep `GuardLabel` in the gateway, remove the redundant checks in the client.

`AddressablesAssetClient.cs:31, 51, 71` — `if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);` at the top of every method, then `cancellationToken.ThrowIfCancellationRequested()` again later. The first one is redundant; the second one already does the job. Drop the up-front checks.

`AddressablesLoadedEntry.GuardAsset` (`AddressablesLoadedEntry.cs:20-26`) checks for null, but the entry is constructed inside `AddressablesAssetReferenceHandler.CreateNewEntry` (`AddressablesAssetReferenceHandler.cs:155-164`) only after `client.LoadAssetAsync<T>` returned a non-null asset (the client throws on null at `AddressablesAssetClient.cs:138-141`). Internal-only construction; guard is dead weight.

### 4.4 `Load<T>` (sync) is misnamed and lies

`IAddressablesGateway` exposes both `LoadAsync<T>` and `Load<T>` (`Contracts/IAddressablesGateway.cs:14-16`). The "sync" overloads in the implementation (`AddressablesGateway.cs:56-80`) construct an empty `AssetHandle<T>` / `AssetGroupHandle<T>`, fire-and-forget the actual load, and return the handle. The handle's asset is not ready when this returns; `handle.Asset` throws `InvalidOperationException("Asset handle is not ready.")` (`AssetHandle.cs:30-34`).

This isn't a "sync" API — it's an async-with-out-parameter API masquerading as sync. The naming will trip every junior dev. Worse, the same handle types model both flows (`AssetHandle.cs:11-23` has two ctors: ready-now and lazy-Loading), which is why the concurrency bug in 4.1 exists.

**Pick one:**

- (A) Drop the `Load<T>` overloads entirely — Addressables is async, period. The async `LoadAsync<T>` returning `Task<IAssetHandle<T>>` covers every case.
- (B) Rename to `BeginLoad<T>` / `AcquireDeferred<T>` and document clearly: "the handle exists immediately, the asset isn't ready until `WhenReady` completes."

Option A is cleaner and matches the rubric ("minimum code, maximum extensibility").

### 4.5 `AddressablesAssetReferenceHandler` ref-count semantics are subtle to the point of being wrong

`Runtime/Implementation/AddressablesAssetReferenceHandler.cs`. The flow is:

1. `AcquireAsync` (`:32`) — adds 1 to refcount unless `isPreload` (`:147-153`).
2. `ReleaseEntry` (`:45`) — decrements unless `Policy == NeverDie` (`:81-92`), but **doesn't decrement if `RefCount == 0`** (`:81-87`).
3. `AddOrReuseEntry` (`:122`) — if entry already exists, just bumps refcount and reuses asset.

Problems:

**(a) Preload + immediate Acquire underflow.** A caller does `AcquireAsync(key, NeverDie, isPreload: true, ct)` (refcount stays 0, entry exists). Another caller does `AcquireAsync(key, ct)` → bumps to 1. Consumer releases → decrements to 0 → `ShouldKeepEntry` returns true (NeverDie) → entry stays. Fine.

But: `AcquireAsync(key, Normal, isPreload: false, ct)` on a fresh key creates with `RefCount=1`. Caller releases → entry removed and asset released. **Two callers** acquiring the same key concurrently before the first store: both `TryGetExistingEntry` returns null, both `client.LoadAssetAsync` is invoked twice (cache miss, double-load), then `AddOrReuseEntry` runs — the second one sees the entry in the dictionary, bumps refcount, and **the freshly-loaded asset from the second call is leaked** (no path releases it). See `:122-137` — `created` is only added if not already present; the just-loaded `asset` parameter from the second caller is silently discarded but never `client.Release(asset)`'d. Asset leak under contention.

**(b) The `Map<Type, string, ...>` choice is wasteful.** `loaded` is declared `Map<Type, string, AddressablesLoadedEntry>` (`:24`). The `Map<TPrimary, TSecondary, TValue>` from the maps package adds index/holder/predicate machinery that's pure overhead here — there's no indexer use. A `Dictionary<(Type, string), AddressablesLoadedEntry>` is correct and cheap.

**(c) Lock granularity.** Single `object sync` (`:25`) wraps the whole dictionary. Fine for typical scale, but the lock is held across **`client.LoadAssetAsync`** in `TryGetExistingEntry` returning an existing entry without re-entering the load — wait, no, the load is outside the lock (`:103`). Good. But that's how (a) above happens.

**Fix outline.** Use a per-key in-flight `TaskCompletionSource` cache so concurrent requests share the same load:

```csharp
private readonly Dictionary<(Type, string), Task<UnityEngine.Object>> inflight = new();
private readonly Dictionary<(Type, string), AddressablesLoadedEntry> loaded = new();

// inside AcquireEntryAsync:
Task<UnityEngine.Object> task;
lock (sync)
{
    if (loaded.TryGetValue(k, out var entry)) { /* refcount++; return entry */ }
    if (!inflight.TryGetValue(k, out task))
    {
        task = LoadAndStore<T>(k, ct);
        inflight[k] = task;
    }
}
var asset = await task; // shared by N callers
```

### 4.6 `NeverDie` policy can be flipped by any caller, monotonically

`AddressablesAssetReferenceHandler.ApplyPreloadPolicy` (`:139-145`) lets *any* `AcquireAsync` call upgrade an entry to `NeverDie`, but never back down. Combined with the public `IAssetReferenceHandler` being internal, the only way to call this is through `AcquireAsync(key, PreloadMode preloadMode, bool isPreload, ct)` — which is **not exposed** through `IAddressablesGateway`. Today, no production code can set NeverDie. So this is a half-built feature. Either wire the gateway to plumb a `PreloadMode` (probably via `IAssetProvider`/`IAssetPreloader` calling the internal handler) or delete `PreloadMode` and the `NeverDie` branch. The README explicitly removed the preload-from-gateway pipeline ("Moved preload ownership out of `AddressablesGateway`"), so this is dead code.

### 4.7 `Debug.Log` spam in `InitializeCoreAsync`

`AddressablesGateway.cs:97-99, 108, 116-119, 122-128, 151, 153`. Six log lines every startup, several of them with `this.GetHashCode()` to debug a multi-init issue. Either:

- The bug is fixed → delete all of them.
- The bug isn't fixed → fix it. `initialized` is read inside `TrySkipAlreadyInitialized` under the lock (`:102-114`), then the actual init runs **outside** the lock, then `MarkInitialized` re-enters the lock. Two parallel `InitializeAsync` calls both see `initialized=false`, both proceed, both call `EnsureAddressablesRuntimeInitializedAsync` which calls `UnityAddressables.InitializeAsync()` twice. That's the bug the logs are debugging. Use a `Task` field set under the lock and return that task on second entry, instead of logging.

### 4.8 `IAssetPreloader` and `PreloadMode` look orphaned

`Contracts/IAssetPreloader.cs` is a single-method interface; it's implemented by `AssetProvider<T>` (`AssetProvider.cs:10`) and `AssetGroupProvider<T>` (`AssetGroupProvider.cs:11`), but the gateway/installer don't surface it as a collected service — bootstrap code outside this package is presumably picking up `IAssetPreloader` registrations and calling `PreloadAsync`. That's fine if it's the convention; needs a comment pointing to the consumer.

`PreloadMode` is unreferenced in any public API and reachable only through the internal `IAssetReferenceHandler`. See 4.6 — kill it or wire it.

### 4.9 `IAddressablesAssetClient.Release(UnityEngine.Object)` is too permissive

`Contracts/IAddressablesAssetClient.cs:19`. Anyone with the client can release any asset — bypassing reference counting. The reference handler depends on this (`AddressablesAssetReferenceHandler.cs:78`), but exposing it in the public contract means a consumer can call `client.Release(asset)` directly and corrupt the refcount. Either move `Release` to the internal `IAssetReferenceHandler` (the handler already owns it) and make the public client read-only, or constrain by making the contract `internal`.

Same shape for `AddressablesAssetClient.cs:80-88`: silent return on null. Default-value-hides-error in violation of the rubric. Throw or assert.

### 4.10 `AddressablesAssetClient.LoadAssetsByLabelAsync` returns `Array.Empty<T>()` on no matches

`AddressablesAssetClient.cs:58-61`. "Label resolved zero locations → return empty list." This silently hides a caller misusing labels. The rubric says fail-fast; throw `InvalidOperationException($"Addressables label '{label.labelString}' resolved 0 locations for type '{typeof(T).Name}'.")` and let the caller wrap it if they want optional behavior.

### 4.11 `ComponentGroupHandle<T>` constructor is `internal`, but the type is `internal sealed` — fine. But the asset list is filtered for non-null silently

`AddressablesGatewayComponentExtensions.cs:78-94` does throw if a component is missing — good, fail-fast there. But `AssetGroupHandle.CopyNonNullAssets` (`:88-104`) silently drops null-loaded assets. If a label resolves something Addressables couldn't load to a typed asset, `LoadAssetsAsync` produces a null at that index; the group hides it. Per the rubric, the right thing is to throw or surface a partial-load count. Today the caller has no signal.

### 4.12 `ComponentAssetHandle<T>` exposes `UntypedAsset` as the component, not the GameObject

`Runtime/ComponentAddressableHandles.cs:27` — `public UnityEngine.Object UntypedAsset => component;`. The original handle's `UntypedAsset` was a `GameObject`. This is a subtle type-coupling reversal: the wrapped handle is a `IAssetHandle<GameObject>` but the wrapper reports its `UntypedAsset` as a `Component`. If anyone iterates a `IReadOnlyList<IAssetHandle>` looking at `UntypedAsset`, they'll get heterogeneous results depending on whether the handle was loaded via `LoadAsync` or `LoadComponentAsync`. Either define semantics or rip `UntypedAsset` from the contract.

### 4.13 `AddressablesGatewayComponentExtensions.GuardGateway` (`:48-54`)

Extension method `this`-parameter null check. Fine to keep as the entry boundary, but the rubric says these single-line guard helpers should be inlined. Replace with `_ = gateway ?? throw new ArgumentNullException(nameof(gateway));`.

### 4.14 `Holder<T>.EnsureValue` is a no-op

(noted in maps audit; relevant here because the cache uses `Map<Type, string, AddressablesLoadedEntry>`.)

### 4.15 Tests are thin

Three tests, all DI-wiring. There is no PlayMode coverage of:

- Refcount actually reusing a cached asset.
- Two concurrent `LoadAsync` for the same key returning the same asset.
- `Release()` after `LoadAsync` actually releases the underlying Addressables handle.
- `Load<T>` (deferred) handle's `Release()` while still loading (the bug in 4.1).
- Label-load returning empty.

The `Tests/PlayMode/.gitkeep` is empty. Given how easy the bugs in 4.1 and 4.5 are to write a test for, this is the highest-leverage improvement.

## 5. Suggested before/after snippets

### 5.1 Fix `AssetHandle<T>` race + collapse the dual-mode constructor

**Before** (`Runtime/Implementation/AssetHandle.cs:11-95`, simplified):

```csharp
public AssetHandle(T asset, Action onRelease) { /* ready ctor */ }
public AssetHandle()                          { state = Loading; }

public T Asset
{
    get { if (!IsReady) throw new InvalidOperationException("Asset handle is not ready."); return asset; }
}

internal void Complete(IAssetHandle<T> loadedHandle) { /* swap inner; if released, release inner */ }
public void Release()
{
    if (Interlocked.Exchange(ref releasedFlag, 1) != 0) return;
    if (state == AssetHandleState.Loading) return;   // <- holes here
    ReleaseReadyHandle();
    state = AssetHandleState.Released;
}
```

**After** (separate types — ready-now and deferred — and remove the deferred path entirely if you take the rubric advice):

```csharp
internal sealed class AssetHandle<T> : IAssetHandle<T> where T : UnityEngine.Object
{
    public AssetHandle(T asset, Action onRelease)
    {
        this.asset = asset;
        this.onRelease = onRelease;
        completion.TrySetResult(true);
    }

    private readonly T asset;
    private readonly Action onRelease;
    private readonly TaskCompletionSource<bool> completion = new();
    private int releasedFlag;

    public Type AssetType => typeof(T);
    public UnityEngine.Object UntypedAsset => asset;
    public T Asset => asset;
    public bool IsReleased => releasedFlag != 0;
    public AssetHandleState State => IsReleased ? AssetHandleState.Released : AssetHandleState.Ready;
    public bool IsReady => !IsReleased;
    public Task WhenReady => completion.Task;

    public void Release()
    {
        if (Interlocked.Exchange(ref releasedFlag, 1) != 0) return;
        onRelease();
    }
}
```

Loading state, `Faulted`, `Complete`/`Fail` all disappear with the deferred API.

### 5.2 Strip the gateway down

**Before** (`AddressablesGateway.cs:39-71`):

```csharp
public async Task<IAssetHandle<T>> LoadAsync<T>(AssetReference reference, CancellationToken ct = default)
    where T : UnityEngine.Object
{
    GuardRuntimeInvariants();
    ct.ThrowIfCancellationRequested();
    string key = ResolveReferenceKey(reference);
    return await assetReferenceHandler.AcquireAsync<T>(key, ct);
}

public IAssetHandle<T> Load<T>(AssetReference reference, CancellationToken ct = default)
    where T : UnityEngine.Object
{
    GuardRuntimeInvariants();
    ct.ThrowIfCancellationRequested();
    string key = ResolveReferenceKey(reference);
    AssetHandle<T> handle = new AssetHandle<T>();
    _ = CompleteLoadAsync(key, handle, ct);
    return handle;
}
```

**After:**

```csharp
public Task<IAssetHandle<T>> LoadAsync<T>(AssetReference reference, CancellationToken ct = default)
    where T : UnityEngine.Object
{
    if (reference?.RuntimeKey is null)
        throw new ArgumentException("Asset reference is not valid.", nameof(reference));
    return assetReferenceHandler.AcquireAsync<T>(reference.RuntimeKey.ToString(), ct);
}

// no Load<T> overloads, no GuardRuntimeInvariants, no fire-and-forget tasks
```

### 5.3 Replace `Map<Type,string,Entry>` with a tuple-keyed dictionary

**Before** (`AddressablesAssetReferenceHandler.cs:24`):

```csharp
private readonly Map<Type, string, AddressablesLoadedEntry> loaded = new();
```

**After:**

```csharp
private readonly Dictionary<(Type, string), AddressablesLoadedEntry> loaded = new();
private readonly Dictionary<(Type, string), Task<UnityEngine.Object>> inflight = new();
```

…and re-key the lookups accordingly. Removes the `Scaffold.Maps` dependency from the addressables runtime (it's currently in `package.json:14`).

### 5.4 Drop the `IAddressablesAssetClient` cancellation pre-checks

**Before** (`AddressablesAssetClient.cs:31, 51, 71`):

```csharp
if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
// ... do thing ...
cancellationToken.ThrowIfCancellationRequested();
```

**After:**

```csharp
cancellationToken.ThrowIfCancellationRequested();   // once, at the top, only if there's no obvious yield point earlier
// ... do thing ...
cancellationToken.ThrowIfCancellationRequested();   // before/after each await is the standard idiom
```

## 6. Easy wins (5–8)

1. **Delete `GuardRuntimeInvariants()`** in `AddressablesGateway` (`:200-206`) and its six call sites.
2. **Delete the six `Debug.Log`s** in `InitializeCoreAsync` (`:97-128, 151, 153`).
3. **Reformat `AssetHandle.cs`** — fix the column-0 `if`-bodies (`:31-34, 52-55, 71-78, 89-92, 99-106, 111-119`).
4. **Remove `Scaffold.Maps` dependency** from `package.json:14` — replace `Map<Type, string, AddressablesLoadedEntry>` (`AddressablesAssetReferenceHandler.cs:24`) with a tuple-keyed `Dictionary`.
5. **Delete `IAssetPreloader`-orphaned `PreloadMode`** and the second `AcquireAsync(...PreloadMode...)` overload until preload-via-gateway is actually wired (`Contracts/PreloadMode.cs`, `Internal/IAssetReferenceHandler.cs:11`, `AddressablesAssetReferenceHandler.cs:32-43, 139-153`).
6. **Throw on empty label load** in `AddressablesAssetClient.LoadAssetsByLabelAsync` (`:58-61`) instead of `Array.Empty<T>()`.
7. **Inline `EnsureValue`/`EnsureAssetWasLoaded`/`GuardAsset`/`GuardLabel` helpers** that exist solely to host a single-line null check.
8. **Remove `Holder<T>` indirection** from the cache entry (it's only needed for `Indexer.Track` in the maps package — see #4 above). `AddressablesLoadedEntry` already wraps the asset.

## 7. Bigger refactors

### 7.1 Collapse "deferred sync" handle into pure async

Per 4.4 and 5.1. Drops `AssetHandle.cs:20-23, 50-95`, `AssetGroupHandle.CompleteFromAssets/Fail` (`:30-56`), and the three `Load<T>` overloads on `IAddressablesGateway`. Net deletion. Strictly better.

### 7.2 Fix the cache + add an in-flight task table

Per 4.5 and 5.3. Single source of truth for "asset loading", "asset loaded with refcount N", "asset NeverDie pinned". Document the state machine on the class with a 5-line comment.

### 7.3 Decide preload story end-to-end

Per 4.6/4.8. Either:

- The gateway plumbs a `PreloadMode` from `IAssetProvider.PreloadAsync` down to `IAssetReferenceHandler.AcquireAsync(..., PreloadMode, isPreload:true, ...)`, refcount stays 0, NeverDie pins it. This is what `PreloadMode` was clearly built for.
- Or, delete `PreloadMode`, delete the second `AcquireAsync` overload, and have `AssetProvider<T>.PreloadAsync` simply load + hold a strong reference (`LoadedAsset` field, `AssetProvider.cs:18`) — when the provider is released, the asset is released.

The README says preload was moved out of the gateway. Pick the first option (preload-via-internal-NeverDie) and finish wiring it, or commit to the second and remove the dead types.

### 7.4 `IAddressablesAssetClient` should be internal

Per 4.9. The README markets it as a public publisher/catalog helper, but `Release(UnityEngine.Object)` is a footgun. Either:

- Make the contract `internal`, expose only the catalog helpers as a separate public `IAddressablesCatalog` interface (`SyncCatalogAndContentAsync`, `ResolveLabelAsync<T>`).
- Or split: `IAddressablesCatalog` (public) + `IAddressablesAssetClient` (internal).

This is the kind of "abstraction at known-changing boundary" the rubric calls for; CCD/catalog ops will keep evolving (publisher API, badge management) and you want them on a stable, narrow contract.

### 7.5 PlayMode tests for the actually-tricky paths

Per 4.15. Six tests covering: refcount-reuse, concurrent-load-dedup, release-while-loading, label-empty-throws, double-init-is-idempotent, NeverDie-pin-survives-release. ~150 lines. Pays for itself the first time someone touches the handler.

## 8. Organization & docs

The README is unusually thorough (CCD setup walkthrough, public-API table, explicit list of related files). It is also slightly out of date:

- `Best Practices > "Release handles/groups exactly once."` is the contract today. With `Interlocked.Exchange` guards on both `AssetHandle.Release` (`:85`) and `AssetGroupHandle.Release` (`:65`), double-release is safe (no-op). Either tighten the implementation (throw `ObjectDisposedException` on second release) or update the README to "release at least once" (idempotent semantics), but pick one.
- The `Provider and Registrar Flow` section says concrete providers may inject `IAddressablesGateway`; the base classes already require it (`AssetProvider.cs:13`, `AssetGroupProvider.cs:13`). Drop "may"; it's mandatory if you use the bases.
- `Public API` table doesn't mention the synchronous `Load<T>` overloads. Either remove them (preferred) or document them as deferred-handle.
- `Tests/PlayMode/.gitkeep` and asmdef should be deleted until there are actual PlayMode tests, or filled in.
- The `Editor/` folder contains only an asmdef, no scripts. Remove the asmdef or add an editor-only Build & Release helper (the README has a CCD section that begs for an editor button).

Namespace hygiene is good (`Scaffold.Addressables`, `Scaffold.Addressables.Contracts`, `Scaffold.Addressables.Internal`, `Scaffold.Addressables.Container`). The `InternalsVisibleTo` list is appropriately narrow (`AssemblyInfo.cs:3-4`).

## References (Unity Addressables / IAssetProvider patterns)

- Unity, *Addressables runtime memory management* — explicit `Release` for every `LoadAssetAsync` is required; no auto-collection. See `https://docs.unity3d.com/Packages/com.unity.addressables@2.9/manual/MemoryManagement.html`. The package's centralized refcount in `AddressablesAssetReferenceHandler` is the right shape; the implementation needs the in-flight dedup from 4.5 to be safe.
- Unity, *AssetReference / AssetReferenceT<T>* — typed references hide string keys, exactly as `IAddressablesGateway.LoadAsync<T>(AssetReferenceT<T>)` does. See `https://docs.unity3d.com/Packages/com.unity.addressables@2.9/manual/AssetReferences.html`.
- Andrew "Unity3DCollege" Connell and the community pattern *"don't let `AsyncOperationHandle` cross your boundary"* — adopted here. Good.
- Unity, *Cloud Content Delivery + Addressables* — the README's CCD walkthrough matches `https://docs.unity3d.com/Packages/com.unity.addressables@2.9/manual/AddressablesCCDIntegration.html`. The gateway's best-effort `SyncCatalogAndContentAsync` swallowing transient failures matches Unity's own guidance for online-optional clients.
- Microsoft, *async/await and TaskCompletionSource* — using `TrySetResult`/`TrySetException`/`TrySetCanceled` and never resolving on the synchronous-shortcut path is the bug class behind 4.1 (`https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap`).

## 9. Consumers

Concrete consumers of `Scaffold.Addressables` outside the package itself (the only ones in the repo are inside `com.scaffold.navigation`):

- `Assets/Packages/com.scaffold.navigation/Runtime/Implementation/AddressablesNavigationPointStrategy.cs:47` — calls **`addressables.Load<GameObject>(config.Asset)`** (the deferred-sync overload, not `LoadAsync`). The handle is then awaited via `await handle.WhenReady` at `:80`, with a `point.Disposed` check before the await and after. **This is the exact call site that triggers the race in audit 4.1**: if `point.Disposed` flips between line 80 and the second `getHandle()` at `:81`, `ReleaseOrBuffer` runs `handle.Release()` while `state == Loading` (early-return on `AssetHandle.cs:85-87`), `WhenReady` never resolves, the awaiter at `:80` is permanently parked, and the inner `AsyncOperationHandle` continues to load and is **never released** — a textbook leak under contention plus a stuck task. Also: every `NavigationPoint.Disposed` callback eventually calls `handle.Release()` once-or-via-`assetHandleBuffer.Return` (`:106-129`), so the audit's "Release at least once" is correct in shape, but on the deferred path it's racy.
- `Assets/Packages/com.scaffold.navigation/Runtime/Implementation/NavigationAssetHandleBuffer.cs:16` — pools released `IAssetHandle<GameObject>` instances **per `ViewConfig`**, max 2. The buffer pops handles whose `handle.IsReleased == true` and `State == Faulted` are filtered out (`:75-83`). This is wrapping-the-wrapper: a second-level cache on top of `AddressablesAssetReferenceHandler`'s refcount cache — meaning a single asset can have a refcount of 1 in the handler and be sitting idle in the navigation pool, which **defeats** Addressables' eviction semantics. If the handler's NeverDie pin (4.6) ever gets wired, this pool becomes redundant; if it doesn't, the pool serves as the de-facto retention layer. Pick one.
- `Assets/Packages/com.scaffold.navigation/Runtime/Implementation/NavigationProvider.cs:11` and `NavigationController.cs:15` — receive `IAddressablesGateway` via constructor injection. Pure pass-through to the strategy; no smell.
- `Assets/Packages/com.scaffold.navigation/Runtime/Providers/NavigationAssetProvider.cs:9` — extends `AssetProvider<NavigationConfig>(gateway)`. The provider/registrar shape is being used as designed.
- `Assets/Packages/com.scaffold.navigation/Tests/NavigationInstallerAndInjectionTests.cs` — DI-wiring tests that resolve the gateway via VContainer. No runtime use.

Verdict on consumer disposal: navigation does call `handle.Release()` on every disposal path (`AddressablesNavigationPointStrategy.cs:65, 129`), but **only on the post-`WhenReady` paths** — the failure mode is the loading-state race, not double-release. The `IAssetGroupHandle<T> : IDisposable` contract is **never used by any consumer in the repo**; nobody loads by label. The whole `IAssetGroupHandle` / `IAssetGroupProvider<T>` half of the public surface is exercised only by the package's own tests. That's a load-bearing observation: half the gateway has no production traffic.

## 10. Alternatives & prior art

- **Native `AddressableAssetSettings` + Unity 2.x `AsyncOperationHandle`** — `https://docs.unity3d.com/Packages/com.unity.addressables@2.9/manual/`. **Verdict: Steal pattern.** What the package gets right (don't leak `AsyncOperationHandle` across the boundary, centralize refcount, swallow CCD failures at boot) is the canonical advice; the bug surface (the deferred sync overload, the in-flight dedup gap) is what custom wrappers historically get wrong. Read the manual's *Memory Management* page once and re-derive the wrapper from scratch — half the file shrinks.
- **CySharp `UniTask.Addressables`** (`https://github.com/Cysharp/UniTask`, `UniTask.Addressables` namespace) — converts `AsyncOperationHandle.ToUniTask()` and provides allocation-free awaiters. **Verdict: Adopt where the project already has UniTask.** Doesn't replace the gateway, but kills the `Task` allocations on every `WhenReady`. If the project doesn't already depend on UniTask, don't pull it in just for this.
- **Unity Sentinel** (`https://github.com/Unity-Technologies/com.unity.addressables` ecosystem; community packages like `https://github.com/dilmerv/UnityAddressablesSamples` and `AddressableTools`) — refcount-aware lifetime extensions. **Verdict: Build.** None of them ship the exact "preload-with-NeverDie" semantic the README invokes, and the refcount story is small enough to own. Steal naming conventions (`Acquire`/`Release`, `IsAlive`).
- **AssetKit / yet-another-addressables-wrapper** (e.g. `https://github.com/akihirosaiki/UnityAddressablesUtility`, `https://github.com/yasirkula/UnityAssetUsageDetector` adjacent tools) — typed wrappers with `AssetReference<T>` validation. **Verdict: Wrap.** The `AssetReferenceT<T>` overload (`IAddressablesGateway.cs:13`) already does the typed-key job; nothing to import.
- **Reflex DI + native Addressables** (`https://github.com/gustavopsantos/Reflex`) — Reflex containers have native `IInstaller` patterns very similar to VContainer's. **Verdict: Steal pattern.** Reflex's per-scene scoping makes module-local preload an order of magnitude simpler than the current `IAssetRegistrar` flow; if you ever rewrite the preload pipeline (per audit 7.3), look at how Reflex composes scopes before hand-rolling a registrar.

## 11. Benchmark plan

Test asmdef target: extend `Assets/Packages/com.scaffold.addressables/Tests/PlayMode/` (the empty `.gitkeep` dir from audit 4.15) and a new `Assets/Packages/com.scaffold.addressables/Tests/Performance/` for `[Test, Performance]` cases. Tool baseline: **Unity.PerformanceTesting** for allocation/timing; **NUnit EditMode** for race-correctness proofs (no perf assertion, just deterministic-fail under contention).

- **Race: `AcquireAsync` concurrent calls for same key produce one underlying load.** Tool: NUnit EditMode + `IAddressablesAssetClient` test double that counts `LoadAssetAsync<T>` invocations and blocks until released. Test location: `Tests/PlayMode/AddressablesAssetReferenceHandlerConcurrencyTests.cs`. Scenario: spawn N=10 / 100 / 1000 `Task.Run` calls of `handler.AcquireAsync<TextAsset>("k", ct)`, await all, then `Release` each. Baseline expectation: today's code calls `LoadAssetAsync` ≥2 times under contention because `TryGetExistingEntry` is not paired with an in-flight table (audit 4.5a). Success criteria: **exactly one** `LoadAssetAsync` invocation, and final refcount drops to 0 with the asset released exactly once. Test should **fail on `main`** until the in-flight `Dictionary<(Type, string), Task<...>>` from 5.3 lands.
- **Defect proof: `AssetHandle<T>.Release()` while `state == Loading` deadlocks awaiters.** Tool: NUnit EditMode with a `TaskCompletionSource`-driven `Complete` trigger. Test location: `Tests/PlayMode/AssetHandleReleaseRaceTests.cs`. Scenario: build an `AssetHandle<TextAsset>` via the deferred ctor, start an `await handle.WhenReady` continuation on a captured `TaskScheduler`, call `handle.Release()`, then **never** call `Complete`. Baseline expectation: the awaiter never resolves; assert with `Task.WhenAny(awaiter, Task.Delay(500))` returns the delay task. Success criteria after fix: `Release()` calls `completion.TrySetCanceled()` and the awaiter throws `OperationCanceledException` within ms.
- **Leak proof: `AcquireAsync` losing the cache race leaks the second-load asset.** Tool: NUnit EditMode + counting test double. Test location: same file as the first bullet. Scenario: a fake client whose `LoadAssetAsync` returns distinct `ScriptableObject` instances with `++loadCount`, two concurrent acquires of the same key, then count `Release(asset)` calls. Baseline expectation: 2 loads, 1 release → 1 leaked asset. Success criteria after fix: 1 load (in-flight dedup) and 1 release on `Release()`, no leak.
- **Cache: `Map<Type, string, AddressablesLoadedEntry>` vs `Dictionary<(Type, string), Entry>` lookup + alloc.** Tool: Unity.PerformanceTesting. Test location: `Tests/Performance/AddressablesCacheLookupBench.cs`. Scenario: 10 / 100 / 1000 distinct keys, 10k repeated lookups via `TryGetExistingEntry`. Baseline expectation: tuple-keyed dictionary is roughly equal in lookup time and allocates **zero** extra heap objects (the `Holder<T>` wrapper goes away); current `Map<,,>` allocates one `Holder<T>` per `Add` and routes lookups through `Index<TPrimary, TSecondary>` boxing-free struct keys but has ~2x the indirection. Success criteria: tuple version ≤ Map version on time and strictly fewer allocations on `Add`.
- **Smoke: `IAddressablesGateway.Load<T>` returns a handle whose `Asset` throws while loading.** Tool: NUnit EditMode. Test location: `Tests/PlayMode/AddressablesGatewayDeferredLoadTests.cs`. Scenario: call `Load<T>` on a fake gateway, assert `handle.IsReady == false` and `handle.Asset` throws. After audit-recommended deletion of `Load<T>`, this test goes away with the API. Success criteria: today the test passes; after refactor 7.1, the test is **deleted alongside the API**.
- **Init dedup: `InitializeCoreAsync` called twice in parallel completes once.** Tool: NUnit EditMode. Test location: `Tests/PlayMode/AddressablesGatewayInitTests.cs`. Scenario: two `Task.Run(() => gateway.InitializeAsync(ct))` calls, count how many times the underlying client `EnsureAddressablesRuntimeInitializedAsync` runs. Baseline expectation: today, **two** runs (audit 4.7). Success criteria after fix: one run, both tasks complete, no `Debug.Log` chatter.

