# Audit: com.scaffold.states

Auditor: senior architect review (state pipeline focus)
Scope: 53 source files under `Assets/Packages/com.scaffold.states` (Runtime + Samples + Tests)
Date: 2026-05-02

## 1. Summary

A Redux-flavored slice/store with per-state mutators, payload routing, snapshot/restore, deferred event dispatch, and aggregate (derived) slices. The skeleton is sound and the generic `Mutator<TState, TPayload>` is the right shape, but the implementation leans on `Type` keys, `IReference` boxing, runtime casts, list/array allocations on hot paths, and reflective ceremony where source generators or stricter generics would be cleaner. The `Store` itself has accumulated buffer fields that are reused across re-entrant code paths and are not reentrancy-safe.

Verdict: **Refactor.** The model is right. Many internals need tightening before it scales to a real game loop.

## 2. Structure

```text
com.scaffold.states/
  Runtime/
    Abstractions/      IPayloadReference, IReference, IStateEventHandler, IStateScope, IStoreScope, ISubscription, IStateEventDeferralController
    Builders/
      State/           StateBuilder<TRef,TState>, GenericStateBuilder<TRef,TState>
      Store/           StoreBuilder, StoreBuilderMethods (extension)
    Events/            StateEventHandler (internal), DeferredStateEventHandler, StateEventHandlers (factory), TypedSubscription, Ledger, StateEventMergeMode
    Mutators/          Mutator<TState>, Mutator<TState, TPayload>
    Pipeline/          MutatorRegistry, MutatorRunner, RegisteredMutator<,>, IPayloadMutatorBinding, IStoreScratchpad, DuplicateMutatorRegistrationException
    State/             BaseState, State, AggregateState, BaseSlice, BaseSlice<T>, Slice, AggregateSlice, AggregateProvider<>, IAggregateProvider, IAggregateRebuild, Snapshot, StateChangeEvent
    Utility/           Reference (Null sentinel)
    Store.cs           main API
    AssemblyInfo.cs    InternalsVisibleTo Tests
    Scaffold.States.asmdef (refs Records, Maps, Pooling)
  Samples/             Counter/Notes/Totals demo with sample mutators, payloads, references, factory
  Tests/               7 Edit-mode test files (~770 LOC) covering aggregates, deferred events, batch pool poisoning, dedup, sample features
```

asmdef: `Scaffold.States` (engine references on; **uses `UnityEngine.Debug` despite no other Unity dependency** — see Issues).
DI: **No `Container/` or VContainer Installer** despite the repo convention (`AGENTS.MD`).
Docs: package-level `README.md` exists. No per-folder docs, no API table, no analyzer-targeted XML on public surface.

## 3. What's Good

- `Mutator<TState>` and `Mutator<TState, TPayload>` are the correct two-shot generic abstraction. Compile-time pairing of state and payload at registration is real.
- `Store.RegisterMutator<TState, TPayload>` enforces the typed signature; payload→mutator dispatch is keyed by `typeof(TPayload)` rather than a string.
- Snapshot/Apply/Prune semantics are clearly separated (`Store.cs:78–143`) and the rule "mutator commits don't prune; snapshot loads do" is genuinely useful.
- `MutatorRunner` is `IPoolable` and reused across `Execute*` calls (`Store.cs:35`, `Pipeline/MutatorRunner.cs`). Good intent — even if execution isn't fully alloc-free yet.
- `DeferredStateEventHandler` uses the decorator pattern and exposes its control surface via `IStateEventDeferralController` — clean composition, no inheritance ladder.
- `AggregateSlice` keeps "aggregate is read-only, derived" enforced at the type level (`AggregateSlice.Set` throws — `State/AggregateSlice.cs:32`).
- `RegisteredMutator<,>.FromPayload` keeps payload reference resolution local: `executeReference` overrides, then `IPayloadReference`, then `Reference.Null` (`Pipeline/RegisteredMutator.cs:27`).
- `MutatorRegistry` enforces "one concrete mutator type per (state,payload)" — protects against accidental double-registration without forbidding the legitimate "many mutators per payload" case (`Pipeline/MutatorRegistry.cs:25, 41`).
- Tests cover the harder cases: pool poisoning on a throwing mutator (`Tests/ExecuteBatchPoolPoisonTests.cs`), keyed canonical aggregate, deferred merge modes, dedup on registry.

## 4. Issues / Smells

### 4.1 `UnityEngine` leaking into a "pure C#" pipeline

`Runtime/Store.cs:264`:
```csharp
UnityEngine.Debug.LogWarning($"[Store] No mutators registered for payload type {payloadType.FullName}.");
```

This is the only Unity dependency in the whole runtime. It violates the Unity/pure-C# boundary preference, prevents flipping `noEngineReferences: true`, and is also a **silent default that masks errors** — a payload with no registered mutator should fail-fast (or surface through an injected diagnostics sink), not log a warning. Two strikes against architect rules in a single line.

### 4.2 Default-masking for missing payload bindings

Same line as 4.1. `Execute<TPayload>` with no registered mutator currently:
1. Logs a warning.
2. Returns silently.
3. Commits an empty overlay (no-op).

This is exactly the "default value that hides errors" pattern the architect dislikes. There is no way for a caller to detect "I forgot to register a mutator" except by reading the console.

### 4.3 Redundant guard clauses (esp. on readonly/typed fields)

The codebase peppers `if (x is null) throw new ArgumentNullException(nameof(x))` at every public surface. Some are at proper entry points (acceptable); many are on parameters whose type already forbids `null` (records, generics with `class` constraints) or are duplicated layer-after-layer. Examples:

