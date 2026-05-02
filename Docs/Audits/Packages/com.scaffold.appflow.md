# Audit — `com.scaffold.appflow`

Senior architect review. Tone: opinionated. Treat preferences in the rubric as binding.

## 1. Summary & verdict

`com.scaffold.appflow` is the boot/startup orchestrator. It is **not** a state machine and **not** a flow DSL. It is a **stacked VContainer scope manager** with an ordered linear sequence (`InstallAllAsync` walks an `IEnumerable<IScopeLayer>`) plus ad-hoc `PushAsync` / `PopAsync` for runtime scope lifecycle. Layers expose `Install`, optional async `PrepareAsync`, optional `IInitializableLayer.InitializeAsync`, `IAsyncInitializable` runners, `IAsyncDisposable` teardown, `ILayerProgressSource` reporting, and an `ILayerPublisher` cross-layer bus.

Strengths: solid contract layering, decent typed progress model, clean error pipeline with dedup. The host has been refactored into many small private methods to keep methods short — this is overdone in places (one logical operation chopped into 6 methods that pass the same 4 args around). Async hygiene is mostly good; `_ = ct` in `PopAsync` is a real bug surface.

The single biggest gap relative to the rubric: **no real flow abstraction**. The architect explicitly said "flow / navigation / scene loading WILL keep changing → these are the right places for solid abstraction." This package is named "AppFlow" but it has no `IFlowStep<TIn,TOut>`, no typed flow result, no scheduler abstraction worth talking about (one parallel scheduler, no sequential or weighted variants), and no notion of named stages (Boot / Intro / Login / MainLoop). It is a *scope assembler* dressed up as a flow engine. Naming and intent diverge.

**Verdict: refactor.** Keep the scope-stack mechanics. Build a typed `IAppFlowStage` / `IAppFlow<TContext>` layer on top so the package name pays for itself. Strip the over-decomposed orchestration methods in `AppFlowHost`. Fix the `_ = ct` swallow.

---

## 2. Structure

```
com.scaffold.appflow/
  Runtime/                 (Scaffold.AppFlow.asmdef, autoReferenced=true, override=true, refs=VContainer)
    AppFlowHost.cs
    AppFlowRoot.cs
    AssetPublisherBase.cs
    AssemblyInfo.cs                (InternalsVisibleTo Tests)
    Contracts/             (10 contract files: IScopeLayer, IAsyncScopeLayer, IInitializableLayer,
                            IAsyncInitializable, IInLayerScheduler, ILayerInitRunner,
                            ILayerProgressSource, ILayerPublisher, ILayerResolver, LayerOperation)
    Errors/                (AppFlowErrorHandler, AppFlowErrorInfo, AppFlowErrorPhase,
                            IAppFlowErrorHandler)
    Internal/              (LayerEntry, LayerInitRunner, LayerPublisher, LayerResolverProxy)
    Progress/              (AppFlowProgress, AppFlowSession, AppFlowOutcome,
                            IAppFlowProgress, LayerProgressEntry, LayerStatus)
    Schedulers/            (ParallelScheduler — only one)
  Samples/                 (Scaffold.AppFlow.Samples.asmdef, not autoReferenced)
    SampleAppFlowRoot.cs, SampleLoadingScreen.cs
    Layers/SampleAssetsLayer.cs, SampleConfigsLayer.cs, SampleFeatureLayer.cs
  Tests/Editor/            (Scaffold.AppFlow.Tests.asmdef)
    AppFlowErrorHandlerTests.cs, AppFlowHostTests.cs, AppFlowProgressTests.cs
```

Asmdef is sane: runtime depends only on VContainer. Samples reference SceneFlow purely to reuse `LoadingView`. Tests cover error reporting, progress, push/pop happy paths, and override warning. PlayMode coverage is missing — host tests use `LifetimeScope.Create` from edit mode and have to `LogAssert.Expect("Destroy may not be called from edit mode")` to pass, which is a smell.

`package.json` declares unity 6000.0 and depends on VContainer 1.17.0 from a git URL — fine.

---

## 3. What's good

