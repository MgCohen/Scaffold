# Scaffold cleanup — Foundation installers (Addressables / Navigation)

Handoff doc for the [Scaffold](https://github.com/MgCohen/Scaffold) project. Captures four package-side fixes that consumer apps cannot do from their own repos. Source observations come from `Gear-Engine`'s [`FoundationLayer`](../Assets/GearEngine/Scripts/App/Bootstrap/Layers/FoundationLayer.cs) and [`SceneFoundationScope`](../Assets/GearEngine/Scripts/Core/SceneFoundation/Bootstrap/SceneFoundationScope.cs).

## TL;DR

A consumer that wants to install Addressables + Navigation + Events should write **three installer calls**, with no leaking of internals. Today the consumer has to:

- Re-implement `AddressablesInstaller` inline because **`IAddressablesAssetClient` is not registered** as a resolvable singleton (only passed via `WithParameter` into the gateway), while publishers need to resolve it.
- Manually `RegisterInstance(navigationSettings)` before calling `NavigationInstaller` (optional convenience — installer can register when the host already has a reference).
- Register a hand-rolled no-op `IViewControllerDependencyInjector` because VContainer does not honor C# default parameter values when no binding exists.

All three are package-shaped problems. Addressing them lets `FoundationLayer` (AppFlow) and `SceneFoundationScope` (LifetimeScope) collapse to the same canonical install sequence.

## Corrections to earlier framing

1. **`AddressablesInstaller` already registers `IAsyncInitializable`.** [Assets/Packages/com.scaffold.addressables/Container/AddressablesInstaller.cs](Assets/Packages/com.scaffold.addressables/Container/AddressablesInstaller.cs) maps the gateway as `IAddressablesGateway` and `IAsyncInitializable`. There is no separate “VContainer startable” path in that installer.
2. **`IAssetReferenceHandler` is not a container binding today** — it is only threaded into `AddressablesGateway` via `WithParameter`, not exposed as something consumers resolve. The cleanup internalizes the contract type so it is not public API surface.
3. **`IScopeLayer.Install` and `IInstaller.Install` share the same signature** — `AddressablesInstaller` is already callable from AppFlow layers; the duplicate consumer wiring is about the asset client, not lifecycle bridging.

## Current consumer pain (evidence)

### 1. Hand-rolled Addressables wiring inside the AppFlow host

```29:30:Assets/GearEngine/Scripts/App/Bootstrap/Layers/FoundationLayer.cs
            builder.RegisterInstance<IAddressablesAssetClient>(assetClient);
            builder.RegisterInstance<IAssetReferenceHandler>(refHandler);
```

Consumer re-registers the asset client because the package installer never registers `IAddressablesAssetClient` on the container.

### 2. `IAssetReferenceHandler` as public contract

It lives in `Runtime/Contracts/` but is only used inside the gateway assembly implementation. It should be **internal** (`Scaffold.Addressables.Internal`) so external code does not depend on it.

### 3. `RegisterInstance(navigationSettings)` duplicated at call sites

Hosts can pass settings into `NavigationInstaller(holder, settings)` when they have a reference; otherwise settings may arrive via another path (publisher pipeline, parent scope). The installer registers settings **only when** the optional parameter is non-null.

### 4. Consumer-side no-op `IViewControllerDependencyInjector`

The navigation runtime needs a binding. **`NavigationInjection`** (already registered via `AsImplementedInterfaces()`) also implements **`IViewControllerDependencyInjector`**, using the same `ILayerResolver` + `Top.Inject` path as open-time injection — one instance, no extra types, no fluent overrides.

## Proposed scaffold-side changes

### CLEANUP-01 — `AddressablesInstaller` exposes `IAddressablesAssetClient` and internalizes the reference handler

**Goal:** one installer call; publishers resolve `IAddressablesAssetClient`; gateway still initializes via `IAsyncInitializable`.

- Register `AddressablesAssetClient` as `IAddressablesAssetClient` (singleton).
- Move `IAssetReferenceHandler` to `Runtime/Internal/`, namespace `Scaffold.Addressables.Internal`, `internal`.
- Make `AddressablesAssetReferenceHandler` `internal sealed`.
- Register handler + gateway via container auto-wiring (no `WithParameter` on the gateway).

**API break:** external code that referenced `Scaffold.Addressables.Contracts.IAssetReferenceHandler` must delete those references (zero in-repo callers).

**Acceptance**

- `new AddressablesInstaller().Install(builder)` is sufficient for AppFlow and `LifetimeScope` hosts.
- Regression tests: gateway resolves as `IAsyncInitializable` and as `IAddressablesGateway` (same instance); `IAddressablesAssetClient` is singleton; internal handler type is not public.

### CLEANUP-02 — `NavigationInstaller` optional `NavigationSettings`

**Goal:** stop forcing every host to pre-register settings when the installer already has the instance.

- Single ctor: `NavigationInstaller(Transform holder, NavigationSettings settings = null)`.
- `if (settings != null) builder.RegisterInstance(settings);`

**Acceptance**

- Callers with settings use `new NavigationInstaller(holder, settings)`; others keep `new NavigationInstaller(holder)` and register settings elsewhere.
- Tests: with settings param, `NavigationSettings` resolves; without param and no other registration, `TryResolve<NavigationSettings>` is false after build.

### CLEANUP-03 — `NavigationInjection` implements `IViewControllerDependencyInjector`

**Goal:** remove consumer no-op injectors.

- `NavigationInjection : INavigationOpenHandler, IViewControllerDependencyInjector`
- `OnOpen` delegates to `Inject`; both use `layers.Top.Inject(controller)`.
- `NavigationController` ctor: remove `= null` default on `IViewControllerDependencyInjector` (binding always supplied by container).

**Constraint (unchanged):** `NavigationInjection` already required `ILayerResolver` (e.g. from `AppFlowRoot`). Pure `LifetimeScope` hosts without AppFlow must still supply `ILayerResolver` if they use this path — out of scope for this cleanup.

**Acceptance**

- Tests: `INavigationOpenHandler` and `IViewControllerDependencyInjector` resolve to the same singleton; `Inject` null-guards.

### CLEANUP-04 — Doc + sample updates inside Scaffold

- Update `com.scaffold.addressables` and `com.scaffold.navigation` README / changelog.
- Canonical install: `AddressablesInstaller`, `NavigationInstaller(holder, settings?)`, `EventsInstaller`.

## Desired consumer end-state (after CLEANUP-01..03 land)

```csharp
public void Install(IContainerBuilder builder)
{
    new AddressablesInstaller().Install(builder);
    new NavigationInstaller(navigationViewHolder, navigationSettings).Install(builder);
    new EventsInstaller().Install(builder);

    for (int i = 0; i < addressableCatalogPublishers.Count; i++)
    {
        addressableCatalogPublishers[i]?.Register(builder);
    }
}
```

If `navigationSettings` is provided by a publisher instead, use `new NavigationInstaller(navigationViewHolder)` and omit the second argument.

Same body works in `FoundationLayer` (`IScopeLayer`, AppFlow host) and `SceneFoundationScope` (`LifetimeScope`, scene host), modulo `ILayerResolver` registration for navigation.

## Sequencing & risk

- Land CLEANUP-02 first (additive ctor parameter).
- CLEANUP-01: API break only for `IAssetReferenceHandler` consumers outside the repo.
- CLEANUP-03: consumers that registered `NoViewControllerDependencyInjector` must remove that registration to avoid duplicate `IViewControllerDependencyInjector` bindings.

## Consumer-side follow-ups (Gear-Engine repo, after the package work)

Tracked here for completeness; not part of the Scaffold work.

- Collapse `FoundationLayer.Install` to the desired end-state above; delete the `NoViewControllerDependencyInjector` private class and redundant `RegisterInstance` / asset-client wiring.
- Drop redundant `RegisterInstance(navigationSettings)` from `SceneFoundationScope` when using the installer overload.
- Update Gear-Engine docs that describe host-level Addressables registration.
- Optional: promote `NavigationSettings` to an `AddressableScriptableObjectPublisherSO` so it rides the publisher pipeline.

## Related references

- Consumer host: [`FoundationLayer`](../Assets/GearEngine/Scripts/App/Bootstrap/Layers/FoundationLayer.cs)
- Scene-scope twin: [`SceneFoundationScope`](../Assets/GearEngine/Scripts/Core/SceneFoundation/Bootstrap/SceneFoundationScope.cs)
- Publisher pipeline that depends on `IAddressablesAssetClient`: [`DataDrivenAddressableScriptableObjectPublisher`](../Assets/GearEngine/Scripts/App/Bootstrap/Publishers/DataDriven/DataDrivenAddressableScriptableObjectPublisher.cs)
- Package manifest pinning the Scaffold sources: [`Packages/manifest.json`](../Packages/manifest.json)
- Adjacent upstream tracking note: [`Scaffold-Upstream-LayeredScope.md`](Scaffold-Upstream-LayeredScope.md)
