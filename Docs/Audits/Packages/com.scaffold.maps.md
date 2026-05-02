# Audit: `com.scaffold.maps`

Audit date: 2026-05-02. Reviewer: senior architect.

## 1. Summary

**Classification:** This package is **type→type maps** — composite-key in-memory dictionaries with predicate-based filtered views. Not asset maps, not geographic maps. Concretely: `Map<TPrimary, TSecondary, TValue>` (a dictionary keyed on a `(TPrimary, TSecondary)` index struct) plus `Indexer<TPrimary, TSecondary, TValue>` (a "live filter" view rebuilt on add/remove and respected on update).

The intent is fine: a domain wants a 2D-keyed bag with named filtered slices, and computing those slices on every read is wasteful, so cache them and update on mutation. Generic, compile-time-typed, no Unity dependency. Dependency on `Scaffold.Records` (per `package.json:14`) but *not actually used by any of the 9 .cs files* — see 4.10 below.

The execution does not hold up to the rubric:

- **`Holder<T>` is dead infrastructure.** The class wraps a `TValue` in a heap object so the indexer can reference-equality-track it. But every read path in `Map<...>`/`BaseMap<...>` immediately dereferences `holder.Value`, so the wrapper exists *only* to give `Indexer.Track`/`Untrack` a stable object identity. Could be replaced by tracking the `Index<TPrimary, TSecondary>` key directly.
- **Defensive guard explosion.** `Map<TPrimary, TSecondary, TValue>` checks `if (predicateIndexers == null) throw new InvalidOperationException("Map indexers were not initialized.");` at the top of *14 methods* (Map.cs:55, 65, 75, 86, 101, 112, 123, 135, 150, 165, 181, 192, 213, 233, 253, 268, 282, 322, 338). The field is `readonly` and assigned in the constructor (`Map.cs:8-11`). It cannot be null. **Delete all 14.** Same shape in `BaseMap` (`BaseMap.cs:59, 69, 81, 91`).
- **`Holder<T>.EnsureValue` is an empty method body.** `Holder.cs:15-17`. Dead code with a misleading name.
- **`Add(TPrimary, TValue)` and `Add(TSecondary, TValue)`** silently use `default(...)` for the missing key (`Map.cs:60, 70`). With `TPrimary = string`, this means a null key — and `Index<TPrimary, TSecondary>(primary, secondary)` throws `ArgumentNullException` (`IndexComposite.cs:11-15`). So those overloads will throw if either type is a reference type. Default-value-hides-error in the worst possible way: looks like a convenience overload, fails at runtime.
- **`Indexer<...>` is not a real "live view".** Despite the README claiming "auto-sync", the `Track` method handles add and remove (when the predicate flips) but **value updates** do nothing. `Map.this[index]` setter (`Map.cs:39-49`) updates the holder's value without re-evaluating any indexer's predicate. The use case in the sample (`MapIndexerUseCases.cs:32-40`) explicitly tests "membership unchanged by value update" — but the predicate is `name == "Matheus" && age > 10`, a *key* predicate, not a value predicate. The whole indexer machinery is keyed-only; this is mentioned nowhere.
- **`GetPrimaryKeys()` and `GetSecondaryKeys()` allocate twice and return a `List` typed as `IReadOnlyCollection`.** `Map.cs:320-334, 336-350`. Builds a `HashSet`, then copies into a `List`, then returns. `HashSet<T>` is already an `IReadOnlyCollection<T>`. Drop the copy.
- **No tests for indexers**, despite indexers being the headline feature. `Tests/MapReadOnlyAndGetAllTests.cs` covers `GetAll`, `IReadOnly*` interfaces, and key enumeration — none test `AddIndexer`, `Track`, `Untrack`, predicate-rebuild semantics, name collisions, or removal.
- **`IReadOnlyMap<TPrimary, TSecondary, TValue>` mixes read with mutation discovery.** It exposes `TryGetIndexer` (`IReadOnlyBaseMap.cs:30`), which returns the live `Indexer<...>` instance — a class with internal mutators (`Track`, `Untrack`, `Clear`, `Rebuild`). Read-only-ish.

**Verdict: Cute idea, leaky implementation, untested where it matters.** Three real bugs (4.4, 4.5, 4.6 below), substantial cruft to delete (4.1, 4.2, 4.3), and a hole in the test coverage that would catch all three. After cleanup this is ~150 lines instead of ~400.

## 2. Structure

```
com.scaffold.maps/
  Runtime/
    BaseMap.cs                Dictionary<TKey, Holder<TValue>> wrapper
    Holder.cs                 1-field box around TValue, useless EnsureValue
    IReadOnlyBaseMap.cs       IReadOnlyBaseMap<TKey,TValue> + IReadOnlyMap<TP,TS,TV>
    IndexPrimary.cs           Index<TPrimary>            single-key index struct
    IndexComposite.cs         Index<TPrimary, TSecondary> composite-key index struct
    Indexer.cs                Predicate-filtered live view
    Map.cs                    Map<TPrimary, TSecondary, TValue> : BaseMap<Index<...>, TValue>
    Scaffold.Maps.asmdef
  Samples/
    MapIndexerUseCases.cs     4 sample methods exercising indexer flow
    Scaffold.Maps.Samples.asmdef
  Tests/
    MapReadOnlyAndGetAllTests.cs   8 tests; none for indexers
    Scaffold.Maps.Tests.asmdef
    .gitkeep                       leftover
  package.json, README.md, asmdef
```

The split is fine. The presence of two `Index<>` structs in separate files (`IndexPrimary.cs` and `IndexComposite.cs`) is gratuitous — they're both ~50 lines of boilerplate for `IEquatable`/`==`/`!=` and could share a file (`Index.cs`) without confusion.

