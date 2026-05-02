# Audit: `com.scaffold.mvvm`

Audited: 2026-05-02. Reviewer: senior architect (audit pass).
Path: `/home/user/Scaffold/Assets/Packages/com.scaffold.mvvm`
Asmdef: `Scaffold.MVVM` (`noEngineReferences: false`).
Source generator authoring lives outside the package at `/home/user/Scaffold/Generators/MVVMCompositionGenerator/MVVMCompositionGenerator.cs`; the compiled DLLs are shipped in-package under `GeneratorsMVVM/`.

## 1. Summary

This is the binding engine. It is **expression-tree-typed**, not stringly-typed at the call site: consumers write `Bind(() => vm.Foo, () => vm.Bar)` and the Roslyn-class source generator (`MVVMCompositionGenerator`) emits a `Bind<TSource,TTarget>` facade on every `[BindSource]` class. Internally the engine still keys subscribers by a flattened dotted path string compiled out of the lambda, which is fine but couples the registry to property names. Boxing is largely avoided in the hot path; INPC is delegated to `CommunityToolkit.Mvvm.ObservableObject` (an excellent decision). The package is salvageable but has clear smells: a Unity coroutine host coupled into a contract assembly, `Debug.Log` calls baked into binding code, and exception-swallowing setters that violate the project's fail-fast preference.

**Verdict: keep with focused refactor.** The shape is right; clean the engine leaks and the boxing/swallowing in `BindedProperty<,>.Update`.

## 2. Structure

```
com.scaffold.mvvm/
├── Runtime/
│   ├── AssemblyInfo.cs                       # InternalsVisibleTo Scaffold.MVVM.Tests
│   ├── NestedObservableObjectAttribute.cs    # marker, consumed by source gen
│   ├── NestedPropertyAttribute.cs            # marker for nested fields
│   ├── Scaffold.MVVM.asmdef                  # noEngineReferences = false
│   ├── Binding/                              # 21 files: registry, factory, sets, contexts, bind handles
│   └── Contracts/                            # 11 interfaces: IBind, IBindContext, IBindSet, IBindSource, IBindings...
├── Tests/
│   ├── Scaffold.MVVM.Tests.asmdef            # EditMode-only
│   └── TreeBindingDeferredUpdateTests.cs     # 4 tests, deferred-update behavior only
├── GeneratorsMVVM/
│   ├── Community/                            # CommunityToolkit.Mvvm + SourceGenerators DLLs
│   ├── Compiler/                             # System.Runtime.CompilerServices.Unsafe
│   └── Composition/                          # MVVMCompositionGenerator.dll (project-built)
├── README.md
└── package.json                              # depends on com.scaffold.maps + com.scaffold.records
```

Asmdef references: `Scaffold.Maps`, `Scaffold.Records`. Precompiled refs: `CommunityToolkit.Mvvm.dll`, `System.Runtime.CompilerServices.Unsafe.dll`. `autoReferenced: true`.

Test coverage: **single test file**, 4 cases, all about deferred-update timing. No coverage of: registry remove-if-empty semantics, lazy/strict failure paths, converter chain selection, adapter usage, collection diffing, expression path parsing, `BindingPath.Create` edge cases, `RegisterBindCollection` lifecycle. The `BindSets.RegisterAdapter` method is dead code (commented-out body, see issues).

Sample usage lives in the **view** package (`Samples/MVVMUseCases.cs`). No samples in this package.

## 3. What's good

