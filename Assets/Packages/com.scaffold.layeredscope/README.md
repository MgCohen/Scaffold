# com.scaffold.layeredscope

UPM package: **stacked VContainer `LifetimeScope` layers** with ordered install, optional `PrepareAsync` against the parent resolver, `IAsyncInitializable` after each child scope is built, and async teardown via `IAsyncDisposable`.

## Layout

- `Runtime/` — `Scaffold.LayeredScope` assembly (`ApplicationHost`, `ApplicationBootstrap`, contracts).
- `Tests/Editor/` — edit-mode tests (`Scaffold.LayeredScope.Tests`).
- `Samples/` — optional sample scene and layers (`Scaffold.LayeredScope.Samples`, not auto-referenced).

## Dependencies

- [VContainer](https://github.com/hadashiA/VContainer) (`jp.hadashikick.vcontainer`), declared in `package.json`.

## Integration

- Subclass `ApplicationBootstrap` on a root `LifetimeScope`, or construct `ApplicationHost` with an existing root scope.
- Implement `IScopeLayer` (and `IAsyncScopeLayer` when you need `PrepareAsync`).
- Register services as `IAsyncInitializable` when they must run after the layer’s container is created.

See the consuming project’s documentation for full bootstrap examples (e.g. Meta / Campaign application hosts).
