# com.scaffold.types — Audit

## Summary
Editor + runtime helpers for `Type` references in serialized data: a `TypeReference` that round-trips through Newtonsoft, a `TypeSelectionAttribute` for `[SerializeReference]` polymorphism, and a `DependencyExtractor` that scans constructor params for an `[Inject]`-named attribute. Useful primitives, but the runtime ships engine-coupled, the package has zero tests, and several files violate the "fail-fast / no redundant guards" rubric. **Verdict: refactor.**

## Structure
```text
com.scaffold.types/
  Runtime/
    TypeReference.cs                  (UnityEngine + Newtonsoft)
    TypeSelectionAttribute.cs         (UnityEngine.PropertyAttribute)
    TypeReferenceFilterAttribute.cs   (pure C# attribute)
    TypeUtility.cs                    (pure C#)
    Contracts/IDependencyExtractor.cs
    Implementation/DependencyExtractor.cs
    Scaffold.Types.asmdef             (autoReferenced=true, no engine flag)
  Editor/
    TypeReferenceDrawer.cs
    TypeSelectionAttributeDrawer.cs
    DerivedTypeDropdown.cs
  Samples/  TypesUseCases.cs
  Tests/    .gitkeep                  (empty — no tests)
  package.json, README.md
```
asmdef has `autoReferenced: true` and no `noEngineReferences`. Container/installer not present (package has no DI surface). Tests folder is a stub.

## What's good
- Runtime/Editor split is correct.
- `Contracts/` + `Implementation/` separation on `IDependencyExtractor`.
- `DependencyExtractor` caches results in a `ConcurrentDictionary` (`Implementation/DependencyExtractor.cs:11`).
- `TypeReference` exposes implicit `Type → TypeReference` conversion (`Runtime/TypeReference.cs:65`) — ergonomic.
- Samples exist and demonstrate primary use cases.

## Issues / smells

### Unity/C# boundary leaks
- `Runtime/TypeReference.cs:4` imports `UnityEngine`. The class is conceptually pure data; only `ISerializationCallbackReceiver` and `[SerializeField]` need the engine. Either keep this engine-coupled and admit it, or ship a Unity-free `TypeRef` and a Unity adapter.
- `Runtime/TypeSelectionAttribute.cs:7` extends `PropertyAttribute` — fine, it is editor-only by intent. But it lives next to pure-C# attributes; mark this fact in folder structure (`Runtime/Unity/`).
- `Runtime/TypeReferenceFilterAttribute.cs:3-4` imports `System.Collections` and `System.Collections.Generic` and `UnityEngine` — none used. Dead imports.
- Asmdef does not set `noEngineReferences`, so the whole runtime drags `UnityEngine` into any consumer regardless.

### Redundant / mislocated guards (rubric: fail-fast at entry only)
- `Runtime/TypeUtility.cs:11-13` — `if (typeof(T) == null)` is unreachable. `typeof(T)` cannot be null. Delete.
- `Runtime/TypeUtility.cs:29-33` — `ValidateTypeLookupRequest<T>` repeats the same impossible check. Delete the method.
- `Runtime/TypeSelectionAttribute.cs:9-15` — `[NotNull]` annotation plus `if (baseType is null) throw` plus the field itself is `[NotNull]` and writable (`public Type BaseType;` `:18`). Pick one: keep the throw, remove `[NotNull]` ceremony, make the field `readonly` and a property.
- `Runtime/TypeReferenceFilterAttribute.cs:13-17` — null-check is fine, but the param has no `[NotNull]`/nullable-ref annotation; pick a consistent style across the package.
- `Runtime/TypeReference.cs:18-22, 35-37` — duplicated null-checks for the same logical entry path (`ctor` calls `Set`, `Set` re-checks). One throw is enough.
- `Runtime/Implementation/DependencyExtractor.cs:15` is fine (entry point). Good example of where guards belong.

### Default-values masking errors
- `Runtime/TypeReference.cs:54-63` — `OnAfterDeserialize` swallows every exception in a bare `catch` and silently nulls `type`. Logging a warning is not fail-fast; the broken asset goes downstream. Either rethrow on Editor-time deserialization or push the failure into `TypeReference.Type` so the consumer fails when it's actually used.
- `Runtime/Implementation/DependencyExtractor.cs:27-28` — when no constructor exists at all, returns `Array.Empty<Type>()`. That's a valid case, but combined with the magic-string `"InjectAttribute"` check (`:22`) and silent fallback to "most params", a wrong attribute name silently becomes "use the default ctor". Inconsistent attribute resolution should fail fast in non-production.
- `Editor/DerivedTypeDropdown.cs:31-33` — `Math.Max(0, selectedIndex)` masks the "nothing selected" state by returning the first option, then `ElementAtOrDefault` returns null silently downstream. Caller in `TypeSelectionAttributeDrawer.cs:37` will then `Activator.CreateInstance(null)` and throw far from the source.

