# Audit: com.scaffold.entities

Reviewer: Senior Architect (audit pass).
Files inspected: 84 (entire package).
Date: 2026-05-02.

---

## 1. Summary

`com.scaffold.entities` is the gameplay-data backbone: a Unity-aware definition/instance/modifier model layered as a three-bag chain (definition → instance-base → instance-effective). It is well-shaped at the macro level — the generic `IReadOnlyEntity<TDefinition>`/`IMutableEntity<TDefinition>` pair, the `VariableValue<T>` virtual-dispatch fold, and the closed-typed `VariableModifier<T>` family are all genuinely good. However, the *identity* of a variable is a `string`+`string` `Variable` record, not a generic type token, which leaks unsafe stringly-typed coupling all the way up to `GetVariable<T>(Variable)`. Combined with reflection-driven assembly scans (`ModifierTypeIndex`, `VariableValueRegistry`), defensive null-coalescing in property getters that *hide* invalid state, and a few pieces of over-decomposition in editor drawers, the package leans about 30 % heavier than a "minimum code" target for what it does.

**Verdict:** **Solid foundation, ship it, but resist adding more before tightening.** Three concrete refactors (typed `Variable<T>`, source-generated `VariableValueRegistry`, fail-fast `Variable.Equals`) would lift the package from "good" to "very good" and unlock the rest of the codebase to drop unsafe casts.

---

## 2. High-level architecture

```text
                 ┌──────────────────────────────────────────────┐
                 │            Authoring (UnityEngine)           │
                 │    VariableSO   EntityDefinitionAsset        │
                 │    EntityModifierEntryAsset                  │
                 └──────────────┬───────────────────────────────┘
                                │ implicit/explicit cast → pure data
                                ▼
   ┌──────────────────────────────────────────────────────────────────┐
   │                      Pure runtime model                          │
   │                                                                  │
   │   Variable (string key + string payloadTypeId)                   │
   │   VariableValue (abstract)                                       │
   │     └─ VariableValue<T> ─ IVariableValue<T>                      │
   │            └─ FloatVariableValue / IntVariableValue / …          │
   │   VariableModifier (abstract; Order)                             │
   │     └─ VariableModifier<T> ─ Apply(T) → T                        │
   │                                                                  │
   │   VariableBag (parent chain, structural events)                  │
   │     ▲                                                            │
   │     │ parent                                                     │
   │   instanceBaseBag (LocalVariableStorage)                         │
   │     ▲                                                            │
   │     │ parent                                                     │
   │   instanceEffectiveBag (modifier fold cache)                     │
   │                                                                  │
   │   VariableModifierHandler  (per-key bucket, ordered insert)      │
   │   VariableNotifier         (per-key Action<VariableValue>)       │
   └─────────────────┬────────────────────────────────────────────────┘
                     │ owned by
                     ▼
   ┌──────────────────────────────────────────────────────────────────┐
   │  EntityInstance<TDefinition> : BaseEntityInstance<TDefinition>   │
   │    IMutableEntity<TDefinition>                                   │
   └─────────────────┬────────────────────────────────────────────────┘
                     │ wrapped by
                     ▼
   ┌──────────────────────────────────────────────────────────────────┐
   │  Hosting (UnityEngine):                                          │
   │  EntityComponent<TDefinition> : MonoBehaviour, IMutableEntity<…> │
   │  EntityBehaviorRunner<TData,TInput>                              │
   └──────────────────────────────────────────────────────────────────┘

   Cross-cutting (reflection, assembly-wide):
     VariableValueRegistry  (id ↔ Type, scans AppDomain)
     ModifierTypeIndex      (value-type ↔ modifier types, scans AppDomain)
     VariablePayloadTypeHelpers (wrapper → inner T via reflection cache)
```

---

## 3. Folder-by-folder verdict

### `Runtime/Core/Contracts/`
Two interfaces (`IReadOnlyEntity<TDefinition>`, `IMutableEntity<TDefinition>`). Tight, generic, MVVM-friendly. `IReadOnlyEntity` is variant (`out TDefinition`), which is correct. **Quality: A.** One nit: `Subscribe`/`Unsubscribe` should not return/accept an `Action<VariableValue>` — the *typed* extension methods unbox after the fact. See §5.

### `Runtime/Core/Definitions/`
`EntityDefinition` (plain `[Serializable]`) and `EntityDefinitionAsset` (`ScriptableObject` wrapper). Clean separation: the SO holds an `EntityDefinition`, mediated by the internal `IDefinitionVariableBagProvider`. Modifier entry/asset pairing follows the same pattern. **Quality: B+.** `EntityModifierEntry.Key` (line 22-37) reaches for legacy fields; the legacy migration path is shipped throughout the package and should be on a removal schedule.

### `Runtime/Core/Hosting/`
`EntityComponent` (empty MonoBehaviour) and `EntityComponent<TDefinition>` (the typed bridge). The empty base exists solely so editor drawers can target a non-generic `MonoBehaviour`. Reasonable. **Quality: A−.** `EntityComponent<TDefinition>` is mostly delegation glue; could be source-generated, but doing so is not worth it at three-method scale.

### `Runtime/Core/Identity/`
- `InstanceId` is a `record` wrapping an `int`. Good fail-fast equality.
- `ModifierId` is a `readonly struct` wrapping a `Guid`. Different shape, different reasoning, fine.
- `IInstanceIdGenerator`/`IncrementingInstanceIdGenerator`: clean. **Quality: A−.**
- Inconsistency: `InstanceId` is a heap-allocated `record class`; `ModifierId` is a struct. If `InstanceId` is created per spawn it's *fine*, but if frame-rate gameplay makes them, switch it to a `readonly struct` (one allocation removed per spawn, sub-microsecond, but it removes the allocation entirely).

### `Runtime/Core/Variables/`
This is the heart of the system. The `VariableValue<T>` virtual `ApplyModifiers` is the cleverest piece in the package — it folds modifiers without boxing or branching by recovering `T` through virtual dispatch. **Strong design.** The `Variable` record itself, however, is **stringly-typed** (`key`, `payloadTypeId`); see §5. Concrete `Values/*VariableValue.cs` files are minimal, parallel, and could be *one* source-generated file or one closed-generic. **Quality: B.**

### `Runtime/Core/VariableBags/`
`VariableBag` does triple duty: serialized list of entries, runtime cache, and parent-chain lookup. The implementation is clean and the structural-events + silent-write split is well-considered. `IVariableBag` is read-only as intended. **Quality: A−.** Caveat: `Add` (line 70) and `SetLocalSilent` (line 98) silently no-op on `null` — that's a default-value-hides-error pattern (§5).

