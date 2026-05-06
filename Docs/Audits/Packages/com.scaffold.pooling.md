# com.scaffold.pooling — Audit

## Summary
Generic `Pool<T>` with optional `IPoolable` lifecycle and self-return events. Tight, well-tested, and well-typed — the strongest package in this audit set. The `Container/PoolingInstaller` is empty and `Active`'s allocation pattern is wasteful, but otherwise this one is "keep". **Verdict: keep, with two small fixes.**

## Structure
```text
com.scaffold.pooling/
  Container/PoolingInstaller.cs                (empty Install)
  Runtime/
    Contracts/IPoolable.cs
    Pool.cs                                    (sealed, generic, ~160 LOC)
    Scaffold.Pooling.asmdef
  Tests/PoolTests.cs                           (NUnit, 6 tests)
  package.json, README.md
```
Asmdef is clean (`Runtime/Scaffold.Pooling.asmdef`), no engine import in `Pool.cs`, but `noEngineReferences` is not set — set it.

## What's good
- `Pool<T>` is **`sealed`** and generic (`Runtime/Pool.cs:7`). No leaky inheritance hierarchy.
- Constructor validates inputs at the boundary and only there (`:11, 13-21`) — exactly the rubric.
- `RejectNullFromFactory` is the *one* repeated check, but it's localized and named (`:155-161`); it lives at the two factory call sites only (`:28, 64`). Acceptable.
- `IPoolable` is opt-in by interface, not required (`Runtime/Pool.cs:70`) — `Pool<int>` works.
- Self-return via `ReturnRequested` event (`Runtime/Contracts/IPoolable.cs:11`) plus handler caching (`:75-78`) avoids leaks across re-takes.
- `Take`/`Return` fail-fast on misuse: re-taking an already active item throws (`:48-50`); returning an unowned item throws (`:88-91`). Good.
- `Tests/PoolTests.cs` covers the meaningful behaviors: take/return movement, IPoolable lifecycle, idle cap, foreign return, clear, prefill. **Real tests, real assertions.**

## Issues / smells

### Wasteful allocation in `Active` getter
- `Runtime/Pool.cs:35` — `Active` returns a fresh `ReadOnlyCollection` over a fresh `List` on every read. For an oft-queried API on hot paths, that's two allocations per call. Either expose `IReadOnlyCollection<T>` directly (`active` is already a `HashSet<T>`, which implements `IReadOnlyCollection<T>` in modern BCL via wrappers — or expose `Count` + an enumerator) or document that `Active` is a snapshot.

### Empty installer
- `Container/PoolingInstaller.cs:8-10` registers nothing. If the package has nothing to install, delete the installer + its asmdef. Empty installers are noise that gets blindly added to scopes.
- Counter-argument: a future `IPoolFactory` might justify it. Decide deliberately.

### Boundary
- `Pool.cs` is pure C# (good). Asmdef should set `"noEngineReferences": true` (`Runtime/Scaffold.Pooling.asmdef:13` — currently false). It's a tools/utility package per `Architecture.md`.

### Default-values masking errors (minor)
- `RegisterPoolableIfNeeded` (`:68-79`) silently no-ops if `T` doesn't implement `IPoolable`. That's correct.
- `UnregisterPoolable` (`:134-148`) silently no-ops if the handler isn't registered. Could be a sign of double-return that already failed in `active.Remove`, so this is fine.

### `Take` ordering
- `Pool.cs:46-54` — `OnTakenFromPool()` fires from `RegisterPoolableIfNeeded` inside `Take`. If the IPoolable's `OnTakenFromPool` calls back into `Return` (programmer error, but possible), `active.Add` is true but the handler is registered after. Order of operations is fine because `RegisterPoolableIfNeeded` returns before `OnTakenFromPool` fires (`:76-78`), but: hooking the event before invoking the lifecycle would let a misbehaving impl re-enter. Worth a comment.

### `IReadOnlyCollection<T> Active` vs typed access
- `Active` exposes `T` instances by reference. Callers can mutate. That's by design for components, but consider `IReadOnlyList<T>` if order matters or `IEnumerable<T>` if iteration is the only use case.

