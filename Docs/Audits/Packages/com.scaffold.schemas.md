# com.scaffold.schemas — Audit

## Summary
"Component-like" composable data on `ScriptableObject`s: `SchemaObject` owns a `SchemaSet` of `[SerializeReference]` `Schema` instances, with editor UI for add/remove/required/duplicate rules. Concept is solid and useful (think Unity ECS authoring or Unreal data tables for designers). Implementation is the most fragile in the audit set: silent failures on type mismatches, public mutable serialized fields, magic strings in editor messages, no asmdef discipline, and zero tests. **Verdict: refactor before relying on it for production data.**

## Structure
```text
com.scaffold.schemas/
  Runtime/
    Schema.cs                                 (empty abstract class)
    SchemaObject.cs                           (ScriptableObject base)
    SchemaSet.cs                              (Serializable collection)
    Attributes/
      AllowDuplicateSchemasAttribute.cs
      RequireSchemaAttribute.cs
      SchemaCustomDrawerAttribute.cs
      SchemaDescriptionAttribute.cs
      SchemaFilterAttribute.cs
      SchemaMenuGroupAttribute.cs
      SchemaTemplateAttribute.cs
    Scaffold.Schemas.asmdef                   (rootNamespace empty)
  Editor/
    SchemaDrawer.cs
    SchemaObjectEditor.cs
    SchemaValidator.cs
    Layout/{SchemaLayout, SchemaStyles}.cs
    Utility/{SchemaCacheUtility, SchemaDrawerContainer, SchemaDrawerFactory}.cs
    Scaffold.Schemas.Editor.asmdef
  package.json                                (no Container/, no Tests/, no Samples/)
```

## What's good
- Composition-by-attribute is the right pattern for this problem (data designers want components).
- `[SerializeReference]` on `SchemaSet.Collection` (`Runtime/SchemaSet.cs:25-26`) is the correct way to do polymorphic serialization in Unity.
- `RequireSchemaAttribute` + `AllowDuplicateSchemasAttribute` (`Runtime/Attributes/RequireSchemaAttribute.cs:9, AllowDuplicateSchemasAttribute.cs:7`) — declarative validation rules. Good idea.
- Editor side is decomposed: `SchemaObjectEditor` (orchestrator), `SchemaDrawer` (per-schema view), `SchemaValidator` (rules), `SchemaCacheUtility` (TypeCache), `SchemaDrawerFactory` (drawer dispatch). Layering is sensible.
- `SchemaCacheUtility` caches `GetTypeDisplayName` / `GetTypeGroupPath` / `GetDerivedTypes` (`Editor/Utility/SchemaCacheUtility.cs:11-13`). Right call for editor perf.

## Issues / smells

### Default-values masking errors (rubric: fail-fast)
- `Runtime/SchemaSet.cs:30-34` — if the `schema` argument doesn't inherit `Schema`, the method `Debug.Log`s a typo'd ("inherint") message and **returns false**. That's an `ArgumentException` situation. Worse: caller `SchemaObject.AddSchema(Type)` (`Runtime/SchemaObject.cs:37-40`) has no idea why it failed. Fail fast.
- `Runtime/SchemaObject.cs:48-51` — `RemoveSchema(Type type)` does `Schemas.FirstOrDefault(s => s.GetType().IsAssignableFrom(type))`. **`IsAssignableFrom` is the wrong direction**: this finds schemas that are assignable from `type`, i.e. *base classes* of `type`, not the schema you asked to remove. Likely bug. Should be `s.GetType() == type` or `type.IsAssignableFrom(s.GetType())`.
- `Runtime/SchemaSet.cs:42` — `Types.Remove(schema.GetType())` removes by element. If the same schema type appears twice (allowed via `AllowDuplicateSchemas`), the `Types` cache and `Collection` desync because `Types.Remove` removes one occurrence and `Collection.Remove` removes a different element by reference.
- `Runtime/SchemaSet.cs:13-21` — `Types` lazily materializes from `Collection`, but mutating `Collection` directly never invalidates the cache. Both `AddSchema`/`RemoveSchema` poke `Types` *and* `Collection`. If anyone touches `Collection` through the public field, `Types` lies. The field is `public` (`:26`) — they will.
- `Editor/SchemaObjectEditor.cs:67-73` — silently `return` on `prop == null || prop.boxedValue == null`. If a `SerializeReference` slot has been nulled (asset corruption or type rename), the user sees nothing instead of "this schema lost its type, click to fix." Surfacing the broken state is the whole point of a validator UI.

