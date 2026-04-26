# LiveOps per-package `Backend~` — authoring and consumer guide

This document describes how **Scaffold** packages ship Cloud Code sources under **`Backend~/`**, how the **Scaffold** repository keeps **`LiveOps/`** and those folders in sync, and how a **game consumer** merges them into a repo-root **`LiveOps/`** tree. It is for humans and for AI agents operating the repo.

For architecture details and client API, see [`../../Assets/Packages/com.scaffold.liveops/README.md`](../../Assets/Packages/com.scaffold.liveops/README.md). For a copy-paste starting point for a new feature, see [`../../Tools/BackendTemplate/com.scaffold.example/README.md`](../../Tools/BackendTemplate/com.scaffold.example/README.md).

---

## 1. Concepts

| Path | Role |
|------|------|
| **`LiveOps/`** (Scaffold repo, repository root) | **Source of truth** while you develop: `LiveOps/Deploy/`, `LiveOps/Scaffold/<Feature>/`, etc. |
| **`Assets/Packages/<packageId>/Backend~/`** | **Shipped snapshot** for that package. The **host** (`com.scaffold.liveops`) carries `Backend~/Directory.Build.props`, `Backend~/Deploy/`, `Backend~/Deploy/Tools/Generators/`, and `LiveOps.Deploy.sln`. **Feature** packages (e.g. `com.scaffold.ads`) carry `Backend~/Scaffold/Ads/` and `Backend~/Scaffold/Ads.DTO/`. |
| **Consumer’s `LiveOps/`** (game project repo root) | **Merged** from **all** installed packages’ `Backend~/` trees by the **install** script. Result: `LiveOps/Directory.Build.props`, `LiveOps/Deploy/`, `LiveOps/Scaffold/`, and `LiveOps/LiveOps.Deploy.sln`, while **`LiveOps/Game/**` is preserved** (see below). |
| **`LiveOps/Game/**`** | **Consumer-only** Cloud Code (game-specific modules, `IGameSetup` implementations you do not want overwritten). **The install script does not copy anything into `LiveOps/Game/`.** Add and maintain that tree in the game repository yourself. |

**Important:** `Backend~` is **not** deployed to `LiveOps/Game/`. The merge target is `LiveOps/Deploy` and `LiveOps/Scaffold` only.

---

## 2. Authoring in the Scaffold repository

### 2.1 Where to edit

- Edit the **real** backend under **`LiveOps/`** (for example `LiveOps/Scaffold/Ads/`, `LiveOps/Deploy/Core/...`).

### 2.2 Push `LiveOps/` into every package `Backend~/` (sync out)

There is **no** Unity menu item that copies a **single** feature (for example only Ads) in isolation. One **refresh** run updates **every** `Assets/Packages/*/Backend~/` that has a matching structure under `LiveOps/`.

**Command line (from repository root):**

```powershell
pwsh -NoProfile -File .agents/scripts/refresh-liveops-template.ps1
```

- **Full** run: builds the Roslyn generator project, copies `Scaffold.LiveOps.Bootstrap.Generators.dll` into `LiveOps/Deploy/Tools/Generators/`, then syncs.
- If the generator is already up to date (for example after an MSBuild step), you can use:

```powershell
pwsh -NoProfile -File .agents/scripts/refresh-liveops-template.ps1 -SkipGeneratorBuild
```

**Unity (this repository only, Editor):**

- Menu: **Scaffold → LiveOps → Refresh Backend Template**
- Requires the **`SCAFFOLD_LIVEOPS_PACKAGE_DEV`** scripting define for the **Editor** platform (see this repo’s `PlayerSettings`).
- Invokes the same `refresh-liveops-template.ps1` as the CLI.

**What the refresh does (summary):** For each package that contains `Backend~/`, it mirrors the matching folders from `LiveOps/` (for example `LiveOps/Scaffold/Ads` → `com.scaffold.ads/Backend~/Scaffold/Ads`, and `LiveOps/Deploy/...` → `com.scaffold.liveops/Backend~/Deploy/...`), then copies `LiveOps/LiveOps.Deploy.sln` and `LiveOps/Directory.Build.props` into the **host** package’s `Backend~/`.

### 2.3 New feature package (bootstrap)

1. Copy [`Tools/BackendTemplate/com.scaffold.example/`](../../Tools/BackendTemplate/com.scaffold.example/) to `Assets/Packages/com.scaffold.<yourfeature>/` (or copy only the `Backend~/` tree and rename `Example` to your feature).
2. Add the new `.csproj` projects to `LiveOps/LiveOps.Deploy.sln` (and `LiveOps.sln` if you use the test project), for example with `dotnet sln add ...`.
3. Develop under `LiveOps/Scaffold/<YourFeature>/` (preferred), then run **refresh** so `Backend~/` stays current.

The [`create-module`](../../.agents/workflows/create-module.md) workflow includes an optional step for `Backend~/` when a module includes a `Scaffold.LiveOps.*` host slice.

---

## 3. Consuming in a game project

### 3.1 Prerequisites

- The game Unity project is the **git repository root** (or you pass that root to the script; the script defaults to the current directory).
- Packages such as `com.scaffold.liveops`, `com.scaffold.ads`, and `com.scaffold.directpush` are present under `Assets/Packages/`, each optionally containing a `Backend~/` folder.

### 3.2 Merge all `Backend~/` into `LiveOps/`