`Index<TPrimary>` (single-key) is **not used anywhere** in the package or — based on the surface — by anything else. Dead.

## 3. What's good

- **Generics throughout.** No reflection, no boxing in the hot path (excluding the `Holder<T>` heap allocation per entry, which is suspect — see 4.1). `EqualityComparer<T>.Default` is used for equality (`IndexComposite.cs:29-31, 41-43`, `Map.cs:218-225, 237-247`).
- **`Index<TPrimary, TSecondary>` is a `readonly struct` with proper `IEquatable<>`.** No accidental boxing on dictionary lookup, stable hash code, `==`/`!=` operators (`IndexComposite.cs:6-66`).
- **`Map.AddIndexer` rebuilds against existing entries** (`Map.cs:121-131`). So registering an indexer after the map has data Just Works. Right call.
- **Composite-key dictionary is a real ergonomic win** over `Dictionary<(TP, TS), TV>` in C# 7.x; with `Index<TP, TS>` you can have dedicated equality semantics, custom hash code, and a typed key without losing inference. `EqualityComparer<TPrimary>.Default` for both halves is correct.
- **The `IReadOnlyBaseMap` / `IReadOnlyMap` separation** is the right split if you want to hand a read-only view to a consumer (sub-system that should not mutate the map). Implementing both on the concrete class (`Map.cs:6`) lets you upcast.
- **Tests use `Is.EquivalentTo` instead of `Is.EqualTo`** for unordered collections (`MapReadOnlyAndGetAllTests.cs:23, 36, 86, 99`). Correctness; dictionary enumeration order is not guaranteed.
- **`AddIndexer` validates name and predicate non-null and rejects empty name** (`Map.cs:121-131` + `Indexer.cs:76-93`). Entry-point validation, which the rubric allows.

## 4. Issues / smells

### 4.1 `Holder<T>` is a wrapper that does nothing

`Holder.cs`:

```csharp
public class Holder<TValue>
{
    public Holder(TValue value)
    {
        EnsureValue(value);
        Value = value;
    }
    public TValue Value { get; set; }
    private void EnsureValue(TValue value) { }
}
```

The `EnsureValue` method has an empty body (`Holder.cs:15-17`) — it's *named* like a guard but does nothing. Dead code with a confusing name; either guard against null `value` for reference `TValue` (and document the constraint) or delete the method.

The class's only justification is "give the indexer a heap object to track by reference." Today, `Indexer.Track`/`Untrack` (`Indexer.cs:50-69`) keep a `List<Holder<TValue>>` and use `holders.Contains(holder)` and `holders.Remove(holder)` — both call `Equals`. `Holder` doesn't override `Equals`, so this is reference equality, which works because the same `Holder` instance is the value in the dictionary entry.

A `Dictionary<Index<TP,TS>, TValue>` plus an `Indexer` that tracks `Index<TP,TS>` keys (which already have value-based equality) is simpler:

```csharp
internal sealed class Indexer<TP, TS, TV>
{
    public IReadOnlyCollection<TV> Values  { get { ... lookup map for each tracked key ... } }
    private readonly HashSet<Index<TP, TS>> matchedKeys = new();
    internal void Track(Index<TP,TS> i, TV v) { if (predicate(i.Primary, i.Secondary)) matchedKeys.Add(i); else matchedKeys.Remove(i); }
    internal void Untrack(Index<TP,TS> i) { matchedKeys.Remove(i); }
}
```

Removes the `Holder<T>` allocation per entry, removes the `BaseMap.Add(TKey, Holder<TValue>)` boundary mismatch, and removes the entire `Holder.cs` file.

### 4.2 14 redundant guard checks in `Map<TPrimary, TSecondary, TValue>`

Every public/protected method on `Map` starts with:

```csharp
if (predicateIndexers == null)
    throw new InvalidOperationException("Map indexers were not initialized.");
```

`predicateIndexers` is `private readonly` and assigned in the constructor (`Map.cs:8-11`). It cannot be null. The 14 sites are:

- `Map.cs:55-58` (`Add(TPrimary, TValue)`),
- `Map.cs:65-68` (`Add(TSecondary, TValue)`),
- `Map.cs:75-78` (`Add(TPrimary, TSecondary, TValue)`),
- `Map.cs:86-89` (`Add(Index<>, TValue)`),
- `Map.cs:101-104` (`Contains`),
- `Map.cs:112-115` (`TryGetValue` overload),
- `Map.cs:123` (`AddIndexer`),
- `Map.cs:135-138` (`TryGetIndexer`),
- `Map.cs:150-153` (`RemoveIndexer`),
- `Map.cs:165-168` (`GetIndexedValues`),
- `Map.cs:181-184` (`Remove`),
- `Map.cs:192` (`Remove(Index<>)`),
- `Map.cs:213-216` (`GetAll(TPrimary)` returning list),
- `Map.cs:233-236` (`GetAll(TSecondary)` returning list),
- `Map.cs:253-256` (`GetAll(TSecondary, ICollection)`),
- `Map.cs:268-271` (`AddPrimaryKeysForSecondary`),
- `Map.cs:282-285` (`GetAll(TPrimary, ICollection)`),
- `Map.cs:322-325` (`GetPrimaryKeys`),
- `Map.cs:338-341` (`GetSecondaryKeys`).

That's 19 (recount) sites once you also count the GetAll overloads. Delete all of them. The rubric is unambiguous: entry-point only, and "entry point" means "the public boundary" not "every method on every class."

`BaseMap` has the same anti-pattern: `BaseMap.cs:59-62, 69-72, 81-84, 91-94` all check `if (data == null) throw`, where `data` is `private readonly` and assigned in *both* constructors (`BaseMap.cs:11, 21`). Cannot be null.

### 4.3 Mixed `is null`, `== null`, and inconsistent style