- **Typed error pipeline.** `AppFlowErrorInfo` is a `readonly struct` with `AppFlowErrorPhase`, layer name, source, exception, UTC timestamp. Single sink (`AppFlowErrorHandler`) with exception dedup via `Exception.Data["AppFlow.Reported"]` to avoid double-logging the same exception across catch frames (`AppFlowErrorHandler.cs:52-66`). Subscriber failures are isolated (`:96-112`).
- **Ring buffer of recent errors** (`AppFlowErrorHandler.cs:68-81`) — useful for diagnostics.
- **Outcome typing.** `AppFlowOutcome` distinguishes `Succeeded`, `Cancelled`, `Failed(exception)`. Consumers can fan-out cleanly (`AppFlowOutcome.cs`).
- **Progress is ViewModel-friendly without being one.** `AppFlowSession` snapshot is immutable, `Changed` event raises snapshots, `WhenSessionCompleted()` returns a `Task<AppFlowOutcome>`. Good MVVM seam.
- **`ILayerProgressSource`** is a clean optional contract (`ILayerProgressSource.cs`); host wires/unwires per-layer and clamps in progress (`AppFlowProgress.cs:158-164`).
- **`ILayerPublisher`** is the most useful idea here — generic publish into the *next* child scope without `PrepareAsync` field-stash plumbing (`LayerPublisher.cs`, replayed via `EnumerateStackRootToParent` in `AppFlowHost.cs:259-275`).
- **Cancellation token plumbed** through `IAsyncScopeLayer.PrepareAsync`, `IAsyncInitializable.InitializeAsync`, `IInitializableLayer.InitializeAsync`, and `AppFlowHost.PushAsync` / `InstallAllAsync`.
- **Async unwind** rolls back already-pushed layers if a later one throws (`AppFlowHost.cs:64-72`, `:87-101`).
- **Tests cover the `IInitializableLayer` warning path** when a layer overrides `InitializeAsync` but skips `RunDefaultInitAsync` (`AppFlowProgressTests.cs:167-182`).

---

## 4. Issues & smells

### 4.1 The package is named "AppFlow" but is a scope manager, not a flow

There is no `IAppFlowStage`, no `IAppFlow<TContext>`, no flow-graph, no named stages (Boot / Intro / Login / MainLoop). Stages are *layers*, but `IScopeLayer.Install(IContainerBuilder)` is a DI-builder seam, not a stage seam. A "stage" in a real product flow has a typed input, a typed result, and a place to declare its predecessor — none of that exists here. The architect explicitly flagged this domain ("flow / navigation / scene loading WILL keep changing"). Right now AppFlow is under-abstracted at the place where it should be most extensible, while it has the *opposite* problem inside `AppFlowHost` (over-decomposed).

The README (`README.md:24-29`) describes startup as `BeginSession("Startup", count)` + a list of layers, with ad-hoc `Push:<Name>` sessions for runtime scope changes. That is a perfectly fine implementation primitive; it should be the *bottom* of the stack, not the public surface.

Suggested layering (see §5.1):

```
IAppFlow<TContext>          -- typed flow, has Stages
IAppFlowStage<TContext>     -- typed stage, has Run(ctx, ct)
AppFlowHost (current)       -- still here, used by stage runner
```

### 4.2 `_ = ct` swallow in `PopAsync` (real async leak)

`AppFlowHost.cs:143-151`:

```csharp
public async Task PopAsync(CancellationToken ct)
{
    LayerEntry entry = RemoveTopEntry();
    bool adHocSession = sessionDepth == 0;
    int layerIndex = BeginPopSessionIfNeeded(entry, adHocSession);
    await RunPopDisposeAsync(entry, layerIndex, adHocSession);
    CompletePopProgress(layerIndex, adHocSession);
    _ = ct;
}
```

`_ = ct;` is a code smell screaming "compiler shut up about my unused parameter." `PopAsync` accepts a `CancellationToken` and silently ignores it. This violates the rubric's "fail-fast / no defaults that hide errors" rule. Either (a) honor it (`ct.ThrowIfCancellationRequested()` between awaitables; pass it to `DisposeAsync` callers via a token-aware shim) or (b) drop the parameter from the public signature. Lying about cancellation in a teardown path is the worst option — callers will believe a cancel actually cancels.

### 4.3 `async void Start()` in `AppFlowRoot`

`AppFlowRoot.cs:29-43` runs the entire startup pipeline from `async void Start()`. This is the standard Unity pattern, and it is wrapped in `try/catch` with a `TaskCompletionSource` (`ReadyTask`), so it is not catastrophically wrong. But:

