# com.scaffold.scope

# Scaffold Infra Scope

## TL;DR

- Purpose: reusable layered startup orchestration for application bootstrap.
- Location: `Assets/Packages/com.scaffold.scope/Runtime/` and `Assets/Packages/com.scaffold.scope/Runtime/Contracts/`.
- Depends on: BCL plus `VContainer` / `VContainer.Unity`.
- Used by: `Scaffold.Bootstrap.Runtime` and services that initialize during layered startup.

## Responsibilities

- Owns `LayeredScope` startup lifecycle.
- Owns `LayerInstallerBase` recursive layer composition and build pipeline.
- Defines initialization contracts and analyzer exception attributes.
- Does not own feature-specific business rules.

## Lifecycle Order

`LayerInstallerBase` pipeline order is now:

1. `InitializeAsync(...)`
2. `OnCompletedAsync(...)`
3. Optional `ILayeredScopeProgress.OnLayerPipelineStep(...)` (once per installer node, depth-first pre-order; `completedLayerIndex` is 1-based through `totalLayers`)
4. `BuildChildrenAsync(...)`

This order allows a parent installer to prepare data in `OnCompletedAsync` before child scopes are created.

**Layer progress:** `LayeredScope` can supply an optional `ILayeredScopeProgress` listener (typically a `MonoBehaviour` assigned in the Inspector). The scope counts `LayerInstallerBase` nodes before `BuildAsRootAsync` and reports one step per node after that node’s `OnCompletedAsync` completes. If the listener is null, no callbacks are made.

## Public API

| Symbol | Purpose | Inputs | Outputs | Failure behavior |
|---|---|---|---|---|
| `LayeredScope` | Coordinates startup with one root layer tree. | Root installer tree + cancellation token. | Initialized final scope and startup completion signal. | Throws on null tree root or startup failures. |
| `LayerInstallerBase` | Recursive installer with deterministic pipeline. | Parent scope and cancellation token. | Built child scope subtree. | Throws on invalid tree topology or initializer failures. |
| `ICrossLayerObjectResolver` | Resolves services and performs injection across all built layer scopes. | Registered layer resolvers + requested type or target instance. | Instance from deepest matching scope or injected object graph. | Throws when `Resolve*` cannot find the type in any registered layer or `Inject` fails in all layers. |
| `IAsyncLayerInitializable.InitializeAsync(IObjectResolver, CancellationToken)` | Async startup contract for layer services. | Resolver and cancellation token. | Startup completion signal. | Cancellation propagates; non-cancellation failures are wrapped by startup orchestration. |
| `ILayeredScopeProgress.OnLayerPipelineStep(int completedLayerIndex, int totalLayers)` | Optional UI or telemetry hook for layered build progress. | 1-based step index and total layer count. | None. | Must be cheap; implementations that touch Unity UI should marshal to the main thread. |
| `IStartupOrderParticipant` | Marker for types that participate in **startup order grouping** (no members). | None. | N/A | N/A |
| `IStartupOrderGroupProvider` | Computes topological **levels** of participant concrete types from inject sites + `TryGetRegistration` (ordering only; no initialization). | `IObjectResolver` or pre-resolved participant list + resolver. | `IReadOnlyList` of levels (`Type` groups). | Throws when the participant subgraph has a cycle. |
| `StartupOrderGroupProvider` | Default implementation of `IStartupOrderGroupProvider`. | Same as interface. | Ordered type groups. | Same as interface. |
| `StartupOrderTopologicalLevels` | Kahn-style levelization for a type set + edge list. | Instance `Compute` method. | Parallel-safe levels. | Throws on cycle. |

## Best Practices

- Keep startup layers explicit and deterministic.
- Treat `IAsyncLayerInitializable` as startup-only contract.
- Keep initializers side-effect bounded and idempotent.
- Use `OnCompletedAsync` for parent-owned data needed by child registration.

## Testing

- Test assemblies:
  - `Scaffold.Scope.Tests`
  - `Scaffold.Bootstrap.Tests`

Run:

```powershell
& ".\.agents\scripts\run-editmode-tests.ps1" -AssemblyNames "Scaffold.Scope.Tests"
& ".\.agents\scripts\run-editmode-tests.ps1" -AssemblyNames "Scaffold.Bootstrap.Tests"
```

## Related

- `../com.scaffold.bootstrap/README.md`
- `../../../Architecture.md`
- `../../../Docs/Testing/Testing.md`

## Changelog

- Added `IStartupOrderParticipant`, `IStartupOrderGroupProvider`, `StartupOrderGroupProvider`, and `StartupOrderTopologicalLevels` for dependency-derived startup **order groups** (prototype; no async init execution). `StartupOrderInjectSiteAnalyzer` calls VContainer’s internal `TypeAnalyzer.AnalyzeWithCache` via reflection so dependency sites match circular-dependency analysis (re-verify when upgrading VContainer).
- Added `ICrossLayerObjectResolver` for resolving services from any built layer scope (deepest-first).
- Documented optional `ILayeredScopeProgress` and pipeline step after `OnCompletedAsync` before `BuildChildrenAsync`.
- Updated pipeline order to `InitializeAsync -> OnCompletedAsync -> BuildChildrenAsync` to support parent completion data before child creation.
- Kept recursive installer model and initialization contracts unchanged.