### `Runtime/Core/Instance/`
`LocalVariableStorage` is the workhorse and is well-decomposed. `BaseEntityInstance<TDefinition>` enforces the read shape. `EntityInstance<TDefinition>` is the standalone serializable/runtime pairing. `VariableModifierHandler` is encapsulated. **Quality: A−.** `BaseEntityInstance.GetVariable<T>` (line 42-52) does a runtime cast that would be unnecessary if `Variable` carried `T` at compile time.

### `Runtime/Core/Modifiers/`
Six concrete modifier types, each ~25 lines, all parallel, all `[Serializable]` + `[SerializeField]`. The shape is right; the count is high relative to the work each does — strong candidate for a generic operator helper or a source generator if more inner types are added. **Quality: B.**

### `Runtime/Core/Subscriptions/`
`VariableNotifier`, `EmptyDisposable`, `CallbackDisposable`. ~25 lines each. Idiomatic. **Quality: A.**

### `Runtime/Core/Utilities/`
The most reflection-heavy folder. `VariableValueRegistry` and `ModifierTypeIndex` walk every loaded assembly on first use. Cached, locked, defensible — but exactly the place where a Roslyn source generator would deliver compile-time safety with zero allocation. `EntityExtensions` does primitive→modifier mapping by `switch` on boxed `T`, which is a small smell. **Quality: B−.**

### `Runtime/Behavior/`
`IEntityBehavior<TData,TInput>` + `EntityBehaviorRunner<TData,TInput>` + `IEntityFrameInputProvider<TInput>`. Minimal, generic, idiomatic. **Quality: A.** Note that this folder is intentionally Unity-coupled (per AGENTS.MD).

### `Editor/`
Five drawers and one editor. `EntityModifierEntryDrawer` and `VariablePropertyDrawer` are over-decomposed (8–10 private methods each, every method 5–10 lines). They work but read like generated code. **Quality: B−.** `VariableSOEditor.BuildTypeCachesIfNeeded` re-uses the registry filter — needless. `VariableKeySoField` is the right shape (shared helper).

### `Tests/`
Good coverage of `EntityInstance<T>` semantics (modifier ordering, structural events, subscribe lifecycles). Editor tests are thin (`*PropertyDrawer_Type_IsRegistered` is a tautology). Missing: round-trip serialization tests, stress tests on `VariableBag` parent chains, no test that `VariableValueRegistry` survives domain reload deterministically. **Quality: B.**

### `Samples/`
Conventional, runs the system end-to-end. Useful as smoke-tests. **Quality: A.**

---

## 4. What's good (preserve)

1. **`VariableValue<T>.ApplyModifiers` (`VariableValueT.cs:9-18`)** — folding modifiers via virtual dispatch is the right call. No boxing, no `is`/`as` chains, no branching on payload type. Delete this and you'll regret it.
2. **Generic `IReadOnlyEntity<out TDefinition>` / `IMutableEntity<TDefinition>` (`Contracts/`)** — variance is correct, the split between mutable/read-only is correct. Use these as the public surface.
3. **Three-bag chain with `SetParent`** — definition → instance-base → instance-effective is conceptually clean, lets editor inspectors render any layer, and handles modifier caching cheaply. Don't replace with a flat dictionary.
4. **`[VariableValueId("…")]` + per-type `[Serializable]`** — `SerializeReference` payload survives type renames as long as the id is stable. Good migration story.
5. **`ModifierId`/`InstanceId` separation** — different lifetimes, different shapes. Resist unifying them.
6. **`EmptyDisposable.Instance` singleton** — zero-allocation empty subscription. Small thing, right thing.
7. **`AssemblyInfo.cs` `InternalsVisibleTo` to Tests/Editor/States** — no public surface bloat for test hooks.
8. **`EntityBehaviorRunner<TData,TInput>` taking `in TInput`** — readonly-ref input passing in the hot path. Correct.
9. **Definition asset is a *wrapper* around a plain `EntityDefinition`** — the `ScriptableObject` is a thin authoring shell, the data class is reusable from non-asset paths. Mirror this pattern elsewhere.
10. **`VariableModifierHandler.ComputeInsertIndex` (`VariableModifierHandler.cs:97-106`)** — stable insertion-order tie-breaking on equal `Order` is the right semantic and matches the docs.

---

## 5. Issues & smells (concrete citations)

### 5.1 Stringly-typed `Variable` defeats the generic API

`Runtime/Core/Variables/Variable.cs:8-28`

```csharp
public sealed class Variable : IEquatable<Variable>
{
    public Variable(string key, string payloadTypeId = "string") { ... }
    public string PayloadTypeId => payloadTypeId ?? "string";
}
```

Every read in the package goes through `IReadOnlyEntity.GetVariable<T>(Variable key)`. The `Variable` itself is a `(string,string)` pair. The compiler can't enforce that `T` matches `payloadTypeId`; the runtime throws `InvalidCastException` (`BaseEntityInstance.cs:50`). This is the single biggest deviation from the architect's "compile-time safety" rubric.

**Fix:** introduce `Variable<T>` (a typed wrapper around the existing untyped `Variable`) and overload `GetVariable<T>(Variable<T>)`, `AddVariable<T>(Variable<T>, T)`, `Subscribe<T>(Variable<T>, ...)`. Keep the untyped `Variable` for serialization. The `VariableSO` already knows its `PayloadTypeId`; it can produce `Variable<T>` via a generic accessor (`VariableSO<T> : VariableSO`).

### 5.2 Default values masking errors — null-coalescing in getters

`Runtime/Core/Variables/Variable.cs:20-22`

```csharp
public string Key => key ?? "";
public string PayloadTypeId => payloadTypeId ?? "string";
```

If serialization fails or a `Variable` is wrong, this *silently returns* an empty key and the literal payload id `"string"`. Identical pattern in `VariableEntry.Key` (`VariableEntry.cs:25-36`) and `EntityModifierEntry.Key` (`EntityModifierEntry.cs:24-37`). **Fail-fast violation.** A null `payloadTypeId` is a programmer error and should `throw`. Today this just ships an entity that *appears* to have a `string` payload and then fails on first read with a misleading message.

### 5.3 Redundant guard clauses (entry-only rule)

The package guards twice. Examples:

- `EntityComponent<TDefinition>.Subscribe` (`EntityComponentT.cs:52-55`) → `Instance.Subscribe` → `BaseEntityInstance.Subscribe` (`BaseEntityInstance.cs:76-84`) which checks `key == null || onChange == null` → `LocalVariableStorage.Subscribe` (`LocalVariableStorage.cs:70-85`) **also** checks `key == null || onChange == null`.
- `LocalVariableStorage.AddVariable` (`LocalVariableStorage.cs:171-180`) → `VariableBag.Add` (`VariableBag.cs:70-85`) **both** check non-null.
- `VariableModifierHandler.RemoveModifier` (`VariableModifierHandler.cs:33-46`) checks `key == null || id.Id == default` after public callers already guarded.
- `BaseEntityInstance.Storage` is `default!` until `Initialize`; every public method then re-guards via `TryResolve` even though `Initialize` is mandatory.