- A second `Start()` call (e.g. during Domain Reload bypass) double-runs.
- The `OnStartupFailedAsync` chain catches *its own* exceptions and reports them through the error handler, but `try` blocks nest 3 deep (`Start` → `RunStartupAsync` → `ExecuteStartupSessionAsync` → ...). Each level has been split into its own tiny method (`RunStartupBodyAsync`, `ExecuteStartupSessionAsync`, `RunStartupFailedCallbackSafeAsync`, `SafeOnStartupFailedAsync`, `TryReportStartupPhaseToHandler`, `ReportStartupFailureAndCompleteReadyAsync`). The control flow is harder to follow than a single 25-line method.

This is exactly the "redundant guard / over-decomposition" the rubric warns against. See §5.2.

### 4.4 `BeginAdHocPushIfNeeded` / `RunPushWithSessionAsync` — over-decomposition

`AppFlowHost.cs:103-141` chops one push into 4 tiny methods:

- `PushAsync` (calls into adhoc-begin then run-with-session)
- `BeginAdHocPushIfNeeded`
- `RunPushWithSessionAsync` (calls `ExecutePushCoreAsync` + ends session)
- `ExecutePushCoreAsync`

`HandlePushFailed`, `BeginPopSessionIfNeeded`, `HandlePopDisposeFailed`, `CompletePopProgress`, `DisposePopScopeResources`, `RunPopDisposeAsync`, `RunDisposeWaveAsync`, `ReleaseEntryMembership`, `RebindProxyToTop`, `FinishSuccessfulPush`, `RecordEntry`, `RunLayerInitAndCollectDisposablesAsync`, `BindLayerProgressIfNeeded`, `UnbindLayerProgress`, `AttemptPushAsync` — same pattern. Many of these are 3–6-line methods called once. This is the "every method must be ≤ 8 lines" school of refactoring; it makes the host harder to reason about because state mutations are scattered across a cloud of one-shot helpers. Inline the trivial ones.

### 4.5 `ParallelScheduler` is the only scheduler, but `IInLayerScheduler` is the abstraction

`Schedulers/ParallelScheduler.cs` does `Task.WhenAll(fresh.Select(p => p.InitializeAsync(ct)))`. There is no `SequentialScheduler`, no weighted/throttled scheduler, no priority scheduler. `IInLayerScheduler` has one method and one impl. The README ships no guidance on writing a new scheduler. The seam is fine, but ship at least a `SequentialScheduler` — the ordering question (do init waves *need* to run in parallel?) is real and a sequential default is often safer for first-pass installation.

Also: `Task.WhenAll` here aggregates exceptions but the host catches the first one. A failing layer with N pending initializables produces an `AggregateException` whose `.InnerException` is logged — fine — but the others become `Exception.Data["AppFlow.Reported"]`-deduped *only if* dedup applies. They actually never reach the handler. Worth documenting.

### 4.6 `AppFlowErrorPhase.Configure` and `Install` exist but are never reported

`AppFlowErrorPhase.cs` enumerates `Configure`, `Prepare`, `Install`, `Init`, `Dispose`, `Unwind`, `Startup`, `Manual`. `LayerOperation` (`LayerOperation.cs`) only has `Prepare, Init, Dispose, Unwind` — and those are the only ones `CreateErrorPhaseFromLayerOperation` (`AppFlowHost.cs:444-454`) ever produces. `Configure` and `Install` phases never fire from the host. Either remove them or wire them. Dead enum values are a fail-fast anti-pattern — they suggest a path that doesn't exist.

### 4.7 `Debug.LogError` and `Debug.LogWarning` mixed into pure-C# orchestration

`AppFlowHost.cs:326`, `:349`, `LayerResolverProxy.cs` (no Debug, fine), `AppFlowErrorHandler.cs:88, :92, :110`, `AppFlowProgress.cs:373`, `AppFlowRoot.cs:60, :72`. The architect's rule is "Keep Unity and pure C# at separate boundaries." `AppFlowHost` is pure orchestration but writes Unity logs directly. Inject an `IAppFlowLogger` (or pipe through `IAppFlowErrorHandler.Report` for warnings as a `Manual` phase). The host then has zero `UnityEngine` references and can be unit-tested without Unity.

The host already imports `UnityEngine` (`AppFlowHost.cs:7`). It uses `LifetimeScope.CreateChild` so it can't lose Unity entirely without abstracting that — but the *logging* can move out trivially.