### Container asmdef
- Verify `Container/Scaffold.Pooling.Container.asmdef` references `VContainer` and `Scaffold.Pooling`. (Not read in audit but follow the events package's pattern.)

## Suggested before/after

**Active without per-call allocation.**
```csharp
// before
public IReadOnlyCollection<T> Active => new ReadOnlyCollection<T>(new List<T>(active));

// after
public IReadOnlyCollection<T> Active => active;            // HashSet<T> is IReadOnlyCollection<T>
public int ActiveCount => active.Count;                    // cheap probe for callers
```
If snapshot semantics are needed, name it `SnapshotActive()` so the cost is visible.

**Hook event before invoking lifecycle, to keep re-entry safe.**
```csharp
private void RegisterPoolableIfNeeded(T item)
{
    if (item is not IPoolable poolable) return;
    Action handler = () => Return(item);
    poolableReturnHandlers[item] = handler;
    poolable.ReturnRequested += handler;
    poolable.OnTakenFromPool();          // call last — handler is fully wired
}
```

## Easy wins
1. Set `"noEngineReferences": true` in `Runtime/Scaffold.Pooling.asmdef`.
2. Replace `Active`'s allocating getter with the existing `HashSet<T>` cast (`Runtime/Pool.cs:35`).
3. Delete `Container/PoolingInstaller.cs` + its asmdef if no registrations are planned, or stub a `// reserved for IPoolFactory` comment.
4. Add `ArgumentNullException.ThrowIfNull(factory)` syntax to `Pool.cs:11` (cosmetic, but consistent with modern BCL).
5. Add one PlayMode/EditMode test for `Clear()` after a `RequestReturn` mid-iteration to prove robustness.

## Organization & docs
- README exists; confirm it documents the `IPoolable.ReturnRequested` ownership rule (handler is owned by the pool, fired once per Take).
- Naming is consistent. Folder layout matches the project pattern (`Runtime/Contracts`, `Runtime/`, `Container/`, `Tests/`).
- Reference: Unity's built-in `UnityEngine.Pool.ObjectPool<T>` (Unity 2021+). This impl is more featureful (self-return event, IPoolable hooks) — worth one line in README explaining why this exists alongside the built-in.

## Consumers

Single consumer in `Assets/`: `com.scaffold.states`. Two files. **One consumer total** for an entire pooling package.

**`com.scaffold.states/Runtime/Store.cs:35, 44`** — the only `Pool<T>` instantiation in the project:
```csharp
mutatorRunnerPool = new Pool<MutatorRunner>(() => new MutatorRunner(new Scratchpad(this)), null, initialSize: 2);
...
private readonly Pool<MutatorRunner> mutatorRunnerPool;
```
Smell: `null` passed for the `onRelease` (or whichever positional param the second slot is). At the call site this reads like "I don't want this", which works because the package supports it — but the convention forces the consumer to know what `null` means in slot 2. Named-arg or builder API would self-document. `initialSize: 2` is the only named arg; either name them all or none.

**`com.scaffold.states/Runtime/Pipeline/MutatorRunner.cs:8, 17-30`** — sole `IPoolable` implementation:
```csharp
internal sealed class MutatorRunner : IStateScope, IPoolable
...
event Action IPoolable.ReturnRequested
{
    add { }
    remove { }
}
void IPoolable.OnTakenFromPool() { }
void IPoolable.OnReturnedToPool() { scratchpad.Reset(); }
```
Smell: the `ReturnRequested` event is implemented as a pair of empty add/remove accessors — the consumer doesn't want self-return semantics, but `IPoolable` forces them to declare the event anyway. This is the strongest signal that `IPoolable` is over-specified: 2 of 3 members are no-ops, only `OnReturnedToPool` is real. Audit's recommendation to keep self-return as opt-in via a separate `ISelfReturning` interface (or function delegate) is vindicated by the only consumer.

**`com.scaffold.pooling.Container/PoolingInstaller`** — zero references in `Assets/`. Audit-flagged empty installer is also unused. Strong delete signal.

**Zero consumers** of `Pool.Active` (the allocating getter the audit flagged). The two-allocation-per-call cost is a foot-gun waiting for a future consumer; **this is the cheapest possible time to fix it** — no breakage radius.

**Zero consumers** of `Pool.Clear()`, `Pool.Prefill`, the idle-cap parameter, or any `IPoolable.OnTakenFromPool`. The single consumer uses ~30% of the public surface. Either delete the unused features or accept the package is over-built for the one current need.

## Alternatives & prior art

- **`UnityEngine.Pool.ObjectPool<T>` / `ListPool<T>` / `HashSetPool<T>`** — built-in since Unity 2021, generic, with idle cap and lifecycle callbacks. `https://docs.unity3d.com/ScriptReference/Pool.ObjectPool_1.html`. **Adopt**: identical surface for the `MutatorRunner` use case. `Pool<T>` adds `IPoolable.ReturnRequested` (self-return) and a `IReadOnlyCollection<T> Active` getter — neither is used by the only consumer. Replacing one `new Pool<MutatorRunner>(...)` with one `new ObjectPool<MutatorRunner>(...)` deletes the entire package.
- **Microsoft.Extensions.ObjectPool `ObjectPool<T>` + `IPooledObjectPolicy<T>`** — ASP.NET Core's pool. `https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.objectpool.objectpool-1`. **Steal pattern**: the `IPooledObjectPolicy<T>.Create()`/`Return()` shape is what `IPoolable` should be — a separate policy object, not an interface the pooled type itself implements.
- **`System.Buffers.ArrayPool<T>`** — for arrays specifically. `https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1`. **Build**: not applicable to `MutatorRunner`, but worth mentioning in README as the right answer for `byte[]`/`T[]` pooling so consumers don't reach for `Pool<byte[]>`.
- **`System.Threading.ObjectPool` (CoreCLR internal)** — TLS-backed lock-free pool. Not public. **Steal pattern**: if this package ever needs concurrent access, this is the reference design.

Honest verdict: `Pool<T>` is a 160-LOC reinvention of `UnityEngine.Pool.ObjectPool<T>` plus a self-return event no consumer uses. Either delete the package and migrate `Store.cs` to `ObjectPool<T>`, or document why `IPoolable.ReturnRequested` is worth the extra package.

## Benchmark plan

- **Take/Return throughput vs `UnityEngine.Pool.ObjectPool<T>`**
  - What: ns/op and allocations for a take→use→return cycle, both libraries.
  - Tool: `Unity.PerformanceTesting`, `SampleGroup(Time + AllocatedManagedMemory)`.
  - Location: `Tests/Performance/PoolBenchmarks.cs`.
  - Scenario: 100k cycles, pool initialSize=4, single thread, 5 warmup.
  - Baseline: `Pool<MutatorRunner>` ≈ tens of ns/op steady-state, 0 alloc; `Active` getter excluded.
  - Success: within 10% of `UnityEngine.Pool.ObjectPool<T>`; if more than 50% slower, justify the gap or migrate.

- **`Active` getter allocation (regression guard)**
  - What: bytes allocated per read of `Pool.Active`.
  - Tool: `Unity.PerformanceTesting`.
  - Location: `Tests/Performance/PoolBenchmarks.cs`.
  - Scenario: 10k reads against a pool with 100 active items, 1 warmup.
  - Baseline: today, ~2 allocs per read (`new List<T>(active)` + `new ReadOnlyCollection<T>(...)`).
  - Success: 0 alloc after the audit's fix (return `HashSet<T>` cast directly).

- **`IPoolable` lifecycle dispatch overhead**
  - What: extra ns/op when `T : IPoolable` vs `T` non-poolable.
  - Tool: `Unity.PerformanceTesting`.
  - Location: `Tests/Performance/PoolBenchmarks.cs`.
  - Scenario: two pools, 100k cycles each.
  - Baseline: 1 interface check + 1 virtcall per Take and Return.
  - Success: ≤ 30 ns overhead vs non-poolable; flag if higher.

- **Self-return re-entry safety**
  - What: not strictly perf — a stress test that 1000 items self-fire `ReturnRequested` during `OnTakenFromPool`.
  - Tool: NUnit (correctness), not `Unity.PerformanceTesting`.
  - Location: `Tests/PoolReentryTests.cs`.
  - Scenario: malicious `IPoolable` that triggers `RequestReturn()` during `OnTakenFromPool`.
  - Baseline: today probably throws or corrupts `active`/`poolableReturnHandlers`.
  - Success: either deterministic throw or correct re-pooled state; document the contract.
