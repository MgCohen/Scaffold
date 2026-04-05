# Scaffold

**Scaffold** is a modular Unity (C#) project used as a development **holder**: first-party modules live as Unity Package Manager (UPM)–style trees under `Assets/Packages/com.scaffold.*/`, each with a `package.json`. In this repository those folders compile as normal project assets; architecture is enforced with assembly boundaries (`.asmdef`), documentation, and custom Roslyn analyzers under `Analyzers/`.

- **Stack:** Unity 6, MVVM-style UI, **VContainer** for DI, **URP**, optional **Addressables** and **Unity Gaming Services (UGS)**.
- **Deeper context:** [Architecture.md](Architecture.md), [AGENTS.MD](AGENTS.MD) (contributor/agent rules), [Docs/ConsumingScaffoldPackages.md](Docs/ConsumingScaffoldPackages.md) (installing packages in other Unity projects), [Docs/NewProjectFromScaffold.md](Docs/NewProjectFromScaffold.md) (what to copy for a new repo: packages vs agent harness vs analyzers).

## Installing a package in another Unity project

Add a Git dependency with a **subpath** to the package folder and a **revision** (`#main`, `#tag`, or commit hash). Replace the URL with your fork if needed.

```json
{
  "dependencies": {
    "com.scaffold.events": "https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.events#main"
  }
}
```

Repeat for each `com.scaffold.*` package you need; internal dependencies are declared in each package’s `package.json`. For local development you can use `file:` paths—see [Docs/ConsumingScaffoldPackages.md](Docs/ConsumingScaffoldPackages.md).

---

## Internal packages

| Package | What it does | Quick explainer / snippet | Git install (UPM) | Path in this repo |
|--------|----------------|---------------------------|--------------------|-------------------|
| **com.scaffold.addressables** | Thin Addressables loading API, catalog sync, asset providers/registrars for startup preload. | `await gateway.InitializeAsync();` then `await gateway.LoadAsync<T>(assetReference);` | `https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.addressables#main` | [Assets/Packages/com.scaffold.addressables](Assets/Packages/com.scaffold.addressables) |
| **com.scaffold.autopacker** | Roslyn source generator: `[AutoPack]` types get generated `Packed` structs and pack/unpack helpers (see `Generators/AutoPacker/` in repo). | `[AutoPack] public partial class PlayerState { public int Health; }` | `https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.autopacker#main` | [Assets/Packages/com.scaffold.autopacker](Assets/Packages/com.scaffold.autopacker) |
| **com.scaffold.cloudcode** | Unity Cloud Code client: typed calls to modules/endpoints with JSON settings and pluggable call pipeline (timeout, retry, logging, etc.). | `await cloudCode.CallEndpointAsync<MyDto>("ModuleName", "endpointName", payload);` | `https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.cloudcode#main` | [Assets/Packages/com.scaffold.cloudcode](Assets/Packages/com.scaffold.cloudcode) |
| **com.scaffold.entities** | Core gameplay building blocks: float attributes, modifiers, `Entity` behaviour runner and input contracts (`UnityEngine` where needed). | `Entity` + `EntityBehaviorRunner<TData,TInput>` for ordered behaviours. | `https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.entities#main` | [Assets/Packages/com.scaffold.entities](Assets/Packages/com.scaffold.entities) |
| **com.scaffold.events** | In-process typed event bus (`IEventBus`) for decoupled module messaging. | `eventBus.Raise(new MyContextEvent(...));` / `AddListener<MyContextEvent>(handler)` | `https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.events#main` | [Assets/Packages/com.scaffold.events](Assets/Packages/com.scaffold.events) |
| **com.scaffold.liveops** | Typed client for Cloud Code **LiveOps** modules; initial `GameDataRequest`, `ILiveOpsService.CallAsync`, response dispatch. | `await liveOps.CallAsync<Response>(request);` · `GetModuleData<T>()` after initial LiveOps fetch. | `https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.liveops#main` | [Assets/Packages/com.scaffold.liveops](Assets/Packages/com.scaffold.liveops) |
| **com.scaffold.maps** | Composite-key maps with predicate-driven indexers for grouped lookups. | `Map<TPrimary,TSecondary,TValue>` with `Indexer<...>` filtered views. | `https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.maps#main` | [Assets/Packages/com.scaffold.maps](Assets/Packages/com.scaffold.maps) |
| **com.scaffold.model** | Unity-free observable **Model** base for MVVM (`noEngineReferences` where applicable). | Derive domain models from `Model` with nested-observable attributes. | `https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.model#main` | [Assets/Packages/com.scaffold.model](Assets/Packages/com.scaffold.model) |
| **com.scaffold.mvvm** | Shared MVVM primitives: bindings (`TreeBinding`, `BindingOptions`), converters/adapters, nested observable contracts. | `TreeBinding` + `IBindedProperty<,>` for reactive UI wiring. | `https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.mvvm#main` | [Assets/Packages/com.scaffold.mvvm](Assets/Packages/com.scaffold.mvvm) |
| **com.scaffold.navigation** | View-controller stack, transitions, `INavigation` / `NavigationController`, Addressables-backed view loading. | `navigation.Open(...)` · `Close(...)` · `Return()` | `https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.navigation#main` | [Assets/Packages/com.scaffold.navigation](Assets/Packages/com.scaffold.navigation) |
| **com.scaffold.records** | Compatibility shim for C# `init` accessors (`IsExternalInit`) on older targets. | Reference `Scaffold.Records` when using `init` properties. | `https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.records#main` | [Assets/Packages/com.scaffold.records](Assets/Packages/com.scaffold.records) |
| **com.scaffold.sceneflow** | Additive Addressables scene load/unload while a bootstrap shell scene stays loaded. | `await sceneFlow.LoadAsync(sceneRef, options, ct);` · unload with returned token. | `https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.sceneflow#main` | [Assets/Packages/com.scaffold.sceneflow](Assets/Packages/com.scaffold.sceneflow) |
| **com.scaffold.scope** | Two-scope startup (`TwoScopeApplicationHost`), `IAsyncInitializationRunner`, cross-layer resolve, legacy `IAsyncLayerInitializable` pass on the main scope. | Subclass `TwoScopeApplicationHost`; implement base + main installers and preload. | `https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.scope#main` | [Assets/Packages/com.scaffold.scope](Assets/Packages/com.scaffold.scope) |
| **com.scaffold.types** | Serializable `TypeReference`, constructor dependency extraction, editor type pickers. | `[TypeReference]` fields in ScriptableObjects / configs. | `https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.types#main` | [Assets/Packages/com.scaffold.types](Assets/Packages/com.scaffold.types) |
| **com.scaffold.ugs** | UGS Core init + anonymous sign-in as `IAsyncLayerInitializable` before dependent services. | `UgsInstaller` registers singleton that runs `UnityServices.InitializeAsync` then anonymous auth. | `https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.ugs#main` | [Assets/Packages/com.scaffold.ugs](Assets/Packages/com.scaffold.ugs) |
| **com.scaffold.view** | Unity MVVM **view** layer: `View<T>`, `UIView<T>`, view event bubbling (`ViewEvents`). | `OnBind()` register bindings; `ViewEvents.Raise<MyEvent>()` up the hierarchy. | `https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.view#main` | [Assets/Packages/com.scaffold.view](Assets/Packages/com.scaffold.view) |
| **com.scaffold.viewmodel** | MVVM **ViewModel** base: `Bind(INavigation)`, binding orchestration with `Scaffold.MVVM` bind graph. | Screens inherit `ViewModel` and register binds after `Bind(navigation)`. | `https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.viewmodel#main` | [Assets/Packages/com.scaffold.viewmodel](Assets/Packages/com.scaffold.viewmodel) |

Each package also has a **README.md** at its root with API tables and integration notes. Version fields in `package.json` follow SemVer; see [Plans/ModulesAsUpmPackages/ModulesAsUpmPackages-ExecPlan.md](Plans/ModulesAsUpmPackages/ModulesAsUpmPackages-ExecPlan.md) for packaging policy.

---

## Extra packages (third-party and sibling repositories)

These are **not** the in-repo `com.scaffold.*` trees. When you consume Scaffold packages in another project, you still need compatible versions of external UPM dependencies—mirror the `dependencies` blocks in each package’s `package.json` and the holder [Packages/manifest.json](Packages/manifest.json).

| Name | UPM package id | Role | Install |
|------|----------------|------|---------|
| **Scaffold Schemas** | `com.scaffold.schemas` | Shared schema / contract types used by navigation, LiveOps, and related modules. | Git: `https://github.com/ScaffoldLibrary/Schemas.git` (pin branch, tag, or revision as needed). |
| **VContainer** | `jp.hadashikick.vcontainer` | Dependency injection (`IObjectResolver`, installers, scopes). | Git (example pin): `https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.17.0` |
| **Newtonsoft.Json** | `com.unity.nuget.newtonsoft-json` | JSON serialization (Cloud Code, LiveOps, and other JSON-heavy paths). | Unity Registry: add by name in Package Manager, or declare a version in `manifest.json` (e.g. `3.2.1` in line with Scaffold packages). |
| **Unity Addressables** | `com.unity.addressables` | Content loading and labels (gateway, navigation, scene flow). | Unity Registry. |
| **Unity UI (uGUI)** | `com.unity.ugui` | Canvas / UI toolkit used by views and startup UI. | Unity Registry. |
| **Unity Cloud Code** | `com.unity.services.cloudcode` | Official Cloud Code client used by `com.scaffold.cloudcode`. | Unity Registry. |
| **Unity Services Core / Authentication** | `com.unity.services.core`, `com.unity.services.authentication` | UGS initialization and sign-in (pulled with UGS workflows; `com.scaffold.ugs` depends on Core). | Unity Registry (often via Unity Gaming Services in Package Manager). |

**Holder-only / tooling** (optional; not required for every consumer): [NaughtyAttributes](https://github.com/dbrizov/NaughtyAttributes) (`com.dbrizov.naughtyattributes`), [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) (`com.github-glitchenzo.nugetforunity`), vendor assets under `Assets/Packages/` (for example **AAGen**).

For a minimal **manifest.json** fragment when pulling Git subpath packages, see [Docs/ConsumingScaffoldPackages.md](Docs/ConsumingScaffoldPackages.md).
