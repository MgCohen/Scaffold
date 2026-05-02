# Audit: com.scaffold.entities.states

Auditor: senior architect review (entity↔state bridge focus)
Scope: 21 source files under `Assets/Packages/com.scaffold.entities.states` (Runtime + Tests)
Date: 2026-05-02

## 1. Summary

Bridge package that funnels `IMutableEntity<TDefinition>` writes through `Scaffold.States.Store` so an entity's variables and modifier stacks become a real `EntityVariableState` slice (snapshot-able). The shape is right — payload+mutator pairs are correctly typed, and `StateEntity<TDefinition>` keeps the engine's existing `IReadOnlyEntity`/`IMutableEntity` polymorphism intact. The bulk of the issues are **micro-method explosion** in `EntityVariableState`/`StoreVariableStorage`, **redundant `null` guards on records and `out` parameters**, and a **handcrafted type-switch** for variable-value equality that wants to be a single generic constraint.

Verdict: **Refactor.** Keep the public API; rewrite `StoreVariableStorage`'s diff/cache pipeline and consolidate `EntityVariableState`'s helpers.

## 2. Structure

```
com.scaffold.entities.states/
  Runtime/
    EntityBridgeContext.cs           (RegisterMutators)
    EntityStateFactory.cs            (Create<TDefinition>)
    EntityStateReference.cs          (record : IReference)
    EntityVariableState.cs           (the canonical record)
    StateEntity.cs                   (IMutableEntity adapter)
    StateEntityOps.cs                (cross-entity ops)
    StoreInstanceIdExtensions.cs     (InstanceId-keyed Store extensions)
    StoreVariableStorage.cs          (IEntityVariableStorage backed by store)
    Mutators/                        6 mutators (Add/Remove modifier, Add/Remove variable, SetBaseValue, RemoveModifiersBySource)
    Payloads/                        6 payload records
    Scaffold.Entities.States.asmdef  (refs Records, Entities, States)
  Tests/
    StateEntityIntegrationTests.cs   (~580 LOC)
    Scaffold.Entities.States.Tests.asmdef
  README.md
  package.json (v0.2.0)
```

asmdef: depends on `Scaffold.Records` (GUID), `Scaffold.Entities`, `Scaffold.States`.
DI: **No `Container/` Installer** despite the convention. `EntityBridgeContext.RegisterMutators(Store)` is the closest thing; it expects the consumer to call once.
Docs: README is unusually thorough — clear semantics on snapshot, batch, structural change, definition swap. Good.

## 3. What's Good

- `EntityStateReference(InstanceId)` (line 10) is a record-with-IReference: value equality is automatic and used as the canonical key. This is the right pattern for the broader `IReference` story (see states audit §4.15).
- Each payload is a `sealed record` paired 1:1 with one `internal sealed` mutator. The pairing is enforced by the `Mutator<EntityVariableState, TPayload>` generic at compile time. Exactly the architect's preference.
- `EntityVariableState` is an immutable record with `With…` methods returning new records — a clean reducer-style API.
- `StateEntityOps.RemoveModifiersFromSource` (`StateEntityOps.cs:12`) does the cross-entity sweep as a single `ExecuteBatch` so all-or-nothing atomicity is preserved, and the README documents this (P0.3 / P0.2 sections).
- `StoreVariableStorage` correctly disposes its `EntityVariableState` subscription on `Dispose` (`StoreVariableStorage.cs:57`), and `StateEntity.Dispose` cascades to it (`StateEntity.cs:31`).
- The `OnCanonicalRemoved` event lets `StateEntity` surface "your slice was pruned by snapshot load" without leaking the store's event types to the entity API (`StateEntity.cs:22`, `StoreVariableStorage.cs:47`).
- `StoreInstanceIdExtensions` (`StoreInstanceIdExtensions.cs`) hides the `EntityStateReference.From(...)` boilerplate at every Store call. Good ergonomic layer.
- Tests cover the rare-but-important paths: snapshot prune, batch coalescing, definition swap, variable structural change semantics.

## 4. Issues / Smells