- **Compile-time-typed binding entry**. `IBindings.RegisterBind<TSource,TTarget>(Expression<Func<TSource>>, Expression<Func<TTarget>>, …)` (`Runtime/Contracts/IBindings.cs:10`) and the corresponding generator-emitted `Bind<TSource,TTarget>` (see `MVVMCompositionGenerator.cs:141-149`) keep type safety end-to-end. There is no `Bind("PropertyName", ...)` overload anywhere — that's the right call.
- **INPC is delegated to CommunityToolkit.Mvvm `ObservableObject`** rather than hand-rolled (used by `ViewModel : ObservableObject` in the sibling `com.scaffold.viewmodel` package). This is the correct choice; `[ObservableProperty]` source-gen produces `EqualityComparer<T>.Default` setters that avoid boxing for value types. Compare to MAUI MVVM Toolkit guidance: same library, same idiom (https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/observableproperty).
- **Source-gen-driven composition**. `MVVMCompositionGenerator` (Generators repo) produces both:
  - `INestedObservableProperties.RegisterNestedProperties()` plumbing for `[NestedObservableObject]` types.
  - The whole `Bind/BindCollection/BindConverter/UpdateBinding/ClearBindings` facade for `[BindSource(typeof(TreeBinding))]` types.
  This is exactly the right place for "machinery you'd otherwise hand-write per ViewModel."
- **Per-bind disposal**. `IBindedProperty<,> : IDisposable` and `IBindedCollection<,> : IDisposable` give consumers handles. `BindedProperty.Dispose` (`Runtime/Binding/BindedProperty.cs:105-115`) and `BindedCollection.Dispose` (`Runtime/Binding/BindedCollection.cs:129-147`) clear setters and unsubscribe from `INotifyCollectionChanged`. Good.
- **Strict vs Lazy modes** with `BindingOptions.Strict / Lazy / StrictImmediate` (`Runtime/Binding/BindingOptions.cs:15-19`) are explicit instead of magic flags.
- **Deferred-update coordinator** is cleanly separated behind `IBindingDeferredCoordinator` (`Runtime/Binding/IBindingDeferredCoordinator.cs`) and is overrideable in tests via `DeferredBindingCoroutineHost.ScheduleCore` (`Runtime/Binding/DeferredBindingCoroutineHost.cs:12,24-27`). That seam works.
- **Multi-key registry** via `Map<string, Type, IBindContext>` (`Runtime/Binding/BindRegistry.cs:24`) makes path-collisions across types non-issues.
- Custom analyzer **SCM003** (`Generators/Scaffold.Mvvm.Analyzers/MvvmBindApiAnalyzer.cs`) bans manual `PropertyChanged += …` and direct `UpdateBinding`/`RegisterNestedProperties` calls in MVVM descendants. This is the correct enforcement mechanism for the architecture rules.

## 4. Issues / smells

### 4.1 Engine leaks into a "shared contracts" assembly

`Runtime/Binding/BindedCollection.cs:5` and `Runtime/Binding/BindedProperty.cs:2` directly `using UnityEngine;` and call `Debug.Log("Collection Changed")` (`BindedCollection.cs:156`) and `Debug.LogException(e)` (`BindedProperty.cs:34`). The README explicitly claims the assembly is the shared MVVM contract layer reusable from non-Unity model code. It is not — it pulls `UnityEngine` transitively. The README even hedges about this in the AI Agent Context: "minimal UnityEngine usage only where bind infrastructure logs..." — that hedge is the smell.

`Runtime/Binding/DeferredBindingCoroutineHost.cs:40-58` instantiates a `MonoBehaviour` host with `DontDestroyOnLoad` from a static getter. This forcibly couples binding to a Unity Player Loop, rules out headless use (server, ECS, dotnet test), and silently allocates a hidden `GameObject` when first deferred bind fires.

### 4.2 Setter swallows all exceptions — violates fail-fast

`Runtime/Binding/BindedProperty.cs:30-35`:

```csharp
public void Update(TSource value)
{
    if (setter == null) return;
    try { UpdateCore(value); }
    catch (Exception e) { Debug.LogException(e); }
}
```

Every binding update converts thrown exceptions into log entries. The custom `BindedPropertyUpdateException<,>` type exists in `Runtime/Binding/BindedPropertyUpdateException.cs` but is **never used** — dead code. The architect's stated preference is fail-fast and "no default values that hide errors"; this catch-all is the textbook anti-pattern.

The `setter == null` check is also wrong: in the constructor (`BindedProperty.cs:13-15`) you `throw new ArgumentNullException` if setter is null, so it can only become null in `Dispose`, where the bind is supposed to be detached anyway. If `Update` arrives after `Dispose`, that's a coordinator bug — silently no-oping hides it. Fail-fast says: throw `ObjectDisposedException`.

