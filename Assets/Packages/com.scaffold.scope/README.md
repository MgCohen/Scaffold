# com.scaffold.scope

## Purpose

Startup orchestration for VContainer: **two-scope** flow (`TwoScopeApplicationHost`), dependency-graph **async initialization** (`AsyncInitializationRunner` + internal graph builder), optional **startup progress** data (`ApplicationStartupProgress` for wiring your own UI), **cross-scope** resolve/inject for presentation, and a **legacy** parallel pass for `IAsyncLayerInitializable` on the main scope.

## Layout (`Runtime/`)

| Folder | Contents |
|--------|----------|
| **`Contracts/`** | `IAsyncInitializable`, `IAsyncInitializationRunner`, `IApplicationStartupProgress`, `ICrossLayerObjectResolver`, `IAsyncLayerInitializable`, analyzer attributes |
| **`InitializationGraph/`** | Internal **dependency graph** from VContainer inject sites: `InitializationGraphBuilder`, `TopologicalLevels`, `InjectSiteAnalyzer`; **`AsyncInitializationRunner`** (public) |
| **`CrossLayer/`** | `CrossLayerObjectResolver` — aggregates registered `IObjectResolver` scopes for cross-scope `Inject` / `TryResolve` (e.g. navigation opening views) |
| **`Host/`** | `TwoScopeApplicationHost`, `ApplicationStartupProgress` |

## Depends on

- `jp.hadashikick.vcontainer` (VContainer)

## Tests

- EditMode: `Scaffold.Scope.Tests`

```powershell
& ".\.agents\scripts\run-editmode-tests.ps1" -AssemblyNames "Scaffold.Scope.Tests"
```

## Related docs

- [Docs/App/AppStartup.md](../../../Docs/App/AppStartup.md)
