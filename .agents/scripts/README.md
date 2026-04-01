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

## Layout

| Path | Purpose |
|------|---------|
| `lib/` | Dot-sourced helpers (`UnityProcess.ps1`, `UnityEditorPaths.ps1`, `InvokeChildScript.ps1`) |
| `migration/` | One-time / rare UPM migration scripts and `package-path-mappings.json` |
| `diagnostics/` | Optional tools (e.g. `scan-meta-health.ps1`) |

## Documentation

- Full gate behavior, parameters, and implementation notes: **`Docs/Testing.md`**
- Agent workflow: **`AGENTS.MD`**
