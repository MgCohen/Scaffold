---
name: Modifier owns Apply (scalar-typed, single-generic, ordered fold)
overview: Delete VariableValue.Combine entirely. Each modifier subclass owns its calculation via Apply(T) where T is the scalar payload type — no VariableValue casts in modifier code. Single generic abstract base VariableModifier<T> means concrete modifiers are one-liners. Public API relaxes to T GetVariable<T>(Variable) matched via "av is IVariableValue<T>". Add an integer Order field on the modifier base; the handler sorts modifiers by Order (stable, ties by insertion) and left-folds Apply over the base scalar. No stable-ID attribute, no per-modifier registry. Initial concrete set: FloatAdd, FloatMultiply, IntAdd, IntMultiply, BoolOverride, StringAppend. No migration — existing EntityModifierEntry assets break and are re-authored.
todos:
  - id: variable-modifier-base
    content: Add [Serializable, Preserve] non-generic abstract VariableModifier carrying int Order; add generic abstract VariableModifier<T> : VariableModifier with abstract T Apply(T current); concrete modifiers inherit VariableModifier<T> directly with no other ceremony
    status: pending
  - id: concrete-modifiers
    content: Ship FloatAddModifier, FloatMultiplyModifier, IntAddModifier, IntMultiplyModifier, BoolOverrideModifier, StringAppendModifier — each is a one-liner Apply override on VariableModifier<T> with the appropriate scalar T
    status: pending
  - id: delete-combine
    content: Delete VariableValue.Combine abstract method and all four overrides; delete EntityVariableComputer (its job collapses into the handler fold)
    status: pending
  - id: scalar-public-api
    content: Relax IReadOnlyEntity.GetVariable<TVar>(Variable) to T GetVariable<T>(Variable) — drop the VariableValue constraint and match storage via "av is IVariableValue<T>"; same shape for TryGetVariable<T>; update EntityComponent<T> wrappers
    status: pending
  - id: modifier-type-index
    content: Add a small ModifierTypeIndex (Dictionary<Type scalarType, IReadOnlyList<Type modifierType>>) populated once via assembly scan for closed VariableModifier<T> subclasses; consumed by the editor picker and the entry rebase; no string IDs, no registry
    status: pending
  - id: entity-modifier-entry-shape
    content: Replace EntityModifierEntry.modifierValue (VariableValue) with modifier (VariableModifier); rebase compares the modifier's scalar T (read from its closed VariableModifier<T> base) against the variable's expected scalar T (read from the payload's IVariableValue<T> implementation); on mismatch, default to the first ModifierTypeIndex entry for that scalar
    status: pending
  - id: handler-ordered-fold
    content: VariableModifierHandler stores per-variable List<ActiveModifier> (named struct holding ModifierId Id + VariableModifier Modifier); insertion keeps the list sorted by Order ascending (stable on ties); GetEffective dispatches on the base value's IVariableValue<T> and folds Apply over the scalar
    status: pending
  - id: storage-and-notifier-shape
    content: Settle during implementation — either keep VariableValue as the effective-bag/notifier currency (handler wraps the scalar back into a fresh VariableValue via a one-line constructor on each concrete) OR push scalar-as-object through the effective bag and notifier; the public API change does not depend on this choice; document the call once made
    status: pending
  - id: editor-modifier-picker
    content: Update EntityModifierEntryDrawer to drive a TypeCache-derived modifier picker filtered by the variable's payload scalar T (via IVariableValue<T> introspection), pulling candidates from ModifierTypeIndex; surface the Order field; rebase managed reference on payload-type change
    status: pending
  - id: il2cpp-preserve
    content: Add [Preserve] on VariableModifier base; existing link.xml already preserves the runtime assembly fully — verify with an IL2CPP-High smoke build
    status: pending
  - id: tests-samples-docs
    content: Update modifier tests to construct concrete modifier types and assert via the new T GetVariable<T> API; add ordering tests (mul-then-add ≠ add-then-mul) and modifier-type-index tests (scalar→modifiers); re-author sample modifier assets; update README modifier section + breaking-change note; run validate-changes.ps1 + Unity EditMode tests
    status: pending
isProject: false
---

# Refactor: `VariableModifier<T>` owns `Apply`, scalar-typed, ordered fold

## Goals

- **Delete `VariableValue.Combine`.** `VariableValue` becomes pure data — no computation responsibility.
- **Modifier base is generic on the scalar.** A concrete modifier inherits `VariableModifier<float>` and overrides `Apply(float)` — that's it. No `VariableValue` casts in modifier code, no per-payload intermediate base classes, no interface to remember to add alongside the inheritance.
- **Public API is scalar.** `entity.GetVariable<float>(key)` instead of `entity.GetVariable<FloatVariableValue>(key).Get()`. The constraint relaxes from `where TVar : VariableValue` to no constraint, and the storage match goes through `av is IVariableValue<T>`. Single is-check, zero allocation.
- **Ordered fold.** `Order` (int) on the modifier base. Handler sorts modifiers by `Order` ascending (stable, ties broken by insertion order) and **left-folds** `Apply` over the base scalar. Multiplicative-then-additive composition (or any other ordering) is expressible.

## Explicitly out of scope (deferred follow-ups)

- **Phase enums / priority groups.** Flat `int Order` only.
- **Conditional / time-based modifiers** (duration, expiry, predicates).
- **Stable-ID rename survival for modifiers.** No `[VariableModifierId]` attribute. Renaming a concrete modifier class breaks any `[SerializeReference]` instances on assets — Unity's baseline behavior. Acceptable since this PR already accepts asset breakage. Adding it later (an attribute + a string field next to the `[SerializeReference]` for fallback resolution) is a one-shot follow-up if rename pain emerges.
- **Migration.** Existing `EntityModifierEntry` assets reference a `VariableValue` payload (e.g. a `FloatVariableValue { Value = 5 }`). After this refactor they reference a `VariableModifier` (e.g. `FloatAddModifier { amount = 5, order = 0 }`). The serialized field name and managed-reference type both change — old assets will not load. Sample assets in this PR are re-authored by hand. Same posture as the previous plan.
- **Additional ops** (`Min`/`Max` clamps, percent-of-base, `StringOverride`, etc.) beyond the parity set. Each is a one-class follow-up.

## Why this shape

### Why modifier-owns-Apply

Today the operation is implicit in the payload type: float means "sum", bool means "last write wins", string means "concatenate". That collapses three orthogonal axes (what type, what value, what operation) into one, which is why "multiply" has nowhere to live and why string-concat snuck in as a default.

Moving `Apply` into the modifier:

- Makes the operation explicit in authoring — author picks `FloatAddModifier` vs `FloatMultiplyModifier` rather than relying on payload-type-implied semantics.
- Removes the need for `VariableValue.Combine` to know about lists at all — modifiers are applied one at a time as a fold.
- Lets ordering matter: `(base=2) + Add(3) + Mul(4) = 20` versus `(base=2) + Mul(4) + Add(3) = 11`. With explicit `Order` and a fold, both are expressible.

### Why a single generic base (not interface, not double-generic)

Earlier iterations considered an `IModifier<T>` interface, or a double-generic `VariableModifier<TValue, TScalar>` paired with a widened `IVariableValue<T>`. Both worse:

- **Interface** forces concrete modifiers to remember to write `: VariableModifier, IModifier<float>` — two things, not one. Easy to forget the interface and end up with a modifier the handler can't fold.
- **Double-generic** drags `FloatVariableValue` into modifier code and requires `IVariableValue<T>` to expose a settable `Value` so the generic intermediate can construct a fresh wrapper. That's a public-API widening for a problem the modifier shouldn't have anyway — wrapping is the handler's concern, not the modifier's.

A single generic abstract class — `VariableModifier<T>` where `T` is the scalar — is the smallest version that works:

```csharp
public sealed class FloatAddModifier : VariableModifier<float>
{
    [SerializeField] private float amount;
    public override float Apply(float current) => current + amount;
}
```

One inheritance, scalar in, scalar out, no wrapper code anywhere in the modifier.

### Why no stable-ID attribute

`[VariableModifierId]` only earns its place if we also serialize the ID string alongside `[SerializeReference]` on the entry to enable rename-survival. We are not doing that. So the attribute would do nothing today. Discovery uses an assembly scan + `TypeCache`; rebase uses scalar-type matching against `VariableModifier<T>`'s closed generic argument; the editor picker labels with `Type.Name`. No strings needed.

### Why scalar public API

`GetVariable<TVar> where TVar : VariableValue` forces every caller to traffic in the wrapper type and then call `.Get()` to extract the scalar — every read is a two-step. Relaxing to `T GetVariable<T>(Variable)` and matching via `av is IVariableValue<T>` is a single is-check that returns the scalar directly. The wrapper type stays as a base-value-authoring concept (it's the `VariableSO` payload shape) and as a storage cell, but disappears from read-side caller code.

## Coupling diagram (after refactor)

```mermaid
flowchart LR
  subgraph keys [Keys]
    VSO[VariableSO]
    VRec[Variable]
  end
  Entry[EntityModifierEntry<br/>Variable Key + VariableModifier]
  Mod["VariableModifier (base: Order)<br/>VariableModifier&lt;T&gt;: Apply(T)"]
  Index[ModifierTypeIndex<br/>scalar Type → modifier Type[]]
  Handler[VariableModifierHandler<br/>sorted-bucket scalar fold]
  Editor[Drawer<br/>TypeCache picker filtered by scalar T]

  VSO -->|payloadTypeId| keys
  Entry --> Mod
  Mod -->|via closed VariableModifier&lt;T&gt;| Index
  Index --> Editor
  Handler -->|sort by Order, fold Apply| Mod
  Handler -->|dispatch via IVariableValue&lt;T&gt;| Mod
```

## 1. `VariableModifier` base + generic intermediate

The non-generic base exists only because heterogeneous storage in `VariableModifierHandler` and `[SerializeReference]` need a common runtime type. It carries `Order` and nothing else:

```csharp
[Serializable]
[Preserve]
public abstract class VariableModifier
{
    [SerializeField] private int order;
    public int Order => order;
}
```

The generic intermediate adds the typed `Apply`:

```csharp
[Serializable]
public abstract class VariableModifier<T> : VariableModifier
{
    public abstract T Apply(T current);
}
```

Concrete modifiers inherit `VariableModifier<T>` directly. No interface, no per-payload intermediate, no wrapper code:

```csharp
[Serializable]
public sealed class FloatAddModifier : VariableModifier<float>
{
    [SerializeField] private float amount;
    public override float Apply(float current) => current + amount;
}
```

`IVariableValue<T>` is **not** widened. It stays as `T Get()`. Wrapping a scalar back into a `VariableValue` (if needed at all — see §7) is the handler's concern, not the modifier's or the interface's.

## 2. Concrete modifier set (this PR)

Each is a one-line `Apply` override on `VariableModifier<T>` for the appropriate scalar.

| Class | Base | Field | Apply |
|---|---|---|---|
| `FloatAddModifier` | `VariableModifier<float>` | `float amount` | `current + amount` |
| `FloatMultiplyModifier` | `VariableModifier<float>` | `float factor` | `current * factor` |
| `IntAddModifier` | `VariableModifier<int>` | `int amount` | `current + amount` (unchecked) |
| `IntMultiplyModifier` | `VariableModifier<int>` | `int factor` | `current * factor` (unchecked) |
| `BoolOverrideModifier` | `VariableModifier<bool>` | `bool value` | `value` (ignores `current`) |
| `StringAppendModifier` | `VariableModifier<string>` | `string suffix` | `current + suffix` |

Notes:

- Int math is unchecked. Documented; checked variant is a one-class follow-up if it ever matters.
- `BoolOverrideModifier` replaces the implicit "last-write-wins" of the old `BoolVariableValue.Combine`. Behavior parity at `Order = 0` ties (last insertion wins).
- `StringAppendModifier` matches the implicit concat from the old `StringVariableValue.Combine`. A `StringOverrideModifier` is the obvious follow-up; not in scope.
- Add ordering convention: `Add` modifiers default `Order = 0`. `Multiply` modifiers default `Order = 100`. Author can override. Documented in README.

## 3. Modifier discovery — `ModifierTypeIndex`

A small static helper, populated once on first access by scanning loaded assemblies for `VariableModifier<>` subclasses and grouping by the closed `T`:

```csharp
internal static class ModifierTypeIndex
{
    public static IReadOnlyList<Type> ModifiersFor(Type scalarType);
    public static bool TryGetScalarType(Type modifierType, out Type scalarType);
    public static IReadOnlyList<Type> AllModifierTypes { get; }
}
```

Build process:

- For each non-abstract, non-open-generic type derived from `VariableModifier`, walk the base chain, find the closed `VariableModifier<T>`, read `T` via reflection.
- Insert into `Dictionary<Type, List<Type>>` keyed by scalar `T`.
- Cache the modifier→scalar reverse map for fast `TryGetScalarType`.

No string IDs. No `[VariableModifierId]`. The editor picker and the entry rebase both consume this index.

This is editor-and-runtime safe — it uses `AppDomain.GetAssemblies()` rather than `TypeCache` (which is editor-only), so it can be invoked from runtime rebase paths. The editor drawer can prefer `TypeCache` for liveness and fall back to or replace this index editor-side; both shapes produce the same set.

## 4. Public API — `T GetVariable<T>(Variable)`

```csharp
public interface IReadOnlyEntity
{
    T GetVariable<T>(Variable key);
    bool TryGetVariable<T>(Variable key, out T value);
}
```

Implementation in `BaseEntityInstance`:

```csharp
public T GetVariable<T>(Variable key)
{
    if (!Storage.TryGetEffective(key, out VariableValue av) || av == null)
        throw new InvalidOperationException(
            $"Variable '{key?.Key ?? "?"}' is not defined on this entity.");
    if (av is IVariableValue<T> typed)
        return typed.Get();
    throw new InvalidCastException(
        $"Variable '{key?.Key ?? "?"}' is {av.GetType().Name}; cannot read as {typeof(T).Name}.");
}

public bool TryGetVariable<T>(Variable key, out T value)
{
    value = default!;
    if (!Storage.TryGetEffective(key, out VariableValue av) || av == null) return false;
    if (av is not IVariableValue<T> typed) return false;
    value = typed.Get();
    return true;
}
```

Caller code shifts from `entity.GetVariable<FloatVariableValue>(key).Get()` to `entity.GetVariable<float>(key)`. The `where TVar : VariableValue` constraint is removed. Update wrapper passthroughs in `EntityComponent<T>` to match.

`VariableValue` survives only as the storage cell type and as the base-value authoring shape; it never appears in the read-side public API.

## 5. `EntityModifierEntry` shape change

Today:

```csharp
[Serializable]
public sealed class EntityModifierEntry
{
    [SerializeField] private VariableSO key;
    [SerializeReference] private VariableValue modifierValue;
    // ...
}
```

After:

```csharp
[Serializable]
public sealed class EntityModifierEntry
{
    [SerializeField] private VariableSO key;
    [SerializeReference] private VariableModifier modifier;

    public Variable Key => /* unchanged */;
    public VariableModifier Modifier => modifier;
}
```

`RebaseSerializedModifierPayloadIfMismatch` resolves the variable's expected wrapper `Type` via the existing `VariablePayloadTypeHelpers.TryResolvePayload` (matching the layering used by `VariableEntry` and the current `EntityModifierEntry`), then introspects `IVariableValue<T>` on the wrapper to recover the scalar `T`. It reads the modifier's scalar `T` from its closed `VariableModifier<T>` base via `ModifierTypeIndex.TryGetScalarType`, and compares:

```csharp
if (!VariablePayloadTypeHelpers.TryResolvePayload(key, nameof(EntityModifierEntry), out Type wrapperType))
    return; // unknown payload — helper already logged
Type expectedScalarType = VariablePayloadTypeHelpers.ExtractScalarType(wrapperType);

if (modifier == null
    || !ModifierTypeIndex.TryGetScalarType(modifier.GetType(), out Type modScalarType)
    || modScalarType != expectedScalarType)
{
    var candidates = ModifierTypeIndex.ModifiersFor(expectedScalarType);
    if (candidates.Count == 0) { /* log + skip */ return; }
    modifier = (VariableModifier)Activator.CreateInstance(candidates[0]);
}
```

`ExtractScalarType` is a small reflection helper added next to `TryResolvePayload` in `VariablePayloadTypeHelpers` — walks a `VariableValue` type's interface list for `IVariableValue<>` and returns the closed `T`. Cached.

## 6. `VariableModifierHandler` — ordered scalar fold

Storage stays per-variable. The bucket holds modifiers (not entries) and is kept sorted on insertion. Bucket entries use a named struct, not a tuple:

```csharp
internal readonly struct ActiveModifier
{
    public readonly ModifierId Id;
    public readonly VariableModifier Modifier;
    public ActiveModifier(ModifierId id, VariableModifier modifier)
    {
        Id = id;
        Modifier = modifier;
    }
}

private readonly Dictionary<Variable, List<ActiveModifier>> buckets = new();
```

`AddModifier` does an insertion-position scan (linear, since buckets are small) to keep the list sorted by `Order` ascending, ties resolved by insertion order. Reads then iterate without sorting.

`GetEffective` dispatches on the base value's `IVariableValue<T>` to recover the scalar type, then folds `Apply` over the scalar:

```csharp
internal VariableValue GetEffective(Variable key, VariableValue baseValue)
{
    if (baseValue == null) return null;
    if (!buckets.TryGetValue(key, out List<ActiveModifier> list) || list.Count == 0)
        return baseValue;

    return DispatchFold(baseValue, list);
}

private static VariableValue DispatchFold(VariableValue baseValue, List<ActiveModifier> list)
{
    if (baseValue is IVariableValue<float> f)  return Fold<float>(f.Get(), list, baseValue);
    if (baseValue is IVariableValue<int> i)    return Fold<int>(i.Get(), list, baseValue);
    if (baseValue is IVariableValue<bool> b)   return Fold<bool>(b.Get(), list, baseValue);
    if (baseValue is IVariableValue<string> s) return Fold<string>(s.Get(), list, baseValue);
    throw new InvalidOperationException(
        $"Base value type {baseValue.GetType().Name} does not implement IVariableValue<T>.");
}

private static VariableValue Fold<T>(T value, List<ActiveModifier> list, VariableValue baseValue)
{
    for (int i = 0; i < list.Count; i++)
        value = ((VariableModifier<T>)list[i].Modifier).Apply(value);
    return WrapScalar(baseValue, value); // see §7
}
```

The `(VariableModifier<T>)` cast is the one runtime check at fold time. Modifier-payload pairing is enforced at insertion (see §5 rebase) so the cast doesn't fail in practice; if it does, fail loud.

`DispatchFold`'s explicit branches cover the four scalar types shipped today (`float`/`int`/`bool`/`string`). Adding a new scalar type means adding a branch. A future generalization (reflection on `IVariableValue<>` to pick the closed `T`, dispatched via a cached delegate per scalar) is straightforward but out of scope here — keep the branches obvious until a fifth scalar lands.

`EntityVariableComputer` is **deleted**.

## 7. Effective-value storage and notifier currency — settle during implementation

The fold's final step is wrapping the scalar back into a `VariableValue` so it can slot into the existing `instanceEffectiveBag` and `notifier.Notify(key, VariableValue)` payload, **OR** the bag and notifier are taught to speak scalar (boxed `object`) and the wrap step disappears.

Two viable paths; pick during implementation:

**Path A — keep `VariableValue` as effective-bag/notifier currency.** Add a `(T value)` constructor to each concrete `VariableValue`:

```csharp
public sealed class FloatVariableValue : VariableValue, IVariableValue<float>
{
    [SerializeField] private float value;
    public FloatVariableValue() { }
    public FloatVariableValue(float value) { this.value = value; }
    public float Get() => value;
}
```

The handler's `WrapScalar` builds a fresh wrapper of the same concrete type as the base via a cached `Dictionary<Type wrapperType, Func<object, VariableValue>>` populated once at startup. Bag and notifier signatures unchanged. Allocation cost is identical to today (one wrapper per recalc).

**Path B — push scalar through bag and notifier.** Effective bag stores boxed scalars (or, if pursued further, a per-scalar-type bag). Notifier event payload becomes `(Variable, object)`. `WrapScalar` disappears. Smaller `VariableValue` footprint at runtime, but bigger surface change rippling through `IEntityVariableStorage`, the notifier's subscriber API, and any external listeners.

Both are workable and the public API change in §4 does not depend on the choice. Default to Path A to minimize ripple; consider Path B as a follow-up if `VariableValue` runtime presence is itself worth eliminating.

Document the call once made — README "Folder layout" + "Public API" sections need to match reality.

## 8. Editor — modifier picker driven by `TypeCache` + scalar filter

Update `Editor/EntityModifierEntryDrawer.cs`:

- Resolve the variable's expected scalar `Type` via `VariablePayloadTypeHelpers.TryResolvePayload` → wrapper type → `VariablePayloadTypeHelpers.ExtractScalarType` (the helper added in §5).
- Picker candidates: `ModifierTypeIndex.ModifiersFor(scalarType)`, optionally cross-referenced with `TypeCache.GetTypesDerivedFrom<VariableModifier>()` for editor liveness.
- Selection writes a default-constructed instance into the `[SerializeReference] modifier` field. The drawer expands the modifier's serialized fields inline below the picker (`amount`, `factor`, `Order`, etc.) — same pattern as the existing payload drawer.
- The `Order` field is surfaced as a small int field next to the picker.
- On variable-key change (payload type drift): if the current modifier's scalar `T` no longer matches, default to the first compatible modifier; do not silently coerce data.

Picker labels use `Type.Name`. No string IDs.

`TypeCache` is editor-only and stays under the `Editor/` asmdef — already enforced by package layout.

## 9. Equality and dictionary-key stability

`Variable` is unchanged from the previous refactor (`record (string Key, string PayloadTypeId)`). `VariableModifier` is **not** a dictionary key — only the per-variable bucket value type. No equality concern.

`ModifierId` (Guid-backed) is unchanged. Per-modifier identity for `RemoveModifier` is by `ModifierId`, not by content.

## 10. Tests

Update:

- `Tests/Runtime/EntityInstanceTests.cs` — `Modifiers_OnInstance_SumFloatValues` constructs `FloatAddModifier` instances and asserts via `entity.GetVariable<float>(key)`; `RemoveModifier_AfterNumericCombine_RestoresDefinitionBase` likewise; bool last-wins test reframed as `BoolOverrideModifier` with `Order = 0` (last insertion wins on ties).
- `Tests/Editor/EntityModifierEntryAssetEditorTests.cs` — modifier-field round-trip swaps to `VariableModifier`.
- Any test that calls `GetVariable<FloatVariableValue>(...).Get()` collapses to `GetVariable<float>(...)`.

Add:

- **Ordering test**: float variable base 2; `FloatAddModifier { amount = 3, order = 0 }` + `FloatMultiplyModifier { factor = 4, order = 100 }` produces `(2 + 3) * 4 = 20`. Swap orders → `(2 * 4) + 3 = 11`. Confirms ordering is observable.
- **Stable-ties test**: two modifiers with identical `Order`, where order-of-application matters (e.g., two `StringAppendModifier`s with different suffixes), confirm insertion order wins.
- **`ModifierTypeIndex` test**: `ModifiersFor(typeof(float))` returns the float-modifier set; `TryGetScalarType` recovers `T` from a concrete modifier type.
- **Type-mismatch test**: a `BoolOverrideModifier` paired with a float-payload variable triggers the entry's rebase logic — log + default to first compatible, do not silently apply.

## 11. Documentation

Update `Assets/Packages/com.scaffold.entities/README.md`:

- Modifier section: `VariableModifier<T>` owns `Apply(T)`. Author selects a concrete subclass per `EntityModifierEntry`. Picker is filtered by the variable's scalar type.
- `Order` semantics: ascending, stable on ties, ties broken by insertion order. Default `Add` modifiers at `0`, `Multiply` at `100`.
- "Adding a new modifier" recipe: `public sealed class XxxModifier : VariableModifier<T> { override T Apply(T current); }`. Parameterless ctor, `[Serializable]`, optional `[SerializeField]` data.
- "Reading a variable" recipe shows the new `entity.GetVariable<T>(key)` shape.
- **Breaking change notes**:
  - `VariableValue.Combine` is gone. Any external code that called it must move to authoring a modifier.
  - `IReadOnlyEntity.GetVariable<TVar>` constraint relaxed and meaning shifted: `T` is now the scalar (`float`), not the wrapper (`FloatVariableValue`). Call sites need updating.
  - Existing `EntityModifierEntry` assets (with `modifierValue: <VariableValue>`) will not load. Re-author with the new modifier picker.
  - Implicit numeric sum / bool last-wins / string concat is no longer the default — author the modifier explicitly.
  - Concrete `VariableValue` classes may grow a `(T value)` constructor (Path A in §7); harmless additive change.

## 12. Quality gate

- `validate-changes.ps1` per `AGENTS.md`.
- Unity EditMode tests: `com.scaffold.entities` package — full pass.
- Manual: re-author `Assets/Packages/com.scaffold.entities/Samples/Assets/Data/Authoring/SampleHealthBonusModifier.asset` (and any other sample modifier assets) using the new picker; confirm YAML now contains `modifier:` as a `[SerializeReference]` to a concrete modifier class; confirm `valueType` and `modifierValue` are absent.
- IL2CPP-High smoke build: dispatch fold succeeds for all six concrete modifier types on a representative entity.

## Risks and decisions

- **Public-surface deletion of `VariableValue.Combine`** — accepted breaking change. Documented. Audit before delete to confirm no out-of-package callers; if any are found, decide per-callsite whether to author a modifier or expose a small helper.
- **`EntityVariableComputer` deletion** — same audit; the type is `internal`-shaped by name. If actually `public`, deprecate-and-delete in this PR (no compat shim, same posture as the prior plan).
- **`GetVariable<TVar>` API change** — relaxed constraint and shifted meaning. `T` is now the scalar; callers passing `FloatVariableValue` will get a runtime `InvalidCastException` (no `IVariableValue<FloatVariableValue>` exists). Documented; mechanical fix at call sites. Affects `IReadOnlyEntity`, `BaseEntityInstance`, `EntityComponent<T>` passthroughs.
- **`int` overflow on `IntMultiplyModifier`** — unchecked semantics; documented. Checked variant deferred.
- **Sorted-on-insert vs sort-on-read** — chose sorted-on-insert because reads are the hot path. Insertion is rare. Linear insert scan is fine for expected bucket sizes (≤ tens).
- **Modifier rename breakage** — without `[VariableModifierId]`, renaming a concrete modifier class breaks `[SerializeReference]` instances on assets. Acceptable since this PR already accepts asset breakage. Mitigation (attribute + serialized-id-string) is a clean follow-up if rename pain emerges.
- **Path A vs Path B (§7)** — settle during implementation. Path A is contained; Path B is more invasive but eliminates `VariableValue` from the runtime read path. Default to A.
- **Asset breakage** — accepted, no migration. Sample assets re-authored in this PR.
- **IL2CPP** — covered by the existing `link.xml`. `[Preserve]` added on the modifier base. Validated by smoke build.

Scope is **package-internal**: all `VariableModifier`-related changes live under `com.scaffold.entities`. Public surface change is bounded to the `GetVariable<T>` constraint relaxation, deletion of `VariableValue.Combine`, deletion of `EntityVariableComputer` (pending audit), the `EntityModifierEntry` field rename, and the new `VariableModifier` hierarchy.
