# Module Vertical Slice (View → ViewModel → ClientService → LiveOps)

This is the canonical walkthrough for adding a new feature module that spans every layer in the repo: a Unity-side **View** + **ViewModel**, a **client service** that calls Cloud Code, a shared **DTO** (config / persistence / snapshot / requests), and a **backend** Cloud Code module under `LiveOps/Scaffold/`. Use **`ModuleX`** as the running name; substitute your real feature name everywhere.

> Scope: this doc bundles the steps. For deep theory, links point at the existing references (`Docs/LiveOps/Backend-Authoring-Guide.md`, `Docs/Core/LiveOpsKeys.md`, `Assets/Packages/com.scaffold.liveops/README.md`). Don't duplicate explanations — link.

---

## 1. Mental model

```text
┌──────────────────────── Unity client ────────────────────────┐         ┌─────────── Cloud Code (UGS) ──────────┐
│                                                              │         │                                       │
│  View<ModuleXViewModel>  ──binds──▶  ModuleXViewModel        │         │   GameApiDispatcher                   │
│        │                                  │                  │         │        │                              │
│        │                                  ▼                  │         │        ▼                              │
│        │                       ModuleXClientService          │  HTTPS  │   GameApiRegistry                     │
│        │                       (or GameClientModuleBase<T>)  │ ──────▶ │        │                              │
│        │                                  │                  │         │        ▼                              │
│        │                                  ▼                  │         │   ModuleXService                      │
│        │                       ILiveOpsService.CallAsync     │         │     : GameModule<ModuleXData>,        │
│        │                       (DoThingRequest)              │ ◀────── │       IGameApiHandler<TReq, TResp>    │
│        │                                                     │         │        │                              │
│        ▼                                                     │         │        ▼                              │
│  Updated ModuleXData snapshot (from response)                │         │   IPlayerData / IRemoteConfig         │
│                                                              │         │   keyed by KeyOf<T>.Module            │
└──────────────────────────────────────────────────────────────┘         └───────────────────────────────────────┘
```

Key contracts (all already exist in the repo):

| Layer | Type | Source |
|---|---|---|
| View | `Scaffold.MVVM.View.View<TViewModel>` | `com.scaffold.view` |
| ViewModel | `Scaffold.MVVM.ViewModel.ViewModel` | `com.scaffold.viewmodel` |
| Client service base (optional) | `GameClientModuleBase<TData>` | `com.scaffold.liveops/Runtime/GameClientModuleBase.cs` |
| Client RPC | `ILiveOpsService.CallAsync<TResponse>(ModuleRequest<TResponse>)` | `com.scaffold.liveops/Runtime/ILiveOpsService.cs` |
| Backend module init | `LiveOps.GameModule.GameModule<TData>` | `LiveOps/Deploy/Core/LiveOps.Core/...` |
| Backend RPC handler | `LiveOps.GameApi.IGameApiHandler<TRequest, TResponse>` | `LiveOps/Deploy/Core/LiveOps.Core/...` |
| Storage / wire keys | `[LiveOpsKey("...")]` + `KeyOf<T>` | `LiveOps/Deploy/Core/LiveOps.DTO/Keys/` (see [LiveOpsKeys.md](../Core/LiveOpsKeys.md)) |

---

## 2. Files you create

For a feature called `ModuleX`:

```text
Assets/Packages/com.scaffold.modulex/
├── package.json
├── README.md                                    # follow Docs/Standards/Module-Documentation-Standard.md
├── Runtime/
│   ├── Scaffold.ModuleX.asmdef                  # asmdef name = Scaffold.ModuleX (Runtime)
│   ├── Abstraction/                             # cross-module contracts (interfaces, DTOs the rest of the app sees)
│   │   └── IModuleXClientService.cs
│   ├── Implementation/                          # concrete logic
│   │   ├── ModuleXClientModule.cs               # : GameClientModuleBase<ModuleXData>, hydrated from initial GameDataRequest
│   │   └── ModuleXClientService.cs              # public service used by ViewModels; wraps ILiveOpsService.CallAsync
│   └── View/                                    # OPTIONAL — only if the module owns Unity views
│       ├── ModuleXView.cs                       # : View<ModuleXViewModel>
│       └── ModuleXViewModel.cs                  # : ViewModel
├── Container/
│   ├── Scaffold.ModuleX.Container.asmdef
│   └── ModuleXInstaller.cs                      # : VContainer.IInstaller — register module + client service
├── Tests/
│   └── Scaffold.ModuleX.Tests.asmdef
└── Backend~/
    └── Scaffold/
        ├── ModuleX/
        │   ├── Scaffold.LiveOps.ModuleX.csproj
        │   └── ModuleXService.cs                # : GameModule<ModuleXData>, IGameApiHandler<DoThingRequest, DoThingResponse>
        └── ModuleX.DTO/
            ├── Scaffold.LiveOps.ModuleX.DTO.csproj
            ├── ModuleXData.cs                   # snapshot — IGameModuleData + [LiveOpsKey("ModuleXData")]
            ├── ModuleXConfig.cs                 # remote config DTO + [LiveOpsKey("ModuleXConfig")]
            ├── ModuleXPersistence.cs            # player save DTO + [LiveOpsKey("ModuleXPersistence")]
            └── Request/
                ├── DoThingRequest.cs            # : ModuleRequest<DoThingResponse> + [LiveOpsKey("DoThingRequest")]
                └── DoThingResponse.cs           # : ModuleResponse — typically wraps refreshed ModuleXData
```