### 4.3 `TryConvertFallback` boxes value types and silently casts

`Runtime/Binding/BindedProperty.cs:61-72`:

```csharp
private bool TryConvertFallback(TSource sourceValue, out TTarget target)
{
    if (TryConvertString(sourceValue, out target)) return true;
    if (sourceValue is TTarget typedValue) { target = typedValue; return true; }
    bool canCastNull = sourceValue == null && typeof(TTarget) == typeof(TSource);
    target = canCastNull ? (TTarget)(object)sourceValue : default;
    return canCastNull;
}
```

`TryConvertString` (lines 74-83) calls `sourceValue.ToString()` — `NullReferenceException` if `sourceValue` is null and a value type instance is missing (yes, `ToString` is virtual, so for reference types it dies; the method does no null check before deref). The `sourceValue is TTarget typedValue` pattern boxes `TSource` if it's a value type and `TTarget` is a reference type or interface. The `(TTarget)(object)sourceValue` cast on line 70 boxes unconditionally and **returns `default`** when types don't match — exactly the "default value that hides an error" the architect bans. This whole fallback path should be deleted: if the user wants string conversion, they register a `Converter<T,string>`. Implicit fallbacks are a debug nightmare.

### 4.4 `BindSets.RegisterAdapter` is a TODO stub

`Runtime/Binding/BindSets.cs:9-14`:

```csharp
internal void RegisterAdapter<TTarget>(Adapter<TTarget> adapter)
{
    //TODO
    //BindSet<TTarget> bindset = GetSet<TTarget>();
    //bindset.RegisterAdapter(adapter);
}
```

Yet `TreeBinding.RegisterAdapter` (`Runtime/Binding/TreeBinding.cs:107-114`) cheerfully accepts adapters and forwards to this no-op. Public API that **silently does nothing** is the worst kind of bug-hider.

### 4.5 `BindGroups.GetGroup` lazily creates groups during `UpdateBind`

`Runtime/Binding/BindGroups.cs:20-28` creates a new empty `BindGroup` whenever `GetGroup` is hit, including from `TreeBinding.UpdateBind` (`Runtime/Binding/TreeBinding.cs:88-96`). If a consumer calls `UpdateBinding("Typo.Path")`, you allocate a permanent empty group and never garbage-collect it. Stringly-typed slow leak. `UpdateBind` should look up read-only and no-op + log on miss.

### 4.6 Hand-rolled property-path expression parsing

`Runtime/Binding/ExpressionsUtility.cs:23-32` walks `MemberExpression` and string-concatenates with `.`. `BuildPropertyPath` returns `result.Remove(result.Length - 1)` after building `"A.B.C."` — fragile string surgery. The whole `BindingPath` / `BindGroups` / "register at every prefix" structure (`BindGroups.cs:9-18`) flattens paths to strings purely so that the source-gen INPC raises something like `OnPropertyChanged("Foo.Bar")` (see `MVVMCompositionGenerator.cs:88-91`). This means binding subscribers **are** ultimately keyed by string path, so any rename of an observable property without the lambda being re-typed loses bindings silently. A symbol-keyed registry would be safer, but probably not worth the refactor cost — at minimum, hash the path once and cache. Today every `RegisterBind` call recompiles the expression and walks the member chain.

### 4.7 Generator output uses raw string keys at runtime, not lambdas

The generator-emitted `UpdateBinding(string bindKey)` (`MVVMCompositionGenerator.cs:172-175`) is the runtime invalidation API. It is called by `ViewModel.OnPropertyChanged` (`com.scaffold.viewmodel/Runtime/ViewModel.cs:54-58`) and by `ViewElement.OnViewModelChanged` (`com.scaffold.view/Runtime/ViewElement.cs:23-25`). So while the **registration** side is compile-time-typed, the **dispatch** side is stringly-typed and lossy. A property rename via IDE refactor will not update string literals coming from `INotifyPropertyChanged` — you rely on `[ObservableProperty]` to keep names in sync, which works in practice because both ends consume `nameof(field)`-derived names. Acceptable; document it explicitly.