### Missing generics / weak typing
- `Runtime/Contracts/IDependencyExtractor.cs:8` takes `Type type` — no generic overload. Add `IEnumerable<Type> GetConstructorDependencies<T>()` for compile-time call sites.
- `Editor/DerivedTypeDropdown.cs:54` `CreateInstance(object oldValue = null)` returns `object` but the dropdown is constructed for a known `targetType`. A `DerivedTypeDropdown<TBase>` with `TBase Create()` would remove the cast in `TypeSelectionAttributeDrawer.cs:37`.
- `DependencyExtractor.AnalyzeDependencies` is `public` (`Implementation/DependencyExtractor.cs:19`) but only called internally. Make it `private` so `IDependencyExtractor` is the only surface.
- Magic string `"InjectAttribute"` (`Implementation/DependencyExtractor.cs:22`) defeats compile-time safety. Use `typeof(VContainer.InjectAttribute)` or accept it as a generic type parameter (`GetConstructorDependencies<TInjectAttribute>` constrained to `Attribute`).

### Over/under-abstraction
- `IDependencyExtractor` has one impl, but the interface is justified — VContainer install-time consumers should depend on the contract. Keep.
- `TypeReference` is concrete and final-ish but mutable (`Set`) and has a parameterless ctor (`:11-14`) that creates an invalid object. Either make it immutable (`readonly` field, only ctor + implicit op) or accept it's a Unity-serialized DTO and document the invalid window.

### Editor smells
- `Editor/TypeReferenceDrawer.cs:15` system-assembly filter is `{"mscorlib","System","System.Core"}` — fragile and incomplete (misses `System.Private.CoreLib`, `netstandard`, etc.). Also `BuildIsSystemAssembly` walks **referenced** assemblies (`:122`) — any assembly that references mscorlib (i.e. all of them) is "system". This effectively disables the filter or excludes everything depending on which branch hits first.
- `Editor/TypeReferenceDrawer.cs` — every method prefixed `Build*` is a naming smell suggesting a builder/extractor that never materialized; rename to plain verbs.
- `Editor/TypeReferenceDrawer.cs:52` `(property.boxedValue as TypeReference).Type` — unguarded cast; if `boxedValue` isn't a `TypeReference`, NRE.
- `Editor/TypeReferenceDrawer.cs:101, 114` filter chain `!t.FullName.Contains("<")` is a hack to dodge generics already excluded by the prior predicates. Dead.
- `Editor/TypeSelectionAttributeDrawer.cs:18-20` — multiple statements per line. Style only, but pervasive.
- Newtonsoft-serialized `Type` (`Runtime/TypeReference.cs:39-40`) is heavy and unstable across rename/refactor. Industry convention is `AssemblyQualifiedName` (or short `Type.FullName + assembly.Name`), see Unity's own `SerializeReference` examples. Newtonsoft `TypeNameHandling.All` is also a known security smell when reading untrusted data; not relevant in-editor but worth a note.

## Suggested before/after

**Drop dead validation, fail fast.**
```csharp
// before — Runtime/TypeUtility.cs
public static IEnumerable<Type> GetTypesDerivedFrom<T>(bool includeAbstract, bool includeSource)
{
    if (typeof(T) == null) throw new InvalidOperationException("Requested type was not resolved.");
    ValidateTypeLookupRequest<T>();
    return GetAllDerivedTypes(typeof(T), includeAbstract, includeSource);
}

// after
public static IEnumerable<Type> GetTypesDerivedFrom<T>(bool includeAbstract = false, bool includeSelf = false)
    => GetAllDerivedTypes(typeof(T), includeAbstract, includeSelf);
```

**Generic dependency extractor with compile-time inject attribute.**
```csharp
public interface IDependencyExtractor
{
    IReadOnlyList<Type> GetConstructorDependencies(Type type);
    IReadOnlyList<Type> GetConstructorDependencies<T>() => GetConstructorDependencies(typeof(T));
}

internal sealed class DependencyExtractor<TInject> : IDependencyExtractor where TInject : Attribute
{
    private readonly ConcurrentDictionary<Type, Type[]> cache = new();
    public IReadOnlyList<Type> GetConstructorDependencies(Type type) =>
        cache.GetOrAdd(type, Analyze);
    private static Type[] Analyze(Type type) { /* uses typeof(TInject), no string */ }
}
```

