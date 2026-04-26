# Repository scripts (`.agents/scripts`)

Quality gates and Unity test runners for this repo. **Prefer PowerShell 7+ (`pwsh`)** on Windows, macOS, and Linux so behavior matches CI and paths work cross-platform. Windows PowerShell 5.1 is still supported for most scripts.

## Prerequisites

- **PowerShell 7+**: [Install PowerShell](https://learn.microsoft.com/powershell/scripting/install/installing-powershell)
- **Unity**: Install via Unity Hub. Scripts locate the editor from `ProjectSettings/ProjectVersion.txt`, or use **`UNITY_PATH`**.

### `UNITY_PATH`

Absolute path to the Unity **binary**:

| OS | Example |
|----|---------|
| Windows | `C:\Program Files\Unity\Hub\Editor\6000.0.xx\Editor\Unity.exe` |
| macOS | `/Applications/Unity/Hub/Editor/6000.0.xx/Unity.app/Contents/MacOS/Unity` |
| Linux | `~/Unity/Hub/Editor/6000.0.xx/Editor/Unity` |

Optional: copy `.agents/local.env.example` to `.agents/local.env` (gitignored), set `UNITY_PATH`, then `source .agents/local.env` (bash/zsh) before running scripts.

### Version fallback

If the exact Hub version folder for the project is missing, **test runners** may pick the **newest** installed editor (same as before this refactor). **Compilation precheck** does not fallback unless **`SCAFFOLD_UNITY_ALLOW_VERSION_FALLBACK=1`** is set.

## Common commands (repository root)

```powershell
pwsh -NoProfile -File .agents/scripts/validate-changes.ps1 -SkipTests
```

```powershell
pwsh -NoProfile -File .agents/scripts/run-unity-tests.ps1 -TestPlatform EditMode
```

**macOS / Linux** (if `pwsh` is on `PATH`):

```bash
./.agents/scripts/validate-changes.sh -SkipTests
```

On Windows you can still use **`validate-changes.cmd`** or **`run-coverage-audit.cmd`**.

## LiveOps backend (Scaffold repo)

| Script | Purpose |
|--------|---------|
| `refresh-liveops-template.ps1` | Build `Scaffold.LiveOps.Bootstrap.Generators`, then sync `LiveOps/` into every `Assets/Packages/*/Backend~/` (host `**Deploy**`, feature `**Scaffold/<Feature>**`; excludes `bin`/`obj`). Use **`-SkipGeneratorBuild`** when MSBuild has already built the generator and copied the DLL (see `LiveOps/Deploy/Build/Scaffold.LiveOps.TemplateSync.targets`). |
| `install-liveops-backend.ps1` | Same merge as **Scaffold → LiveOps → Install or Update Backend** (implemented in `com.scaffold.liveops` as `LiveOpsBackendInstall`); this script is for CLI/CI or when running from a `.agents/scripts` checkout. |
| `test-liveops-deploy-cold-warm.ps1` | Times `dotnet build` / `dotnet publish` for `LiveOps.Deploy.sln` and `Deploy/LiveOps/LiveOps.csproj`; use **`-DeleteArtifacts`** to remove `LiveOps/.artifacts` first (simulates a clean tree). Lists generated `LiveOpsManifest.g.cs` under `LiveOps/.artifacts` (not per-project `obj/`). |

**LiveOps `Backend~` sync on `dotnet build`:** By default, after building the **`LiveOps`** host or **`Scaffold.LiveOps.Bootstrap.Generators`**, MSBuild runs `refresh-liveops-template.ps1 -SkipGeneratorBuild` so all package `Backend~` trees stay in sync.

To disable, add a repo-root **`Directory.Build.user.props`** (gitignored; optional import in root `Directory.Build.props`) with `<ScaffoldSyncLiveOpsTemplateOnBuild>false</ScaffoldSyncLiveOpsTemplateOnBuild>`.

In the Unity Editor (Scaffold repo only, **Editor** scripting define `SCAFFOLD_LIVEOPS_PACKAGE_DEV`), **Scaffold → LiveOps → Refresh Backend Template** runs the full refresh script and calls **AssetDatabase.Refresh**; use it when you prefer not to run `dotnet` from a terminal. Consumer projects do not ship that menu; they use **Install or Update Backend** (in-package) or the optional CLI script.

## Layout

| Path | Purpose |
|------|---------|
| `lib/` | Dot-sourced helpers (`UnityProcess.ps1`, `UnityEditorPaths.ps1`, `InvokeChildScript.ps1`) |
| `migration/` | One-time / rare UPM migration scripts and `package-path-mappings.json` |
| `diagnostics/` | Optional tools (e.g. `scan-meta-health.ps1`) |

## Documentation

- Full gate behavior, parameters, and implementation notes: **`Docs/Testing.md`**
- Agent workflow: **`AGENTS.MD`**