### 4.8 Inconsistent guard-clause style — exactly the redundancy the architect dislikes

Files mix two formats. Compare:

`Runtime/Binding/BindGroups.cs:32-39` (broken indentation on purpose, copy-pasted):

```csharp
if (path is null)
{
    throw new ArgumentNullException(nameof(path));
}
if (context is null)
{
    throw new ArgumentNullException(nameof(context));
}
```

vs `Runtime/Binding/BindRegistry.cs:28`:

```csharp
if (source is null) throw new ArgumentNullException(nameof(source));
```

`BindGroups.Unregister` even has nested-method indentation broken — the generator emitted those literally. Most of these ArgumentNullExceptions are at internal callsites that already validated the argument upstream. `BindContext.Bind` re-checks `binding`/`options` in `CreateRegistration` (`Runtime/Binding/BindContext.cs:47-58`) when both are guaranteed non-null because `TreeBinding.RegisterBind` (`Runtime/Binding/TreeBinding.cs:33-57`) already checked. Pure duplicate guards. Trim to entry points only: the public `IBindings.Register*` methods on `TreeBinding`. Internals should assume invariants.

### 4.9 `BindContext.TryGetValue` rethrows after catching

`Runtime/Binding/BindContext.cs:118-125`:

```csharp
try { value = source(); return true; }
catch (NullReferenceException ex)
{
    if (HasStrictBind()) throw ex;
    value = default;
    return false;
}
```

Catching `NullReferenceException` to implement "lazy mode" is wrong on three counts: (a) catching NRE is forbidden by FxCop and CA2200 warns about `throw ex` losing the stack, (b) any NRE inside the user expression — even from the user's own lambda body — is treated as a missing binding root, (c) it's slow (exception construction). Replace with explicit null-walk on the expression chain at compile time, or check the parent receiver before invoking. `throw ex` should be `throw;` regardless.

### 4.10 `BindedCollection.Update()` (no-arg) is dead and lies via `Debug.Log`

`Runtime/Binding/BindedCollection.cs:149-157`:

```csharp
public void Update()
{
    if (source == null) return;
    Debug.Log("Collection Changed");
}
```

Doesn't do anything useful, isn't part of `IBind<ICollection<TSource>>` (the interface uses `Update(ICollection<TSource>)`). Confusing dead method.

### 4.11 `IBindSource` and `IBindings` overlap; dual contracts are unclear

`Runtime/Contracts/IBindSource.cs` and `Runtime/Contracts/IBindings.cs` expose nearly the same surface with different naming (`Bind` vs `RegisterBind`, `BindCollection` vs `RegisterBindCollection`, `BindConverter` vs `RegisterConverter`). The generator emits the `IBindSource`-style names on the user's class but delegates to `IBindings`. Two parallel contracts for the same thing — pick one. Consumers will not know which to depend on.

### 4.12 `Adapter<TTarget>.CanAdapt` and `Converter<TSource,TTarget>.CanConvert` default-return-true with a null guard

`Runtime/Binding/Adapter.cs:5-12` and `Runtime/Binding/Converter.cs:5-12` both define `CanX` as "non-null = true". Consumers write subclasses with `Convert(value)` body, and at runtime `BindSet.TryConvertWithConverters` (`Runtime/Binding/BindSetT2.cs:38-48`) iterates `converters` calling `CanConvert` then `Convert`. The "first match wins" is implicit and order-dependent on registration. If a consumer registers two converters from `int → string`, only the first one ever fires. There's no warning, no analyzer. Fine for prototype; will surprise people in production.

### 4.13 `INotifyCollectionChanged` subscription leak risk