The architect's rule is "entry point only." Move all these checks to the *outermost* public surface (`EntityComponent<T>` and `EntityInstance<T>` public methods); make every internal method assume valid input. That alone removes ~40 lines.

### 5.4 Reflection where source generation belongs

- `Runtime/Core/Utilities/VariableValueRegistry.cs:76-83` walks `AppDomain.CurrentDomain.GetAssemblies()`, calls `GetTypes()` on each, and reads `[VariableValueId]`.
- `Runtime/Core/Utilities/ModifierTypeIndex.cs:79-90` does the same dance for `VariableModifier` subclasses.
- `Runtime/Core/Utilities/VariablePayloadTypeHelpers.cs:53-72` reflects `IVariableValue<T>`.

This is the textbook source-generator scenario. A `Generators/` Roslyn generator could emit a single `VariableValueRegistry.Generated.cs` with a static initializer, eliminating:
- AppDomain assembly scans (cold-path cost on first access),
- the `lock`/double-check pattern,
- the `ReflectionTypeLoadException` recovery code,
- duplicate-id editor-vs-runtime divergence at `VariableValueRegistry.cs:166-171`.

The architect's preferences explicitly call out: "Source generators under `Generators/`."

### 5.5 Boxing/allocation in `EntityExtensions`

`Runtime/Core/Utilities/EntityExtensions.cs:49-61`

```csharp
private static VariableModifier CreateModifierFromPrimitive<T>(T value)
{
    object boxed = value!;
    return boxed switch { float f => new FloatAddModifier(f), ... };
}
```

Every call boxes `T` to dispatch. The same pattern in `VariableValueFactory.From<T>` (`VariableValueFactory.cs:30-41`). This is a hot-path-adjacent helper (used by sample scenes and tests). Replace with a tiny generic strategy table:

```csharp
private static class ModifierFactory<T> { public static Func<T, VariableModifier> Make = ...; }
```

initialized once, looked up by closed generic. No box.

### 5.6 Over-abstraction (interfaces with one implementation)

- `IInstanceIdGenerator` (`IInstanceIdGenerator.cs`) — only `IncrementingInstanceIdGenerator` implements it. Acceptable for tests, but no other concrete impl ships and DI is VContainer (which can mock the concrete with `RegisterInstance`). Worth keeping only if multiplayer/server-id is on the roadmap.
- `IDefinitionVariableBagProvider` (`IDefinitionVariableBagProvider.cs`) — implemented by `EntityDefinition` and `EntityDefinitionAsset` only, and only used internally to call `Bag` and `RebuildLookup`. Could be replaced with a shared base class or an abstract `EntityDefinitionBase`; right now both classes carry the same `IDefinitionVariableBagProvider.Bag => …` boilerplate.
- `IEntityVariableStorage` (`IEntityVariableStorage.cs`) — single impl `LocalVariableStorage`. Borderline; *might* be earned later if pooled storage or networked storage is added. Mark with a `// extension point: networked storage planned` comment or delete.
- `IVariableValue<T>` is *not* over-abstraction — it's the variance lever for the dispatch in `BaseEntityInstance`. Keep.

### 5.7 Unity boundary leaks

The package is allowed `UnityEngine` (per AGENTS.MD), but the architect prefers data/identity to be Unity-free *where possible*. Citations:

- `Variable.cs:3` (`using UnityEngine;`) — `Variable` is pure data + identity; the only UnityEngine surface it uses is `[SerializeField]`. Either wrap the serialization in a `Variable.Serialized` adapter or accept the dependency. Today `Variable` cannot be reused from a pure-C# assembly even though it's conceptually a `(string,string)` value object.
- `BaseEntityInstance.cs:4` (`using UnityEngine;`) only for `[SerializeField]` on `id` and `definition` — same comment.
- `VariableValue.cs:3` (`using UnityEngine.Scripting;` for `[Preserve]`) — defensible (IL2CPP), but document why.

### 5.8 Allocation in subscribe & structural callbacks

`Runtime/Core/Instance/LocalVariableStorage.cs:84` allocates a `CallbackDisposable` (and a closure) per `Subscribe`. Per-frame subscribe storms (e.g. binding many UI rows at once) measurable. Pool `CallbackDisposable` or use a small struct-disposable + handle pattern. Same at `LocalVariableStorage.cs:104-110` (the `Structural` local-function adds a closure allocation even though it just forwards).

### 5.9 `VariableNotifier` event fanout via multicast delegate

`Runtime/Core/Subscriptions/VariableNotifier.cs:18-25` stores subscribers as `Action<VariableValue> existing + onChange`. Adding/removing on a dictionary value re-allocates the multicast delegate every time. For UI-heavy bindings this is hot. Replace with `Dictionary<Variable, List<Action<VariableValue>>>`; iterate; use a tombstone slot for safe removal during dispatch. Cost: ~30 lines, removes most subscribe-time allocation.

### 5.10 Editor drawers over-decomposed

`Editor/EntityModifierEntryDrawer.cs` and `Editor/VariablePropertyDrawer.cs` each split a single `OnGUI` into 8–10 private helpers, each 4–8 lines. The intent (analyzer-friendly cyclomatic complexity) is fine, but the result is harder to read than one focused 60-line method. Compress: keep `BuildCompatibleModifierTypes`, `DrawModifierTypePopup`, and the height calculator separate; collapse the rest into `OnGUI`.

### 5.11 `EntityComponent<TDefinition>` is delegation noise

`Runtime/Core/Hosting/EntityComponentT.cs:27-75` — every method is `Instance.X(...)`. 50 lines of glue. A source generator (or `using static` + a `[GeneratedDelegate]` attribute) eliminates it. Until then, accept it; do not write a *second* one for any other type.

### 5.12 `VariableValueRegistry` `HandleDuplicateId` (`VariableValueRegistry.cs:158-172`) — silent in non-editor

```csharp
#if UNITY_EDITOR
    throw new InvalidOperationException(...);
#else
    Debug.LogError(... "Keeping '{existing.FullName}'.");
#endif
```

This is **default-value-masks-error** in player builds. A duplicate `[VariableValueId]` is a programmer bug, not a recoverable runtime condition. Throw in player too; let the crash log find it.

### 5.13 `LocalVariableStorage.Variables` allocates a `HashSet<Variable>` per call (`LocalVariableStorage.cs:21-51`)

If callers iterate it once it's fine; if anything binds it, redo. Cache and invalidate on structural changes, or document that callers must materialize once.

