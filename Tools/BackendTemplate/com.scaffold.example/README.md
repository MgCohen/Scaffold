# com.scaffold.example (backend template)

Sample `Backend~/Scaffold/Example` + `Scaffold/Example.DTO` layout for a new LiveOps feature module. Copy the `Backend~/` tree into your real package (`Assets/Packages/com.scaffold.<feature>/Backend~/`), then rename `Example` → `<Feature>` everywhere (folder names, `*.csproj` filenames, `AssemblyName`, namespaces `LiveOps.Modules.{Example|Example.DTO}`).

## What's in the box

```text
Backend~/Scaffold/
├── Example/
│   ├── Scaffold.LiveOps.Example.csproj    # net6.0 runtime project; references Deploy/Core/LiveOps.Core + Example.DTO
│   ├── ExampleModule.cs                   # GameModule<ExampleData> + IGameApiHandler<DoThingRequest, DoThingResponse>
│   └── ExampleGameSetup.cs                # optional IGameSetup hook (delete or move to LiveOps/Game in real projects)
└── Example.DTO/
    ├── Scaffold.LiveOps.Example.DTO.csproj  # netstandard2.1; references Deploy/Core/LiveOps.DTO; CopyDtoToUnityPlugins=true
    ├── ExampleData.cs                       # snapshot — IGameModuleData + [LiveOpsKey("ExampleData")]
    ├── ExampleConfig.cs                     # remote config DTO + [LiveOpsKey("ExampleConfig")]
    ├── ExamplePersistence.cs                # player save DTO + [LiveOpsKey("ExamplePersistence")]
    └── Request/
        ├── DoThingRequest.cs                # : ModuleRequest<DoThingResponse> + [LiveOpsKey("DoThingRequest")]
        └── DoThingResponse.cs               # : ModuleResponse, wraps the refreshed ExampleData
```

The two `.csproj` files are required — **Install or Update Backend** discovers them by globbing `LiveOps/Scaffold/**/*.csproj` and registers them in `LiveOps/LiveOps.Deploy.sln` under the `Scaffold/<Feature>` solution folder (DTO and runtime projects share one folder; see `LiveOpsBackendInstall.MapCsprojToSolutionFolder` in `com.scaffold.liveops`). The deploy build itself doesn't need a `.sln` edit — `LiveOps/Deploy/Build/Scaffold.LiveOps.Deploy.targets` already globs every `Scaffold/**/*.csproj` as a `ProjectReference`.

## How to use this template for a new module

1. Copy `Tools/BackendTemplate/com.scaffold.example/Backend~/` into `Assets/Packages/com.scaffold.<feature>/Backend~/`.
2. Rename `Example` → `<Feature>` in: folder names (`Example/`, `Example.DTO/`), csproj filenames + `AssemblyName`, namespaces (`LiveOps.Modules.Example` → `LiveOps.Modules.<Feature>`, `LiveOps.Modules.DTO.Example` → `LiveOps.Modules.DTO.<Feature>`), `[LiveOpsKey("…")]` values, and class names.
3. (Scaffold repo only) Copy the renamed tree to `LiveOps/Scaffold/<Feature>/` and `LiveOps/Scaffold/<Feature>.DTO/`. The `Backend~/` tree under the package is a *snapshot* — `LiveOps/` is the source of truth while you develop.
4. Run `dotnet build LiveOps/LiveOps.sln -c Release` to verify, then `pwsh -File .agents/scripts/refresh-liveops-template.ps1` to push back into `Backend~/` (or the **Scaffold → LiveOps → Refresh Backend Template** menu).

For the full client + view + installer side, see [`Docs/Standards/Module-Vertical-Slice.md`](../../../Docs/Standards/Module-Vertical-Slice.md).
