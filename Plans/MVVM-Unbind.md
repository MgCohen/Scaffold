## Reference-Based Individual Unbind for MVVM Bindings

### Summary
Implement per-binding unbind using direct object references (no IDs), with `IDisposable` handles as the primary API.  
A bind registration returns a disposable binding object; disposing it detaches only that registration from its context/group. Full `Unbind()` remains as clear-all lifecycle teardown.

### Key Implementation Changes
- **Public contract updates**
  - Make `IBindedProperty<TSource, TTarget>` extend `IDisposable` so property binds are individually unbindable via `Dispose()`.
  - Keep `IBindedCollection<TSource, TTarget> : IDisposable` as-is.
  - Update generated `IBindSource` API so `BindCollection<TSource, TTarget>(...)` returns `IBindedCollection<TSource, TTarget>` (not `void`) for symmetry.
- **Source generator updates**
  - Ensure generated bind-source methods expose:
    - `Bind(...) -> IBindedProperty<,>`
    - `BindCollection(...) -> IBindedCollection<,>`
  - Keep the recent cleanup behavior (auto-implement `IBindSource` for `[BindSource]`, single `bindings` accessor, no `__` field names).
- **Binding runtime: reference-based detach flow**
  - Add a bind-registration path in `BindRegistry`/`TreeBinding` that registers a concrete bind instance and returns a **detach callback** for that exact instance.
  - `BindedProperty` and `BindedCollection` store this callback and invoke it in `Dispose()` (idempotent).
  - `BindContext<T>` gains targeted removal by bind reference (`RemoveBinding(IBind<T>)`) plus `IsEmpty`.
- **Context/group cleanup on last reference**
  - When a single binding is disposed and its context becomes empty:
    - remove that context from registry maps;
    - unregister it from all `BindGroups` entries for the binding path.
  - Add unregister support to `BindGroup`/`BindGroups` so no stale context references remain after per-binding dispose.
- **Behavioral rules**
  - `Dispose()` is safe to call multiple times.
  - Disposed binding no longer receives updates.
  - `Unbind()` still clears everything and disposes remaining active bindings.
  - Existing caller code that ignores bind return values keeps working unchanged.

### Test Plan
- **Property bind disposal**
  - Register one bind, dispose it, update source key, verify target no longer updates.
- **Collection bind disposal**
  - Register collection bind, dispose it, mutate source collection, verify handler no longer receives add/remove callbacks.
- **Selective disposal**
  - Register two binds on same source path; dispose one; verify only remaining bind updates.
- **Idempotency**
  - Dispose same bind twice; no throw, no duplicate unregister side effects.
- **Context/group cleanup**
  - After disposing the last bind in a context, updating that path does not invoke removed context work and does not throw.
- **Generated API regression**
  - Compile-time/runtime test proving `[BindSource]` class exposes returning `BindCollection(...)` and still auto-implements `IBindSource`.
- **Full teardown compatibility**
  - `TreeBinding.Unbind()` still stops all updates and clears active registrations.

### Assumptions and Defaults
- Chosen API direction (per your selections):
  - Primary flow: `IDisposable` handles.
  - Property handles: `IBindedProperty` becomes disposable (no external wrapper type required).
  - Collection flow: returning handle from generated `BindCollection(...)`.
- No ID-based unregister APIs will be introduced.
- No expression-based `Unbind(() => source, () => target)` in this iteration; can be added later as optional convenience over reference disposal.
- Implementation should remain backward-compatible for existing call sites that do not capture bind return values.
