---
type: plan
status: proposed
tags: [#scaffold, #states, #catalog, #ref]
---

# Scaffold.States — `Ref<T>` + Catalog primitive

> **Plan A** of the Scaffold update sequence. Adds a typed-ref + in-process catalog primitive to `com.scaffold.states`. Pure additive — no removals, no breaking changes to existing APIs in this plan. Plan B (Scaffold.Entities refactor) depends on this landing first.

**Source spike:** [`Product/spikes/entities-state-unification.md`](../../Product/spikes/entities-state-unification.md) — locked decisions #13, #14, #15, #16, #17; final shapes §1.

---

## Decisions ratified during review (2026-05-05)

These resolve gaps surfaced in plan review against the current Scaffold.States codebase. Changes folded into the relevant sections below; Plan B-bound items noted in "Out-of-band reminders."

| # | Decision | Affects |
|---|---|---|
| 1 | Inline `Catalog` property in `Store.cs`. No `partial` declaration. | §5 |
| 2 | `IRef : IReference`. Existing `IReference`-typed APIs accept refs without overloads. Future kill of `IReference` is a separate cascade pass. | §1, §6 |
| 3 | `IPayloadReference.GetReference()` return type unchanged. Payloads with `IRef Target` `return Target;` — works because `IRef : IReference`. | §6 |
| 4 | (Plan B) Delete `BaseEntityInstance<TDefinition>`; fold into `EntityInstance<TDef>`. | Plan B |
| 5 | (Plan B) Rename `EntityVariableState` → `EntityState`. | Plan B |
| 6 | `Store` constructs its own `Catalog()` internally. `StoreBuilder` untouched (no `.WithCatalog()` step). | §5 |
| 7 | (Plan B) Delete `EntityStateReference`. Slices keyed directly by `Ref<T>`. | Plan B |
| 8 | (Plan B) Drop `IEntityVariableStorage.TryGetEffective`. Handle folds modifiers on every read. | Plan B |
| 9 | (Plan B) Strip `VariableBag` parent pointer. Definition default-fallback is handle-level only. Storage `Parent` is overlays only. | Plan B |
| 10 | (Plan B) Replace `InstanceId` with `Ref<T>` everywhere. `InstanceId` deleted at end of Plan B. | Plan B |
| 11 | (Plan B) `ModifierSource(IRef Source, int Tag)`. | Plan B |
| 12 | No-key `Store.Get<TState>()` overload untouched. `Reference.Null` continues to serve global/singleton slices. | §6 |
| 13 | `Register(obj)` for `ICatalogged` is idempotent on same-object; throws on different-object-same-Key. | §4 |
| 14 | `Ref<T>.ToString()` returns `$"Ref<{typeof(T).Name}>({Id:N})"`. | §2 |

---

## TL;DR

Add `IRef`, `Ref<T>` (`readonly struct` wrapping a `Guid`), `ICatalogged`, and an `ICatalog` interface exposed as `Store.Catalog`. The catalog API: `AllocateRef`, `RegisterAt`, `Register`, `Resolve`, `TryResolve`, `Unregister`. Catalog state is in-process side-state on the `Store`; **not** included in snapshots. Slice access accepts `IRef` as a key.

Goal: any object can be registered with the Store's catalog, returns a stable typed handle (`Ref<T>`), resolvable back to the object. Refs flow into slices as keys and as values; the catalog persists across `LoadSnapshot` so refs in restored slices stay valid.

---

## Scope

### In scope (this plan)
- New types: `IRef`, `Ref<T>`, `ICatalogged`, `ICatalog`.
- `Store.Catalog` sub-property exposing `ICatalog`.
- Slice access accepts `IRef` (additive overloads or unified key shape).
- Snapshot save/load contract: catalog untouched.

### Out of scope (deferred)
- Removing `Identifier<T>` family or any existing key types. Coexist for now.
- Wire-form serialization of refs (cross-run save/load).
- Catalog telemetry / introspection (`list all Refs of type T`, dumps).
- Periodic cleanup of orphaned entries (e.g., from transformation ref-swap).
- Thread-safety guarantees beyond what `Store` already provides.
- Anything in Scaffold.Entities (Plan B) or the bridge (Plan C).

---

## Hard requirements

1. **`Ref<T>` is a `readonly struct`.** Value semantics. Cheap equality, cheap copy. Never a class. (Unity version determines whether `readonly record struct` is available; if yes, use it; if no, hand-rolled `IEquatable<Ref<T>>` + `==` / `!=` / `GetHashCode`.)
2. **`Ref<T>` wraps a `Guid` directly.** No separate id wrapper type. The `Guid` is the underlying stable id — auto-id minted via `Guid.NewGuid()`, asset/declared keys provided as `Guid` via `ICatalogged`.
3. **`Ref<T>` equality is on the underlying `Guid` only.** Type parameter `T` is for compile-time clarity; not part of equality.
4. **`IRef` is the non-generic base.** `Ref<T> : IRef`. Polymorphic APIs (slice keys, payload targets) accept `IRef`.
5. **Catalog is in-process side-state, exposed via `Store.Catalog`** (sub-property of type `ICatalog`). NOT a flat addition of methods to `Store`. Same lifetime as `Store`.
6. **Catalog is NOT included in `SaveSnapshot`. `LoadSnapshot` does not touch it.**
7. **Refs in restored slices resolve correctly after `LoadSnapshot`.** Because the catalog persists, the registered objects are still there. This is the load-bearing rollback story.
8. **`Register` derives the ref's `Guid` from `ICatalogged.Key` when the object implements it; mints `Guid.NewGuid()` otherwise.** Auto-id is run-local; objects needing cross-run survival must implement `ICatalogged`.
9. **Two-step registration is the underlying primitive.** `AllocateRef<T>()` reserves a Guid; `RegisterAt<T>(ref, obj)` binds the object. `Register<T>(obj)` is the shorthand for the simple case. Two-step is required for construction patterns where the object needs its own ref at construction time (entity spawn).
10. **Slice access (`Get<TState>`, `Subscribe<TState>`, `AddState`, `Execute`) accepts `IRef`.** Satisfied via `IRef : IReference` — existing `IReference`-typed API surface accepts `Ref<T>` without overloads. Existing call sites unchanged.
11. **`ICatalogged.Key` returns `Guid`.** No abstraction layer.

---

## Hard avoids

1. **Do NOT include catalog state in `SaveSnapshot` / `LoadSnapshot`.**
2. **Do NOT make `Ref<T>` a class.** Value type only.
3. **Do NOT introduce a `StableId` (or similar) wrapper type.** `Ref<T>` wraps `Guid` directly. If we later need mixed key shapes, introduce a wrapper *then*.
4. **Do NOT introduce `AnonymousRef` or any other "fake ref" type.** Identity is a registration concern. Things not registered have no ref. (See Plan B for how `EntityInstance<TDef>` base handles the no-catalog case: it doesn't carry a SelfRef at all.)
5. **Do NOT couple `Ref<T>` equality to type `T`.** Equality is on `Guid`. Two `Ref<T>` with the same `Guid` but different `T` can't be directly compared at compile time (different types) — but their underlying `IRef.Id` equates.
6. **Do NOT add catalog methods directly to `Store`.** The catalog API lives under `Store.Catalog` (an `ICatalog` property). This is a hard structural choice.
7. **Do NOT auto-generate `Guid` non-deterministically when `ICatalogged` is implemented.** Auto-id is the fallback path only. If `ICatalogged.Key` is provided, that's the ref's Guid.
8. **Do NOT remove or rename existing identity types in this plan.** `Identifier<T>` / `InstanceId` / `IReference` stay. Migration is a later cascade pass.
9. **Do NOT add subscription-on-catalog-changes machinery.** The catalog is a data store. Reactivity is at the slice level, not the catalog level.
10. **Do NOT auto-Unregister.** Catalog entries are explicit-lifetime. Implementer decides when to call `Unregister`.

---

## Deliverables — concrete shapes

### 1. `IRef`

```csharp
public interface IRef : IReference
{
    Guid Id { get; }
}
```

Non-generic marker. Slice keys, payload targets, polymorphic APIs take this. Extending `IReference` (the existing empty marker in Scaffold.States) means every existing `IReference`-typed Store API — `Get`, `Subscribe`, `RegisterSlice`, `Execute`, etc. — accepts any `Ref<T>` without overloads. `IReference` may be retired in a later cascade pass; for now both interfaces coexist and `IRef` is the recommended primitive.

### 2. `Ref<T>`

```csharp
public readonly struct Ref<T> : IRef, IEquatable<Ref<T>>
{
    public Guid Id { get; }

    internal Ref(Guid id) { Id = id; }                  // construction is catalog-internal
    public static Ref<T> FromGuid(Guid id) => new(id);  // public escape hatch for cross-run rebuild

    public bool Equals(Ref<T> other) => Id.Equals(other.Id);
    public override bool Equals(object? obj) => obj is Ref<T> r && Equals(r);
    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Ref<T> a, Ref<T> b) => a.Equals(b);
    public static bool operator !=(Ref<T> a, Ref<T> b) => !a.Equals(b);

    public override string ToString() => $"Ref<{typeof(T).Name}>({Id:N})";
}
```

**Notes:**
- `internal` ctor: only the catalog mints `Ref<T>` normally. `FromGuid` is a public factory for cross-run rebuild paths (out of scope for Plan A; the door is open).
- Type parameter `T` is for compile-time ergonomics only — equality ignores `T`.
- If Unity supports `readonly record struct`, use that and most of the boilerplate vanishes:
  ```csharp
  public readonly record struct Ref<T>(Guid Id) : IRef;
  ```

### 3. `ICatalogged`

```csharp
public interface ICatalogged
{
    Guid Key { get; }
}
```

Optional. Objects implementing this provide the catalog's stable id. Use cases:
- ScriptableObject definitions returning the asset GUID via `AssetDatabase.AssetPathToGUID(...)` cast to `Guid`.
- Named entities returning a declared key cast to `Guid` (e.g., GUID derived from a stable string).
- Anything with a natural cross-run-stable identity.

Objects NOT implementing this get an auto-id (`Guid.NewGuid()`) from the catalog at register-time.

### 4. `ICatalog`

```csharp
public interface ICatalog
{
    // Two-step register — for construction patterns that need the ref before the object is fully built.
    Ref<T> AllocateRef<T>();
    void RegisterAt<T>(Ref<T> @ref, T obj);

    // One-step shorthand — derives Guid from ICatalogged.Key if present, else mints Guid.NewGuid().
    Ref<T> Register<T>(T obj);

    // Resolution.
    T Resolve<T>(Ref<T> @ref);                          // throws if not found
    bool TryResolve<T>(Ref<T> @ref, out T obj);

    // Explicit lifetime end.
    void Unregister<T>(Ref<T> @ref);
}
```

**Behavioral requirements:**
- `AllocateRef<T>()`: reserves a fresh `Guid`; returns `Ref<T>(id)`. The id is registered as "allocated, no object yet" — `TryResolve` returns false until `RegisterAt` lands.
- `RegisterAt<T>(ref, obj)`: binds `obj` to `ref.Id`. Throws if the ref was not allocated, or if a different object is already bound (idempotent re-binds of the same object are fine).
- `Register<T>(obj)`:
  - If `obj is ICatalogged c`, use `c.Key` as the `Guid`.
    - If that `Guid` is already bound to **the same `obj`**, return the existing `Ref<T>` (idempotent).
    - If it is bound to a **different** object, throw — different objects with colliding `Key` is a programmer error.
  - Else, mint via `Guid.NewGuid()`.
  - Bind, return `Ref<T>(id)`.
- `Resolve<T>(ref)`: throws (`KeyNotFoundException` or similar) if no entry. Returns the object cast to `T`.
- `TryResolve<T>(ref, out obj)`: false-with-default if no entry.
- `Unregister<T>(ref)`: removes the entry. Subsequent `Resolve` fails. Idempotent (no-op on already-removed).

### 5. `Store.Catalog`

```csharp
public sealed class Store    // existing declaration, unchanged
{
    public ICatalog Catalog { get; }    // inline addition
    // ... existing members
}
```

**Notes:**
- Property is added inline to the existing `Store.cs`. **Not** a partial class extension — `Store` stays `sealed`.
- The catalog instance is constructed inside the existing `Store` constructor (`this.Catalog = new Catalog();`). `StoreBuilder` is untouched — no `.WithCatalog()` step; if a swap reason ever appears (test spy, telemetry decorator), promote then.
- Same lifetime as the `Store`. The property is read-only — no runtime swap.
- Implementation holds the catalog state inside the concrete `ICatalog` impl, behind the interface. Not visible at the API surface.

**Internal storage:** likely a `Dictionary<Guid, object>` or `Dictionary<Type, Dictionary<Guid, object>>`. Implementer's call.

### 6. Slice access takes `IRef`

**Resolved by `IRef : IReference`.** Because the new ref interface extends the existing `IReference` marker, every Store API that already takes `IReference` — `Get<TState>(IReference?)`, `Subscribe<TState>(IReference, …)`, `RegisterSlice(IReference?, State)`, `Execute<TPayload>(IReference?, TPayload)`, etc. — accepts `Ref<T>` automatically. No new overloads. No signature changes. The internal `Map<IReference, Type, Slice>` keys on `IReference`, so `Ref<T>` flows in as a key for free.

The no-key `Get<TState>()` overload (which uses `Reference.Null` for global/singleton state) is untouched. Global slices keyed by `Reference.Null` continue to work alongside ref-keyed slices.

`IPayloadReference.GetReference()` continues to return `IReference`. Payloads with an `IRef Target` field implement `GetReference()` as `return Target;` — type-compatible because `IRef : IReference`.

### 7. Snapshot contract

`SaveSnapshot()` produces a snapshot that does NOT contain catalog state.
`LoadSnapshot(snap)` restores slice contents and does NOT touch the catalog.

**Test invariant:** register an object, take a snapshot, register another object, load the snapshot. Both objects still resolve (the second one because the catalog wasn't rolled back).

---

## Implementation order

1. **`IRef` + `Ref<T>`** — pure additive. Standalone.
2. **`ICatalogged`** — interface only, no logic.
3. **`ICatalog`** — interface declaration only (no impl yet).
4. **Catalog implementation behind `ICatalog`** — internal dictionary + the six methods. Hardest piece.
5. **`Store.Catalog` property** — wire the impl into `Store`.
6. **Slice access accepts `IRef`** — touch existing `Store.Get` / `Subscribe` / `AddState` signatures. Most likely to surface integration friction.
7. **Snapshot contract** — verify `SaveSnapshot` / `LoadSnapshot` do not touch the catalog. Likely a no-op in the existing impl (since the catalog is new), but assert via test.

Steps 1–3 can land in one PR (small additive types). Step 4–5 in another PR (catalog impl + Store wiring + tests). Step 6 in its own PR (slice key shape). Step 7 verified within step 4–5's PR or as a follow-up sweep.

---

## Validation

### Unit tests (required)

| Test | Asserts |
|---|---|
| `Register_NonICatalogged_AssignsAutoId` | Auto-Guid minted; `Resolve` returns the registered object |
| `Register_ICatalogged_UsesKey` | `Ref<T>.Id` equals the `ICatalogged.Key` value |
| `Register_TwoEquivalentICatalogged_ReturnsSameRef` | Same `Key` → same `Guid` → equal `Ref<T>` |
| `Register_TwoNonICatalogged_ReturnsDifferentRefs` | Auto-Guids are unique within process |
| `AllocateThenRegisterAt_ResolvesSuccessfully` | Two-step path works |
| `RegisterAt_BeforeAllocate_Throws` | Can't bind to a ref that wasn't allocated |
| `RegisterAt_DifferentObjectAtBoundRef_Throws` | Re-binding to a different object is rejected |
| `RegisterAt_SameObjectAtBoundRef_Idempotent` | Re-binding to the same object is fine |
| `Resolve_UnregisteredRef_Throws` | After `Unregister`, `Resolve` fails |
| `TryResolve_UnregisteredRef_ReturnsFalse` | `TryResolve` returns false-with-default |
| `RefEquality_SameGuid_AreEqual` | `Ref<T>` with same `Guid` equate, regardless of how minted |
| `RefHash_SameGuid_SameHash` | Hash codes consistent with equality |
| `IRefPolymorphism_DowncastWorks` | `IRef ref = someRef; ref.Id` returns the underlying Guid |
| `Snapshot_DoesNotContainCatalog` | `SaveSnapshot` output has no catalog state in it |
| `LoadSnapshot_DoesNotTouchCatalog` | After `LoadSnapshot`, refs registered between save and load still resolve |
| `LoadSnapshot_ReverseDirection_RefsStillResolve` | After `LoadSnapshot` rolling back across registrations, originally-registered refs still resolve |
| `Store_Catalog_PropertyAlwaysSet` | `store.Catalog` is non-null after `Store` construction |

### Integration tests (recommended)

- Slice access: `store.AddState(someRef, fooState)` then `store.Get<FooState>(someRef)` returns the state. Same for `IRef`-typed access.
- Slice value containing refs: store a slice that contains `Ref<T>` as a value; snapshot/rollback; verify slice value's ref still resolves.
- A small end-to-end: register two objects, store a slice keyed by one and referencing the other in a value, snapshot, register a third, load — original two still resolve, third does not appear in any slice but is still in the catalog.

### Property tests (nice-to-have, not required)

- For all `(Guid a, Guid b)`: `Ref<T>.FromGuid(a) == Ref<T>.FromGuid(b)` iff `a == b`.
- Register/Unregister cycles maintain catalog consistency.

---

## Open / decide-during-impl

1. **Internal catalog storage shape.** Single `Dictionary<Guid, object>` vs. `Dictionary<Type, Dictionary<Guid, object>>`. The latter avoids the cast on `Resolve` but doubles bookkeeping. Lean single-dict-with-cast unless perf surfaces an issue.
2. ~~**`Store` already partial?**~~ **Resolved (review #1):** add the `Catalog` property inline to the existing `Store.cs`; do not introduce a partial declaration. `Store` stays `sealed`.
3. **`Ref<T>` syntax — `readonly record struct` vs. hand-rolled.** Depends on Unity's C# version. Use record struct if available.
4. **Concurrency.** Single-threaded sim assumed. If `Store` is already thread-safe, mirror that on the catalog (lock or `ConcurrentDictionary`). Otherwise, no extra guarantees.
5. **`Unregister` while ref is referenced from a slice.** No automatic cleanup. The catalog entry is gone; slice values referencing it become dangling (`TryResolve` returns false). Document the hazard; don't try to prevent it.
6. **`ICatalog` impl name.** `Catalog` (concrete class) or something more specific (`StoreCatalog`, `InProcessCatalog`). Implementer's call.

---

## What "validate" means before moving to Plan B

Plan A is validated when:

- All "required" unit tests above pass.
- All "recommended" integration tests pass.
- An ad-hoc REPL test: register an arbitrary object, store its ref in a slice, snapshot, register more, load, ref still resolves.
- The Scaffold.States build is green and existing tests are unaffected.
- A short retro pass on this plan: any hard requirement or hard avoid that was wrong gets noted in the spike's open items, and the spike is updated.

After validation passes, Plan B (Scaffold.Entities refactor) starts.

---

## Out-of-band reminders

- The bridge package (`com.scaffold.entities.states`) is touched in Plan C, not Plan B and not here. Don't preemptively change bridge code while implementing this plan.
- The project layer (`Card`, `Zone`, `Player`, slices) is touched after the bridge. Same rule — don't preemptively migrate.
- Existing `Identifier<T>` / `InstanceId` / `IReference` machinery stays untouched in this plan. Migration is a later cascade pass after Plans A, B, C all land.
- Plan B (Scaffold.Entities) will NOT use `AnonymousRef` — `EntityInstance<TDef>` base has no `SelfRef`. Identity belongs to catalog-registered subclasses (`Card`, `Zone`), not the base class. Plan A does not produce or expose any "fake ref" types.

### Inherited by Plan B (ratified during Plan A review, 2026-05-05)

These were decided while reviewing Plan A but apply to Plan B's scope. Plan B's author should treat them as locked inputs:

- **Delete `BaseEntityInstance<TDefinition>`** ([file](../../Assets/Packages/com.scaffold.entities/Runtime/Core/Instance/BaseEntityInstance.cs)). Fold its responsibilities (definition field, storage field, read accessors) into `EntityInstance<TDef>` directly. Single class.
- **Rename `EntityVariableState` → `EntityState`.** The trimmed `(Bases, Modifiers)` shape no longer needs "Variable" in the name; the rename also signals the type is gutted vs. the old 365 LOC version.
- **Delete `EntityStateReference`** ([file](../../Assets/Packages/com.scaffold.entities.states/Runtime/EntityStateReference.cs)). With slices keyed directly by `Ref<T>`, the wrapper has no role.
- **Drop `IEntityVariableStorage.TryGetEffective`.** The handle folds modifiers on every read; storage exposes `TryGetBase` + `GetModifiers` only. The current `instanceEffectiveBag` cache in `LocalVariableStorage` goes away. Re-add a private cache only if profiling later shows it's needed; do not re-introduce it on the contract.
- **Strip `VariableBag`'s parent pointer.** Today `LocalVariableStorage.WireToDefinition` wires the definition's bag as a parent for default lookup. Remove that linkage. Definition default-fallback lives at the handle level (`Definition.TryGetDefaultValue` after the `IEntityVariableStorage.Parent` chain bottoms out). Storage `Parent` is overlays-only.
- **Replace `InstanceId(int)` with `Ref<T>(Guid)` everywhere.** All bridge payloads, `StateEntity<TDef>`, factories — migrate to `Ref<T>` / `IRef`. `InstanceId` is deleted at the end of Plan B.
- **`ModifierSource` shape:** `readonly struct ModifierSource(IRef Source, int Tag)`. Polymorphic source identity matches the rest of the system.