### 4.1 Redundant null-checks on records and `Store` parameters

`EntityStateFactory.cs:20–36` has a dedicated `ValidateCreateArgs` method that null-checks `definition`, `store`, `instanceId`. Then `StoreVariableStorage`'s constructor (`StoreVariableStorage.cs:14–18`) null-checks the same three again. Three layers (factory → factory helper → storage ctor) for the same parameters.

`EntityStateReference.From` (`EntityStateReference.cs:14`) null-checks `entityId`, then every caller (`StoreInstanceIdExtensions.RegisterSlice`, `Get`, `Subscribe`, …) routes through it — fine — but `StateEntity.AddVariable` *also* null-checks `key` and `initialBase` (`StateEntity.cs:43–53`) before the payload constructor would have thrown anyway. The architect's rule: a few at the entry point. Right now there's a *gauntlet*.

`AddModifierPayload`/`AddEntityVariablePayload`/etc. are records — their compiler-generated constructors don't enforce non-null on their parameters either, so the entry-point check has to live somewhere. Pick **one** layer (the public `StateEntity.AddVariable`/payload ctor) and remove the rest.

### 4.2 `EntityVariableState` is fragmented into 15 micro-methods

`EntityVariableState.cs` is 364 lines for a record that conceptually has six operations. The split isn't wrong — methods are small and have single responsibilities — but it crosses into "abstraction theater":

- `WithoutModifiersFromSource` → `BuildStrippedModifierStacks` → `TryRebuildStackWithoutSource` → `TryBuildBucketWithoutSource` → `ApplyBucketIndexForSourceStrip` → `CopyBucketPrefix` → `ApplyRebuiltBucket` (lines 153–237).
- `ResolveEffectiveValues` → `CollectKeysUnion` → `PopulateEffectiveFromKeys` → `TryAddFoldedEffective` → `ResolveBaseValueForVariable` (lines 239–311).

Six private methods for "strip modifiers from a single source." The single `for` loop with two locals would be 15 lines and easier to verify. Some of these helpers (`CopyBucketPrefix`, `ApplyRebuiltBucket`) read like junior over-decomposition; refactoring them back into the parent method costs nothing and reads better.

This is the opposite extreme of the "minimum code" rule.

### 4.3 `WithVariable` silently no-ops when the variable already exists

`EntityVariableState.cs:102–105`:
```csharp
if (BaseValues.ContainsKey(variable))
{
    return this;
}
```

Same shape in `WithoutVariable` (`:119`), `WithoutModifier` (`:65–68`). These are "idempotent no-op" defaults. The architect prefers fail-fast. At minimum, the *Mutator* level (`AddEntityVariableMutator.Change`, `:11`) and the *Entity* level (`StateEntity.AddVariable`, `:55–58`) should make the "already present" decision explicitly and return a typed result — not embed it as a silent state-equal record return that subscribers can't distinguish.

(Note: the `bool` return on `StateEntity.AddVariable` *does* surface this — but **only** by re-reading the store before the payload runs. The check is a TOCTOU race if anything else writes between read and `Execute`.)

### 4.4 `WithVariable` doesn't add an empty modifier stack — but `WithoutVariable` removes both

Asymmetric. `WithoutVariable` (`:112–125`) removes the variable from both `BaseValues` and `ModifierStacks` if present in either. `WithVariable` (`:90–110`) only sets `BaseValues`; `ModifierStacks` is untouched. This is fine semantically but unobvious and undocumented. Add a comment or unify the surface.

### 4.5 `WithModifier` doesn't validate that the variable exists

`EntityVariableState.cs:37–50` allows adding a modifier for a variable that has no `BaseValue` and isn't `definition.DefinedVariables`. Later `ResolveEffectiveValues` will silently drop it (`TryAddFoldedEffective` returns when `baseValue` is null, `:288–293`). That's a default-masking-error pattern: the user added a modifier, the store accepts it, the resolver ignores it.

Either:
- Throw at mutation time (fail-fast), or
- Surface "orphan modifier stacks" via a diagnostic property.