### 5.14 `VariableModifierHandler.AddModifier` returns `default(ModifierId)` on bad input (`VariableModifierHandler.cs:14-17`)

`default` is a valid-looking `Guid.Empty`. Callers (e.g. `LocalVariableStorage.AddModifier`, `LocalVariableStorage.cs:140-145`) then re-check `id.Id == default`. Throw on null entry; don't fabricate a sentinel.

### 5.15 `EntityModifierEntry.RebaseSerializedModifierPayloadIfMismatch` does runtime type-juggling

`Runtime/Core/Definitions/EntityModifierEntry.cs:51-111` — picks "the first compatible candidate" (`candidates[0]`) and instantiates by reflection. This is acceptable as an *editor-only* fixup, but it's compiled into the runtime assembly and reachable in player builds. Move to `EntityModifierEntry.Editor.cs` (gated `#if UNITY_EDITOR`).

### 5.16 Authoring legacy paths still shipping

Three places still carry `variableLegacy` / `[FormerlySerializedAs("variable")]` migration code:
- `VariableEntry.cs:30-35`
- `EntityModifierEntry.cs:31-37`
- `EntityBehaviorRunner.cs:13-15`

Schedule removal. Add an analyzer or one-shot migration script and delete the legacy fields next milestone. Carrying them indefinitely leaks editor surface and bloats every drawer.

---

## 6. Suggested before/after snippets

### A. Typed `Variable<T>` (compile-time safety)

```csharp
// BEFORE — Variable.cs / IReadOnlyEntity.cs
public sealed class Variable : IEquatable<Variable>
{
    public string Key { get; }
    public string PayloadTypeId { get; }
}
T GetVariable<T>(Variable key);   // T is decoupled from key.

// caller
float hp = entity.GetVariable<float>(hpKey); // wrong T → InvalidCastException at runtime
```

```csharp
// AFTER
[Serializable]
public sealed class Variable : IEquatable<Variable> { /* as today, untyped on disk */ }

public readonly struct Variable<T>
{
    public readonly Variable Untyped;
    public Variable(Variable untyped) => Untyped = untyped;
    public static implicit operator Variable(Variable<T> v) => v.Untyped;
}

public interface IReadOnlyEntity<out TDefinition> ...
{
    T GetVariable<T>(Variable<T> key);                      // typed
    bool TryGetVariable<T>(Variable<T> key, out T value);
    IDisposable Subscribe<T>(Variable<T> key, Action<T> cb);
}

// VariableSO emits the typed form
public abstract class VariableSO<T> : VariableSO
{
    public new Variable<T> AsVariable() => new((Variable)this);
    public static implicit operator Variable<T>(VariableSO<T> so) => so.AsVariable();
}

// caller
float hp = entity.GetVariable(hpKey);   // T inferred. Wrong T = compile error.
```

The untyped path remains for serialization and editor drawers.

### B. Source-generated `VariableValueRegistry`

```csharp
// BEFORE — VariableValueRegistry.cs (76-83)
foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
    CollectFromAssembly(assembly, orderedConcrete);
```

```csharp
// AFTER — generated (Generators/Scaffold.Entities.Generators)
// VariableValueRegistry.Generated.cs
internal static partial class VariableValueRegistry
{
    static partial void RegisterAll(Dictionary<string, Type> idToType,
                                    Dictionary<Type, string> typeToId)
    {
        Register<FloatVariableValue>("float", idToType, typeToId);
        Register<IntVariableValue>("int", idToType, typeToId);
        Register<BoolVariableValue>("bool", idToType, typeToId);
        Register<StringVariableValue>("string", idToType, typeToId);
        // user types appended here
    }
}
```

No reflection, no AppDomain walk, no `lock`, no `ReflectionTypeLoadException`. Compile-time duplicate-id detection.

### C. Fail-fast `Variable` getters

```csharp
// BEFORE
public string Key => key ?? "";
public string PayloadTypeId => payloadTypeId ?? "string";
```

```csharp
// AFTER
public string Key => key ??
    throw new InvalidOperationException("Variable.key was not deserialized.");
public string PayloadTypeId => payloadTypeId ??
    throw new InvalidOperationException("Variable.payloadTypeId was not deserialized.");
```

Plus `OnAfterDeserialize` validation in `VariableEntry`.

### D. Collapse internal guard chain

```csharp
// BEFORE — three layers each guard key/onChange
EntityComponent<T>.Subscribe → Instance.Subscribe → Storage.Subscribe (all guard)
```

```csharp
// AFTER — entry-only
public IDisposable Subscribe(Variable key, Action<VariableValue> onChange)
{
    if (key is null) throw new ArgumentNullException(nameof(key));
    if (onChange is null) throw new ArgumentNullException(nameof(onChange));
    return instance.SubscribeInternal(key, onChange);
}

// Internal methods take non-null parameters by contract.
internal IDisposable SubscribeInternal(Variable key, Action<VariableValue> onChange)
    => storage.SubscribeInternal(key, onChange); // no re-guard
```

---

## 7. Easy wins (each <30 min)

1. **Remove redundant null guards** in `BaseEntityInstance.Subscribe/Unsubscribe/SubscribeToVariableStructuralChanges` (lines 76-104) and the corresponding `LocalVariableStorage` methods. Move to `EntityComponent<T>` + `EntityInstance<T>` only. (~25 lines deleted).
2. **Throw on duplicate `[VariableValueId]` in player too** — delete the `#if UNITY_EDITOR` split at `VariableValueRegistry.cs:165-171`.
3. **Move `EntityModifierEntry.RebaseSerializedModifierPayloadIfMismatch` (and helpers) into `EntityModifierEntry.Editor.cs`** — it's editor-only; no need to compile in player.
4. **Delete `IInstanceIdGenerator`** if VContainer-mocking the concrete is acceptable. If not, document the planned second impl. Reduces public surface by one interface.
5. **Compress `EntityModifierEntryDrawer` + `VariablePropertyDrawer`** — each can shed 4-5 thin private methods without losing readability.
6. **Replace `VariableNotifier` multicast-delegate `existing + onChange`** with `List<Action<VariableValue>>`. ~30 lines.
7. **Convert `InstanceId` from `record class` to `readonly struct`** if you don't need `with`. Saves a heap allocation per spawn. Equality and `ToString` are identical to write.
8. **Audit `Variable.cs` for `using UnityEngine`** — wrap `[SerializeField]` in a partial under `#if UNITY` or move the serialized fields to a `[Serializable]` adapter so the type can be referenced from a Unity-free assembly.
9. **Delete the `legacyVariable` field** in `VariableEntry` and `EntityModifierEntry` once all assets have been re-saved (one-time migration script run, then drop).
10. **Add `FormatVariableText` helper to the package** — `EntitiesSampleDriver.cs:94-104` reimplements payload formatting that should be a `VariableValue.ToString` override on each concrete type. Move it into `Values/*VariableValue.cs`.