### 4.8 `AppFlowProgress.HostSetSubProgress` always returns `mutated = true`

`AppFlowProgress.cs:139-156` and `:158-164` — `ApplySubProgressLocked` unconditionally returns `true` and replaces the entry struct even when the clamped value is identical. This means high-frequency progress sources (e.g. an Addressables `PercentComplete` polling) can cause an event flood with no actual delta. Cheap fix: compare and skip.

### 4.9 `AppFlowProgress` raises `Changed` outside the lock but builds a *second* snapshot under a *re-acquired* lock

`AppFlowProgress.cs:345-363`. `RaiseChanged` calls `CaptureSnapshotForSubscribers()` which re-locks `gate`. So every state mutation: lock → mutate → unlock → call RaiseChanged → re-lock to snapshot → unlock → invoke handler. Two locks per change. Cheaper: take the snapshot inside the original lock (the call sites already hold it once) and pass it out. The current shape is also race-prone: between unlock and snapshot lock, another thread can mutate, so subscribers receive a snapshot that wasn't the one their event corresponds to. For startup this is probably fine. Document or fix.

### 4.10 `IScopeLayer.Name` default = `this.GetType().Name`

`IScopeLayer.cs:7`: `string Name => this.GetType().Name;` — a default interface implementation. Fine, but ad-hoc session names then become `Push:SampleFeatureLayer` which is a typed identifier dressed as a string. For typed flows you want `IScopeLayer<TKey>` or stage keys (`enum`/`record`). String-keyed names will haunt you when the architect adds analytics events.

### 4.11 `LayerInitRunner.RunDefaultInitAsync` flips `DefaultInitInvoked` *before* awaiting

`LayerInitRunner.cs:25-29`:

```csharp
public Task RunDefaultInitAsync(CancellationToken ct)
{
    DefaultInitInvoked = true;
    return scheduler.RunAsync(PendingInitializables, ct);
}
```

If the scheduler throws synchronously (rare but possible), `DefaultInitInvoked = true` is already set, and the warning in `AppFlowHost.InitializeCustomLayerAsync` (`AppFlowHost.cs:321-330`) won't fire even though the wave failed. This is benign but inconsistent. Set after `await`.

### 4.12 `AppFlowProgress` constructor takes `IAppFlowErrorHandler` and adds a never-removed handler

`AppFlowProgress.cs:13-15`: `this.errorHandler.OnError += OnErrorFromHandler;`. There is no unsubscribe path. `AppFlowProgress` is singleton-scoped to the root scope, so leak is bounded — but the design has no `IDisposable` on `AppFlowProgress`. If the root scope is recreated (PlayMode start/stop in the editor), the handler keeps a reference to a stale progress instance. Make `AppFlowProgress : IDisposable` and unsubscribe.

### 4.13 `AppFlowErrorHandler.ReportedDataKey` mutates the exception object

`AppFlowErrorHandler.cs:62-65` writes to `info.Exception.Data` to dedup. This is an in-band side effect on a shared object. If anything else inspects `Exception.Data`, it sees an `"AppFlow.Reported" = true` key. Use a `ConditionalWeakTable<Exception, byte>` instead.

### 4.14 `Task` everywhere — no UniTask

The README says VContainer is the only dep; Cysharp UniTask is widely used in Unity for zero-allocation, sync-context-aware async without `Task.WhenAll` hot-path allocs. Given the project ships Roslyn analyzers and source generators (your stated convention), and Unity 6000.0, UniTask is a near-default. `Task.Delay(200, ct)` in samples (`SampleAssetsLayer.cs:24`) and `ParallelScheduler` would benefit. Decision is fine either way, but document the rule explicitly. The current `Task.WhenAll(fresh.Select(...).ToArray())` allocates a closure + array per init wave.

### 4.15 `AppFlowHost.PushAsync` constructor does not validate `errorHandler`/`progress`

`AppFlowHost.cs:15-28`. Both are nullable parameters; the host then guards at every use site (`if (errorHandler == null) return;`, `progress?.HostXxx(...)`). This is exactly the "redundant guard clause" the rubric calls out. Two paths:

- **Fail-fast**: require both, drop nullability, drop every internal null-check (preferred per rubric).
- Provide null-object defaults (`NullErrorHandler`, `NullProgress`) and drop the checks.