`Runtime/Binding/BindedCollection.cs:50-65`: when `Update` is called with a new collection that is itself `INotifyCollectionChanged`, the code unsubscribes from the **old** `source` (line 41-44) but not from the new one if `Update` is called again with `value == source`'s previous value pattern — actually the current code paths are okay because of the early `ReferenceEquals` check on line 36, *but* `Dispose` (line 138-140) only unsubscribes from `source`. If the bind handler swaps an already-tracked instance through `ReplaceSourceCollection`, the old subscription is dropped before being unhooked because line 51 does `ClearTargets` then attempts to `+=` after `-=` on the NEW collection (line 55-56) — note `oldObservable.CollectionChanged -= …` is called on `source` (the old one) at line 43. Logic works; but the `-=` on line 55 right before `+=` looks defensive against the same handler being subscribed twice. If `value` was already subscribed from a previous registration that did not go through this path, you'd still get the duplicate. Tighten by tracking the currently-subscribed observable explicitly.

### 4.14 `WithConverter`/`WithAdapter` mutate after registration without reapplying

`Runtime/Binding/BindedProperty.cs:85-103`: `WithConverter` and `WithAdapter` set the field but do not trigger a re-update with the current source value. So if a binding is registered with `BindingOptions.Strict` (immediate apply) and you fluent-chain `.WithConverter(...)` afterward, the first-applied value used the fallback path, and only future updates use the converter. Either reapply on configure, or require converters be passed at `RegisterBind` time.

### 4.15 `TreeBinding.Unbind` does not detach `INotifyCollectionChanged` subscriptions

`Runtime/Binding/TreeBinding.cs:116-132` calls `bindSets.Clear()`, `groups.Clear()`, `registry.Clear()`. `BindRegistry.Clear` (`Runtime/Binding/BindRegistry.cs:59-67`) calls `context.Unbind()` for each context, and `BindContext.Unbind` (`Runtime/Binding/BindContext.cs:148-157`) calls `DisposeBinds`. `DisposeBinds` invokes `IDisposable.Dispose` on each `IBind`, which for `BindedCollection` *does* unsubscribe — good. But the `coordinator.IsUnbinding` flag is set true during this whole tree-unbind, and `BindedProperty.Dispose` calls `detach?.Invoke()` which calls back into `TreeBinding.DetachBind` which short-circuits because `isUnbinding` is true (`TreeBinding.cs:80-86`). So per-bind handles get disposed but the registry/groups bookkeeping doesn't run for them — that's fine because `Clear` wipes all maps. Just brittle.

### 4.16 Generator produces a base `RegisterNestedProperties` per derived class — duplicates per inheritance level

`MVVMCompositionGenerator.cs:99-108` (`GetNestedChildClassBody`): each child class emits its own `RegisterNestedProperties` with `base.RegisterNestedProperties()`. If a class hierarchy has 5 levels and only one introduces nested fields, the generator emits 5 `_nested.g.cs` files, four of which only call `base`. Wasteful but not wrong. Cap emission to classes that actually declare tracked fields or `[NestedObservableObject]`.

### 4.17 `ToPascalCase` in the generator is duplicating work CommunityToolkit.Mvvm already does

`MVVMCompositionGenerator.cs:360-392` reimplements field-to-property name conversion. CommunityToolkit's `[ObservableProperty]` already exposes the public name; the generator could read the property symbol from the type instead of guessing from the field name. Minor.

### 4.18 No tests for the lazy/strict semantics, converter pipeline, or path parsing

The single test file covers deferred-update behavior. The behaviors most likely to break in real use (lazy null traversal, converter-chain fallthrough, collection diffing on `Reset` vs `Add`/`Remove`) are uncovered.

## 5. Suggested before/after

### Fail-fast in `BindedProperty.Update`

**Before** (`Runtime/Binding/BindedProperty.cs:30-35`):

```csharp
public void Update(TSource value)
{
    if (setter == null) return;
    try { UpdateCore(value); }
    catch (Exception e) { Debug.LogException(e); }
}
```

**After:**

```csharp
public void Update(TSource value)
{
    if (disposed) throw new ObjectDisposedException(nameof(BindedProperty<TSource, TTarget>));
    UpdateCore(value);
}
```

Let exceptions bubble. The `TreeBinding` deferred coordinator already isolates per-context, so a single bad converter cannot poison unrelated bindings if the coordinator catches at the boundary (it currently does not — adding a single try/catch in `BindContext.UpdateImmediateBinds` with a *typed* `BindedPropertyUpdateException` rethrow is the right place).