### 4.6 `IndexOfModifierInBucket` linear scan on every `WithoutModifier`

`EntityVariableState.cs:330–341`. Acceptable for small modifier counts (<10) but the method is called inside a write hot path. If buckets can be hundreds of entries (long-running buff systems), an `Id`→index map per bucket cuts it. Note for later, not today.

### 4.7 `StoreVariableStorage.SameTypedPayloadValues` is a hand-rolled type switch

`StoreVariableStorage.cs:362–420`. Six `MatchXxxEquals` methods enumerate float/int/double/long/bool/string. Any new `IVariableValue<T>` (Vector3? GUID? enum?) silently falls through to `ReferenceEquals` — meaning unchanged values will look "different" and notify spuriously. Default-masks-error and doesn't scale.

A single generic `IEquatable<VariableValue>` contract on `VariableValue` (or an `EqualityComparer<VariableValue>` injected by the entity definition) would replace all six methods with one call.

Before:
```csharp
private bool MatchKnownPayloadEquals(VariableValue prev, VariableValue cur)
{
    return MatchFloatEquals(prev, cur)
        || MatchIntEquals(prev, cur)
        || MatchDoubleEquals(prev, cur)
        || MatchLongEquals(prev, cur)
        || MatchBoolEquals(prev, cur)
        || MatchStringEquals(prev, cur);
}
```

After:
```csharp
private static bool ValuesEqual(VariableValue? prev, VariableValue? cur)
    => EqualityComparer<VariableValue?>.Default.Equals(prev, cur);
```

If `VariableValue` doesn't already implement value equality, fixing it there is a one-line change that benefits every consumer.

### 4.8 `StoreVariableStorage` uses `.ToArray()` and `.ToList()` on hot paths

`StoreVariableStorage.cs:239`, `:240`, `:252`, `:272`, `:280`, `:286`, `:299`. Every notify path snapshots the subscriber list (good — defends against re-entry) but does so by allocating a fresh array/list each time:

```csharp
foreach (Variable subKey in perVariableSubscribers.Keys.ToList())   // line 252
foreach (Action<VariableValue> cb in list.ToArray())                 // line 272
Action<...>[] handlers = structuralSubscribers.ToArray();            // line 240, 280
```

Per `EntityVariableState` mutation, on an entity with N subscribers, this is `O(N)` allocations. The store's `ExecuteBatch` is supposed to keep this to one notify per slice (README line 41) — but the per-variable fan-out happens **per** state change. For a 50-variable entity in an active battle, this is dozens of arrays per frame.

Use ping-pong buffers or a generation counter. Same fix as states audit §4.9.

### 4.9 `StoreVariableStorage` rebuilds the entire effective-value map per state change

`StoreVariableStorage.cs:200–207`:
```csharp
HashSet<Variable> newKeys = ComputeKeySet(newState);
Dictionary<Variable, VariableValue> newMap = BuildCurrentValueMap(newState);
NotifyVariableSubscribers(newMap);
NotifyStructuralDiffs(newKeys, newMap);
CopyMapsIntoCaches(newMap, newKeys);
```

Two new dictionaries + a hashset, every change. Even for a single-modifier add. The README claims "rebuilds an internal effective-value map once per subscribed rebuild" (line 63), which is honest, but for a frequent-mutation entity (tick-based DoTs) this is the dominant allocation. Incremental-update logic against `lastKeySnapshot` would be a real win. Big change — see §7.

### 4.10 `BuildCurrentValueMap` calls `ComputeKeySet` *and* `state.ResolveEffectiveValues` — both walk the same union

`StoreVariableStorage.cs:308–319` builds `withMods = state.ResolveEffectiveValues(definition)` (which internally walks `BaseValues ∪ ModifierStacks ∪ definition.DefinedVariables` via `CollectKeysUnion`, `EntityVariableState.cs:250–269`), then immediately calls `ComputeKeySet(state)` (`StoreVariableStorage.cs:341–360`) which walks the same three sources again. Redundant work, two `HashSet<Variable>` allocations.