`BaseMap.cs:16` uses `is null`. `Map.cs:55` uses `== null`. `Indexer.cs:88` uses `== null`. `IndexComposite.cs:11` uses `is null`. Pick one. (`is null` for reference types is preferred in modern C# because operator-overloaded `==` doesn't change behavior, but the package never overloads `==` on a reference type, so it doesn't matter functionally — it just looks scattershot.)

`Indexer.cs:23-26, 44-46, 81-84, 89-91, 99-101, 105` opens braces in column 0 (de-dented under the line). Same auto-format issue as `Scaffold.Addressables/Implementation/AssetHandle.cs`. Reformat.

### 4.4 `Add(TPrimary, TValue)` and `Add(TSecondary, TValue)` use `default(T)` for the missing half

`Map.cs:60, 70`:

```csharp
public void Add(TPrimary primary, TValue value)        { Add(primary, default, value); }
public void Add(TSecondary secondary, TValue value)    { Add(default, secondary, value); }
```

Then in `Index<TPrimary, TSecondary>` constructor (`IndexComposite.cs:8-22`):

```csharp
if (primary is null)   throw new ArgumentNullException(nameof(primary));
if (secondary is null) throw new ArgumentNullException(nameof(secondary));
```

So:

- `Map<string, int, string>.Add("A", "X")` → secondary becomes `default(int)` = 0. Works.
- `Map<int, string, string>.Add(1, "X")` → secondary becomes `default(string)` = null. **Throws.**
- `Map<string, int, string>.Add(7, "X")` (the `TSecondary` overload) → primary becomes `default(string)` = null. **Throws.**

Two of the three overload combinations throw at runtime depending on `TPrimary`/`TSecondary` reference-vs-value. This is the rubric's "default values that hide errors" antipattern — the API offers it, the user expects it to work, the runtime says no. Either:

- Constrain `TPrimary` and `TSecondary` to `struct` for those overloads (compile-time error if reference type), **or**
- Delete those overloads entirely. They're shorthand for "I don't care about the other half" but the data structure does care; a value with a null half is meaningless.

Recommendation: delete. If users want sentinel keys, let them pass them.

### 4.5 Indexer is keys-only but not documented as such

`Indexer.predicate` is `Func<TPrimary, TSecondary, bool>` (`Indexer.cs:38`). Predicates run on the **key**, not the value. `Map.this[index]` setter (`Map.cs:39-49`) only mutates `holder.Value` — no indexer is consulted on set. So `value`-changes never reclassify entries.

This is a defensible design (an indexer is a key-filter, not a query) but the **README** says (line 13): "Owns automatic track/untrack behavior for indexers", and the sample (`MapIndexerUseCases.cs:32-40`) is named `UseCaseUpdateEntry_IndexerMembershipUnchangedByValue` — implying the design is intentional and tested. Two problems:

1. The README doesn't say "indexer predicates are key-only", and it should. A reader will assume `predicate(key, value)` because that's the more general shape.
2. Any consumer wanting **value-aware filtered views** (e.g., "all entries where value.Status == Active") cannot use this. They'll have to maintain their own collection. That's the use case "indexer" most strongly evokes.

If the design is truly key-only, rename `predicate` to `keyPredicate`, change the parameter type signal, and put a one-line "Note: predicates filter by key, not by value" in `Indexer.cs` and the README. If you want to support value predicates too, the data structure needs to call `Track` on every value mutation — `Holder<T>.Value` setter is the choke point but it's just a property, no observability. You'd add `Map.NotifyMutated(index)` and call it from the setter and from the `[index] = value` paths.

### 4.6 `Indexer.Values` allocates a new `List` on every read

`Indexer.cs:18-28`:

```csharp
public IReadOnlyCollection<TValue> Values
{
    get
    {
        List<TValue> result = new List<TValue>(holders.Count);
        foreach (Holder<TValue> holder in holders) result.Add(holder.Value);
        return result;
    }
}
```

Every read of `indexer.Values` allocates a new `List<TValue>`, copies N items, returns it. The sample reads it on the hot path (`MapIndexerUseCases.cs:12, 20, 29, 39`) and `Map.GetIndexedValues` calls it (`Map.cs:175-176`).

Two fixes:

- (A) Return a `ReadOnlyCollection<Holder<TValue>>` projected via `Select(h => h.Value)` — but that's IEnumerable allocation per call too.
- (B) Track keys (per 4.1's restructure) and read values lazily through the dictionary on demand.
- (C) Cache the projection and invalidate on `Track`/`Untrack`/`Clear`/`Rebuild`.

(B) is the right answer if you take 4.1; the keys-tracked variant lets `Values` be `holders.Select(k => map[k])` over a `HashSet<Index<TP,TS>>` and the GC pressure goes away. The semantic "Values is a snapshot" is preserved.

### 4.7 `BaseMap.this[key]` getter doesn't handle missing keys gracefully

`BaseMap.cs:24-34`:

```csharp
public virtual TValue this[TKey key]
{
    get { return data[key].Value; }
    set { data[key].Value = value; }
}
```

The getter throws `KeyNotFoundException` (Dictionary's default) — that's fail-fast and fine. The setter assumes the key already exists, will throw `KeyNotFoundException` on missing key. But `Map<...>.this[index]` setter (`Map.cs:39-49`) does try-get-then-add:

```csharp
public override TValue this[Index<TPrimary, TSecondary> index]
{
    get { return base[index]; }
    set
    {
        if (TryGetHolder(index, out Holder<TValue> holder)) { holder.Value = value; return; }
        Add(index, value);
    }
}
```

So the base setter is broken-by-design (only works for existing keys) and the override fixes it for `Map` but not for `BaseMap`. If `BaseMap` is meant to be subclassed by anything other than `Map`, the setter contract is wrong. If it's not, then `BaseMap` should be `internal abstract` and its `this[key]` setter removed (force subclasses to define `set` semantics). The `IReadOnlyBaseMap` interface only has a getter (`IReadOnlyBaseMap.cs:11`) — keep it that way.

### 4.8 `GetPrimaryKeys` / `GetSecondaryKeys` double-allocate

`Map.cs:320-334`:

```csharp
HashSet<TPrimary> keys = new HashSet<TPrimary>(EqualityComparer<TPrimary>.Default);
foreach (var entry in GetEntries()) keys.Add(entry.Key.Primary);
return new List<TPrimary>(keys);
```

`HashSet<T>` is `IReadOnlyCollection<T>`. The `List<TPrimary>(keys)` copy is wasted work. Just return the `HashSet<TPrimary>`. Same for secondary keys.

### 4.9 `IReadOnlyMap.TryGetIndexer` exposes a mutable `Indexer<>` from a "read-only" interface

`IReadOnlyBaseMap.cs:30`:

```csharp
bool TryGetIndexer(string name, out Indexer<TPrimary, TSecondary, TValue> indexer);
```

`Indexer<>` is a `public class` (`Indexer.cs:6`). Yes, the mutators (`Track`, `Untrack`, `Clear`, `Rebuild`) are `internal` — but that's only true within the assembly. Anyone consuming the `Scaffold.Maps` assembly can take the returned `Indexer<>` and read its `Values` / `Count` / `Name`. Fine. But the *type itself* lacks an `IReadOnlyIndexer<>` companion. If `IReadOnlyMap` is meant as a hand-off contract, hand off an `IReadOnlyIndexer<>` (with `Name`, `Count`, `Values`), not the concrete class.

### 4.10 `Scaffold.Records` dependency is unused

`package.json:14` declares `"com.scaffold.records": "0.1.0"`. Across all 9 `.cs` files in `Runtime/Samples/Tests/`, the string `Records` does not appear. The README's "Allowed Dependencies" lists `Scaffold.Records` (line 103) as if it's required.

Either the dependency was historical (delete it) or the integration is on a branch that didn't land here. Verify and prune.

### 4.11 `Index<TPrimary>` (single-key) is unused

`IndexPrimary.cs` defines `Index<TPrimary> : IEquatable<Index<TPrimary>>`. No `Map<TPrimary, TValue>` uses it; `Map<TP, TS, TV>` only uses `Index<TP, TS>`. If a future single-key `Map<TPrimary, TValue>` is planned, fine; if not, delete. YAGNI.

### 4.12 Tests miss the indexer machinery entirely

`Tests/MapReadOnlyAndGetAllTests.cs` has 8 tests:

- `GetAll(primary)` order/empty,
- `GetAll(secondary)` order,
- `IReadOnlyMap` upcast works,
- `IReadOnlyBaseMap` upcast works,
- `GetPrimaryKeys` distinct,
- `GetSecondaryKeys` distinct,
- empty-map `GetPrimaryKeys`.

Zero tests for: `AddIndexer` rebuild-on-add, `Track` on `Add`, `Untrack` on `Remove`, indexer-name collision, name validation in `AddIndexer`, `RemoveIndexer`, `Clear` clearing indexers, `TryGetIndexer` happy/missing, `GetIndexedValues` happy/missing-name (the latter throws `KeyNotFoundException` from `Map.cs:175` — uncovered).

Given the indexer is the package's reason to exist, this is the largest gap.

### 4.13 `AddIndexer` will throw `ArgumentException` on duplicate name

`Map.cs:129`:

```csharp
predicateIndexers.Add(indexer.Name, indexer);
```

Dictionary's `Add` throws on duplicate key. There's no test for this and no documented behavior. Either throw a typed `InvalidOperationException("Indexer 'foo' already exists")` for clarity, or use `predicateIndexers[name] = indexer;` to overwrite (and document overwrite).

### 4.14 `Map.GetIndexedValues(name)` throws `KeyNotFoundException` on missing name

`Map.cs:175`:

```csharp
Indexer<TPrimary, TSecondary, TValue> indexer = predicateIndexers[name];
return indexer.Values;
```

`Dictionary` indexer throws `KeyNotFoundException` when missing. The README claims "missing-indexer lookup" is covered (README:122). It isn't — there's no test, and the exception type is the framework's, not a domain one. Either return empty (debatable; hides typos), or wrap with a `KeyNotFoundException("Indexer 'foo' not registered. Call AddIndexer first.")` — fail-fast with a useful message.

### 4.15 `Tests/.gitkeep` leftover

A `.gitkeep` is for empty directories. The directory is not empty (it has `MapReadOnlyAndGetAllTests.cs` and the asmdef). Delete.

## 5. Suggested before/after snippets

### 5.1 Strip the redundant guards

**Before** (one example, `Map.cs:53-61`):

```csharp
public void Add(TPrimary primary, TValue value)
{
    if (predicateIndexers == null)
    {
        throw new InvalidOperationException("Map indexers were not initialized.");
    }

    Add(primary, default, value);
}
```

**After:**

```csharp
public void Add(TPrimary primary, TSecondary secondary, TValue value)
    => Add(new Index<TPrimary, TSecondary>(primary, secondary), value);
```

(And delete the two-arg overloads per 4.4.)

### 5.2 Replace `Holder<T>` with key-tracking

**Before** (relevant pieces of `Indexer.cs`, `Map.cs`, `BaseMap.cs`):

- `BaseMap` stores `Dictionary<TKey, Holder<TValue>>`.
- `Indexer` stores `List<Holder<TValue>>`.
- `Holder.Value` mutated in place keeps indexer references stable.

**After:**

```csharp
public class BaseMap<TKey, TValue> : IReadOnlyBaseMap<TKey, TValue>
{
    private readonly Dictionary<TKey, TValue> data;
    public BaseMap() { data = new(); }
    public BaseMap(IEqualityComparer<TKey> comparer) { data = new(comparer); }
    public virtual TValue this[TKey key]
    {
        get => data[key];
        set => data[key] = value;
    }
    public bool TryGetValue(TKey key, out TValue value) => data.TryGetValue(key, out value);
    public virtual bool Remove(TKey key) => data.Remove(key);
    public virtual void Clear() => data.Clear();
    // ...
}

public sealed class Indexer<TP, TS, TV>
{
    private readonly Func<TP, TS, bool> predicate;
    private readonly HashSet<Index<TP, TS>> keys = new();
    private readonly Func<Index<TP, TS>, TV> resolveValue;

    public IEnumerable<TV> Values => keys.Select(resolveValue);
    public int Count => keys.Count;

    internal void Track(Index<TP, TS> i)   { if (predicate(i.Primary, i.Secondary)) keys.Add(i); else keys.Remove(i); }
    internal void Untrack(Index<TP, TS> i) => keys.Remove(i);
    internal void Clear()                  => keys.Clear();
}
```

Drops `Holder.cs` (one file gone). Removes `Holder<TValue>` allocations from every `Add`. `Indexer.Values` is now O(1) to construct (an enumerable) rather than O(n) per call.

### 5.3 Constrain or delete the half-key Add overloads

**Before** (`Map.cs:53-71`):

```csharp
public void Add(TPrimary primary, TValue value)     { Add(primary, default, value); }
public void Add(TSecondary secondary, TValue value) { Add(default, secondary, value); }
```

**After:** delete both. Force the caller to pass both halves. If a "default secondary" is meaningful in the consumer's domain, the consumer should pass it explicitly.

### 5.4 Return `IReadOnlyCollection<T>` from key getters without copying

**Before** (`Map.cs:320-334`):

```csharp
HashSet<TPrimary> keys = new HashSet<TPrimary>(EqualityComparer<TPrimary>.Default);
foreach (var entry in GetEntries()) keys.Add(entry.Key.Primary);
return new List<TPrimary>(keys);
```

**After:**

```csharp
public IReadOnlyCollection<TPrimary> GetPrimaryKeys()
{
    HashSet<TPrimary> keys = new();
    foreach (var entry in GetEntries()) keys.Add(entry.Key.Primary);
    return keys;
}
```

## 6. Easy wins (5–8)

1. **Delete the 19+ `if (predicateIndexers == null)` / `if (data == null)` checks** in `Map.cs` and `BaseMap.cs` (4.2).
2. **Delete `Holder<T>.EnsureValue`** (empty body, 4.1) and rename / clean up the file.
3. **Delete `Map.Add(TPrimary, TValue)` and `Map.Add(TSecondary, TValue)`** (4.4) — the two half-key overloads that throw on reference types.
4. **Drop the `List<>` copy** in `GetPrimaryKeys` / `GetSecondaryKeys` (4.8); return the `HashSet<>` directly.
5. **Remove `Scaffold.Records` from `package.json`** if the dependency is unused (4.10) — verify first.
6. **Delete `IndexPrimary.cs`** (`Index<TPrimary>` is unreferenced, 4.11).
7. **Delete `Tests/.gitkeep`** (4.15).
8. **Reformat `Indexer.cs`** — fix the column-0 brace placement (4.3).
9. **Document indexer predicates as key-only** in `README.md` and `Indexer.cs` XML doc (4.5). Optional: rename `predicate` to `keyPredicate`.

## 7. Bigger refactors

### 7.1 Kill `Holder<T>` and re-key indexers to `Index<TP, TS>`

Per 5.2. This is the structural change that pays for the rest. Net deletion: `Holder.cs` plus boilerplate in `BaseMap`, `Map`, `Indexer`. Net addition: a tiny lambda in `Indexer` to resolve values from the parent map. The `Indexer` becomes nearly stateless (only stores matched keys) and `Indexer.Values` is allocation-free.

### 7.2 Decide: key-only predicate vs. value-aware predicate

Per 4.5. The current design is key-only. If you ship that, document it loud. If you want value-aware predicates, you need a value-mutation hook on the map and an `Indexer.UpdateValue(index, oldValue, newValue)` path. Doable but doubles the indexer surface.

Recommendation: stay key-only, rename `predicate` to `keyPredicate`, and make a separate `LiveQuery<TP, TS, TV>` type later if value predicates ever come up. Don't bake value predicates into `Indexer` retroactively.

### 7.3 Test the indexer

Per 4.12. Add `MapIndexerTests.cs` with at least these tests:

- `AddIndexer_RebuildsAgainstExisting()`,
- `Add_TracksMatchingPredicate()`,
- `Add_DoesNotTrackNonMatching()`,
- `Remove_UntracksFromIndexer()`,
- `Clear_ClearsAllIndexers()`,
- `AddIndexer_DuplicateName_Throws()`,
- `RemoveIndexer_RemovesByName_AndReturnsTrueWhenPresent()`,
- `TryGetIndexer_Missing_ReturnsFalse()`,
- `GetIndexedValues_MissingName_Throws()` (or returns empty; whichever you pick in 4.14).

About 60-80 lines. Catches every regression in 7.1.

### 7.4 Introduce `IReadOnlyIndexer<TP, TS, TV>`

Per 4.9. Three properties: `Name`, `Count`, `Values`. `IReadOnlyMap.TryGetIndexer` returns this; `Map.AddIndexer` returns the concrete `Indexer<>` to its creator. Keeps the read-only contract truly read-only.

### 7.5 Consider `Map<TKey, TValue>` (single-key)

If you have an actual use for `Index<TPrimary>` (4.11), build a `Map<TKey, TValue>` peer and let it host single-key indexers. If not, delete the struct; its presence implies an unfinished plan.

## 8. Organization & docs

Namespace is consistently `Scaffold.Maps`. asmdef name `Scaffold.Maps`. Folder name `com.scaffold.maps`. Good consistency, unlike the autopacker package.

The README is well-organized but wrong about a few specifics:

- README:18 says "Owns automatic track/untrack behavior for indexers." True, but doesn't qualify "by key, not by value." Add the qualification.
- README:99-101 lists invariants:
  - "index equality/hash remains stable" — yes, `Index<TP, TS>` is `readonly struct`.
  - "indexer membership reflects key predicate, not value-only updates" — *this is buried in invariants* but should be in the public-facing description.
  - "remove/clear operations fully untrack entries" — partially covered by `Map.Remove(Index<>)` (`Map.cs:190-200`) and `Map.Clear()` (`Map.cs:202-209`). No test for `Clear`.
- README:103 says `Allowed Dependencies: Scaffold.Records` — but the source uses none. Either the dependency is real and just imported elsewhere (a using directive somewhere not in this audit's 9 files), or it's stale. Reconcile.
- README:122 says "Added map/indexer coverage for missing-indexer lookup and null predicate guard on `AddIndexer`." Both claims are not present in `MapReadOnlyAndGetAllTests.cs`. Either the changelog is aspirational or the tests weren't checked in.
- The mermaid sequence diagram (README:49-61) is helpful and accurate.
- No rationale for `Holder<T>` is given anywhere, even though it's the key implementation device. If 7.1 deletes it, none needed.

The two-file split for `Index<>` (`IndexPrimary.cs` + `IndexComposite.cs`) is unusual; merge into `Index.cs` (or delete the unused single-key one).

The `Samples/MapIndexerUseCases.cs` is a good documentation artifact — the test names read like a use-case spec. Mirror that style in the actual test file (or move the samples into the test fixture and delete the redundant `Samples/` folder).

## References (composite-key dictionary patterns / live views)

- Microsoft, *Tuple keys in Dictionary* — `https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/value-tuples`. `Dictionary<(TP, TS), TV>` is the modern alternative; `Index<TP, TS>` is justified only if you need custom hash/equality (the current implementation doesn't).
- LINQ's `ILookup<TKey, TValue>` — `https://learn.microsoft.com/dotnet/api/system.linq.ilookup-2`. The "filtered grouped view" use case maps to `ILookup` more naturally than to a custom indexer; consider whether `Map.AddIndexer` is reinventing it.
- Reactive Extensions (Rx) `IObservable<T>` and "live query" patterns — for *value-aware* live filtering, a reactive collection (`ObservableDictionary` + `Observable.Where`) is the standard answer. If you go to value-aware predicates (7.2 alternative), look at `DynamicData` (`https://github.com/reactivemarbles/DynamicData`) before rolling your own — it solves exactly this and has years of edge-case fixes. Adding a dependency is a separate decision; the inspiration is free.
- *.NET runtime types EqualityComparer<T>.Default* — used correctly in this package (`IndexComposite.cs:29-31`, `Map.cs:218-225, 237-247, 298-301, 310-312`). No notes.
- Eric Lippert, "Why have both `is null` and `== null`?" (Stack Overflow / blog cross-post) — argues for `is null` for reference-type comparisons because operator overloading can't subvert pattern matching. Apply consistently across this package (4.3).

## 9. Consumers

A repo-wide `grep -rn 'new Map<\|: Map<\|: BaseMap<'` excluding the `com.scaffold.maps` package returns **exactly six call sites in three packages**:

| File | Declaration | Value type | Predicate / `AddIndexer` use? |
|---|---|---|---|
| `/home/user/Scaffold/Assets/Packages/com.scaffold.states/Runtime/State/Snapshot.cs:6` | `class Snapshot : Map<IReference, Type, State>` | class (`State`) | **No** |
| `/home/user/Scaffold/Assets/Packages/com.scaffold.states/Runtime/Store.cs:16` | `new Map<IReference, Type, Slice>()` | class (`Slice`) | **No** |
| `/home/user/Scaffold/Assets/Packages/com.scaffold.states/Runtime/Store.cs:17` | `new Map<IReference, Type, AggregateSlice>()` | class | **No** |
| `/home/user/Scaffold/Assets/Packages/com.scaffold.addressables/Runtime/Implementation/AddressablesAssetReferenceHandler.cs:24` | `new Map<Type, string, AddressablesLoadedEntry>()` | class | **No** |
| `/home/user/Scaffold/Assets/Packages/com.scaffold.mvvm/Runtime/Binding/BindRegistry.cs:24` | `new Map<string, Type, IBindContext>()` | interface (class behind it) | **No** |
| `/home/user/Scaffold/Assets/Packages/com.scaffold.mvvm/Runtime/Binding/BindSets.cs:7` | `new Map<Type, Type, IBindSet>()` | interface | **No** |

A second sweep — `grep -rn 'AddIndexer\|GetIndexedValues\|TryGetIndexer\|new Indexer<'` outside the package — returns **zero hits**. The headline feature of `com.scaffold.maps` has no production consumers in this repo. The `Indexer<>`, `Holder<T>`, `predicateIndexers` dictionary, the rebuild-on-`AddIndexer` machinery, and the entire `IndexPrimary.cs` file are dead infrastructure measured by call-site count.

Concrete observations from the call sites:

- **`Store.cs:16-17`** uses two `Map<IReference, Type, Slice>` / `Map<IReference, Type, AggregateSlice>` instances purely as 2-key dictionaries. Every operation visible in `Store.cs` is `Add(r, t, slice)` (`:23, 29, 122, 295, 351`), `Contains(r, t)` (`:121, 294, 350`), `TryGetValue(r, t, out _)` (`:317, 455, 461`), `Remove(r, t)` (`:327`), `GetAll(stateType, buffer)` (`:433, 440`), and a foreach over entries (`:81, 102, 129`). Not one `AddIndexer` call. The `GetAll(secondary, buffer)` path is the only thing remotely "indexer-like" — it sweeps all entries to find ones with matching `Type`. With ~10s of references × ~10s of state types per gameplay frame, this is O(N) over all slices, but never measured.
- **`Snapshot.cs:6`** subclasses `Map<IReference, Type, State>` purely to add `Set` and `Get<TState>` shortcuts. The `Snapshot` is enumerated in `Store.cs:102, 129` and built via `snapshot.Add(...)` in `:84`. Zero indexers. A `Dictionary<(IReference, Type), State>` would be a drop-in replacement; the only API the consumer relies on is the foreach yielding `KeyValuePair<Index<IReference, Type>, State>` and `TryGetValue`/`Contains`/`Add`/`Remove`.
- **`AddressablesAssetReferenceHandler.cs:24`** — already called out in audit 4.5b. No indexer; pure `(Type, string) → Entry` cache.
- **`BindRegistry.cs:24`** — `Map<string, Type, IBindContext>` keyed on `(propertyPath, sourceType)`. The class adds, looks up via `TryGetValue(path, type, out _)`, removes via `Remove(path, type)`, iterates via `Values`. Indexers: zero.
- **`BindSets.cs:7`** — `Map<Type, Type, IBindSet>` keyed on `(sourceType, targetType)`. Identical pattern: get-or-add. Indexers: zero.

**Note on `StoreVariableStorage` (audit cross-reference):** the existing audit text said "rebuilds a Dictionary per state change". I confirmed at `/home/user/Scaffold/Assets/Packages/com.scaffold.entities.states/Runtime/StoreVariableStorage.cs:202-235` — `ApplyCanonicalUpdate` calls `BuildCurrentValueMap` (`:308-319`) which allocates a fresh `Dictionary<Variable, VariableValue>` *every state-change tick*, then `CopyMapsIntoCaches` rebuilds `effectiveCache` and `lastKeySnapshot` from scratch. **`StoreVariableStorage` does not import `Scaffold.Maps` at all.** It uses raw `Dictionary` and `HashSet` because — in this consumer's data model — the keying is single (`Variable`), not composite, so `Map<,,>` was the wrong shape. The double-allocate-on-mutate is a real perf smell, but it's *not* a maps-package issue. It's the consumer reinventing a dictionary-with-snapshot rather than using a value-aware reactive structure (see `DynamicData` in audit 8/Refs).

**Predicate-only-on-keys: bug magnet in practice?** Across these six consumers, **no one uses predicates at all**. The keys are stable composite identifiers (a `Reference` × a `Type`, or a `propertyPath` × a `Type`); they don't get filtered. Value mutation is what changes (a `Slice.State` flips, an `IBindContext` accumulates bindings, an `AddressablesLoadedEntry.RefCount` increments). If anyone ever did add an indexer for "all slices whose state == Active", they'd hit audit 4.5 immediately — predicates don't see value updates. Conclusion: **the design isn't currently a bug magnet because the trap is unreachable** (no caller is anywhere near it), but the moment someone tries to use the headline feature, the trap will spring. The README oversells; the implementation under-delivers; the consumers don't notice because they never tried.

**Is the `Map<,,>` abstraction earning its keep, or is `Dictionary<(TP, TS), TV>` enough at every consumer?** It is **not earning its keep**. At every one of the six call sites:
- `Index<TP, TS>` provides nothing that `(TP, TS)` value-tuples don't (both have proper `IEquatable<>` + hash; tuples auto-derived).
- The `BaseMap<TKey, Holder<TValue>>` indirection adds a `Holder<T>` heap allocation per `Add` (audit 4.1) for *zero* reuse — `Holder.Value` is read once in every consumer.
- `GetAll(secondary, buffer)` (`Store.cs:433`) is the only method that gives `Map<,,>` a slight ergonomic win over a tuple-keyed dictionary: it iterates entries and matches the secondary key. With a tuple-keyed dictionary, the consumer would write `foreach (var kv in dict) if (Equals(kv.Key.Item2, t)) buffer.Add(kv.Value);` — which is **what `Map.GetAll` does internally** (`Map.cs:233-256`). One less LOC at six call sites; not enough to justify the package.

**Verdict on the package itself:** delete it, inline `Dictionary<(TPrimary, TSecondary), TValue>` at each of the six call sites, and add a single static helper if `GetAll(secondary)` shows up in profiling. Net deletion: ~400 LOC and the `Scaffold.Records` ghost dependency.

## 10. Alternatives & prior art

- **`Dictionary<(TKey1, TKey2), TValue>` (BCL, zero deps)** — `https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2`. **Verdict: Adopt outright.** This is the actual answer for all six current consumers. C# value-tuple equality is auto-derived; `EqualityComparer<(T1,T2)>.Default` works as expected; performance is identical to a custom `Index<TP,TS>` struct; debugger displays the key inline.
- **`MultiKeyDictionary` NuGet** (`https://www.nuget.org/packages/MultiKeyDictionary`) — small package providing `Dictionary<TKey1, TKey2, TValue>` style. **Verdict: Build, only if needed.** Adds an external dep for what tuple-keys already give you. Skip.
- **DOTS `NativeMultiHashMap<TKey, TValue>`** (`https://docs.unity3d.com/Packages/com.unity.collections@2.5/api/Unity.Collections.NativeMultiHashMap-2.html`) — burst-compatible multi-value map. **Verdict: Steal pattern.** Wrong fit for these consumers (they're managed-heap-only, no burst), but the API contract "key → enumerable of values" is what `Map.GetAll(primary)` is reaching for. If `Store.GetAll<TState>()` ever moves to a Job, this is the model.
- **`ILookup<TKey, TElement>` (BCL LINQ)** — `https://learn.microsoft.com/dotnet/api/system.linq.ilookup-2`. **Verdict: Steal pattern.** The "filtered grouped view" use case (which is what `Indexer<>` *should* be) is `ILookup`. If indexers ever come back, model `IIndexer<TP, TS, TV> : ILookup<...>` so consumers can use LINQ.
- **DynamicData / `SourceList<T>` + `.Connect().Filter(...)`** (`https://github.com/reactivemarbles/DynamicData`) — production-grade reactive collection with **value-aware** live filters and grouping. **Verdict: Adopt** if value-aware filtered views ever become a real requirement (rather than a README claim). Audit 7.2 already pointed at this; the consumer survey here strengthens the case: *no one* needs the current key-only `Indexer`, so the only world where the maps package is justified is the future world where someone needs DynamicData-style live queries — at which point use DynamicData, not a hand-rolled `Indexer`.
- **`ConcurrentDictionary<TKey, TValue>` for the addressables refcount cache** — `https://learn.microsoft.com/dotnet/api/system.collections.concurrent.concurrentdictionary-2`. **Verdict: Adopt** in `AddressablesAssetReferenceHandler` once the in-flight dedup lands (cross-ref addressables audit 4.5). Removes the manual `lock (sync)` and gives `GetOrAdd` for the in-flight task table.

## 11. Benchmark plan

- **`Indexer.Values` per-read allocation.** Tool: Unity.PerformanceTesting + `MeasureMemory`. Test location: `Tests/Performance/MapIndexerValuesBench.cs`. Scenario: build a `Map<int, int, string>` with 10 / 100 / 1000 entries, register one indexer matching all keys, call `indexer.Values` 10k times. Baseline expectation: today allocates one `List<TValue>` of size N per call (`Indexer.cs:18-28`), so 10k × N items copied. Success criteria after refactor 7.1: zero allocations on read because `Values` is `keys.Select(resolveValue)` over a `HashSet<Index<TP,TS>>`.
- **`GetPrimaryKeys` / `GetSecondaryKeys` double-allocation.** Tool: Unity.PerformanceTesting (`Allocates` measurement). Test location: `Tests/Performance/MapKeyEnumerationBench.cs`. Scenario: 1000-entry map, call `GetPrimaryKeys()` 10k times. Baseline expectation: 10k `HashSet<TPrimary>` + 10k `List<TPrimary>(keys)` allocations (audit 4.8). Success criteria after fix: 10k `HashSet<TPrimary>` and **zero** `List<TPrimary>` allocations.
- **Index rebuild on bulk `Add`/`Remove`.** Tool: Unity.PerformanceTesting timing. Test location: `Tests/Performance/MapIndexerBulkRebuildBench.cs`. Scenario: empty map → register 5 indexers with varying selectivity (10%, 50%, 90% match) → bulk-`Add` 10 / 100 / 1000 entries; measure total time and allocations. Baseline expectation: today, `Indexer.Track` runs 5 times per `Add` (one per registered indexer; `Map.cs:96-99`), each scanning a list. Success criteria: documented O(K·N) cost; the test exists to *quantify* the cost so future "indexer slow" reports have a number to point at.
- **Predicate re-evaluation on value update — README-claim regression.** Tool: NUnit EditMode. Test location: `Tests/MapIndexerValueAwareTests.cs`. Scenario: register an indexer with predicate `(p, s) => true` (key-only, harmless); then attempt to register a *value-aware* predicate via the public API — it doesn't exist. Then: add an entry with a value that "should match", mutate the value to one that "shouldn't", read `indexer.Values`. Baseline expectation: the entry is **still in `Values`** because predicates never re-run on value changes (audit 4.5). Success criteria: the test **asserts the current behavior** with a name like `IndexerValues_DoesNotReflectValueMutation_ByDesign_KeyOnlyPredicates` and a comment pointing at the README line that misleadingly claims "auto-sync". This test pins the contract; if anyone changes `Map.this[index].set` to invoke `Track`, the test catches the silent semantic flip.
- **Race: concurrent `Add` to the same `Map<,,>` in `Store`.** Tool: NUnit EditMode (correctness, no perf). Test location: `Tests/MapConcurrencyTests.cs`. Scenario: spawn N=100 `Task.Run(() => map.Add(refX, typeY, sliceZ))` for distinct keys. Baseline expectation: **throws** or corrupts because `BaseMap<TKey, Holder<TValue>>` wraps a non-thread-safe `Dictionary` with no lock (`BaseMap.cs:11`) — and `Store` makes no claim of thread safety either, but it's also undocumented. Success criteria: documented behavior in `BaseMap` XML doc ("not thread-safe; callers must serialize access"), and either a `lock` in `BaseMap` or an explicit `[ThreadStatic]` warning in the README. (This is technically a `Store` issue; including it because `Map` is the implementation that would need to change.)
- **Tuple-keyed `Dictionary<(TP, TS), TV>` vs `Map<TP, TS, TV>` lookup/add/iterate.** Tool: Unity.PerformanceTesting. Test location: `Tests/Performance/MapVsTupleDictBench.cs`. Scenario: 10 / 100 / 1000 entries; benchmark `Add`, `TryGetValue`, foreach. Baseline expectation: tuple-keyed dictionary is within 5% of `Map<,,>` on lookup (both are dictionary-backed) but allocates **0 bytes** per `Add` vs `Map`'s one `Holder<TValue>` per `Add`. Success criteria: numbers good enough to justify deleting the package per the consumer-survey verdict.