---

## 8. Bigger refactors (day+)

### 8.1 Introduce `Variable<T>` typed key (1–2 days)
Rationale: removes the single largest unsafe-cast surface in the package and lets every consumer drop runtime `<T>` annotations. Strategy: keep untyped `Variable` for serialization; layer `Variable<T>` (struct) on top; new generic overloads on `IReadOnlyEntity`/`IMutableEntity`; `VariableSO<T> : VariableSO` for typed authoring; deprecate untyped `GetVariable<T>(Variable)` over a release. Also enables compile-time-checked `Subscribe<T>` instead of the cast-in-callback `EntityExtensions.DispatchSubscribePrimitive`.

### 8.2 Source-generated `VariableValueRegistry` and `ModifierTypeIndex` (1 day)
Rationale: eliminates ~250 lines of reflection, AppDomain scanning, and `lock`-based double-check init. Compile-time duplicate-id detection becomes possible. New analyzer-friendly. Aligns with the project rule "Source generators under `Generators/`." Keep the public API identical.

### 8.3 Replace `VariableValue` family with closed-generic + provided ops (1 day)
Rationale: today there is one concrete `*VariableValue` per inner `T`, each ~35 lines, all parallel. Pattern is begging for a generic:

```csharp
[Serializable]
public sealed class VariableValueOf<T> : VariableValue<T> { [SerializeField] T value; ... }
```