### Public mutable serialized state
- `Runtime/SchemaSet.cs:26` — `public List<Schema> Collection` is a public mutable serialized field. Combined with the `Types` cache problem, any external write breaks invariants. Make `Collection` `[field: SerializeField] private` and expose `IReadOnlyList<Schema>`; mutate only through `AddSchema`/`RemoveSchema`.
- `Runtime/Attributes/RequireSchemaAttribute.cs:16` — `public Type[] SchemaTypes = new Type[0];` is a public mutable field on an attribute. Make it a get-only property: `public IReadOnlyList<Type> SchemaTypes { get; }`.
- `Runtime/Attributes/AllowDuplicateSchemasAttribute.cs:14`, `SchemaDescriptionAttribute.cs:13`, `SchemaFilterAttribute.cs:16`, `SchemaMenuGroupAttribute.cs:13`, `SchemaTemplateAttribute.cs:14` — all use `{ get; private set; }`. For attributes, that's `{ get; }`. Read-only. Trivial fix, multiple files.

### Missing generics / weak typing
- `Runtime/SchemaObject.cs` exposes `AddSchema(Type)`, `RemoveSchema(Type)`, `HasSchema(Type)` alongside `AddSchema<T>()` etc. The `Type`-taking overloads have no constraint that the type derives from `Schema`. `AddSchema(typeof(string))` compiles and only fails at runtime via the `Debug.Log` (`SchemaSet.cs:30-34`). A generic constraint or `where T : Schema` overload only would fix this at compile time.
- `Runtime/Attributes/RequireSchemaAttribute.cs:11` — `params Type[]` — same problem; no compile-time constraint that those types derive from `Schema`. Cannot fix on the attribute (no generic attributes pre-C# 11; Unity may not allow them yet), but a Roslyn analyzer can validate `[RequireSchema(typeof(Foo))]` resolves to a `Schema` subclass.
- `Editor/Utility/SchemaDrawerFactory.cs:25-32` — `GetDrawerType` walks `BaseType` looking for a registered drawer; the loop has no termination condition for "no match all the way to `object`" — `targetType.BaseType` becomes null, then `drawerLookUp.TryGetValue(null, …)` throws on the next iteration. Should fall back to a default `SchemaDrawer` registration explicitly.

### Unity / C# boundary
- `Runtime/Schema.cs:1-13` imports `System.Collections`, `System.Collections.Generic`, `UnityEngine` — uses none of them. Pure-C# abstract class with no body. Delete imports; consider whether `Schema` should live in an engine-free asmdef so plain-C# tests can reference it.
- `Runtime/SchemaSet.cs:32` — `Debug.Log` couples runtime to UnityEngine. If schema validation moves into an engine-free assembly, a logger abstraction is needed (or just `throw`).
- `Runtime/Scaffold.Schemas.asmdef:3` — `rootNamespace: ""` again. Set to `Scaffold.Schemas`.
- `Runtime/Attributes/*` use `using UnityEngine;` (`AllowDuplicateSchemasAttribute.cs:2`, `RequireSchemaAttribute.cs:4`, `SchemaFilterAttribute.cs:4`, `SchemaTemplateAttribute.cs:2`) without using anything from it. Remove.

### Over/under-abstraction
- `Schema` is an empty abstract class (`Runtime/Schema.cs:9-12`). Acts as a tag. Acceptable, but consider `interface ISchema` so structs can be schemas too — though `[SerializeReference]` requires a class. Keep `class Schema` and document the constraint.
- `SchemaDrawer` is a concrete class registered for `typeof(Schema)` (`Editor/SchemaDrawer.cs:10-11`). This is the entry point for custom inspectors and should be `abstract` or have an `IsDefault` registration. Currently overlaps with the `[SchemaCustomDrawer(typeof(Schema))]` registration.
- `SchemaDrawerContainer` is a `ScriptableSingleton` (`Editor/Utility/SchemaDrawerContainer.cs:8`), keyed by `prop.boxedValue` (`:15, 26`). `boxedValue` allocates on every access and uses default `Equals`/`GetHashCode`; for `Schema` reference types, that's fine, but two reads of `boxedValue` may not return the same instance. Cache the result of one `boxedValue` call per drawer lookup.

### Editor smells
- `Editor/SchemaValidator.cs:79-80` — `attribute.AllowMultiple` dereferences a `FirstOrDefault` result with no null check. If a type is in `TypeCache.GetTypesWithAttribute<T>` results, the attribute exists, but `FirstOrDefault` is the wrong API — use `First()` so a corrupted result fails fast.
- `Editor/SchemaValidator.cs:35` — `attributes != null && attributes.Count() > 0` over `IEnumerable<RequireSchemaAttribute>` enumerates twice. Use `.Any()` once or materialize to a list.
- `Editor/SchemaObjectEditor.cs:54` — `serializedObject.FindProperty("schemas.Collection")` is a magic path string; if `SchemaObject.schemas` is renamed, only a runtime check catches it. Use `nameof` chain or a constant.
- `Editor/SchemaObjectEditor.cs:134, 142` — same issue with `"schemas"` literal. Constants.

## Suggested before/after

**Fail fast on type mismatch.**
```csharp
// before — Runtime/SchemaSet.cs
public bool AddSchema(Type schema)
{
    if (!typeof(Schema).IsAssignableFrom(schema))
    {
        Debug.Log($"schema object you are trying to add does not inherint from SCHEMA");
        return false;
    }
    ...
}

// after
public void AddSchema(Type schemaType)
{
    if (!typeof(Schema).IsAssignableFrom(schemaType))
        throw new ArgumentException($"{schemaType.FullName} is not a Schema.", nameof(schemaType));
    var instance = (Schema)Activator.CreateInstance(schemaType);
    Collection.Add(instance);
    types?.Add(schemaType);     // keep cache in sync only when materialized
}
```

**Fix `RemoveSchema(Type)` direction.**
```csharp
// before — Runtime/SchemaObject.cs:48-51
public bool RemoveSchema(Type type)
{
    Schema schema = Schemas.FirstOrDefault(s => s.GetType().IsAssignableFrom(type));
    return schemas.RemoveSchema(schema);
}

// after
public bool RemoveSchema(Type type)
{
    Schema schema = Schemas.FirstOrDefault(s => type.IsAssignableFrom(s.GetType()))
        ?? throw new InvalidOperationException($"No schema of type {type} present.");
    return schemas.RemoveSchema(schema);
}
```

**Lock down public mutable state.**
```csharp
// before — Runtime/SchemaSet.cs:25-26
[SerializeReference, SerializeField]
public List<Schema> Collection = new List<Schema>();

// after
[SerializeReference, SerializeField] private List<Schema> collection = new();
public IReadOnlyList<Schema> Collection => collection;
```

## Easy wins
1. Fix the `IsAssignableFrom` direction bug in `Runtime/SchemaObject.cs:49`.
2. Delete unused `using` lines in `Schema.cs`, all attribute files, and `SchemaSet.cs`.
3. Convert all attribute `{ get; private set; }` to `{ get; }` in `Runtime/Attributes/*.cs` (5 files).
4. Make `SchemaSet.Collection` private + `IReadOnlyList<Schema>` getter (`Runtime/SchemaSet.cs:25-26`); update editor `FindProperty("schemas.Collection")` callers.
5. Replace `Debug.Log` typo in `SchemaSet.cs:32` with `throw new ArgumentException`. Fail fast.

## Organization & docs
- No `Container/`, no `Tests/`, no `Samples/` — odd for a package this size and complexity. Add at least a `Samples/` showing a `RequireSchema`/`AllowDuplicateSchemas`/custom drawer triplet, and `Tests/` covering `SchemaSet` add/remove/duplicate semantics.
- README at root: confirm it documents the validator's auto-add behavior (`SchemaValidator.cs:32-46` adds required schemas without prompting), the duplicate rules, and the drawer registration pattern.
- Asmdef `rootNamespace: ""` and `noEngineReferences: false` (correct, since `ScriptableObject`). But split the attributes into a `Scaffold.Schemas.Contracts` engine-free asmdef so consumers can declare `[RequireSchema(typeof(...))]` from non-Unity assemblies.
- Naming: `SchemaCacheUtility` could be `SchemaTypeCache`; `SchemaDrawerContainer` → `SchemaDrawerCache`. The `Container` suffix collides with the `Container/` DI folder convention used elsewhere in the repo.
- Reference: Unity's `[SerializeReference]` polymorphism is the foundation here; for a hardened pattern, look at `Unity.Entities` authoring components or `Odin Inspector`'s `[Polymorphic]` for design hints.

## Consumers

Single consumer in `Assets/`: `com.scaffold.navigation` (3 files). No other Scaffold package uses Schemas. (The `AAGen-0.3.0/Editor/AddressableGroupCommandQueue.cs:175` hit on the search is Unity Addressables' `SchemaObjects` — a totally unrelated type, false positive.)

**`com.scaffold.navigation/Runtime/Implementation/ViewConfig.cs:11-12`** — the canonical use:
```csharp
[SchemaFilter(typeof(ViewSchema))]
public class ViewConfig : SchemaObject
```
Smell: `SchemaFilter` accepts `typeof(...)` with no `: Schema` constraint at compile time. Audit flagged this on the package side; here we see the consumer paying the price — `[SchemaFilter(typeof(string))]` would compile and fail at runtime via the `Debug.Log` typo'd path (`Runtime/SchemaSet.cs:30-34`). Generic attributes (C# 11) or a Roslyn analyzer would cut this.

**`com.scaffold.navigation/Runtime/Implementation/ViewSchema.cs:14`** — only `Schema` subclass in `Assets/`:
```csharp
public abstract class ViewSchema : Schema
```
The package ships a single concrete consumer, and that consumer wraps the base type with its own abstract — meaning the consumer didn't trust the empty `Schema` tag class. `ViewSchema.cs` itself adds no members (`{}`). All it does is narrow `[SchemaFilter(typeof(ViewSchema))]` to the navigation domain. If `Schema` had been `interface ISchema` the consumer wouldn't need this empty-shell intermediate.

**`com.scaffold.navigation/Runtime/Implementation/ViewSchema.cs:1-10`** — dead `using`s confirmed:
```csharp
using UnityEngine; using Scaffold.Types; using Scaffold.Navigation.Contracts;
using Scaffold.Events.Contracts; using Scaffold.Events;
using System.Threading.Tasks; using System.Linq; using System.Collections.Generic;
using System; using Scaffold.Schemas;
```
Five of nine imports are unused for an empty class body. Smells like a copy-paste template — and it's repeated across `AnimationViewSchema.cs:1-11`, `TransitionViewSchema.cs:1-10`. The `Scaffold.Schemas` import is the only one that's load-bearing in those two; the rest are ceremonial.

**`com.scaffold.navigation/Editor/ViewConfigEditor.cs:9, 14`**:
```csharp
public class ViewConfigEditor : SchemaObjectEditor
...
private static readonly string[] ExcludingViewFields = { "m_Script", "schemas", "viewAssetSource", ... };
```
Smell: the consumer hardcodes `"schemas"` as a magic string to exclude the schemas list from `DrawPropertiesExcluding`. Audit flagged the same magic string inside the package (`SchemaObjectEditor.cs:54, 134, 142`). The leak now extends to consumers — anyone subclassing `SchemaObjectEditor` must know the internal field name. Either expose `SchemaObjectEditor.SchemasFieldName` as a constant, or expose a virtual `GetExcludedFieldNames()` template method.

**`com.scaffold.navigation/Runtime/Implementation/AnimationViewSchema.cs:13`** — concrete schema:
```csharp
public class AnimationViewSchema : ViewSchema { ... }
```
Combined with `TransitionViewSchema`, the navigation package has 2 concrete schemas plus the `ViewSchema` abstract. Tiny consumer surface for a package this large (15+ files, 7 attributes, 4 editor utility classes).

**Zero consumers** of: `[AllowDuplicateSchemasAttribute]`, `[SchemaCustomDrawerAttribute]`, `[SchemaDescriptionAttribute]`, `[SchemaMenuGroupAttribute]`, `[SchemaTemplateAttribute]`, `[RequireSchemaAttribute]`. **Six of seven attributes have no consumer in `Assets/`.** The package's main feature surface is dead weight today.

**Zero consumers** of `SchemaSet` directly (it's wrapped inside `SchemaObject`). The audit's worry about `public List<Schema> Collection` mutability has no current breakage, but no test coverage either.

## Alternatives & prior art

- **Unity Addressables `AddressableAssetGroupSchema`** — Addressables already ships a near-identical "composable schema on a ScriptableObject" pattern (and the navigation package's only false-positive search hit was *that* type). `https://docs.unity3d.com/Packages/com.unity.addressables@1.21/api/UnityEditor.AddressableAssets.Settings.AddressableAssetGroupSchema.html`. **Steal pattern**: their `RequireSchema`/`AllowDuplicateSchemas` semantics, group-template flow, and editor UI are directly comparable. They also use `[SerializeReference]` polymorphism. Worth a head-to-head before keeping `com.scaffold.schemas`.
- **Unity DOTS / `IComponentData` authoring** — composable component data on `MonoBehaviour`/`ScriptableObject` baked at conversion time. `https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/concepts-baking.html`. **Steal pattern**: the authoring → runtime split. `Schema` could be an authoring-only concept that bakes to plain data classes; would remove `[SerializeReference]` runtime fragility.
- **Odin Inspector `[Polymorphic]` / `[ListDrawerSettings]`** — the standard third-party answer to polymorphic serialized lists in Unity. `https://odininspector.com/`. **Adopt** if budget allows; it would replace ~80% of `Editor/SchemaDrawer*.cs`. Not free.
- **FluentValidation / DataAnnotations** — for the `[RequireSchema]`/`[AllowDuplicateSchemas]` rule layer specifically. `https://docs.fluentvalidation.net/`. **Build (current path)**: validation rules expressed as attributes are a reasonable Unity-friendly choice; FluentValidation is too heavy. Keep the attribute pattern, but make it generic-attribute-friendly when the project moves to C# 11.

Verdict: **refactor**, but interrogate the duplication with Addressables' schema model first. If a Unity asset designer would intuitively reach for Addressables' grouping, this package is solving a parallel problem; consider whether a single schema concept can serve both.

## Benchmark plan

- **`SchemaObject.AddSchema<T>` / `RemoveSchema<T>` correctness + cost**
  - What: time + alloc per add/remove cycle on a populated `SchemaObject`.
  - Tool: `Unity.PerformanceTesting`, `SampleGroup(Time + AllocatedManagedMemory)`.
  - Location: `Tests/Performance/SchemaSetBenchmarks.cs`.
  - Scenario: 100 schemas, 10k add/remove cycles, 5 warmup. Both `<T>` and `Type` overloads.
  - Baseline: today, `Activator.CreateInstance` per add (~150 ns + closure alloc); `LINQ.Any` + `IsAssignableFrom` per check (`SchemaObject.cs:48-51`, the bug).
  - Success: 0 alloc on `RemoveSchema` (dictionary path); ≤ 1 µs/add; correctness asserted (audit's `IsAssignableFrom` direction bug must be fixed first or this benchmark passes while doing the wrong thing).

- **`SchemaSet.Types` cache invalidation regression test**
  - What: not perf — verify the lazy `Types` cache stays in sync after `Collection` mutation.
  - Tool: NUnit (correctness benchmark).
  - Location: `Tests/SchemaSetCacheTests.cs`.
  - Scenario: read `Types` (materializes), mutate `Collection` directly, read `Types` again — assert it reflects the change.
  - Baseline: today the cache lies (audit `SchemaSet.cs:13-21`).
  - Success: either the cache is invalidated or `Collection` is read-only.

- **Editor drawer dispatch cost (TypeCache + drawer factory)**
  - What: time to resolve and instantiate the right drawer for a `Schema` instance.
  - Tool: `Unity.PerformanceTesting` editor harness.
  - Location: `Tests/Performance/Editor/SchemaDrawerBenchmarks.cs`.
  - Scenario: 50 schema types, 1k drawer lookups per type, 5 warmup.
  - Baseline: today, `SchemaDrawerFactory.GetDrawerType` walks `BaseType` chain (audit `:25-32`); cached via `SchemaCacheUtility`.
  - Success: ≤ 1 µs/lookup steady-state, 0 alloc; first-call ≤ 10 ms.

- **`SchemaValidator` over a fully populated project**
  - What: time to validate every `SchemaObject` asset in a project against `[RequireSchema]` rules.
  - Tool: `Unity.PerformanceTesting` editor harness.
  - Location: `Tests/Performance/Editor/SchemaValidatorBenchmarks.cs`.
  - Scenario: 200 `ViewConfig` assets × 5 schemas each, 1 iteration (validation runs on inspector enable).
  - Baseline: today, `IEnumerable.Count() > 0` double-enumeration + `FirstOrDefault().AllowMultiple` NRE risk (audit `:35, 79-80`).
  - Success: ≤ 50 ms total over 200 assets; no NRE on corrupt-attribute path; converted to `Any()` single-enumeration.
