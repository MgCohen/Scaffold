## Explicit Lazy Binding Mode for Deferred Instances

### Summary
Use an optional `BindingOptions` parameter with `Lazy` semantics so existing binds stay unchanged, while deferred-instance binds can register safely without immediate source evaluation.

### Key Changes
- Add a public `BindingOptions` type (MVVM binding) with:
  - `bool Lazy` (default `false`)
- Extend public binding APIs with optional options parameter (default `null`) so current callsites are unchanged:
  - `IBindings.RegisterBind(...)`
  - `IBindSource.Bind(...)`
  - generated bind-source methods from `ObservableNestedPropertiesGenerator`
- Implement lazy behavior in binding context:
  - If `Lazy = false`: keep current behavior (evaluate on bind, push initial value).
  - If `Lazy = true`: do not evaluate source getter at bind registration.
  - First evaluation happens only when `UpdateBinding(...)` path updates occur.
- In lazy mode, handle unresolved deferred chains by catching only `NullReferenceException` from source getter evaluation and skipping setter update until the path becomes valid.
- Keep converter/adapter and non-null-path errors unchanged (still surfaced).

### Test Plan
- Regression test: bind to `() => vm.LateInstance.Value` where `LateInstance` starts null, with `Lazy = true`.
  - Verify bind registration does not evaluate/throw.
  - Verify no target update while unresolved.
  - Assign `LateInstance`, raise/update parent bind key, verify target updates.
- Test strict default path (`Lazy = false`) still behaves exactly as before.
- Test lazy mode does not swallow non-null-path exceptions.
- Run `.agents/scripts/run-editmode-tests.ps1` and `.agents/scripts/check-analyzers.ps1`.

### Assumptions
- `UpdateBinding(...)` is the single trigger for first evaluation in lazy mode (as chosen).
- Parent property change notifications fire when deferred instance is assigned.
- Scope is property binds (`RegisterBind`), not collection binds in this pass.