**Command line (optional; from a Scaffold repo checkout, consumer project root = folder that contains `Assets/`):**

```powershell
pwsh -NoProfile -File .agents/scripts/install-liveops-backend.ps1
```

**Unity menu (recommended in game repos; same merge logic as the script, shipped in the package):**

- **Scaffold → LiveOps → Install or Update Backend**

**What the install does (summary):** For every `Assets/Packages/*/Backend~/`, it **merges** that folder into the repo-root `LiveOps/` (adds/updates `Directory.Build.props`, `Deploy`, `Scaffold`, etc., using `robocopy /E` — it does **not** mirror-delete unrelated paths like a full `Game` tree wipe). It copies `LiveOps.Deploy.sln` from the **host** package (`com.scaffold.liveops/Backend~`) and creates `LiveOps/Game` if missing.

**It does not populate `LiveOps/Game`.** If you need game-only handlers or `IGameSetup` under source control, create `LiveOps/Game/**` in the game repo and add projects to the solution as needed.

### 3.3 After install

- Build the Cloud Code module, for example:  
  `dotnet build "LiveOps\LiveOps.Deploy.sln" -c Release`  
- Point UGS / `.ccmr` at `LiveOps.Deploy.sln` as described in the package README (not `LiveOps.sln` for upload size limits to Cloud Code, unless you know you need the test project).

---

## 4. Quick reference

| Goal | Where to work | Action |
|------|---------------|--------|
| Change Ads (or any feature) backend | `LiveOps/Scaffold/...` in **Scaffold** repo | **Refresh** (CLI or menu) to update `com.scaffold.<feature>/Backend~` |
| Change host (Deploy, Core, generator DLL in Tools) | `LiveOps/Deploy/...` | **Refresh** to update `com.scaffold.liveops/Backend~` |
| Apply packages → `LiveOps/` on a **game** repo | Game repo root | **Install** (CLI or menu) |
| Game-only Cloud Code | Consumer **`LiveOps/Game/**`** | Maintain manually; **not** part of `Backend~` install |

---

## 5. Guidance for AI agents

- In the **Scaffold** repo, prefer editing **`LiveOps/`**, then run **`refresh-liveops-template.ps1`** (or `-SkipGeneratorBuild` when the generator DLL is already current). Commit changes to both `LiveOps/` and the affected **`Backend~/`** trees when those files change.
- In a **consumer** repo, run **Scaffold → LiveOps → Install or Update Backend** (or from a Scaffold checkout, **`install-liveops-backend.ps1`**) after adding or updating packages, then build **`LiveOps.Deploy.sln`**.
- **Do not** describe install as copying into **`LiveOps/Game/`**.
- **Do not** invent a one-click “sync only this feature to `Backend~`” — the standard tool is a **full refresh** that updates all package `Backend~` folders that map to `LiveOps/`.
- For a **new** module, start from **[`Tools/BackendTemplate/com.scaffold.example`](../../Tools/BackendTemplate/com.scaffold.example)**, add projects to the LiveOps solution, implement under `LiveOps/Scaffold/`, then **refresh**.

---

## 6. Related files

| File | Purpose |
|------|---------|
| [`.agents/scripts/refresh-liveops-template.ps1`](../../.agents/scripts/refresh-liveops-template.ps1) | Scaffold repo: `LiveOps/` → `Assets/Packages/*/Backend~/` |
| [`.agents/scripts/install-liveops-backend.ps1`](../../.agents/scripts/install-liveops-backend.ps1) | Optional CLI: same as menu; use when not in Unity (e.g. CI) or from a Scaffold checkout |
| [`Assets/Packages/com.scaffold.liveops/Editor/LiveOpsBackendInstall.cs`](../../Assets/Packages/com.scaffold.liveops/Editor/LiveOpsBackendInstall.cs) | **Install** merge logic in the package (menu); [`LiveOpsBackendInstallContext.cs`](../../Assets/Packages/com.scaffold.liveops/Editor/LiveOpsBackendInstallContext.cs) holds path bundle |
| [`Assets/Packages/com.scaffold.liveops/Editor/LiveOpsTemplateMenu.cs`](../../Assets/Packages/com.scaffold.liveops/Editor/LiveOpsTemplateMenu.cs) | Unity menu: **Install** (in-package) and **Refresh** (requires `.agents/scripts` in Scaffold) |
| [`.agents/msbuild/Scaffold.LiveOps.TemplateSync.targets`](../../.agents/msbuild/Scaffold.LiveOps.TemplateSync.targets) | MSBuild: dev-only post-build hook that runs refresh with `-SkipGeneratorBuild` after `LiveOps.csproj` builds or after the generator copies its DLL. Imported via `LiveOps/Directory.Build.props` (Exists-guarded so it self-disables in consumer installs). |

---

## 7. Common misconceptions

- **“I click to copy only Ads to `Backend~`.”** There is no separate button per feature. You refresh once; all mapped `Backend~` trees update.
- **“Install unpacks `Backend~` into `LiveOps/Game`.”** No. Install merges into `LiveOps/Deploy` and `LiveOps/Scaffold`. `LiveOps/Game` is for your game’s exclusive code and is not overwritten by any package’s `Backend~/Game/` (and packages should not ship a `Game` subtree in `Backend~` for that reason).

If this guide drifts from behavior, the scripts in **`.agents/scripts/`** and the **package README** are the source of truth for exact merge semantics and paths.