The `Backend~/` tree is exactly the layout in `Tools/BackendTemplate/com.scaffold.example/` — copy it and rename `Example` → `ModuleX`.

---

## 3. Endpoints and keys

### 3.1 Storage keys (player data + remote config)

Every persistence and config DTO declares its slot with `[LiveOpsKey("...")]`. `KeyOf<T>.Module` resolves to that string at runtime; `IPlayerData.Get/Set/GetOrSet` and `IRemoteConfig.Get` use it via the `DataCacheExtensions` in `LiveOps.Core`.

```csharp
[LiveOpsKey("ModuleXPersistence")]
public sealed class ModuleXPersistence
{
    [JsonProperty] public int CallCount { get; set; }
}

[LiveOpsKey("ModuleXConfig")]
public sealed class ModuleXConfig
{
    [JsonProperty] public string SampleConfigValue { get; set; } = "default";
}
```

Persistence and config **do not** implement `IGameModuleData`.

### 3.2 Snapshot key (aggregated `GameData`)

The snapshot DTO (what the client receives in the initial `GameDataRequest`) implements `IGameModuleData` *and* declares its own slot key:

```csharp
[LiveOpsKey("ModuleXData")]
public sealed class ModuleXData : IGameModuleData
{
    [JsonProperty] private int _callCount;
    [JsonProperty] private string _sampleConfigValue = string.Empty;

    [JsonIgnore] public int CallCount => _callCount;
    [JsonIgnore] public string SampleConfigValue => _sampleConfigValue;

    [JsonConstructor] private ModuleXData() { }

    public ModuleXData(ModuleXPersistence p, ModuleXConfig c)
    {
        _callCount = p.CallCount;
        _sampleConfigValue = c.SampleConfigValue;
    }
}
```

### 3.3 Wire keys (Cloud Code endpoints)

Each endpoint = one `ModuleRequest<TResponse>` subtype. Default wire key is the request type's `Name`; override with `[GameApiRequest("Custom.Wire")]` if you need a stable name across renames.

```csharp
[LiveOpsKey("DoThingRequest")]
public class DoThingRequest : ModuleRequest<DoThingResponse>
{
    public string Message { get; set; } = string.Empty;
}

public class DoThingResponse : ModuleResponse
{
    public DoThingResponse(ModuleXData data) { Data = data; }
    public ModuleXData Data { get; protected set; }
}
```

`GameApiRegistry` throws at startup if two request types resolve to the same wire key — no silent collisions. Full key model: [Docs/Core/LiveOpsKeys.md](../Core/LiveOpsKeys.md).

---

## 4. Backend module

```csharp
public class ModuleXService :
    GameModule<ModuleXData>,
    IGameApiHandler<DoThingRequest, DoThingResponse>
{
    private readonly ILogger<ModuleXService> _logger;
    public ModuleXService(ILogger<ModuleXService> logger) { _logger = logger; }

    public override async Task<IGameModuleData> InitializeAsync(
        GameApiSession session, CancellationToken ct = default)
    {
        var p = await session.Player.GetOrSet(session.Context, new ModuleXPersistence());
        var c = await session.RemoteConfig.Get (session.Context, new ModuleXConfig());
        return new ModuleXData(p, c);
    }

    public async Task<DoThingResponse> HandleAsync(GameApiSession session, DoThingRequest request)
    {
        var p = await session.Player.GetOrSet(session.Context, new ModuleXPersistence());
        var c = await session.RemoteConfig.Get (session.Context, new ModuleXConfig());
        p.CallCount += 1;
        await session.Player.Set(session.Context, p);            // cache only — flushed on batch dispose
        return new DoThingResponse(new ModuleXData(p, c));
    }
}
```

The `GameApiSession` provides `Context`, `Player` (`IPlayerData`), and `RemoteConfig` (`IRemoteConfig`). Inside `GameApiDispatcher.Invoke`, the dispatcher already opened a batch on player + game state; your `Set` calls are cached and persisted exactly once when the batch disposes (`FlushAsync`). Do **not** call `SaveCache`/`FlushAsync` from a handler. See **Cloud Code data pipeline (backend)** in [`com.scaffold.liveops/README.md`](../../Assets/Packages/com.scaffold.liveops/README.md).