Either is more honest than null-everywhere.

Same pattern for `SceneFlowService` (see SceneFlow audit) and `NavigationInstaller` — this is repo-wide.

### 4.16 `AppFlowRoot` test (`Root_ExposesProgressAndErrorsBeforeFirstLayer`) accesses `root.Container` after `AddComponent` without invoking `Awake/Configure`

`AppFlowProgressTests.cs:148-164`. The test calls `root.Build()` if container is null. This works because `LifetimeScope.Build()` calls `Configure`. But the public `AppFlowRoot.Configure` is `sealed override` (`AppFlowRoot.cs:144`) — good. The test path is brittle: it depends on `Configure` running on `Build()`. Document as expected, or move the field init out of `Configure` into a defensive lazy.

### 4.17 `EnumerateStackRootToParent` allocates an array each push

`AppFlowHost.cs:268-275`: `LayerEntry[] arr = stack.ToArray();` then iterates in reverse. Push frequency is low so this is not a perf issue, but it is allocating a snapshot on every push of the entire scope chain. Walk the linked underlying storage if available, or maintain a deque.

### 4.18 No timeout, no overall startup deadline

`AppFlowRoot` runs all initial layers under `destroyCancellationToken` — there is no application-level "give up after 30 seconds" path. For a real boot flow with login + remote config + Addressables download, you want timeouts per stage and a global deadline. Right now your only failure modes are exception-thrown or user-quit. This is part of the missing flow abstraction (§4.1).

---

## 5. Suggested before/after

### 5.1 Add a typed flow layer above the host

Right now you have `IScopeLayer.Install(IContainerBuilder)` and that's it. A typed flow could look like:

**Before (current):**
```csharp
public sealed class SampleAppFlowRoot : AppFlowRoot
{
    protected override IEnumerable<IScopeLayer> GetInitialLayers()
    {
        yield return new SampleAssetsLayer();
        yield return new SampleConfigsLayer();
    }

    protected override async Task OnReadyAsync(CancellationToken ct)
    {
        await Host.PushAsync(new SampleFeatureLayer(), ct);
    }
}
```

**After (proposed):**
```csharp
public sealed class StartupFlow : IAppFlow<StartupContext>
{
    public IReadOnlyList<IAppFlowStage<StartupContext>> Stages { get; } = new IAppFlowStage<StartupContext>[]
    {
        new BootStage(),       // wraps SampleAssetsLayer
        new IntroStage(),      // wraps SampleConfigsLayer
        new LoginStage(),
        new MainLoopStage(),
    };
}

public interface IAppFlowStage<TContext>
{
    StageKey Key { get; }                 // typed identity (struct key, not string)
    TimeSpan Timeout { get; }
    Task RunAsync(TContext ctx, IFlowReporter reporter, CancellationToken ct);
}
```

Each stage internally drives `AppFlowHost` if it needs scope mutation, but the public surface is typed. `IFlowReporter` becomes the only thing UI binds to (replaces `IAppFlowProgress` for app code; progress stays internal to a stage runner).

### 5.2 Inline over-decomposed orchestration

**Before** (`AppFlowHost.cs:103-141`):
```csharp
public async Task PushAsync(IScopeLayer layer, CancellationToken ct)
{
    if (layer == null) throw new ArgumentNullException(nameof(layer));
    bool adHocSession = BeginAdHocPushIfNeeded(layer);
    int layerIndex = progress != null ? progress.HostAddLayer(layer.Name) : -1;
    try { await RunPushWithSessionAsync(layer, ct, layerIndex, adHocSession); }
    catch (Exception ex) { HandlePushFailed(layerIndex, adHocSession, ex); throw; }
}

private bool BeginAdHocPushIfNeeded(IScopeLayer layer) { /* 7 lines */ }
private async Task RunPushWithSessionAsync(IScopeLayer layer, ...) { /* 5 lines */ }
private void HandlePushFailed(...) { /* 6 lines */ }
```

**After:**
```csharp
public async Task PushAsync(IScopeLayer layer, CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(layer);

    bool adHoc = sessionDepth == 0;
    if (adHoc) BeginSession($"Push:{layer.Name}", 1);
    int idx = progress.HostAddLayer(layer.Name);   // progress is non-null per fail-fast

    try
    {
        await ExecutePushCoreAsync(layer, idx, ct);
        if (adHoc) EndSession(null);
    }
    catch (Exception ex)
    {
        progress.HostSetLayerStatus(idx, LayerStatus.Failed);
        if (adHoc) EndSession(ex);
        throw;
    }
}
```