**Stop swallowing in `OnAfterDeserialize`.**
```csharp
public void OnAfterDeserialize()
{
    if (string.IsNullOrWhiteSpace(serializedType)) { type = null; return; }
    var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
    type = JsonConvert.DeserializeObject<Type>(serializedType, settings)
           ?? throw new InvalidOperationException($"Unresolvable serialized type: {serializedType}");
}
```

## Easy wins (each <30 min)
1. Delete `TypeUtility.ValidateTypeLookupRequest` and the duplicate `if (typeof(T) == null)` (`Runtime/TypeUtility.cs:11-13, 29-33`).
2. Remove dead `using` lines in `TypeReferenceFilterAttribute.cs:2-4`.
3. Make `BaseType` readonly + property in `TypeSelectionAttribute.cs:18`; drop `[NotNull]`.
4. Make `DependencyExtractor.AnalyzeDependencies` private (`Implementation/DependencyExtractor.cs:19`).
5. Replace magic string `"InjectAttribute"` with `typeof(VContainer.InjectAttribute)` (or a generic param) in `Implementation/DependencyExtractor.cs:22` — pick one source of truth.

## Organization & docs
- `Tests/.gitkeep` — empty; either add tests for `DependencyExtractor` (cache hit, multiple `[Inject]`, no-ctor), `TypeReference` (round-trip, missing assembly), and `TypeUtility`, or remove the folder.
- README exists but the architect's rule is full module docs at the package root; verify it covers `IDependencyExtractor`, the `[Inject]` attribute resolution rule, and the JSON serialization choice.
- `Container/` is missing — fine while there is nothing to register, but expose `IDependencyExtractor` registration here when consumers materialize.
- Folder pattern `Runtime/Contracts/` + `Runtime/Implementation/` is good; apply the same to `TypeReference`/`TypeUtility` if they grow.
- `autoReferenced: true` on the runtime asmdef is a wide blast radius — flip to `false` once consumers explicitly reference it.

## Consumers

Cross-package usage scoped to `Assets/`. Two consumers: `com.scaffold.navigation` (heavy) and `com.scaffold.view` (one file). Zero non-package consumers in `Assets/Scaffold/`.

**`com.scaffold.navigation/Runtime/Implementation/ViewConfig.cs:23-27`** — primary `TypeReference` use site:
```csharp
public Type ViewType => viewType.Type;
[SerializeField, TypeReferenceFilter(typeof(IView))] private TypeReference viewType;
public Type ControllerType => controllerType.Type;
[SerializeField, TypeReferenceFilter(typeof(IViewController))] private TypeReference controllerType;
```
Smell: every consumer rewrites the same `Type Foo => fooRef.Type;` unwrap pair. The sister property is the only public surface; `TypeReference` is internal plumbing. A `[SerializeField, TypeFilter(typeof(IView))] Type ViewType { get; }` macro (source-gen) or just exposing `Type` as the public field via a custom property drawer would remove this boilerplate at every call site.

**`com.scaffold.navigation/Runtime/Implementation/AnimationViewSchema.cs:45`** — comparison through `.Type`:
```csharp
return viewTypes.Any(vt => vt.Type == targetPoint?.Config.ViewType || (targetPoint == null && vt.Type == typeof(NoView)));
```
Repeated in `TransitionViewSchema.cs:42`. `vt.Type` is a property call that goes through `Type` getter — not field access. If `OnAfterDeserialize` ever silently nulled (the bug flagged in `Runtime/TypeReference.cs:54-63`), every navigation predicate silently returns `false`. Consumers cannot tell broken-asset from no-match.

**`com.scaffold.navigation/Runtime/Implementation/ViewConfig.cs:97-99`** — wrapping after compute:
```csharp
this.viewType = new TypeReference(viewType);
controllerType = controller == null ? null : new TypeReference(controller);
```
Two `new TypeReference(...)` allocations per `OnValidate`. The package's implicit `Type → TypeReference` op (`Runtime/TypeReference.cs:65`, called out in audit) would let this read `this.viewType = viewType;` — but consumer didn't use it. Either implicit ops aren't discoverable, or the writer didn't trust them on `null`. Validate the null behavior of the implicit op and document.