### Drop the implicit string fallback

**Before** (`Runtime/Binding/BindedProperty.cs:50-83`): everything from `TryConvertValue` onwards.

**After:**

```csharp
private bool TryConvertValue(TSource sourceValue, out TTarget target)
{
    if (converter is { } c && c.CanConvert(sourceValue))
    {
        target = c.Convert(sourceValue);
        return true;
    }
    if (binding.TryConvert(sourceValue, out target)) return true;

    if (sourceValue is TTarget typedValue)
    {
        target = typedValue;
        return true;
    }

    target = default;
    return false;
}
```

No `ToString` magic. No null cross-cast. Callers register a `Converter<T, string>` explicitly when they want stringification. That's three lines saved and a class of bugs gone.

### Engine-free contract assembly

Move `DeferredBindingCoroutineHost` and the `Debug.Log`/`Debug.LogException` calls out of `Scaffold.MVVM` into a thin Unity-side adapter. Concretely:

- New asmdef `Scaffold.MVVM.UnityRuntime` (`noEngineReferences: false`).
- Move `DeferredBindingCoroutineHost.cs` and `BindingOptions.UpdateTiming` host wiring there.
- Replace `Debug.Log…` in `BindedProperty`/`BindedCollection` with `IBindingDiagnostics`; default no-op; Unity adapter wires `Debug.LogException`.
- Set `Scaffold.MVVM.asmdef`'s `noEngineReferences: true`.

This unlocks server-side / unit-test usage of the Model + ViewModel layer without spinning up a Unity Player Loop, which is the architect's stated boundary.

### Replace dead `BindedPropertyUpdateException` usage