One method, all the state changes visible, no helper sprawl.

### 5.3 Honor or drop the cancellation token in `PopAsync`

**Before** (`AppFlowHost.cs:143-151`):
```csharp
public async Task PopAsync(CancellationToken ct)
{
    LayerEntry entry = RemoveTopEntry();
    bool adHocSession = sessionDepth == 0;
    int layerIndex = BeginPopSessionIfNeeded(entry, adHocSession);
    await RunPopDisposeAsync(entry, layerIndex, adHocSession);
    CompletePopProgress(layerIndex, adHocSession);
    _ = ct; // <-- swallow
}
```

**After:**
```csharp
public async Task PopAsync(CancellationToken ct)
{
    ct.ThrowIfCancellationRequested();
    LayerEntry entry = RemoveTopEntry();
    // ...
    await RunDisposeWaveAsync(entry, ct);   // pass ct downstream
    // ...
}
```

If `IAsyncDisposable.DisposeAsync()` cannot accept a token (it cannot — `ValueTask DisposeAsync()`), at least throw on entry and between awaits.

### 5.4 Use `ConditionalWeakTable` for exception dedup

**Before** (`AppFlowErrorHandler.cs:52-66`):
```csharp
if (info.Exception.Data.Contains(ReportedDataKey)) return false;
info.Exception.Data[ReportedDataKey] = true;
return true;
```

**After:**
```csharp
private static readonly ConditionalWeakTable<Exception, object> reported = new();

private bool DetermineShouldLog(AppFlowErrorInfo info)
{
    if (info.Exception == null) return true;
    return reported.TryAdd(info.Exception, null);
}
```

No mutation of caller-owned state.

### 5.5 Drop nullable error/progress and add a `SequentialScheduler`

```csharp
public AppFlowHost(LifetimeScope root,
                   IAppFlowErrorHandler errors,
                   IAppFlowProgress progress,
                   IInLayerScheduler scheduler = null)
{
    ArgumentNullException.ThrowIfNull(root);
    ArgumentNullException.ThrowIfNull(errors);
    ArgumentNullException.ThrowIfNull(progress);
    this.errors    = errors;
    this.progress  = (AppFlowProgress)progress;
    this.scheduler = scheduler ?? new ParallelScheduler();
    // ...
}
```

Then drop every `if (errorHandler == null) return;` and `progress?.HostXxx(...)` inside the host.

---

## 6. Easy wins (5–8)

1. **Delete `_ = ct;` in `PopAsync`** (`AppFlowHost.cs:150`). Either honor `ct` (throw between awaits, throw on entry) or remove the parameter from the public signature.
2. **Skip identical sub-progress updates** in `AppFlowProgress.ApplySubProgressLocked` (`AppFlowProgress.cs:158-164`) — return `false` when `Math.Abs(clamped - e.SubProgress) < epsilon`.
3. **Move all `Debug.Log*` calls behind an `IAppFlowLogger`** (or pipe non-fatal warnings through `IAppFlowErrorHandler.Report` as `Manual`). Then drop `using UnityEngine;` from `AppFlowHost`. Pure-C# host, easier to test.
4. **Inline single-call private helpers** in `AppFlowHost` (`BeginAdHocPushIfNeeded`, `RunPushWithSessionAsync`, `HandlePushFailed`, `BeginPopSessionIfNeeded`, `HandlePopDisposeFailed`, `CompletePopProgress`, `DisposePopScopeResources`, `RunPopDisposeAsync`, `FinishSuccessfulPush`, `RecordEntry`, `BindLayerProgressIfNeeded`, `UnbindLayerProgress`, `RunLayerInitAndCollectDisposablesAsync`).
5. **Remove unused enum values** (`AppFlowErrorPhase.Configure`, `AppFlowErrorPhase.Install`) or wire them. Dead enum values are landmines.
6. **Add a `SequentialScheduler`** and document choosing between them on a per-layer or per-app basis.
7. **`ConditionalWeakTable<Exception, object>` for dedup** instead of `Exception.Data` mutation (`AppFlowErrorHandler.cs:62-65`).
8. **`AppFlowProgress : IDisposable`** with `errorHandler.OnError -= OnErrorFromHandler;` so PlayMode restart doesn't accumulate handlers.