Module + handler are both auto-discovered by the source generator (`Scaffold.LiveOps.Bootstrap.Generators`) and registered via `LiveOpsBootstrapper.InstallFromManifest` — no manual DI registration on the backend.

For game-only DI extras, implement `IGameSetup` in the same assembly (it's auto-discovered when the assembly is tagged `ScaffoldLiveOpsAssembly` — `Common.props` does this for every `LiveOps/**` csproj).

---

## 5. `Backend~/` round-trip (`LiveOps/` ↔ `Assets/Packages/*/Backend~/`)

In **this** Scaffold repo, the source of truth while developing is `LiveOps/`. The `Backend~/` trees inside packages are **shipped snapshots**.

1. Edit code under `LiveOps/Scaffold/ModuleX/` and `LiveOps/Scaffold/ModuleX.DTO/`.
2. Build / test with `dotnet build LiveOps/LiveOps.sln -c Release` (or just rebuild `LiveOps.csproj`).
3. Push into the package's `Backend~/`:
   ```powershell
   pwsh -NoProfile -File .agents/scripts/refresh-liveops-template.ps1
   ```
   Or in Unity: **Scaffold → LiveOps → Refresh Backend Template** (requires `SCAFFOLD_LIVEOPS_PACKAGE_DEV` for Editor — already set in this repo).
4. Commit both trees (the change in `LiveOps/` *and* the mirrored change under `Assets/Packages/com.scaffold.modulex/Backend~/`).

For consumer game projects (no `LiveOps/` source of truth):

- **Scaffold → LiveOps → Install or Update Backend** (or `pwsh -File .agents/scripts/install-liveops-backend.ps1` from a Scaffold checkout) merges every `Assets/Packages/*/Backend~/` into the consumer's repo-root `LiveOps/`. The same pass also **auto-adds discovered csprojs** to `LiveOps/LiveOps.Deploy.sln` (`MapCsprojToSolutionFolder` → `dotnet sln add`) — you do **not** need to hand-edit the `.sln`.
- **Scaffold → LiveOps → Backend Window** lists every `com.scaffold.*/Backend~` package with per-package Update/Refresh, Update All / Refresh All, and a Deploy button (UGS CLI).
- `LiveOps/Game/**` is for consumer-only Cloud Code and is never overwritten by install.

Full operational matrix (CLI vs menu, author vs consumer): [Docs/LiveOps/Backend-Authoring-Guide.md](../LiveOps/Backend-Authoring-Guide.md).

---

## 6. Client service + ViewModel

Two patterns; pick one.

### 6.1 Lightweight: ViewModel calls `ILiveOpsService` directly

```csharp
public sealed class ModuleXViewModel : ViewModel
{
    private readonly ILiveOpsService liveOps;

    public ModuleXViewModel(ILiveOpsService liveOps) { this.liveOps = liveOps; }

    public async Task DoThing(string message)
    {
        var response = await liveOps.CallAsync<DoThingResponse>(new DoThingRequest { Message = message });
        // response.Data is the refreshed ModuleXData
    }
}
```

Use this when there is no shared client-side cache and each ViewModel owns its own state.

### 6.2 With a shared client module (recommended for hydrated state)

If the feature has aggregated state that multiple Views read — and it should hydrate at startup from the initial `GameDataRequest` — wrap it in a `GameClientModuleBase<T>` subclass plus a public service:

```csharp
// Implementation/ModuleXClientModule.cs
internal sealed class ModuleXClientModule : GameClientModuleBase<ModuleXData>
{
    public ModuleXClientModule(ILiveOpsService liveOps) : base(liveOps) { }
    public ModuleXData Snapshot => data;
}

// Abstraction/IModuleXClientService.cs
public interface IModuleXClientService
{
    int CallCount { get; }
    Task DoThing(string message);
}

// Implementation/ModuleXClientService.cs
internal sealed class ModuleXClientService : IModuleXClientService
{
    private readonly ILiveOpsService liveOps;
    private readonly ModuleXClientModule module;

    public ModuleXClientService(ILiveOpsService liveOps, ModuleXClientModule module)
    { this.liveOps = liveOps; this.module = module; }

    public int CallCount => module.Snapshot?.CallCount ?? 0;

    public async Task DoThing(string message)
    {
        await liveOps.CallAsync<DoThingResponse>(new DoThingRequest { Message = message });
    }
}
```

`GameClientModuleBase<T>` implements `IAsyncInitializable` and reads `data` from `liveOps.GetModuleData<T>()` after the initial `GameDataRequest`. Bootstrap layering must run `LiveOpsService` before this module — see the `LiveOpsInstaller` registration in `com.scaffold.liveops/README.md` (Registration section).

### 6.3 Wiring in the Container installer

```csharp
public sealed class ModuleXInstaller : IInstaller
{
    public void Install(IContainerBuilder builder)
    {
        // Client module — hydrated by initial GameDataRequest. Both interfaces are needed:
        // IGameClientModule (so it shows up alongside other modules) and IAsyncInitializable
        // (so AppFlow drives InitializeAsync after LiveOpsService finishes).
        builder.Register<ModuleXClientModule>(Lifetime.Singleton)
            .AsSelf()
            .As<IGameClientModule>()
            .As<Scaffold.AppFlow.IAsyncInitializable>();

        builder.Register<ModuleXClientService>(Lifetime.Singleton)
            .As<IModuleXClientService>();
    }
}
```

Register `ModuleXInstaller` in your composition root **after** `LiveOpsInstaller` (or in a layer that runs after the LiveOps layer).

### 6.4 View

```csharp
public sealed class ModuleXView : View<ModuleXViewModel>
{
    [SerializeField] private Button doThingButton;
    [SerializeField] private TMP_Text callCountLabel;

    protected override void OnBind()
    {
        Bind(() => Controller.CallCount, () => callCountLabel.text, c => c.ToString());
        doThingButton.onClick.AddListener(() => _ = Controller.DoThing("hello"));
    }
}
```

---

## 7. Endpoint checklist (per new endpoint)

For every endpoint you add after the first one, repeat just these:

- [ ] New `Foo`Request : `ModuleRequest<FooResponse>` under `Backend~/Scaffold/ModuleX.DTO/Request/`.
- [ ] `[LiveOpsKey("FooRequest")]` (and `[GameApiRequest("Custom.Wire")]` only if the type name shouldn't be the wire key).
- [ ] New `FooResponse : ModuleResponse` (or reuse `DoThingResponse` if the shape is identical).
- [ ] `IGameApiHandler<FooRequest, FooResponse>` on `ModuleXService` (or a separate `Foo`Handler if the responsibility is distinct — see `LiveOps/Scaffold/DirectPush/` for a multi-handler module).
- [ ] Refresh + commit: `pwsh -File .agents/scripts/refresh-liveops-template.ps1`.
- [ ] Client call site: `liveOps.CallAsync<FooResponse>(new FooRequest { ... })`.

No solution-file edits are required: the deploy build globs `LiveOps/Scaffold/**/*.csproj` (`LiveOps/Deploy/Build/Scaffold.LiveOps.Deploy.targets`) and the install pass globs the same paths into `LiveOps.Deploy.sln`.

---

## 8. Bootstrap module checklist (one-time, per new module)

- [ ] Copy `Tools/BackendTemplate/com.scaffold.example/Backend~/` into `Assets/Packages/com.scaffold.modulex/Backend~/`. Rename `Example` → `ModuleX` everywhere (folders, csproj names, namespaces `LiveOps.Modules.{Example|Example.DTO}`).
- [ ] Mirror the same tree under `LiveOps/Scaffold/ModuleX{,.DTO}/`. (You can `git mv` once; `refresh-liveops-template.ps1` will keep them in sync afterwards.)
- [ ] Add the package's `Runtime/Container/Tests` asmdefs (see `com.scaffold.ads/Runtime/Scaffold.Ads.asmdef` for the convention, including `precompiledReferences` listing `LiveOps.DTO.dll` and `Scaffold.LiveOps.ModuleX.DTO.dll`).
- [ ] Build: `dotnet build LiveOps/LiveOps.sln -c Release`. The DTO project's `CopyDtoToUnityPlugins` target writes `Scaffold.LiveOps.ModuleX.DTO.dll` into `Assets/Plugins/Scaffold.LiveOps.DTO/` so Unity sees it.
- [ ] Refresh: `pwsh -File .agents/scripts/refresh-liveops-template.ps1` (or **Refresh Backend Template** menu).
- [ ] Register `ModuleXInstaller` in your composition root.
- [ ] Add the package's `README.md` per [Module-Documentation-Standard.md](Module-Documentation-Standard.md).

---

## Related

- [Module-Documentation-Standard.md](Module-Documentation-Standard.md)
- [.agents/workflows/create-module.md](../../.agents/workflows/create-module.md)
- [Docs/LiveOps/Backend-Authoring-Guide.md](../LiveOps/Backend-Authoring-Guide.md)
- [Docs/Core/LiveOpsKeys.md](../Core/LiveOpsKeys.md)
- [Assets/Packages/com.scaffold.liveops/README.md](../../Assets/Packages/com.scaffold.liveops/README.md)
- [Tools/BackendTemplate/com.scaffold.example/README.md](../../Tools/BackendTemplate/com.scaffold.example/README.md)

## Changelog

- 2026-05-06: initial walkthrough; consolidates create-module workflow, backend authoring guide, LiveOps keys, and the example template into one slice-oriented doc.