- `Store.cs:194` — `Execute<TPayload>` null-checks `payload`. Reasonable as entry point.
- `Store.cs:213–222` — `ExecuteBatch` null-checks `payloads` AND inside the loop `ApplyOnePayloadToOverlay` null-checks each payload again (`Store.cs:251`). One check on entry suffices; per-element ought to be `Debug.Assert` or removed.
- `Store.cs:286` — `RegisterSlice` null-checks `state` while `state.GetType()` is also called in `Slice.Create`; the `NullReferenceException` would be perfectly informative there. The reference parameter is already `IReference?` — so the explicit `?? Reference.Null` does the work; the extra null-check on `state` is fine but the **mirror check pattern** appears in `RegisterAggregate`, `UnregisterSlice`, `LoadSnapshot`, `RegisterMutator`, `DeferredStateEventHandler.Notify` (line 29), etc.
- `EventHandler.Subscribe/Unsubscribe/SubscribeAny` (`Events/StateEventHandler.cs:65, 75, 85, 113`) all null-check `action`. The `Store` already exposes these — pushing the check up to `Store.Subscribe` (the entry point) covers it once.
- `Pipeline/MutatorRegistry.cs:13` — null-check on `mutator`, but `Store.RegisterMutator` (`Store.cs:273`) already null-checks before delegating. Duplicate.

### 4.4 Stringly-typed-equivalent code: `Type` keys + runtime casts

Although there are no string keys, the design routes through `System.Type` everywhere (`Map<IReference, Type, Slice>`, `Dictionary<Type, List<IPayloadMutatorBinding>>`, `Snapshot : Map<IReference, Type, State>`) and then casts:

- `Store.cs:261` — `Type payloadType = payload.GetType();` followed by registry lookup, then in `RegisteredMutator.Apply` (`Pipeline/RegisteredMutator.cs:20`) a `(TPayload)payload` cast that's only safe because the lookup key matched.
- `Store.cs:396` — `return (TState)slice.State;` — type assertion that is only correct because `typeof(TState)` was used as the dictionary key.
- `Snapshot.cs:24` — `return (TState)s;` — same.
- `TypedSubscription.Notify` (`Events/TypedSubscription.cs:28`) — runtime `is not TState` guard which "should be unreachable."

This is fine for a v1 but it is the spot where a **source generator** would shine: emit a compile-time `PayloadDispatcher` whose `Dispatch(TPayload p)` directly invokes the registered mutator without `object` boxing, dictionary lookup, or `GetType()`. See "Bigger refactors."

Boxing surface: every `Execute<TPayload>(payload)` call where `TPayload` is a value type today **boxes** the payload (it is stored as `object` inside `RunRegisteredMutatorsWithoutCommit` and unboxed in `RegisteredMutator.Apply` line 20). Records are reference types, so this isn't biting hard yet — but `ExecuteBatch(IReadOnlyList<object>)` (`Store.cs:212`) hard-codes the boxed contract.

### 4.5 `Store` has shared mutable buffers — not reentrancy-safe

`Store.cs:45–48`:
```csharp
private readonly List<Slice> mapSliceBuffer = new List<Slice>();
private readonly List<AggregateSlice> aggregateSliceBuffer = new List<AggregateSlice>();
private readonly List<BaseSlice> sliceBuffer = new List<BaseSlice>();
private readonly List<(IReference Reference, Type StateType)> pruneBuffer = new List<(IReference, Type)>();
```

These instance fields are mutated by `FillSlices`, `EnumerateAll<TState>`, `GetAll<TState>`, and `PruneCanonicalSlicesNotInSnapshot`. If a subscriber to a snapshot-load `Notify` calls back into `store.GetAll<...>` (entirely plausible in MVVM rebinds), it will trash the buffer mid-iteration. The `EnumerateAll` iterator (`Store.cs:406`) is particularly bad: it `yield return`s while holding indices into `sliceBuffer`, which the subscriber can mutate.

This is a real concurrency-shaped bug even on a single thread.

### 4.6 `EnumerateAll<TState>` allocates an iterator + boxes the tuple every call

`Store.cs:406–416`. The boxed `IEnumerable<(IReference, TState)>` can't be avoided fully, but the implementation also fills a shared list, then yields — so **two allocations per call** plus reentrancy hazard. A `struct` enumerator over a per-state-type bucket (a `Dictionary<Type, List<Slice>>`) would be alloc-free.

### 4.7 `FillSlices` re-scans the entire map, every call

`Store.cs:429–445`. `Map<IReference, Type, Slice>.GetAll(stateType, …)` (an external generic Map) is presumably `O(N)` over all slices. Hot enumeration paths (subscribers, `IStoreScratchpad.GetAll<TState>`) will pay this each time. A `Dictionary<Type, List<Slice>>` secondary index, maintained on register/unregister, gets you `O(k)` where k is the count of that type.

### 4.8 `Snapshot.Get<TState>` returns null on miss — masking errors

`State/Snapshot.cs:20–27`:
```csharp
public TState Get<TState>(IReference reference) where TState : State
{
    if(TryGetValue(reference, typeof(TState), out var s))
    {
        return (TState)s;
    }
    return null;
}
```

`State` is reference type so this compiles, but: returning `null` for a missing key contradicts the architect's fail-fast rule and contradicts `Store.Get<TState>` (`Store.cs:394`) which throws `KeyNotFoundException`. Inconsistent and dangerous.