---

## 7. Bigger refactors

### 7.1 Add the typed flow layer (the real one)

This is the rubric's "abstraction at places that will keep changing". Today AppFlow has:
- A scope manager (`AppFlowHost`).
- A progress reporter (`AppFlowProgress`).
- An error handler.

It is missing:
- A typed stage abstraction with a typed context (`IAppFlowStage<TContext>`).
- A typed flow that sequences stages and exposes a state-machine-like cursor (`Boot → Intro → Login → MainLoop → Shutdown`).
- A way to express "this stage requires that stage" (predicate or DAG, not just ordering).
- Per-stage timeouts.
- A `IAppFlowDriver<TContext>` that runs the flow and emits typed events (`StageStarted<TStage>`, `StageCompleted<TStage>`).

`AppFlowHost` becomes an internal mechanism a stage uses if it needs scope mutation. The sample app then becomes:

```csharp
public sealed class StartupFlow : IAppFlow<StartupContext> { ... }
public sealed class BootStage : IAppFlowStage<StartupContext> { ... }
```

Pair with a Roslyn analyzer (you have those) that enforces stage key uniqueness at compile time.

### 7.2 Decouple `AppFlowRoot` from `LifetimeScope` inheritance

Right now `AppFlowRoot : LifetimeScope` couples your flow root to a Unity component. Test infrastructure has to spin up GameObjects and `DestroyImmediate` them between tests (`AppFlowHostTests.cs:33-41`). Composition over inheritance: a `MonoBehaviour AppFlowBootstrap` *holds* an `AppFlow` (pure C#) and forwards `Start` / `OnDestroy`. Pure host then runs in NUnit without `LogAssert.Expect("Destroy may not be called from edit mode")` workarounds.

### 7.3 Replace `IScopeLayer` string `Name` with a typed key

Strings in error info, progress entries, session names. Replace with a `LayerKey` struct (or `enum`-keyed flows). Compile-time safety on session names and analytics events.

---

## 8. Organization & docs

### Asmdef hygiene
- `Scaffold.AppFlow` is `autoReferenced=true, overrideReferences=true, references=[VContainer]`. Good. Pure-C# (modulo Unity logging issue §4.7).
- `Scaffold.AppFlow.Samples` is not auto-referenced — correct.
- `Scaffold.AppFlow.Tests` is editor-only — correct.

### Documentation
- `README.md` is clear and dense. Honest about scope. The "Cross-layer registration patterns" section is the best part.
- Missing: a one-paragraph "this is *not* a flow / state machine, it is a scope manager — see [external] for flow guidance". Right now the name implies more than it delivers.
- `Samples/README.md` is fine.
- No XML doc comments on the public contracts. Given Roslyn analyzers + source generators are in the project conventions, public APIs should ship XMLDoc so generators and consumers get IDE help.

### Tests
- Edit-mode only. The "Destroy may not be called from edit mode" `LogAssert.Expect` patterns (`AppFlowHostTests.cs:78, :97`, `AppFlowProgressTests.cs:87`) say "we know this is a Unity hack". Move host tests to pure-C# once the host is detangled from `UnityEngine.Debug`.
- No PlayMode tests. The progress event handler races (§4.9) won't surface in edit mode.
- `AppFlowProgressTests.HostSetSubProgress_ClampsToZeroOne` directly calls internal methods — fine via `InternalsVisibleTo`.

### Naming & layout
- `Internal/` correctly hides `LayerEntry`, `LayerInitRunner`, `LayerPublisher`, `LayerResolverProxy`. Good.
- `Contracts/` mixes *interfaces* with `LayerOperation` (an enum). Move `LayerOperation` to `Internal/` or `Errors/`.
- `Schedulers/` contains one type. Either commit (add `SequentialScheduler`) or fold it back.

### Sources / patterns to reference
- Cysharp/UniTask flow patterns and `UniTask.WhenAll` for zero-alloc completion.
- MAUI Shell for typed stage / route concepts (URL-style, but the typed-key idea applies).
- VContainer's own `IInitializable` / `IAsyncStartable` are conceptually adjacent to your `IAsyncInitializable`; you could either inherit or document the divergence so newcomers don't write both.