If you keep the catch (you shouldn't), at least produce the typed exception:

```csharp
catch (Exception inner)
{
    throw new BindedPropertyUpdateException<TSource, TTarget>(value, setter, inner);
}
```

Otherwise delete `BindedPropertyUpdateException.cs` — dead code.

### `UpdateBind` should not allocate on miss

**Before** (`Runtime/Binding/TreeBinding.cs:88-96`):

```csharp
public void UpdateBind(string bindKey)
{
    if (string.IsNullOrWhiteSpace(bindKey)) return;
    BindGroup group = groups.GetGroup(bindKey);
    group.NotifyBindingKeyChanged();
}
```

**After:**

```csharp
public void UpdateBind(string bindKey)
{
    if (string.IsNullOrWhiteSpace(bindKey)) return;
    if (!groups.TryGetExistingGroup(bindKey, out BindGroup group)) return;
    group.NotifyBindingKeyChanged();
}
```

Add `TryGetExistingGroup` to `BindGroups` that does a read-only lookup without creating.

## 6. Easy wins (each <30 min)

1. Delete `BindedPropertyUpdateException.cs` (`Runtime/Binding/`) — it has no callers. Or wire it in.
2. Delete the empty `BindedCollection.Update()` overload (`Runtime/Binding/BindedCollection.cs:149-157`).
3. Throw `NotImplementedException` in `BindSets.RegisterAdapter` (`Runtime/Binding/BindSets.cs:9-14`) until implemented; keep the API honest.
4. Replace `throw ex;` with `throw;` in `BindContext.TryGetValue` (`Runtime/Binding/BindContext.cs:121`) — preserves stack.
5. Strip duplicate `ArgumentNullException` checks at every internal callsite. Keep them on the public `TreeBinding.Register*` methods only. Internal callers already know.
6. Re-format the malformed indentation in `BindGroups.Unregister` (`Runtime/Binding/BindGroups.cs:32-39`) and `BindSetT2.cs:13-15`/`21-24` — looks like an editor accident.
7. Add `BindingPath.Create("")` and `BindingPath.Create(".")` tests; that path-builder is unguarded against malformed input despite the validation method.
8. Pick **one** of `IBindSource` and `IBindings` and remove the duplicate. The generator already binds to both.

## 7. Bigger refactors

### R1. Split engine from contract assembly (1-2 days)

As above: `Scaffold.MVVM` becomes engine-free, `Scaffold.MVVM.UnityRuntime` provides the coroutine host and diagnostics adapter. Unblocks the architecture rule that pure-C# assemblies should not transitively pull `UnityEngine`.

Compare to **R3** (https://github.com/Cysharp/R3) and **MessagePipe** (https://github.com/Cysharp/MessagePipe), both of which ship engine-agnostic cores with separate Unity adapter packages — the canonical Unity OSS pattern.

### R2. Replace `Expression<Func<T>>` registration with weak-event observers (3-5 days, optional)

The current model: register lambda → compile → store path → on `OnPropertyChanged(name)` raise group → invoke setter. This re-derives what `INotifyPropertyChanged` already tells you. Two alternatives worth comparing:

- **R3.Observable** style: each `[ObservableProperty]` exposes an `Observable<T>` field. Bind = `Subscribe(setter)`. Disposes via `IDisposable`. No expression compilation, no path string, no registry. The source generator can emit these from `[ObservableProperty]` with one extra method per property.
- **Avalonia/MAUI-style compiled bindings**: `x:Bind` produces statically-typed callbacks; the closest we'd come in C# is what your generator already does — `Action<TTarget>` setters. No string path is even needed if the generator emits per-property `BindFoo(Action<int>)` methods on the source class.

The second is **more in line with the architect's "compile-time typing" preference** and removes most of the registry. The expression-tree path is the C# 4.0 idiom (https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) and works, but in 2026 a typed-callback per property is cheaper, more refactor-safe, and avoids the entire `BindGroups`/`BindingPath` codepath. Treat as a research spike.

### R3. Symbol-keyed bind registry instead of string paths

If R2 is too heavy, at minimum hash `(Type, propertyName)` once at registration and key `BindGroups` by `int` (or by a struct key). String hashing on every `OnPropertyChanged` is fine but loses the deduplication benefits.

## 8. Organization & docs

- README is thorough and standards-compliant. Good.
- The README claim "Allowed Dependencies: ... minimal UnityEngine usage only where bind infrastructure logs or integrates with Unity diagnostics" (line 124) is hedge-language for an architecture violation. Either commit to engine-aware (rename the package) or extract the engine code (R1).
- The package depends on `com.scaffold.maps` and `com.scaffold.records` per `package.json`, but `Scaffold.Records` does not appear in `Runtime/Scaffold.MVVM.asmdef.references` *and* is unreferenced from any source file in `Runtime/`. Drop the dependency.
- Sample code lives in `com.scaffold.view/Samples/MVVMUseCases.cs`. That's odd — a sample exercising MVVM contracts should sit in this package and be view-free. Move or duplicate.
- Generator authoring source (`/home/user/Scaffold/Generators/MVVMCompositionGenerator/MVVMCompositionGenerator.cs`) lives outside the package; the compiled DLLs ship inside (`GeneratorsMVVM/Composition/MVVMCompositionGenerator.dll`). Document the build pipeline in the package README — right now it's invisible to consumers.
- No XML doc comments anywhere. The public binding surface is the most-used API in the project — at least `IBindings`, `IBindedProperty<,>`, `BindingOptions`, `BindingUpdateTiming` should have triple-slash docs.

## References

- Microsoft .NET MAUI MVVM Toolkit (`[ObservableProperty]`, `[RelayCommand]`): https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/
- ReactiveUI WhenAnyValue / typed bindings: https://www.reactiveui.net/docs/handbook/when-any/
- Avalonia compiled bindings (`x:CompileBindings`): https://docs.avaloniaui.net/docs/data-binding/compiled-bindings
- Cysharp R3 (Rx for .NET, Unity-friendly): https://github.com/Cysharp/R3
- Cysharp MessagePipe (Pub/Sub for in-memory & DI): https://github.com/Cysharp/MessagePipe
- Unity UI Toolkit data bindings (string-path-based): https://docs.unity3d.com/Manual/UIE-data-binding.html (good counter-example: this is the stringly-typed approach you correctly avoided)
- MVVMCommunityToolkit performance considerations re: `EqualityComparer<T>.Default`: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/observableproperty#observable-property-with-validation