Modifiers similarly: `AddModifier<T>` and `MultiplyModifier<T>` parameterized by `INumber<T>` (C# 11) or by a passed-in `IOperator<T>`. Twelve files become four. Caveat: Unity's serializer cannot serialize open generics; the workaround is a closed concrete sealed type per `T` that inherits the generic — same line count as today, but the *operator logic* is shared. Pick this up when you add the next inner type, not before.

### 8.4 Pool subscriptions / migrate to a struct handle (~half day)
Rationale: `CallbackDisposable` + closure per `Subscribe` is the dominant allocation for any UI-binding-heavy frame. Replace with a `SubscriptionHandle` struct and a per-storage free-list. Roughly halves managed allocations under typical inspector-binding load.

### 8.5 Deprecate `IDefinitionVariableBagProvider` (~half day)
Rationale: it abstracts two intentionally-coupled types. Replace with a sealed `EntityDefinition` shared by the asset and the plain class via composition (today) but mark the interface `[Obsolete]` and remove. Net deletion.

### 8.6 Compare to peers (web research)

Looked at the public design notes for:

- **Unity ECS / DOTS authoring–baking** ([docs.unity3d.com](https://docs.unity3d.com/Packages/com.unity.entities@latest)) — DOTS uses authoring `MonoBehaviour`s baked into pure-data components, very similar in spirit to `EntityDefinitionAsset → EntityDefinition`. Difference: DOTS components are generic value types resolved at compile time; this package goes through a string `payloadTypeId`. The `Variable<T>` refactor in §8.1 is exactly the DOTS-style move.
- **Entitas** ([github.com/sschmid/Entitas](https://github.com/sschmid/Entitas)) — uses a code generator to produce typed component accessors. Same pattern as §8.2. Their generator emits `Entity.HasFoo()`, `Entity.AddFoo(...)`, etc.; equivalent here would be `Entity.GetVariable<Health>()` typed by closed generic.
- **Apple GameplayKit `GKAttribute` / `GKEntity`** — similar three-layer pattern (definition / instance / modifier). They use `NSString` keys (the same stringly-typed weakness this package has).

The tightest peer in C#-land is Entitas; its source-generator approach validates §8.2.

---

## 9. Organization & docs

**Naming.** Mostly idiomatic. Two oddities:

- `BaseEntityInstance` (`Instance/BaseEntityInstance.cs`) — the `Base` prefix is C++-flavored; prefer `EntityInstanceBase` or, better, drop the base and put the read-only API on an internal sealed `EntityInstanceCore<TDefinition>` reachable from `EntityInstance<T>` via composition.
- `VariableSOEditor` is named correctly but `VariableSO` itself should be `VariableAsset` (consistent with `EntityDefinitionAsset` / `EntityModifierEntryAsset`). The `*SO` suffix is informal.
- `IEntityBehavior<TData,TInput>` first generic is named `TData` but the parameter is constrained to `EntityComponent` — call it `TEntity`.

**Folder structure.** Reasonable. One nit: `Runtime/Core/Instance/` and `Runtime/Core/Hosting/` are conceptually adjacent (Instance = pure model, Hosting = MonoBehaviour wrapping the model). Today `EntityInstance.Editor.cs` lives in `Instance/` while `EntityComponentT.Editor.cs` lives in `Hosting/`. Fine. The `LocalVariableStorage.Editor.cs` partial split is *over-applied*; the editor partial only adds 25 lines and could collapse into `LocalVariableStorage.cs` under a `#if UNITY_EDITOR` region.

**Docs.** `README.md` is excellent — better than most modules in this repo. It documents the three-bag chain, the `[VariableValueId]` migration story, and the modifier ordering. Ship the same level for the next module. Missing: an example of a *custom* `VariableValue<T>` type from end to end (registration → authoring asset → consumption). Add 30 lines.

**Tests.**
- Add a serialization round-trip test (`EntityDefinition` → JSON via `EditorJsonUtility` → back).
- Add a stress test for `VariableBag` deep parent chains (≥5 levels) to lock in lookup performance.
- Replace `VariablePropertyDrawer_Type_IsRegistered` tautologies (`Tests/Editor/VariablePropertyDrawerEditorTests.cs:9-30`) with actual drawer-rendered-into-headless-window assertions, or delete them.
- Add a regression test for §5.14 (modifier id sentinel) once the throw-on-bad-input fix lands.

**Sample.** `EntitiesSampleDriver.cs` still has `OnGUI` for HUD; for a 2026 sample, prefer UI Toolkit. Low priority.

---

## Closing scorecard

| Dimension                          | Grade | Notes |
|-----------------------------------|-------|-------|
| Generic / compile-time safety     | C+    | `Variable` stringly-typed; reads are runtime-cast. Largest gap. |
| Abstraction discipline            | B     | Two interfaces are 1-impl. The rest are earned. |
| Guard-clause hygiene              | C+    | Re-guarding three layers deep in subscribe/add paths. |
| Fail-fast posture                 | C     | Multiple `?? ""`, sentinel `default(ModifierId)`, silent dup-id in player. |
| Unity boundary discipline         | B+    | Sample/Editor cleanly separated; `Variable` itself leaks `UnityEngine` for `[SerializeField]`. |
| Allocation discipline             | B−    | Subscribe path allocates a closure + disposable. Multicast delegate churn. |
| Reflection vs source-gen          | C+    | Two assembly scans. Strong source-gen candidate. |
| Tests                             | B     | Good runtime coverage, weak editor + serialization coverage. |
| Docs                              | A−    | README is exemplary. |
| **Overall**                       | **B+** | Foundationally sound. The §8.1/§8.2/§8.3 refactors take it to A. |

---

## 10. Consumers

Searched for `Scaffold.Entities` across `Assets/`, `GameModule/`, `LiveOps/` (excluding the package itself). **Finding #1: there are essentially no first-party consumers outside the package and `entities.states`.** GameModule and LiveOps return zero matches for `Scaffold.Entities`, `EntityComponent`, `EntityInstance`, `GetVariable`, `VariableSO`, or `EntityDefinition`. The directories `Assets/Scaffold/Packages/com.scaffold.entities/` and `Assets/Packages/com.scaftold.entities/` (note typo) contain only `.meta` files — empty husks; recommend deletion as part of cleanup. **All real consumption is concentrated in two places: the in-package `Samples/` and `com.scaffold.entities.states`.** This dramatically lowers the cost of the §8.1 typed `Variable<T>` migration: the blast radius is tiny.

### 10.1 `com.scaffold.entities.states` — the dominant consumer

`entities.states` *reimplements* large parts of `LocalVariableStorage` against the `Store` slice model rather than reusing it. This is exactly the chain the prompt asked about: the typing pain shows up here.

- **Reinvented subscriber map (`StoreVariableStorage.cs:37-38, 67-89, 250-276`).** It reproduces the `Dictionary<Variable, List<Action<VariableValue>>>` that §5.9 of this audit recommends for the package itself, plus a separate structural-change list. The package's own `VariableNotifier` was unsuitable to reuse, evidently because of the multicast-delegate churn flagged in §5.9. The consumer found the same smell and worked around it independently — strong evidence the §5.9 fix is correct.

  ```csharp
  // StoreVariableStorage.cs:37-38
  private readonly Dictionary<Variable, List<Action<VariableValue>>> perVariableSubscribers = new();
  private readonly List<Action<VariableStructuralChange, Variable, VariableValue?>> structuralSubscribers = new();
  ```

- **Hand-rolled typed-payload equality chain (`StoreVariableStorage.cs:382-420`).** `MatchKnownPayloadEquals` enumerates every concrete inner type (`float`, `int`, `double`, `long`, `bool`, `string`) with a six-method ladder. Every new `VariableValue<T>` requires editing this consumer too. This is the §5.1 stringly-typed problem expressed as a maintenance tax on a downstream package — the consumer cannot ask `prev.Equals(cur)` typedly because `Variable` does not carry `T`. A `Variable<T>`-aware `IEqualityComparer<VariableValue<T>>` would collapse this to one generic call.

  ```csharp
  // StoreVariableStorage.cs:392-395
  private bool MatchFloatEquals(VariableValue prev, VariableValue cur)
      => prev is IVariableValue<float> pf && cur is IVariableValue<float> cf && pf.Get().Equals(cf.Get());
  ```

- **Stringly-typed `Variable` literals at the call site (`StateEntityIntegrationTests.cs:13, 111, 300`).**

  ```csharp
  private static readonly Variable hp = new("hp", "float");
  var armor = new Variable("armor", "float");
  ```

  Tests bypass `VariableSO` and hand-build `Variable` from raw strings; if the test typo'd `"flaot"` the failure surfaces only at `BaseEntityInstance.cs:50` as `InvalidCastException`. **`Variable<T>` would make this `Variable<float>("hp")` and the typo a compile error.** Note `entities.states` ships no typed authoring layer of its own.

- **`GetVariable<T>` callers always pre-know `T` and pass it as an explicit type argument (`StateEntityIntegrationTests.cs:22, 29, 38, 48, 57-59, 94, 102-104, …40+ call sites`).**

  ```csharp
  Assert.That(entity.GetVariable<float>(hp), Is.EqualTo(15f));
  ```

  Every single call site duplicates the `<float>` annotation that should be inferable from `hp`. With `Variable<T>` this becomes `entity.GetVariable(hp)` and the redundancy disappears. The 40+ duplications are a direct consequence of §5.1.

- **`Subscribe` callbacks unbox via cast inside the lambda (`StateEntityIntegrationTests.cs:123, 288`).**

  ```csharp
  entity.Subscribe(hp, value => captured.Add(((IVariableValue<float>)value).Get()));
  ```

  Every subscriber casts. With `Subscribe<T>(Variable<T>, Action<T>)` (proposed in §5.1) this is `entity.Subscribe(hp, v => captured.Add(v))`. The cast is a downstream tax for the package's untyped `Subscribe(Variable, Action<VariableValue>)` signature. `IReadOnlyEntity.Subscribe` (`Contracts/IReadOnlyEntity.cs`) is already flagged in §3 — the consumer evidence confirms it.

- **Mutators serialize `Variable` straight into payloads (`Payloads/AddModifierPayload.cs:5`, `RemoveModifierPayload`, `SetBaseValuePayload`, `AddEntityVariablePayload`, `RemoveEntityVariablePayload`).** Each payload record carries a raw `Variable` and a raw `VariableValue` / `VariableModifier`. **None carry a `T`.** This means the entire Store mutation log is stringly-typed end-to-end; a wrong-typed `VariableValue` in `SetBaseValuePayload` is undetectable until the next `ApplyModifiers` runs. Strong argument for §8.1 plus a `SetBaseValuePayload<T>(Variable<T>, VariableValue<T>)` overload.

- **`StoreVariableStorage` re-guards the same `key == null || onChange == null` (lines 67-72, 99-106, 118-125, 137-142) that §5.3 already calls out for the package.** Same pattern, copied. Confirms the entry-only rule is not actually being followed at the *consumer* boundary either — the rule needs a public-vs-internal split (`SubscribeInternal` etc.) that consumers can adopt.

- **Definition consumers.** `EntityDefinition` (plain class) is instantiated directly in `EntityStateFactory.Create` and in tests (`StateEntityIntegrationTests.cs:74-76`); the `EntityDefinitionAsset` ScriptableObject path is **never used** outside the in-package sample. `IDefinitionVariableBagProvider` (flagged in §5.6 as 2-impl) has no third-party impl in the consumer code. Recommend deleting the interface as proposed in §8.5.

- **Modifier registration sites.** Custom modifiers in consumers: **zero.** All six modifier types ship from the package; `entities.states` adds none, samples add none, and there are no `: VariableModifier<T>` subclasses elsewhere. The reflection-based `ModifierTypeIndex` (§5.4) walks AppDomain to find a closed set of six types that will not grow this milestone — pure cold-start tax for nothing. This sharpens the §8.2 source-generator argument.

- **Entity hosting.** Two host shapes ship: `EntityComponent<TDefinition>` (MonoBehaviour) and `StateEntity<TDefinition>` (state-store-backed, `BaseEntityInstance<TDefinition>` subclass). Sample uses `EntityComponent`; tests for `entities.states` use `StateEntity`. **Game code uses neither yet.** The `BaseEntityInstance` extensibility surface earns its keep solely because of `StateEntity`; without that one consumer the class hierarchy could collapse.

### 10.2 In-package samples

- **`Samples/Scripts/EntitiesSampleDriver.cs:56-65`** — `entity.TryGetVariable(healthVariable, out VariableValue health)` then `FormatVariableText` switches on the concrete subclass. **Defensive null-checks downstream of nullable returns** show up as `slot != null && entity.TryGetVariable(...)` (`EntitiesSampleDriver.cs:88`) — exactly the pattern §5.2 predicts when `Variable` getters silently swallow null. The driver also reimplements `ToString` per concrete `VariableValue<T>` (lines 96-103), which §7.10 already calls out.

- **`Samples/Scripts/SampleCharacterMoveBehavior.cs:22-26`** — `data.TryGetVariable(moveSpeedVariable, out FloatVariableValue floatSpeed)` then `floatSpeed.Value`. Pattern matching against the concrete *wrapper* (not `T`). With `Variable<float>` this would become `data.TryGetVariable(moveSpeedVariable, out float speed)`; one type-test removed per behavior.

- **`Samples/Scripts/SampleCharacterEntity.cs`** is two lines: `sealed class SampleCharacterEntity : EntityComponent<SampleCharacterDefinition> {}`. Confirms §5.11: every consumer gets a one-line trampoline class because `EntityComponent<T>` cannot be added to a GameObject directly.

### 10.3 Chain of pain

`entities` → `entities.states` → (no further consumers yet). The pain is concentrated in `entities.states` and visible in three forms: redundant `<T>` arguments on every read, hand-rolled per-type equality, and stringly-built `Variable` literals in tests. **Fixing §5.1 (`Variable<T>`) and §5.9 (`VariableNotifier` rewrite) in the package would let `entities.states` drop ~120 lines** (the equality chain, the duplicated subscriber book-keeping where applicable, and the `<float>` clutter in tests). That is a much higher leverage refactor than the audit body alone implied — the consumer evidence makes §8.1 the clear top priority.

---

## 11. Alternatives & prior art

Six libraries / patterns that solve "entity-with-typed-variable-bag" or "ScriptableObject-driven definitions." Verdict legend: **Adopt** = use it directly; **Wrap** = thin facade over it; **Build** = roll our own informed by it; **Steal pattern** = copy the design idea, not the code.

1. **Unity DOTS / ECS** — typed `IComponentData` in archetypes; change-filter queries; unmanaged via Burst. [docs.unity3d.com/Packages/com.unity.entities](https://docs.unity3d.com/Packages/com.unity.entities@latest). **Verdict: Steal pattern.** DOTS proves typed-component-by-archetype scales; the authoring-baking split is exactly what `EntityDefinitionAsset → EntityDefinition` already does. We will not adopt DOTS proper (sample-scene + UI binding model is too far from chunk iteration), but `Variable<T>` is the small-scale equivalent of DOTS' compile-time component identity.

2. **Entitas** — codegen ECS for Unity; generator emits `Entity.HasFoo()`/`AddFoo()` per component. [github.com/sschmid/Entitas](https://github.com/sschmid/Entitas). **Verdict: Steal pattern.** The validating peer for §8.2: a Roslyn generator over `[VariableValueId]` produces typed accessors and a static registry, replacing both `VariableValueRegistry` AppDomain scan and the per-call cast.

3. **Unreal GAS — `FGameplayAttribute` / `UAttributeSet`** — typed attribute identifier struct; `GAMEPLAYATTRIBUTE_PROPERTY_GETTER` macro emits a static accessor per attribute; `FGameplayEffect` is the modifier analogue. [dev.epicgames.com/documentation/en-us/unreal-engine/API/Plugins/GameplayAbilities/FGameplayAttribute](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Plugins/GameplayAbilities/FGameplayAttribute). **Verdict: Steal pattern.** GAS already solved the "typed identifier + base/current/effective" three-layer problem; `FGameplayAttribute` is morally `Variable<float>` plus a property-field reflection token. Borrow the macro idea (we have source generators instead) and the base/current value split it pairs with.

4. **MemoryPack** — Cysharp serializer using an incremental source generator + `ModuleInitializer` to register formatters with zero reflection at startup; AOT-friendly. [github.com/Cysharp/MemoryPack](https://github.com/Cysharp/MemoryPack). **Verdict: Steal pattern.** The reflection-replacement template for §8.2: emit a generated registration call from `[ModuleInitializer]`, fall back to a manual `RegisterFormatter()` on Unity (Unity does not honour `[ModuleInitializer]` reliably) — same compromise we will need for `VariableValueRegistry.Generated.cs`.

5. **Apple GameplayKit — `GKEntity` / `GKComponent` / `GKAttribute`** — three-layer entity-component-attribute with string keys and `NSObject`-typed values. [developer.apple.com/documentation/gameplaykit](https://developer.apple.com/documentation/gameplaykit). **Verdict: Build (informed-by, anti-pattern caution).** GameplayKit has the same stringly-typed weakness this package has — citing it as confirmation that string keys are a known sharp edge that several mature systems failed to remove. Do *not* mirror its API surface; do mirror its three-layer entity/component/attribute split, which we already have.

6. **DDD `TypedId<T>` / strongly-typed wrappers (Vladimir Khorikov, Andrew Lock)** — pattern: `readonly struct OrderId(Guid value)` instead of raw `Guid`; same shape generalises to `Variable<T>`. [andrewlock.net/strongly-typed-id-updates](https://andrewlock.net/strongly-typed-id-updates/). **Verdict: Adopt.** The `Variable<T>` proposal in §5.1 / §8.1 is the canonical strongly-typed-id move applied to a value-bag key. Pair with a `[StronglyTypedKey]` source generator (Andrew Lock's `StronglyTypedId` package is the reference implementation) and we get equality, hashing, debugger display, and JSON converters for free.

---

## 12. Benchmark plan

Each entry: what to measure, tool, test location, scenario, baseline expectation, success criteria for the proposed refactor. Tight bullets; no padding.

### 12.1 `VariableValueRegistry` first-access AppDomain scan
- **What:** wall-clock ms on first `VariableValueRegistry.GetType(...)`; bytes allocated; `ReflectionTypeLoadException` count (should be 0 in CI, may be >0 in player).
- **Tool:** EditMode test with `System.Diagnostics.Stopwatch` + `GC.GetAllocatedBytesForCurrentThread`. Force fresh state by restarting the registry via reflection on its private static caches (or use a domain-reload-bracketed test).
- **Location:** `Tests/Performance/VariableValueRegistryColdStartTests.cs`.
- **Scenario:** project AppDomain as-is (currently ~120 assemblies); also a synthetic worst case with 500 dummy assemblies loaded.
- **Baseline:** ~30-80 ms cold; ~50-200 KB allocated (assembly enumeration + `Type[]` arrays).
- **Success criteria for §8.2 source-gen replacement:** **<2 ms first call, <4 KB allocated, zero `Type[]` enumeration, zero `lock` contention.**

### 12.2 `ModifierTypeIndex` reflection
- **What:** time + alloc on first `GetCompatibleModifiers(typeof(float))`.
- **Tool:** Unity.PerformanceTesting `Measure.Method`.
- **Location:** `Tests/Performance/ModifierTypeIndexBenchmarks.cs`.
- **Scenario:** cold first call vs. 1000 warm calls.
- **Baseline:** cold ~20-50 ms; warm <1 µs (cached).
- **Success criteria:** cold path eliminated entirely (source-gen registration); warm path unchanged.

### 12.3 `LocalVariableStorage.Subscribe` closure allocation
- **What:** bytes allocated per call, total alloc count for 1000 sequential subscribes.
- **Tool:** Unity.PerformanceTesting `Measure.GcAllocations`; cross-check with `Allocations` counter.
- **Location:** `Tests/Performance/SubscribeBenchmarks.cs`.
- **Scenario:** 1, 100, 1000 subscriptions on one entity; same on 100 entities each with 16 vars.
- **Baseline:** today, two allocations per call (`CallbackDisposable` + the `Unsubscribe` closure capturing `key`+`onChange`); 64–96 bytes each on 64-bit. Consumer evidence: `StoreVariableStorage.cs:77` does the same allocation.
- **Success criteria for §8.4 struct-handle refactor:** **0 allocations on hot path; subscription token <16 bytes (struct on stack); `Dispose` returns the slot to a pooled free-list.**

### 12.4 `VariableNotifier` multicast-delegate churn
- **What:** allocation count per `Add`/`Remove` cycle; dispatch time for N subscribers.
- **Tool:** BenchmarkDotNet (pure-C# project under `Tests/Performance/Bdn/`) — `VariableNotifier` has no Unity surface.
- **Location:** `Tests/Performance/Bdn/VariableNotifierBenchmarks.cs`.
- **Scenario:** add 1/16/256 subscribers, then dispatch 10 000 times; remove 1 of N during dispatch.
- **Baseline:** every add/remove re-allocates the multicast `Action<VariableValue>` (.NET combines delegates by allocating a new `MulticastDelegate` array). At 256 subscribers, add = ~1 KB each.
- **Success criteria for §5.9 list-based refactor:** **add: ≤32 bytes amortised (list resize); remove: 0 bytes; dispatch unchanged within 5 %.**

### 12.5 `ApplyModifiers` virtual dispatch — verify "no boxing"
- **What:** allocations per `VariableValue<float>.ApplyModifiers(IReadOnlyList<ActiveModifier>)` with stack of 1/8/64 modifiers.
- **Tool:** BenchmarkDotNet with `[MemoryDiagnoser]`.
- **Location:** `Tests/Performance/Bdn/ApplyModifiersBenchmarks.cs`.
- **Scenario:** float, int, bool inner type; modifier-stack depths 1, 8, 64.
- **Baseline (audit's claim):** zero allocation; only virtual-call cost. **Verifying** because §4 asserts it without a measurement.
- **Success criteria:** confirmed 0 B allocated at all depths; if any allocation appears, find the path (likely `IReadOnlyList<ActiveModifier>` enumerator boxing on a struct-enumerator-less collection).

### 12.6 Three-layer guard chain on `EntityComponent → EntityInstance → LocalVariableStorage`
- **What:** time per `Subscribe` and per `GetVariable<T>` through the three layers vs. a hypothetical direct call.
- **Tool:** Unity.PerformanceTesting `Measure.Method` with 100 000 iterations.
- **Location:** `Tests/Performance/GuardChainBenchmarks.cs`.
- **Scenario:** repeat `GetVariable<float>(key)` 100 000× on a hot entity; same for `Subscribe`/`Unsubscribe` pairs.
- **Baseline:** 3 redundant null-checks + virtual call + dictionary lookup; expect ~50-80 ns/call.
- **Success criteria for §5.3 entry-only refactor:** **≥30 % reduction on `GetVariable` hot path; `Subscribe` reduction dominated by §12.3 fix.**

### 12.7 Modifier add/remove on combat-tick loop
- **What:** time + allocations for `AddModifier`/`RemoveModifier` cycles at combat-realistic rates.
- **Tool:** Unity.PerformanceTesting.
- **Location:** `Tests/Performance/ModifierChurnBenchmarks.cs`.
- **Scenario:** 100 entities, each receives 4 modifiers added then removed every simulated tick for 600 ticks (10 s @ 60 Hz). Variants: ordered insert (current) vs. append-and-sort.
- **Baseline:** `VariableModifierHandler.ComputeInsertIndex` is O(n) per insert; per-key bucket reallocation if `List<ActiveModifier>` grows; per add a `ModifierId` struct + `ActiveModifier` struct (no heap on those, but the bucket list resize is). The consumer-side equivalent in `EntityVariableState.WithModifier` (`entities.states`) **always** allocates a new dictionary + new bucket list (record-with semantics) — should be benchmarked separately as it is genuinely worse.
- **Success criteria:** keep mutable path under 1 alloc per add (bucket grow only); document that `entities.states` immutable path is structurally per-mutation allocating and is the cost of the Store model.

### 12.8 `Variable` key lookup — `Dictionary<Variable, …>` vs. typed
- **What:** lookup time and `GetHashCode`/`Equals` calls per `GetVariable`.
- **Tool:** BenchmarkDotNet.
- **Location:** `Tests/Performance/Bdn/VariableLookupBenchmarks.cs`.
- **Scenario:** bag with 4, 16, 64 variables; key is interned (typical `VariableSO`) vs. fresh `new Variable("hp","float")` (test pattern observed in `StateEntityIntegrationTests`).
- **Baseline:** `Variable.GetHashCode` combines two `string.GetHashCode` calls; `Equals` is two `string.Equals`. Fresh-allocation pattern in tests adds an allocation per call.
- **Success criteria for §8.1 `Variable<T>`:** equal or better lookup time; **fresh-Variable test pattern eliminated** (tests use a static `Variable<float> Hp = new("hp")` field, zero per-call alloc); compile-time T match removes the `BaseEntityInstance.cs:50` cast on read.

