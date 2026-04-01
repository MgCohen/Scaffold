# LiveOps

Cloud Code backend under `LiveOps/` (Unity repo root): **DTO** (`LiveOps.DTO/`) and **main** module (`Project/`). Unity consumes the precompiled **`Scaffold.LiveOps.DTO.dll`** plugin (Newtonsoft.Json types and `GameModuleDTO.*` contracts).

## Layout

| Part | Path | Role |
|------|------|------|
| **DTO** | `LiveOps/LiveOps.DTO/` | Contracts (`GameModuleDTO.*` in source); project file **`Scaffold.LiveOps.DTO.csproj`**, assembly name **`Scaffold.LiveOps.DTO`** |
| **Main** | `LiveOps/Project/` | Cloud Code host (`GameModule.*`), `net6.0`, output assembly **`LiveOps.dll`** |

Build the shared contracts with **`LiveOps/LiveOps.sln`**. The DTO project copies **`Scaffold.LiveOps.DTO.dll`** (and `.pdb` when present) to **`Assets/Plugins/Scaffold.LiveOps.DTO/`** after each build (`CopyDtoToUnityPlugins` target in `Scaffold.LiveOps.DTO.csproj`). The Cloud Code host under **`LiveOps/Project/`** is built with your Unity Cloud Code / deployment pipeline when applicable.

## Unity plugins

Paths are **repo-root relative** (DTO `.csproj` lives at `LiveOps/LiveOps.DTO/`, so `..\..\Assets\Plugins\Scaffold.LiveOps.DTO\` resolves correctly regardless of the Unity project folder name on disk).

Manual copy is only needed if you build outside MSBuild or disable the post-build target:

- `LiveOps\LiveOps.DTO\bin\Release\netstandard2.1\Scaffold.LiveOps.DTO.dll` → `Assets\Plugins\Scaffold.LiveOps.DTO\`

## Build commands

```powershell
dotnet build "LiveOps\LiveOps.sln" -c Release
```

Optional manual copy:

```powershell
Copy-Item "LiveOps\LiveOps.DTO\bin\Release\netstandard2.1\Scaffold.LiveOps.DTO.dll" "Assets\Plugins\Scaffold.LiveOps.DTO\Scaffold.LiveOps.DTO.dll" -Force
```

Deploy the **LiveOps** Cloud Code module (dashboard name should match what the client uses, e.g. `"LiveOps"`). Remote config is loaded from the configured HTTP or UGS Remote Config source only; there is no on-disk JSON fallback in the module.

`LiveOps/Directory.Build.props` disables repository Roslyn analyzers for these projects.

## Unity client

Use **`ILiveOpsService`** / **`LiveOpsService`** (`Scaffold.LiveOps`, see `Docs/Core/LiveOps.md`) for typed **`ModuleRequest` / `ModuleResponse`** calls, or call **`ICloudCodeModuleService`** directly. Shared contracts ship in **`Scaffold.LiveOps.DTO.dll`** (`GameModuleDTO.*` namespaces).