### 4.9 `DeferredStateEventHandler.Flush*` allocates an array per flush cycle

`Events/DeferredStateEventHandler.cs:99–127`:
```csharp
var snapshot = preserveAll.ToArray();
preserveAll.Clear();
```

Inside a `while` loop. If a flushed handler enqueues more events (legit re-entrancy), each pass allocates a fresh array. Use a swap-buffer (two `List`s, ping-pong) to keep this allocation-free.

Same pattern in `FlushLatestPerKey` (`:114`) and in `StoreVariableStorage.NotifyVariableSubscribers` / `NotifyStructuralRemovedAll` (entities.states package, see that audit).

### 4.10 `Ledger` uses `ContainsKey` + indexer pattern (double lookup) and `Enumerable.Empty<>`

`Events/Ledger.cs:11–18`:
```csharp
public IEnumerable<ISubscription> Get(Type stateType)
{
    if (Lookup.ContainsKey(stateType))
    {
        return Lookup[stateType];
    }
    return Enumerable.Empty<ISubscription>();
}
```

Double dictionary access. `TryGetValue` is the idiomatic single-lookup. Same pattern in `Add` (`:22`).

### 4.11 `StateEventHandler.NotifyReferenceSubscriptions` allocates an enumerator per notify

`Events/StateEventHandler.cs:33–38`. `Ledger.Get` returns the list directly when the key exists, but boxes it as `IEnumerable<ISubscription>`. The `foreach` then allocates a `List<T>.Enumerator` boxed via `IEnumerator<>`. On a hot mutator path with many subscriptions per slice, this is real garbage. Returning `List<ISubscription>` (or a `Span` over a backing array) avoids it.

Also: this list is iterated **while subscribers can call `Subscribe`/`Unsubscribe`**, which will re-enter `Ledger.Add` / `Ledger.RemoveSubscription` on the same list — `InvalidOperationException: Collection was modified`. A snapshot-then-iterate or generation counter is required.

### 4.12 `MutatorRunner.RunMutatorBindingsWithoutCommit` foreach over `IReadOnlyList` allocates

`Pipeline/MutatorRunner.cs:34`. `foreach` on `IReadOnlyList<IPayloadMutatorBinding>` boxes the enumerator. Hot path. Use `for (int i = 0; i < mutators.Count; i++)`.

### 4.13 `RegisteredMutator.Apply`: `executeReference.Equals(Reference.Null)` is brittle

`Pipeline/RegisteredMutator.cs:29`:
```csharp
if (!executeReference.Equals(Reference.Null))
```