### 4.11 `StoreVariableStorage` has a default-! initialization pattern

`StoreVariableStorage.cs:42–44`:
```csharp
private Store store = default!;
private InstanceId entityId = default!;
private IEntityDefinition definition = default!;
```

…then nullified in `Dispose` (`:58–60`). Combined with `disposed` checks (`ThrowIfDisposed`, `:168–174`) this is fine, but using `default!` plus explicit re-nulling on dispose to silence nullable analysis is the kind of pattern that an analyzer should catch later. Tag it for a `WeakReference`-or-`null` rewrite if disposability matters; otherwise drop the re-null and rely on `disposed` only.

### 4.12 `StateEntity.ClearModifiers` allocates a `List<object>` even when empty

`StateEntity.cs:90–106`. The list is created before the count is known. Move the allocation inside the `if (count > 0)` branch, or pre-size it from the modifier stacks count.

Also: payloads here are `RemoveModifierPayload(Id, ...)`. Each one is a record allocation per modifier — fine, they're immutable, but worth noting.

### 4.13 `StateEntity.RemoveModifier` returns `true` unconditionally

`StateEntity.cs:84–88`:
```csharp
public bool RemoveModifier(Variable key, ModifierId id)
{
    store.Execute(Id, new RemoveModifierPayload(Id, key, id));
    return true;
}
```

The mutator (`RemoveModifierMutator → EntityVariableState.WithoutModifier`) silently no-ops if the modifier doesn't exist (see §4.3). The bool return is therefore a lie. Either thread the result back through the mutator (requires a `ref bool` or an `out`-paramish payload) or drop the bool entirely.

### 4.14 `StateEntity` fields use `default!` and are nulled on dispose, but `Initialize` is from a base class