**`com.scaffold.navigation/Runtime/Implementation/ViewChangedEvent.cs:14-15, 24`** — same `[TypeReferenceFilter] TypeReference` + property unwrap pattern as `ViewConfig`. Third repeat. Boilerplate confirmed across the package.

**`com.scaffold.view/Runtime/BaseEvents/NavigateViewEvent.cs:17-19`** — same pattern again, fourth repeat.

**`com.scaffold.navigation/Runtime/Utility/NavigationExtensions.cs:1-9`** — `using Scaffold.Types;` is dead (no `TypeReference` used in the file). This is the `autoReferenced: true` blast radius the audit flagged: every navigation file `using`s the package whether it needs it or not.

**`com.scaffold.navigation/Runtime/Implementation/ViewSchema.cs:1-10`** — also dead `using Scaffold.Types;`. Same story.

No consumer of `IDependencyExtractor`, `TypeUtility`, or `TypeSelectionAttribute` found in `Assets/`. Three of the package's six exported APIs have zero consumer evidence — only `TypeReference` and `TypeReferenceFilterAttribute` are paying rent.

## Alternatives & prior art

- **Unity `[SerializeReference]` + plain `Type.AssemblyQualifiedName` string field** — Unity's own pattern for polymorphic serialization. Stable, no Newtonsoft. Reference: `https://docs.unity3d.com/ScriptReference/SerializeReference.html`. **Steal pattern**: drop Newtonsoft from `TypeReference.OnBeforeSerialize`, store AQN, deserialize via `Type.GetType(aqn, throwOnError: false)`. Same surface, no JSON dependency, no `TypeNameHandling.All` security smell.
- **Odin Inspector `TypeFilterAttribute` / `[TypeRegistryAttribute]`** — paid asset; known good filtered-type-picker UX. `https://odininspector.com/`. **Steal pattern**: dropdown ergonomics for `Editor/DerivedTypeDropdown.cs`. Don't adopt the dependency for one drawer.
- **Microsoft Roslyn `INamedTypeSymbol` + source generators** — for *editor-time* type discovery without reflection. `https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/`. **Build (current path)**: runtime `TypeReference` is a serialization concern, not a discovery one. Source gen is overkill here.
- **VContainer's own `Inject` resolution** — VContainer already walks ctors looking for `[Inject]`. `https://github.com/hadashiA/VContainer/blob/master/VContainer/Assets/VContainer/Runtime/Internal/InjectorBuilder.cs`. **Adopt**: `IDependencyExtractor` reimplements VContainer's logic via reflection + magic string. Either depend on VContainer's `IInjector` directly or drop the abstraction; the package has zero consumers of it anyway (see Consumers above).

## Benchmark plan

- **TypeReference round-trip allocation**
  - What: bytes allocated per `OnBeforeSerialize`/`OnAfterDeserialize` cycle (Newtonsoft path).
  - Tool: `Unity.PerformanceTesting`, `Measure.Method` with `SampleGroup` configured for `AllocatedManagedMemory`.
  - Location: `Tests/Performance/TypeReferenceBenchmarks.cs`.
  - Scenario: 10 distinct types × 1000 round-trips, 5 warmup, 10 iterations.
  - Baseline: ~2 KB/round-trip estimated (JsonSerializerSettings + JObject + reflection).
  - Success: AQN-string variant ≤ 200 B/round-trip; full Newtonsoft ≤ 1 KB if kept.

- **DependencyExtractor cache hit cost**
  - What: time and allocations for cached `GetConstructorDependencies(type)` after warm-up.
  - Tool: BenchmarkDotNet (pure C#).
  - Location: `Tests/Performance/DependencyExtractorBenchmarks.cs`.
  - Scenario: 100 types pre-seeded, 10k random lookups, no warmup needed for hot path.
  - Baseline: `ConcurrentDictionary.TryGet` ≈ 20 ns, 0 alloc.
  - Success: 0 alloc steady-state; ≤ 50 ns/lookup.

- **DerivedTypeDropdown population cost (editor)**
  - What: time to enumerate `TypeUtility.GetTypesDerivedFrom<TBase>` over the loaded AppDomain.
  - Tool: `Unity.PerformanceTesting` editor harness.
  - Location: `Tests/Performance/Editor/TypeUtilityBenchmarks.cs`.
  - Scenario: 3 base types (`IView`, `IViewController`, `Schema`), 5 iterations, 1 warmup.
  - Baseline: O(n) over all loaded types; n ≈ 5–20k in this project; expect ~5–20 ms.
  - Success: cached variant returns in < 0.1 ms after first call; first-call ≤ 30 ms.