This compares against the singleton via `Equals`. If a custom `IReference` overrides `Equals` poorly (and there's no analyzer enforcing equality contract for `IReference`), the routing breaks silently. `ReferenceEquals(executeReference, Reference.Null)` would be both faster and stricter — `Reference.Null` is a singleton, so identity is the right check.

### 4.14 `IStateEventHandler` is a "fat" interface with default interface methods

`Abstractions/IStateEventHandler.cs:10–13` uses C# 8 default-interface-method to inject the two-arg `Notify`. That's fine in isolation but it requires every alternative implementation to opt out of it explicitly if they want different behavior — and it creates an asymmetry where `DeferredStateEventHandler` re-implements both overloads while `StateEventHandler` only implements the three-arg one. Mixing DIM with `internal sealed` test-targeted handlers makes mocking awkward.

### 4.15 `IReference` is an empty marker interface

`Abstractions/IReference.cs:1–8`. No contract for `Equals`/`GetHashCode`/identity beyond what `object` defines. The whole indexing scheme depends on `IReference` having sensible value equality. `Reference.Null` is reference-identity; `EntityStateReference` is a record (value-equality). Mixing them in the same `Map` will work today only because the keys for distinct slice classes never collide. Add an analyzer or constrain to a sealed `abstract record Reference` so equality is mandatory.

The `Store.Scratchpad.ReferenceByValueEqualityComparer` (`Store.cs:554`) implies someone has already noticed the ambiguity and worked around it locally instead of fixing it at the abstraction.

### 4.16 `StateBuilder<TRef, TState>` doesn't constrain `TRef`

`Builders/State/StateBuilder.cs:3`:
```csharp
public abstract class StateBuilder<TRef, TState> where TState: State
```

`TRef` is unconstrained — accidentally passing a `string` compiles. The `WithBuilder` extension does add `where TRef : IReference` (`StoreBuilderMethods.cs:19`), but the builder itself does not.

### 4.17 `StoreBuilder` does not detect duplicate canonical slice (state, reference) pairs

`Builders/Store/StoreBuilder.cs:74–76`. `AddState` simply appends. The `Store` constructor then throws much later when `map.Add` collides. Duplicate aggregates **are** checked (`:58`), but canonical slices are not — asymmetry.

### 4.18 Aggregate rebuild reuses the same `RebuildCallback` instance per slice but never disposes wired event handlers

`State/AggregateSlice.cs:24–30`. `provider.Wire(store, callback)` calls `Subscribe` on the store, but the wiring lifetime is never tracked. If the aggregate is later replaced (no API for that today, but planned by the README's discussion), the subscribers leak.

### 4.19 `MutatorRunner` is `IPoolable` but `OnReturnedToPool` is `scratchpad.Reset()` only

`Pipeline/MutatorRunner.cs:27–30`. The runner has no internal mutable state to reset (good), but the scratchpad's `Reset()` only clears the overlay; the `refSet` and `sliceBuffer` inside the scratchpad (`Store.cs:481–483`) are not cleared and retain references across rentals. They're cleared lazily inside `GetAll<TState>` (`:503`) — so any scratchpad consumer that reads via `Get<TState>` then keeps a snapshot of the scratchpad's working set across rental boundaries can see stale data. Defensive `Reset` should `Clear()` everything.

Also: the `mutatorRunnerPool` initialSize is 2 (`Store.cs:35`). Re-entrant `Execute` (subscriber-triggered) will exhaust the pool and force allocations. A minor smell, but worth a comment.

### 4.20 `MutatorRegistry.TryGet` returns nullable list inside `out`

`Pipeline/MutatorRegistry.cs:29–39`. The signature is `bool TryGet(Type, out IReadOnlyList<IPayloadMutatorBinding>? bindings)`. The contract should be: "true ⇒ non-null." Annotation-wise this needs `[NotNullWhen(true)]` on the out param. The caller in `Store.cs:262` then does `bindings == null || bindings.Count == 0` — defensive null-check that compiler-flow analysis would otherwise eliminate.

### 4.21 No DI Installer (`Container/` folder)

Repo convention from `AGENTS.MD` is "DI is VContainer (each package has a `Container/` folder with Installer)." This package does not register anything in a VContainer scope. Consumers must wire `StoreBuilder` by hand. For a package this central, ship a `StatesInstaller` that registers `Store` (or a builder factory) as `Lifetime.Scoped`.

## 5. Suggested Before/After Snippets

### 5.1 Fail-fast on missing payload mutator

Before — `Store.cs:262–266`:
```csharp
if (!mutatorRegistry.TryGet(payloadType, out IReadOnlyList<IPayloadMutatorBinding>? bindings) || bindings == null || bindings.Count == 0)
{
    UnityEngine.Debug.LogWarning($"[Store] No mutators registered for payload type {payloadType.FullName}.");
    return;
}
```

After:
```csharp
if (!mutatorRegistry.TryGet(payloadType, out var bindings))
{
    throw new MutatorNotRegisteredException(payloadType);
}
```

Drops Unity dep, fails fast, gives caller a typed exception. If logging is genuinely wanted, inject an `IStateDiagnostics` sink at the boundary.

### 5.2 Replace `Type` dictionaries with a typed dispatcher (longer-term)

Before — runtime dispatch (`Store.cs:259`):
```csharp
private void RunRegisteredMutatorsWithoutCommit(MutatorRunner runner, object payload, IReference executeReference)
{
    Type payloadType = payload.GetType();
    if (!mutatorRegistry.TryGet(payloadType, out var bindings)) { ... }
    runner.RunMutatorBindingsWithoutCommit(payload, bindings, executeReference);
}
```

After — generic entry resolves at call-site:
```csharp
public void Execute<TPayload>(IReference? reference, TPayload payload) where TPayload : IPayload
{
    var bindings = mutatorRegistry.GetTyped<TPayload>(); // typed list, no GetType()
    var runner = mutatorRunnerPool.Take();
    try { runner.Run<TPayload>(reference ?? Reference.Null, payload, bindings); runner.CommitOverlay(); }
    finally { mutatorRunnerPool.Return(runner); }
}
```

Requires `IPayload` marker (or a source-generator-emitted `PayloadDispatcher<TPayload>`). The `ExecuteBatch(IReadOnlyList<object>)` path stays as-is for heterogenous batches but now goes through a slow-path that's clearly labeled.

### 5.3 Snapshot.Get fail-fast

Before — `Snapshot.cs:20–27`:
```csharp
public TState Get<TState>(IReference reference) where TState : State
{
    if(TryGetValue(reference, typeof(TState), out var s))
        return (TState)s;
    return null;
}
```

After:
```csharp
public TState Get<TState>(IReference reference) where TState : State
    => TryGetValue(reference, typeof(TState), out var s)
        ? (TState)s
        : throw new KeyNotFoundException($"No {typeof(TState).Name} in snapshot at {reference}.");

public bool TryGet<TState>(IReference reference, out TState state) where TState : State { ... }
```

Mirrors `Store.Get<TState>` and `Store.TryGetSlice`.

### 5.4 Reentrancy-safe enumerate

Before — `Store.cs:406–416`:
```csharp
public IEnumerable<(IReference, TState)> EnumerateAll<TState>() where TState : BaseState
{
    FillSlices(typeof(TState), sliceBuffer);
    for (int i = 0; i < sliceBuffer.Count; i++) { ... yield return ...; }
}
```

After — local list:
```csharp
public IReadOnlyList<(IReference, TState)> EnumerateAll<TState>() where TState : BaseState
{
    var buffer = new List<(IReference, TState)>(); // or rent from pool
    foreach (var slice in indexByType[typeof(TState)])
        if (slice.State is TState ts) buffer.Add((slice.Reference, ts));
    return buffer;
}
```

(`indexByType` is the recommended secondary index from 4.7.)

### 5.5 Push `?? Reference.Null` to a single helper

Before — repeated 8+ times:
```csharp
var r = reference ?? Reference.Null;
```

After:
```csharp
private static IReference Resolve(IReference? r) => r ?? Reference.Null;
```

…and inline in callers. Cosmetic, but it makes the rule visible.

## 6. Easy Wins (each <30 min)

1. **Drop `UnityEngine.Debug.LogWarning` in `Store.cs:264`**; throw `MutatorNotRegisteredException`. Flip `noEngineReferences: true` in `Scaffold.States.asmdef`.
2. **`Snapshot.Get<TState>` should throw on miss** (`Snapshot.cs:26`) and add `TryGet<TState>` for the optional path.
3. **De-duplicate null-checks** along the call chain: keep them in `Store.*` public entry points, remove from `MutatorRegistry.Register`, `StateEventHandler.Subscribe/Unsubscribe`, internal handler internals.
4. **Replace `ContainsKey + indexer` with `TryGetValue`** in `Ledger.cs:11, 22` and `StateEventHandler.cs:28`.
5. **Use `for`-loop indexer in `MutatorRunner.RunMutatorBindingsWithoutCommit`** (`MutatorRunner.cs:34`) to avoid the `IEnumerator<>` boxing.
6. **`StoreBuilder.AddState` should detect duplicate `(reference, state-type)`** (`StoreBuilder.cs:74`), matching the aggregate path at `:58`.
7. **`StateBuilder<TRef, TState>` add `where TRef : IReference`** (`StateBuilder.cs:3`).
8. **`RegisteredMutator.Apply` use `ReferenceEquals(..., Reference.Null)`** (`RegisteredMutator.cs:29`).

## 7. Bigger Refactors

### 7.1 Source-generated payload→mutator dispatcher (1–2 days)

Eliminate `Dictionary<Type, List<IPayloadMutatorBinding>>` and `RegisteredMutator<,>` runtime ceremony. Introduce a partial class `MutatorDispatcher` whose generated code looks like:

```csharp
partial class MutatorDispatcher
{
    public void Dispatch<TPayload>(Store s, IReference r, TPayload p)
    {
        if (typeof(TPayload) == typeof(AddModifierPayload)) { _addModifier.Apply(s, r, (AddModifierPayload)(object)p); return; }
        ...
    }
}
```

JIT specialization eliminates the cast at runtime for value-type payloads. Pair with a `[PayloadOf(typeof(TState))]` source attribute; the generator collects all `Mutator<TState, TPayload>` and emits the table. Pure compile-time pairing, zero `Type` keys, no boxing for value-type payloads. Aligns with the Generators/ folder convention from `AGENTS.MD`.

### 7.2 Replace shared instance buffers with per-call rented buffers (half day)

`Store.mapSliceBuffer`, `aggregateSliceBuffer`, `sliceBuffer`, `pruneBuffer` (lines 45–48) are all broken under reentrancy (4.5). Either:
- Pull from a `Pool<List<T>>` per call and return on completion, or
- Use `ArrayPool<T>.Shared` rentals with a `Span<T>` view.

Same applies to `StateEventHandler.NotifyReferenceSubscriptions` (4.11).

### 7.3 Indexed slice store (1 day)

Replace `Map<IReference, Type, Slice>` linear scans (`Store.FillSlices`) with a primary `Dictionary<(IReference, Type), Slice>` and a secondary `Dictionary<Type, List<Slice>>`, kept in sync on `RegisterSlice` / `UnregisterSlice`. `EnumerateAll<TState>` then becomes a struct enumerator over the per-type list. Knock-on win for aggregates, since they currently re-walk the whole map at every rebuild.

### 7.4 Tighten `IReference` contract (half day)

Make `IReference` an `abstract record Reference` (or interface + analyzer that requires implementers to be records or override `Equals`). Compare for value equality everywhere. Drop the `Scratchpad.ReferenceByValueEqualityComparer` workaround (`Store.cs:554–581`).

### 7.5 Aggregate lifetime API (half day)

Expose `UnregisterAggregate(IReference, Type)` that unwires the provider's subscriptions. Today aggregates are register-once. Pair with a `IDisposable` returned by `IAggregateProvider.Wire(...)` (or change the contract to take a `CompositeSubscription` and append). This addresses 4.18.

### 7.6 Provide a VContainer Installer (half day)

Add `Container/StatesInstaller.cs` with:
```csharp
public sealed class StatesInstaller : IInstaller
{
    public void Install(IContainerBuilder builder)
    {
        builder.Register<StoreBuilder>(Lifetime.Transient);
        builder.RegisterFactory<Store>(c => c.Resolve<StoreBuilder>().Build(), Lifetime.Singleton);
        builder.Register<IStateEventHandler>(_ => StateEventHandlers.CreateDefault(), Lifetime.Singleton);
    }
}
```
Mirrors repo convention.

## 8. Organization & Docs

- `Runtime/Utility/Reference.cs` is the singleton sentinel; arguably it belongs in `Abstractions/` next to `IReference`. Today they're split.
- `Runtime/Mutators/` holds `Mutator.cs` only; `RegisteredMutator.cs` and `MutatorRegistry.cs` live in `Pipeline/`. The split is right (public abstraction vs. internal pipeline) but undocumented.
- `Builders/State/` holds the `StateBuilder` family; `Builders/Store/` holds `StoreBuilder`. The naming `WithBuilder` (`StoreBuilderMethods.cs:19`) is uninformative — `WithBuiltSlices` or `WithStateForEach` would be better.
- `StateEventHandlers` (plural, factory) vs `StateEventHandler` (singular, internal) — confusing pair. Rename factory to `StateEventHandlerFactory`.
- `IStateEventDeferralController` is exposed only by casting (`README.md` line 46). A `Store.Deferral` property (returning the controller, or null when no decorator is wrapped) would make the affordance discoverable.
- `Snapshot` extends `Map<IReference, Type, State>` (`Snapshot.cs:6`). Public-inheritance of a third-party generic exposes the entire `Map` API on `Snapshot`. Should compose, not inherit.
- README is decent for a recovery-from-history package, but no `## Public API` section, no example of the mutator pipeline (just snapshots and deferred events). Add an end-to-end "Counter store" example wiring `StoreBuilder.RegisterMutator → store.Execute(payload) → store.Subscribe`.
- No XML doc comments on the public surface — `Store`, `Mutator<,>`, `IPayloadReference`, `IStateEventHandler` are all undocumented at the API level. With C# nullable enabled this is a missed opportunity for IDE tooltips.

### Comparison points

- **Redux / Fluxor** (https://fluxor.mrpmorris.com/): action-class-as-payload + `ReducerMethod<TState, TAction>` attribute model. Source-gen-friendly. Scaffold's `Mutator<TState, TPayload>` is comparable but lacks the attribute-driven discovery — registration is hand-rolled. (See 7.1.)
- **MessagePipe** (https://github.com/Cysharp/MessagePipe): typed pub/sub with zero-alloc generic dispatch and DI integration; demonstrates how far you can push compile-time `IPublisher<T>/ISubscriber<T>` pairs. Scaffold's event handler is closer to a hand-rolled Observable; the typed bus pattern would replace `IStateEventHandler.Subscribe<TState>(...)` cleanly.
- **Unity ECS / Burst**: state-pipeline analogue — system updates run on chunks with zero allocation. Scaffold isn't trying to be ECS, but the "no allocation per Execute" bar is reasonable to set.

## Consumers

`Scaffold.States` is consumed in exactly **one** place outside its own package: `com.scaffold.entities.states`. There are zero references in `Assets/Packages/com.scaffold.view`, `com.scaffold.viewmodel`, `com.scaffold.liveops`, `GameModule/`, or `LiveOps/` (verified via `grep -rn "Scaffold\.States" Assets/ GameModule/ LiveOps/ --include="*.cs"`). The view/viewmodel binding story and the liveops server-state story are **not yet wired through this package** — that's a finding in itself: the audit's recommendations should not be over-fitted to a single consumer's pain, but the single consumer's pain is currently the only ground truth.

- `Assets/Packages/com.scaffold.entities.states/Runtime/StoreInstanceIdExtensions.cs:10–53` — eight extension methods that exist solely to wrap `Store.X(reference, …)` as `Store.X(InstanceId, …)`. Every public `Store` method gets its own one-liner forwarding through `EntityStateReference.From(instanceId)`. **Smell: the `IReference` parameter on `Store` is awkward enough that the only consumer rebuilds the entire surface in their own helper.** This is the strongest "API is wrong" signal in the audit pair. Recommendation: either let `Store` accept any `IReference`-convertible (a `Ref<T>` factory) or generate consumer-side extensions from the public `Store` API.
- `Assets/Packages/com.scaffold.entities.states/Runtime/StateEntityOps.cs:29` — `foreach ((IReference reference, EntityVariableState state) in store.EnumerateAll<EntityVariableState>())` — consumer iterates the boxed tuple enumerator (states audit §4.6) and immediately allocates a `List<object>` for `ExecuteBatch`. Two visible costs: the boxed enumerator and the `List<object>` (line 28).
- `Assets/Packages/com.scaffold.entities.states/Runtime/StateEntityOps.cs:39` — `if (reference is not EntityStateReference entityRef) { return; }` — manual cast at the consumer side because `Store` returns `IReference`. The consumer rediscovers a runtime invariant ("this slice key is always an `EntityStateReference`") that the type system could have enforced (states audit §4.15, entities.states audit §4.16). Typed slice keys (states §7.4 / entities.states §7.2) eliminate this branch.
- `Assets/Packages/com.scaffold.entities.states/Runtime/StoreVariableStorage.cs:21` — `EntityVariableState initial = store.Get<EntityVariableState>(entityId);` — fail-fast `Get<TState>` is fine here, but the consumer also calls `store.Subscribe<EntityVariableState>(entityId, onStateChangedHandler)` (line 24) where the action signature is `Action<IReference, EntityVariableState, StateChangeEvent>` — three parameters where the consumer only uses the third. The 1st and 2nd are redundant for an entity-keyed subscription (the consumer already knows its own `entityId` and can read the state from `store.Get`). **Smell: `Subscribe`'s callback shape is over-broad for the most common use case.** A second overload `Subscribe<TState>(IReference, Action<TState, StateChangeEvent>)` would let consumers drop the `_, _, ev =>` lambda boilerplate seen in tests.
- `Assets/Packages/com.scaffold.entities.states/Tests/StateEntityIntegrationTests.cs:506–508` — every consumer subscription discards two of the three callback args: `store.Subscribe<EntityVariableState>(heroId, (_, _, ev) => { if (ev == StateChangeEvent.Updated) { heroUpdates++; } });` — repeated three times. Concrete evidence of the over-broad callback above.
- `Assets/Packages/com.scaffold.entities.states/Runtime/EntityBridgeContext.cs:14–19` — `store.RegisterMutator(new AddModifierMutator()); … store.RegisterMutator(new RemoveModifiersBySourceMutator());` — six hand-rolled `RegisterMutator` calls. **Smell: registration is manual, not generated.** Every new payload+mutator pair requires the consumer to remember to add a line here; nothing fails at compile time if you forget. Source-generated discovery (states §7.1, entities.states §7.4) closes the loop.
- `Assets/Packages/com.scaffold.entities.states/Runtime/EntityStateReference.cs:14–19` — consumer null-checks `entityId` *before* delegating to `new EntityStateReference(...)`. The only reason this helper exists at all is to gate construction; consumers downstream still null-check at every layer (entities.states audit §4.1). The `From` factory is itself a workaround for the lack of a primary-constructor null-attribute on the record positional parameter.
- `Assets/Packages/com.scaffold.entities.states/Runtime/StateEntity.cs:30–34` (Dispose) — consumer manually orchestrates `store.UnregisterSlice<EntityVariableState>(Id)` and `storage.Dispose()`. There is no `IDisposable` returned by `RegisterSlice`/`Subscribe` to bundle this. Consumers re-implement teardown ordering each time. (See entities.states §7.5 — but the root cause is the `Store` API not returning subscription handles.)

Cross-cutting consumer findings:

- The `IReference` indirection forces every consumer to build their own `XxxStateReference : IReference` wrapper plus a parallel set of `Store` extensions. If `Store` accepted a generic `Ref<TKey>` (where `TKey` carries the consumer's identity type), most of `StoreInstanceIdExtensions` evaporates.
- `Subscribe(reference, Action<IReference, TState, StateChangeEvent>)` is over-specified for the keyed case. Add `Subscribe(reference, Action<TState, StateChangeEvent>)` and `Subscribe(reference, Action<TState>)` overloads — every observed consumer call would shrink.
- Registration via `EntityBridgeContext.RegisterMutators(store)` is essentially a manual installer. The states package owes consumers a discovery mechanism (source-gen attribute or assembly-scan installer) so consumers never have to maintain the list.

## Alternatives & prior art

- **Fluxor (.NET) — `[ReducerMethod]` source-gen registration. Verdict: Steal pattern.** Fluxor's reducer model is structurally identical to Scaffold's `Mutator<TState, TPayload>` pair, but Fluxor uses a Roslyn source generator to discover `[ReducerMethod]`-decorated methods at build time and emit registration. Direct fit for `EntityBridgeContext.RegisterMutators` (which today is six hand-edited lines). See states §7.1 / entities.states §7.4. (https://github.com/mrpmorris/Fluxor)
- **MessagePipe (Cysharp) — `IPublisher<T>`/`ISubscriber<T>` with VContainer integration. Verdict: Wrap.** Already considered project-wide. The typed pub/sub generic dispatch is exactly what `IStateEventHandler` should look like internally; a `MessagePipe`-backed `IStateEventHandler` would replace the `Ledger` dictionary and notify mechanism wholesale (states §4.10, §4.11). Don't replace the `Store` mutator pipeline with it — payloads aren't broadcast events — but the *event* surface is a clean swap. (https://github.com/Cysharp/MessagePipe)
- **R3 (Cysharp) — `ReactiveProperty<T>` / `Observable<T>`. Verdict: Wrap (consumer-facing).** R3's `ReactiveProperty<T>` already does value deduplication via `IEqualityComparer<T>` (eliminates `StoreVariableStorage.SameTypedPayloadValues` entirely) and produces `IDisposable` subscriptions (eliminates §4.18 leak risk). For the view/viewmodel consumer that doesn't yet exist, exposing `Store.Observe<TState>(reference) : Observable<TState>` would be a more idiomatic Unity binding surface than the current 3-arg `Action`. Do not replace internal storage with R3 — keep mutator pipeline pure C#. (https://github.com/Cysharp/R3)
- **Reflex (Unity DI). Verdict: Skip.** Reflex is a DI alternative with a Roslyn source-gen registration story but **no built-in reactive state container**. It would replace VContainer, not the `Store`. Out of scope for this package; mentioned only to dismiss it as a candidate. (https://github.com/gustavopsantos/Reflex)
- **Unity DOTS / ECS — `SystemState`, change filtering, unmanaged components. Verdict: Steal pattern.** ECS pays zero cost for `Execute<TPayload>` because chunks are typed and burst-compiled. Scaffold can't match that without becoming ECS, but two patterns transfer cleanly: (1) per-type indexed buckets (`Dictionary<Type, List<Slice>>`) for `EnumerateAll` (states §4.7 / §7.3); (2) change-filtering — record a `version` per slice and let subscribers ask "did this change since version N?" instead of holding callbacks. The version pattern is a better fit for liveops/server-driven state than the current event firehose.
- **Source-generated dispatchers (Spectre.Console.Cli command resolution, MagicOnion service registry). Verdict: Steal pattern.** Both libraries solve the same shape — "given a typed message, dispatch to a registered handler with no runtime `Type` lookup." The Roslyn pattern is well-trodden: scan assemblies for attribute-decorated types, emit a `partial class Dispatcher { public void Dispatch<T>(T t) { if (typeof(T) == typeof(...)) ... } }`. Direct application: states §7.1's `MutatorDispatcher`. (https://github.com/Cysharp/MagicOnion)
- **AutoMapper — Skip.** Not relevant: state projection mapping in Scaffold is small (`EntityVariableState.ResolveEffectiveValues`) and AutoMapper's reflection-driven model is the wrong direction (states §4.4). If projection mapping grows, prefer a hand-rolled or source-gen mapper.

## Benchmark plan

All scenarios use **Unity.PerformanceTesting** (the package convention; `[Performance]` + `Measure.Method` / `Measure.Frames`). Tests live under `Assets/Packages/com.scaffold.states/Tests/Performance/` (new folder) so they can be excluded from the unit-test run by asmdef define constraint `UNITY_INCLUDE_PERFORMANCE_TESTS`.

- **Typed dispatch path: `Store.Execute<TPayload>(reference, payload)`** — *What:* allocations + ns/op for one `Execute` call against a registered `Mutator<TState, TPayload>` with one slice. *Tool:* Unity.PerformanceTesting + `GC.GetAllocatedBytesForCurrentThread()` delta. *Test:* `Tests/Performance/StoreExecuteBenchmarks.cs::Execute_SingleMutator_OneSlice`. *Scenario:* 10⁴ iterations, payload is `record class` (current shape) and a `readonly record struct` (post-§4.4 fix). *Baseline:* current path allocates 1× boxed payload (struct case) + 1× `List<>` enumerator boxing in `MutatorRunner.RunMutatorBindingsWithoutCommit` (§4.12). *Success:* zero alloc per call after §4.12 + §7.1 fixes; ≥3× ns/op improvement on struct payload path.
- **`Store.EnumerateAll<TState>` reentrancy + alloc** — *What:* per-call alloc and reentrancy correctness. *Tool:* Unity.PerformanceTesting + a hand-rolled reentrancy regression test (subscriber calls `EnumerateAll` from inside notify). *Test:* `Tests/Performance/StoreEnumerateAllBenchmarks.cs` and `Tests/Reentrancy/EnumerateAllUnderNotifyTests.cs`. *Scenario:* 1k slices of one `TState`, 100 enumerations per frame, then nested enumeration from a subscriber. *Baseline:* `sliceBuffer` reuse currently produces ~0 alloc on linear path but corrupts under reentry (§4.5). *Success:* identical alloc count after §7.3 (per-type indexed bucket) and zero corruption (assertions pass) under nested call.
- **`StateEventHandler.NotifyReferenceSubscriptions` snapshot + alloc** — *What:* allocations per notify and `InvalidOperationException` resistance when a subscriber calls `Subscribe`/`Unsubscribe`. *Tool:* Unity.PerformanceTesting + a regression test enrolling 50 subscribers, half of which `Unsubscribe` themselves on first notify. *Test:* `Tests/Performance/NotifyBenchmarks.cs::Notify_50Subs_HalfUnsubscribeInline`. *Scenario:* 10⁵ notifies. *Baseline:* current code allocates `List<T>.Enumerator` boxed via `IEnumerator<>` per notify (§4.11) and **throws** under inline-unsubscribe. *Success:* zero alloc per notify (struct enumerator over snapshot list), zero exceptions, ns/op within 10% of baseline non-mutating case.
- **`StoreVariableStorage.SameTypedPayloadValues` typed equality switch (entities.states bridge — measured here because the perf cost is amortized into states' notify path)** — *What:* time per `ApplyCanonicalUpdate` + alloc for `EqualityComparer<VariableValue?>.Default.Equals` vs the six-method match chain. *Tool:* Unity.PerformanceTesting. *Test:* `Tests/Performance/VariableEqualityBenchmarks.cs` in the entities.states package. *Scenario:* 50 variables, 10⁴ updates, each touching one variable. *Baseline:* six virtual calls + boxing for value-typed `VariableValue` payloads. *Success:* single virtual call, ≥4× speedup, zero alloc once `VariableValue` is a `record` or `IEquatable<>`.
- **`Variables` `OrderBy` per read (entities.states bridge §4.20, but the symptom appears as states-API friction)** — *What:* alloc per `Variables` getter call. *Tool:* Unity.PerformanceTesting + `GC.GetAllocatedBytesForCurrentThread()`. *Test:* `Tests/Performance/VariablesEnumerationBenchmarks.cs`. *Scenario:* 10⁴ getter reads with no underlying state change. *Baseline:* `OrderBy` LINQ enumerator allocation per call. *Success:* zero alloc per read after §7.3 (cached sorted list), invalidated only on `CopyMapsIntoCaches`.
- **Hand-rolled type switch vs `EqualityComparer<T>.Default`** — covered by the variable-equality benchmark above; the same harness compares `RegisteredMutator.Apply`'s `executeReference.Equals(Reference.Null)` (§4.13) versus `ReferenceEquals`. *Test:* `Tests/Performance/ReferenceEqualityBenchmarks.cs`. *Scenario:* 10⁶ comparisons, mixed `Reference.Null` and custom `IReference` with overridden `Equals`. *Success:* `ReferenceEquals` ≥10× faster, identical correctness under a deliberately-misbehaving `Equals` override.
- **`Subscribe` closure allocation per call** — *What:* alloc per `Store.Subscribe<TState>(reference, action)` invocation. *Tool:* Unity.PerformanceTesting. *Test:* `Tests/Performance/SubscribeBenchmarks.cs`. *Scenario:* 10⁴ `Subscribe` calls, each with a fresh lambda capturing one local. *Baseline:* one delegate alloc per call (unavoidable for a captured-lambda). *Success:* track only the *additional* allocations inside `Ledger.Add` (§4.10's `ContainsKey + indexer`); after `TryGetValue` fix, zero extra alloc beyond the delegate itself.
- **`MutatorRegistry.TryGet` lookup + `IPayloadMutatorBinding` enumeration** — *What:* lookup time and zero-alloc proof for the dispatch decision. *Tool:* Unity.PerformanceTesting. *Test:* `Tests/Performance/MutatorRegistryBenchmarks.cs`. *Scenario:* registry preloaded with 50 payload types, 10⁵ `TryGet` calls cycling through known and unknown keys. *Baseline:* `Dictionary<Type, List<>>` lookup + `Count == 0` guard. *Success:* zero alloc per lookup, ns/op within Dictionary baseline; serves as the "before" measurement for the §7.1 source-generated dispatcher.

---

End of audit.
