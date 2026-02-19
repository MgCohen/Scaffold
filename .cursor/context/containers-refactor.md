# Containers refactor – context notes

Context captured after completing the Container library refactor. Use this for future work on the `Scaffold.Containers` assembly and the sample Bootstrap.

---

## 1. Current state (post-refactor)

The refactor is **complete**. The assembly is fully DI-agnostic in its public surface.

- **`Boostrap`** inherits `MonoBehaviour`. `Start()` calls `GetAdapter().Run(transform, Build)`. `GetAdapter()` is `protected virtual` and returns `new VContainerAdapter()` by default — overridable for testing or alternative backends.
- **`Container`** exposes `protected virtual void Build(IContainerRegistry registry, Transform holder)`. An `internal void BuildInternal(IContainerRegistry, Transform)` trampoline is used by the adapter since `protected` members are not accessible from non-subclasses. No VContainer references.
- **`Context`** is `internal`. Holds `private IContainerScope scope`. Root constructor `internal Context(IContainerScope)`. `internal void SetScope(IContainerScope)` is called by the adapter after `BuildChild`. `Build(Container, Context parent)` calls `parent.scope.BuildChild(container, childContext, parent.scope.Transform)`.
- **`Installer`** exposes `public abstract void Install(IContainerRegistry registry, Transform holder)`.
- **`IContainerRegistry`** (public) is the renamed replacement for the old `IContainerBuilder`.
- **`IContainerScope`** (internal) carries `Transform Transform { get; }` and `BuildChild(Container, Context, Transform)`. The `Transform` property lets `Context` pass the correct holder without knowing VContainer types.
- **`VContainerAdapter`** (internal) creates a disabled child `GameObject` on `root`, adds a nested `VContainerRootScope : LifetimeScope` to it, sets the build callback, then enables the GO so VContainer's `Awake → Configure` fires. All LifetimeScope usage is contained inside this one class.
- **`VContainerScope`** (internal) wraps `LifetimeScope`. `BuildChild` uses `_scope.CreateChild(...)`, registers `IContext` and `IContainerResolver` inside the child scope, calls `container.BuildInternal(registry, holder)`, then sets the child scope on the context via `childContext.SetScope(new VContainerScope(childScope))`.
- **`Runtime/Adapters/`** folder is gone entirely. All adapter logic is in `Runtime/Internal/`.
- **`NavigationInstaller`** and **`EventsInstaller`** had their `Install` signatures updated to `IContainerRegistry` (they subclass `Installer`).

---

## 2. Architecture

- **Public surface** (zero VContainer references):  
  `Bootstrap : MonoBehaviour`, `IContainerAdapter`, `IContainerRegistry`, `IContainerResolver`, `IRegistrationBuilder<T>`, `IContext`, `Container`, `Installer`, `ContainerLifetime`.

- **Internal plumbing**:  
  `IContainerScope` (internal, in `Runtime/Abstractions/`), `Context` (internal), and all VContainer adapter types in `Runtime/Internal/`.

- **Adapter entry point pattern**:  
  `Container.protected virtual Build` is the user hook. `Container.internal BuildInternal` is the trampoline the adapter calls, since `protected` is not accessible outside the type hierarchy. This mirrors the original `internal LifetimeScope Build(LifetimeScope, Context)` pattern.

- **VContainer scope lifecycle**:  
  Root scope created by `VContainerAdapter` on a hidden child GO. Child scopes created via `LifetimeScope.CreateChild(...)` inside `VContainerScope.BuildChild`. `ChangeContext` disposes the current `IContainerScope` via `scope.Dispose()`.

---

## 3. File map (final state)

All paths under Containers are relative to `Assets/Scripts/Infra/Containers/`.

| Action    | Path |
|-----------|------|
| Added     | `Runtime/Abstractions/IContainerAdapter.cs` |
| Added     | `Runtime/Abstractions/IContainerScope.cs` (internal) |
| Added     | `Runtime/Abstractions/IContainerRegistry.cs` |
| Removed   | `Runtime/Abstractions/IContainerBuilder.cs` |
| Removed   | `Runtime/Adapters/` (entire folder) |
| Added     | `Runtime/Internal/VContainerAdapter.cs` |
| Added     | `Runtime/Internal/VContainerScope.cs` |
| Added     | `Runtime/Internal/VContainerRegistry.cs` |
| Added     | `Runtime/Internal/VContainerResolver.cs` |
| Added     | `Runtime/Internal/VContainerRegistrationBuilder.cs` |
| Added     | `Runtime/Internal/NoOpRegistrationBuilder.cs` |
| Edited    | `Runtime/Implementation/Boostrap.cs` |
| Edited    | `Runtime/Implementation/Context.cs` |
| Edited    | `Runtime/Implementation/Container.cs` |
| Edited    | `Runtime/Implementation/Installer.cs` |
| Edited    | `Assets/Samples/Container/Boostrap/SampleBoostrap.cs` |
| Edited    | `Assets/Scripts/Infra/Navigation/Container/Runtime/NavigationInstaller.cs` |
| Edited    | `Assets/Scripts/Infra/Events/Container/Runtime/EventsInstaller.cs` |
| No change | `Runtime/Scaffold.Containers.asmdef` |
| No change | `Runtime/Abstractions/IContainerResolver.cs` |
| No change | `Runtime/Abstractions/IRegistrationBuilder.cs` |
| No change | `Runtime/Abstractions/IContext.cs` |
| No change | `Runtime/Implementation/ContainerLifetime.cs` |