`StateEntity.cs:13–14`. Same pattern as 4.11. Combined with `Initialize(...)` happening in `InitializeStateBacked` (calling base) and the parameterless ctor being public-ish (it's public via `BaseEntityInstance<TDefinition>` requirements?) — there's no enforcement that `InitializeStateBacked` was actually called. A factory method (`EntityStateFactory.Create`) is the only correct path; mark the constructor `internal` or hide it.

### 4.15 `EntityBridgeContext.RegisterMutators` is order-sensitive but order isn't documented

`EntityBridgeContext.cs:7–21`. Six mutators registered in a specific order. Are they order-sensitive? The `MutatorRegistry.Register` (states package, `MutatorRegistry.cs:11`) keys by `typeof(TPayload)` and each payload here maps to one mutator, so order shouldn't matter — but a future change adding a second mutator for, say, `AddModifierPayload` will silently inherit registration order. Add a comment, or assert "exactly one mutator per payload at registration time" elsewhere.

### 4.16 `StateEntityOps.TryAppendPayloadForSlice` filters by `EntityStateReference` only

`StateEntityOps.cs:39–42`:
```csharp
if (reference is not EntityStateReference entityRef) { return; }
```

Silently skips any `EntityVariableState` slice keyed under a non-`EntityStateReference` reference. Today nothing else writes `EntityVariableState`, but if a consumer accidentally does (`store.RegisterSlice(SomeOtherReference, EntityVariableState.Empty)` — totally legal in the states API), it would be silently dropped. Either reject at registration (typed slice keys would prevent this entirely — see §7) or log/throw here.

### 4.17 No DI Installer

Same as the states audit. Repo convention says `Container/`. The `EntityBridgeContext.RegisterMutators(store)` ceremony is itself the Installer-equivalent — but it's a static helper rather than a VContainer `IInstaller`. Wrap it.

### 4.18 README's "Definition swap (G2)" is not validated by code

README lines 47–49: "create a `StateEntity<TNewDef>` for the **same `InstanceId`**." There's no analyzer or runtime check that two `StateEntity` handles for the same `InstanceId` agree on which definition is current. The feature is "by convention." Document the invariant ("retain only one *active* StateEntity per id") and consider tracking it in a side-table on the `Store` if anyone ever depends on this.

### 4.19 Internal state mutability via `IReadOnlyDictionary` cast

`EntityVariableState.cs:11`:
```csharp
public sealed record EntityVariableState(
    IReadOnlyDictionary<Variable, VariableValue> BaseValues,
    IReadOnlyDictionary<Variable, IReadOnlyList<ActiveModifier>> ModifierStacks) : State;
```

Constructor takes `IReadOnlyDictionary` and the helpers always copy into a fresh `Dictionary` before mutating (`CreateMutableValues`, `:15`). Good defensive copying — but the record is also publicly constructible by anyone (records have public copy constructors by default), so a caller can pass in a *live* dictionary, hold a reference to it, mutate it externally, and break the canonical-immutability invariant. Either:
- Make the constructor `internal` and force allocation through factory methods, or
- Always copy into the record, even on direct construction.

`Empty` (`:13`) reuses two pre-allocated dictionaries — safe today because `WithVariable`/`WithModifier` always copy.

### 4.20 `Variables` enumeration is sorted on every read

`StoreVariableStorage.cs:30–34`:
```csharp
get
{
    ThrowIfDisposed();
    return lastKeySnapshot.OrderBy(v => v.Key, StringComparer.Ordinal);
}
```

`OrderBy` allocates a `LINQ` enumerator + sorts every time the property is read. README promises "deterministic for replay/UI" (line 88) which is honored, but the cost is paid even if the consumer doesn't need ordering. Cache the sorted list and invalidate on `CopyMapsIntoCaches`.

## 5. Suggested Before/After Snippets

### 5.1 Collapse `WithoutModifiersFromSource` chain

Before — 7 methods, ~85 lines (`EntityVariableState.cs:153–237`).

After:
```csharp
public EntityVariableState WithoutModifiersFromSource(ModifierSource source)
{
    Dictionary<Variable, IReadOnlyList<ActiveModifier>>? next = null;
    foreach (var (variable, bucket) in ModifierStacks)
    {
        var rebuilt = StripBucket(bucket, source);
        if (rebuilt is null) continue;
        next ??= CreateMutableStacks(ModifierStacks);
        if (rebuilt.Count == 0) next.Remove(variable);
        else next[variable] = rebuilt;
    }
    return next is null ? this : this with { ModifierStacks = next };
}

private static List<ActiveModifier>? StripBucket(IReadOnlyList<ActiveModifier> bucket, ModifierSource source)
{
    List<ActiveModifier>? rebuilt = null;
    for (int i = 0; i < bucket.Count; i++)
    {
        var m = bucket[i];
        bool drop = m.Source.HasValue && m.Source.Value.Equals(source);
        if (drop && rebuilt is null) { rebuilt = new(bucket.Count); for (int j = 0; j < i; j++) rebuilt.Add(bucket[j]); continue; }
        if (rebuilt is not null && !drop) rebuilt.Add(m);
    }
    return rebuilt;
}
```

Two methods, ~25 lines, same behavior, easier to step through.

### 5.2 Drop the type-switch on `VariableValue` equality

Before — `StoreVariableStorage.cs:362–420` (six `MatchXxxEquals` methods).

After:
```csharp
private static bool ValuesEqual(VariableValue? prev, VariableValue? cur)
    => EqualityComparer<VariableValue?>.Default.Equals(prev, cur);
```

Requires `VariableValue` to override `Equals`/`GetHashCode` (or be a `record`). If it isn't already, fix it at the source — that's a Scaffold.Entities concern and gives every consumer free value equality.

### 5.3 `StateEntity` factory-only construction

Before — public parameterless ctor + `InitializeStateBacked` (called only from factory).

After:
```csharp
public sealed class StateEntity<TDefinition> : ... { internal StateEntity() { } ... }
```

Plus `[InternalsVisibleTo("Scaffold.Entities.States.Tests")]` if the tests new it directly. Anybody else has to go through `EntityStateFactory.Create`.

### 5.4 Single null-check in `EntityStateFactory.Create`

Before — 3 layers of null-checks (factory → `ValidateCreateArgs` → `StoreVariableStorage` ctor).

After — one layer:
```csharp
public static StateEntity<TDefinition> Create<TDefinition>(TDefinition definition, Store store, InstanceId instanceId) where TDefinition : IEntityDefinition
{
    ArgumentNullException.ThrowIfNull(definition);
    ArgumentNullException.ThrowIfNull(store);
    ArgumentNullException.ThrowIfNull(instanceId);
    store.RegisterSlice(instanceId, EntityVariableState.Empty);
    var storage = new StoreVariableStorage(store, instanceId, definition);
    var entity = new StateEntity<TDefinition>();
    entity.InitializeStateBacked(instanceId, definition, store, storage);
    return entity;
}
```

…and remove the null-checks from `StoreVariableStorage`'s ctor.

## 6. Easy Wins (each <30 min)

1. **Collapse `EntityVariableState.WithoutModifiersFromSource` helpers** (lines 153–237) into 2 methods.
2. **Replace `SameTypedPayloadValues` and the six `Match…Equals` helpers** in `StoreVariableStorage.cs:362–420` with `EqualityComparer<VariableValue?>.Default.Equals(...)`. Verify that `VariableValue` has value equality; if not, that's a one-record-conversion in `Scaffold.Entities`.
3. **De-duplicate null-checks** between `EntityStateFactory.Create`, `EntityStateFactory.ValidateCreateArgs`, and `StoreVariableStorage` ctor — keep only at the public factory entry.
4. **Allocate `payloads` lazily in `StateEntity.ClearModifiers`** (`StateEntity.cs:93`) — only when the count is known to be > 0.
5. **Cache `Variables` sorted list** on `CopyMapsIntoCaches` (`StoreVariableStorage.cs:32, 222`); rebuild only when keys change.
6. **`StateEntity.RemoveModifier` either return `void` or actually thread the result back** (`StateEntity.cs:84`). Same for `AddModifier` (the `ModifierId` return is fine; it's the bool that's bogus).
7. **Reject `EntityVariableState` slices keyed under a non-`EntityStateReference`** in `StateEntityOps.TryAppendPayloadForSlice` (`StateEntityOps.cs:39`) — throw, don't skip.
8. **Make `EntityBridgeContext.RegisterMutators` a `Container/EntitiesStatesInstaller`** that registers via `IContainerBuilder` and exposes `RegisterMutators` for non-DI consumers.

## 7. Bigger Refactors

### 7.1 Incremental effective-value updates (1 day)

Today `StoreVariableStorage.ApplyCanonicalUpdate` rebuilds the entire key set + value map for every state change (`:200–207`, §4.9). For real entities (50+ variables, ticking modifiers) this dominates the per-frame allocation budget.

Plan:
- Keep `lastEffective` as a `Dictionary<Variable, VariableValue>` mutated in place.
- On `OnStateChanged`, *diff* `oldState.ModifierStacks` vs `newState.ModifierStacks` and `oldState.BaseValues` vs `newState.BaseValues` — only recompute effective values for changed variables.
- Notify per-variable subscribers only for variables whose effective value actually changed.

Requires preserving the previous `EntityVariableState` reference — it's already known via `store.Get<EntityVariableState>` *before* the mutator runs (record reference equality). The state slice is already immutable so the diff is cheap.

### 7.2 Typed slice keys (1 day, cross-package)

`Store.RegisterSlice(reference, state)` accepts any `IReference` for any `State`. The states audit §7.4 suggests tightening `IReference`. Going further: introduce a phantom-typed key:

```csharp
public readonly struct SliceKey<TState> : IReference where TState : State
{
    public IReference Inner { get; }
}
```

Then `EntityVariableState` registers under `SliceKey<EntityVariableState>(EntityStateReference.From(id))`. `StateEntityOps` no longer needs the `is not EntityStateReference` filter (§4.16) — the key type makes it a compile-time proof.

Cross-package change. Requires bumping `Scaffold.States` API.

### 7.3 Replace per-call `OrderBy` with a sorted maintenance set (half day)

§4.20. `lastKeySnapshot` is a `HashSet<Variable>`. Switch it to a `SortedSet<Variable>` with a comparer for `Variable.Key` ordinal, or maintain a `List<Variable>` parallel to the hashset that's kept in sorted order on `CopyMapsIntoCaches`. `Variables` then returns `IEnumerable<Variable>` over the sorted list — no LINQ, no allocation.

### 7.4 Source-generated payload→mutator pairs (cross-package, 1 day)

Same proposal as the states audit §7.1. Specifically applied here, the six payload+mutator pairs in this package (`AddModifierPayload`/`AddModifierMutator`, etc.) are perfect generator fodder — annotate the payload with `[MutatedBy(typeof(AddModifierMutator))]` (or vice versa) and let the generator emit `EntityBridgeContext.RegisterMutators` from compile-time discovery. No more hand-edit when a payload is added.

### 7.5 Dispose subscriptions via `IDisposable` returns rather than custom events (half day)

`OnCanonicalRemoved` is an `event Action` (`StoreVariableStorage.cs:47`) and `StateEntity.OnEntityRemoved` is an `event Action` (`StateEntity.cs:16`). Convert to `IDisposable Subscribe(Action)` for consistency with the rest of the package (`Subscribe`, `SubscribeToVariableStructuralChanges` already return `IDisposable`). One pattern per package.

## 8. Organization & Docs

- Folder layout is fine: `Mutators/`, `Payloads/` separation makes the 1:1 pairing visible at the directory level. Good.
- The `Runtime/` root has 7 top-level files (`StateEntity`, `EntityVariableState`, `StoreVariableStorage`, `StateEntityOps`, `EntityStateFactory`, `EntityStateReference`, `EntityBridgeContext`, `StoreInstanceIdExtensions`). That's the right grain — no need to add subfolders.
- README is the strongest in this audit pair: snapshot semantics, batch coalescing, modifier ordering, definition swap, disposal, enumeration order all documented. The `Subscription patterns` table (lines 53–58) is a model for other packages.
- README lacks a **public API summary** at the top — what the consumer can `new` vs what's `internal`. `StoreVariableStorage` is internal, `StateEntity<TDefinition>` is public-via-factory; this distinction isn't called out.
- No `Container/` folder. Add one and a `EntitiesStatesInstaller` (§4.17, §6.8).
- No XML doc comments on the public API (`StateEntity<TDefinition>`, `EntityStateFactory`, `EntityBridgeContext`, payloads). Records get auto-generated doc on the positional ctor — but methods like `EntityVariableState.WithModifier` are undocumented. Given the README's quality, adding `<see cref="…">` cross-links would be a near-zero-cost win.
- `StateEntityOps` is a static class with one public method. Either rename to make it discoverable (`EntityStateBatchOps`?) or fold the method into `StateEntity` as a static helper. Today the name suggests "operations on a StateEntity" and the user has to dig to find that it's actually a cross-store sweep.
- `StoreInstanceIdExtensions` is a long extension class (60 lines, 8 methods). Splitting Read/Write/Mutator extensions into three files is overkill — but a comment at the top stating "InstanceId-keyed convenience overloads of Store.* methods" would help discoverability.

### Comparison points

- **Fluxor's** `[ReducerMethod]` model: payload class + reducer method, discovered by source generation. This package is structurally identical (payload record + `Mutator<TState, TPayload>`) but registers manually via `EntityBridgeContext`. Source-gen replacement is straightforward. (https://fluxor.mrpmorris.com/)
- **Redux Toolkit's** `createSlice`: bundles state + reducers into one declaration. Scaffold's separation (state record / payload records / mutator classes) is more verbose but more analyzable. The trade-off is fine; just lean into the typing harder by removing residual `Type`-keyed dispatch.
- **Unity DOTS** `IComponentData` + `ISystem`: the moral equivalent of "EntityVariableState" + "AddModifierMutator." DOTS pays nothing per dispatch because of generic specialization. Scaffold pays a `Dictionary<Type, …>` lookup + one cast. Acceptable for now, removable with §7.4.

---

End of audit.
