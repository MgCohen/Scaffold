# ExecPlan: Backend manifest in `package.json`

**Status:** Draft (not yet started)
**Owner:** TBD
**Branch:** new branch off `main` (separate from `claude/modulex-vertical-slice-DXMbu`)

---

## 1. Purpose

Today, the binding between **what's in `LiveOps/Scaffold/<X>/`** and **which Unity package ships it inside `Backend~/Scaffold/<X>/`** is **implicit**: `refresh-liveops-template.ps1` and `install-liveops-backend.ps1` discover the mapping by enumerating folders and matching identical names. There is no manifest, no ownership declaration, no validation. This works in the happy path but four failure modes are all silent:

1. Source exists in `LiveOps/Scaffold/Foo/` but **no package** has a `Backend~/Scaffold/Foo/` shell — refresh skips it; the code never ships in any UPM tarball.
2. Shell exists in `Backend~/Scaffold/Foo/` but no source under `LiveOps/Scaffold/Foo/` — refresh prints `Write-Warning "No source in repo (skip)"` and moves on; the shell goes stale (or worse, holds last-known-good content forever).
3. Two packages declare a `Backend~/Scaffold/Foo/` shell — refresh writes the same content into both. Later, install merges *both* into the consumer's `LiveOps/`, last-write-wins.
4. `Backend~/Scaffold/Foo/` and `LiveOps/Scaffold/foo/` differ only in case — works on Windows (case-insensitive), silently broken on macOS/Linux CI.

After this plan: each package declares the slices it owns inside its own `package.json` under a `scaffold.backend.slices` field. Refresh, install, and a new validator script all read that single source of truth. The four failure modes become hard CI errors with line-precise diagnostics.

You will know it worked when:

- A new module is registered by adding one entry to its `package.json` — no need to pre-create empty `Backend~/Scaffold/<Foo>/` shells.
- Renaming `Scaffold/Ads` → `Scaffold/Advertising` in only one location fails CI with a clear error.
- The hardcoded `if ($topName -ne "Deploy" -and $topName -ne "Scaffold") { continue }` in `refresh-liveops-template.ps1:49` is gone, replaced by per-slice `kind` data.

---

## 2. Terms (plain language)

- **Slice** — one directory pair: a path inside `LiveOps/` (the dev source of truth) that mirrors to the same path inside one package's `Backend~/`. Example: `Scaffold/Ads` is a slice owned by `com.scaffold.ads`; the dev side is `LiveOps/Scaffold/Ads/`, the package side is `Assets/Packages/com.scaffold.ads/Backend~/Scaffold/Ads/`.
- **Host slice** — special-purpose slices owned only by `com.scaffold.liveops`: the Cloud Code build host (`Deploy/**`), `LiveOps.Deploy.sln`, and `Directory.Build.props`.
- **Backfill** — adding the new manifest field to packages that don't yet have one.
- **Reader** — any tool that consumes the manifest: refresh script, install script, validator, future Editor tooling.
- **`Backend~/`** — UPM-special folder. Unity ignores it during asset import (the trailing `~` does that), but git tracks it and `npm pack` / UPM tarballs include it. That's how Cloud Code source ships through a Unity package without polluting the Unity AssetDatabase.

---

## 3. Schema (v1)

Reserve a single top-level **`scaffold`** key in `package.json` as the namespace for *all* Scaffold-specific package metadata going forward. v1 defines exactly one subkey: `scaffold.backend`.

    {
      "name": "com.scaffold.ads",
      "version": "1.0.0",
      "displayName": "Scaffold Ads",
      "description": "...",
      "scaffold": {
        "backend": {
          "schemaVersion": 1,
          "slices": [
            { "path": "Scaffold/Ads",     "kind": "runtime", "solutionFolder": "Scaffold/Ads" },
            { "path": "Scaffold/Ads.DTO", "kind": "dto",     "solutionFolder": "Scaffold/Ads" }
          ]
        }
      }
    }

Field rules:

- **`schemaVersion`** — integer, currently `1`. Future-proofing; readers must check and refuse unknown versions.
- **`slices[]`** — required when the package ships any `Backend~/` content. Empty array is allowed and means "this package has a `Backend~/` for non-sync reasons" (rare). Absent field means "this package ships no Cloud Code slices" — readers skip it.
- **`slices[].path`** — relative path; same string for both sides. Resolves to `LiveOps/<path>` (dev) and `<packageRoot>/Backend~/<path>` (package). Forward slashes only. No leading slash. No `..`.
- **`slices[].kind`** — enum `runtime | dto | host | host-sln | host-props`. Drives reader behavior:
  - `runtime` / `dto` — normal feature slice. Synced as a folder.
  - `host` — `Deploy/**` subtree owned only by `com.scaffold.liveops`. Synced as a folder.
  - `host-sln` — single file `LiveOps.Deploy.sln` at `Backend~/` root. Owned only by `com.scaffold.liveops`.
  - `host-props` — single file `Directory.Build.props` at `Backend~/` root. Owned only by `com.scaffold.liveops`.
- **`slices[].solutionFolder`** — optional. Where the install step files this slice's csprojs in `LiveOps.Deploy.sln`. Defaults to `path` when omitted. Replaces `LiveOpsBackendInstall.MapCsprojToSolutionFolder`'s hardcoded path-prefix mapping.

JSON Schema lives at `Tools/BackendManifest/scaffold-backend.schema.json` and is referenced from a small `Tools/BackendManifest/package-json-with-scaffold.schema.json` so IDE intellisense works on full `package.json` files (via the standard `$schema` field in each package).

---

## 4. Reader behavior

Three readers, one schema. Each replaces a folder-enumeration today.

### 4.1 `refresh-liveops-template.ps1` (Scaffold repo author flow)

Today (`refresh-liveops-template.ps1:43-69`): for each `Assets/Packages/*/Backend~/`, walk the `Deploy`/`Scaffold` subtrees, robocopy each child folder from `LiveOps/<top>/<name>` → `Backend~/<top>/<name>`.

After: for each `Assets/Packages/*/package.json` that has `scaffold.backend.slices`:

    foreach slice in package.scaffold.backend.slices:
        switch slice.kind:
            runtime, dto, host:
                src = LiveOps/<slice.path>
                dst = <packageRoot>/Backend~/<slice.path>
                require src to exist (else: hard error, exit non-zero)
                robocopy /MIR src -> dst (excluding bin obj .vs .artifacts)
            host-sln:
                copy LiveOps/LiveOps.Deploy.sln -> <packageRoot>/Backend~/LiveOps.Deploy.sln
            host-props:
                copy LiveOps/Directory.Build.props -> <packageRoot>/Backend~/Directory.Build.props

The hardcoded `Deploy`/`Scaffold` whitelist is gone. The `LiveOps.Deploy.sln` and `Directory.Build.props` carve-outs at lines 71-87 are gone, replaced by `host-sln` / `host-props` slice entries on the host package.

### 4.2 `install-liveops-backend.ps1` + `LiveOpsBackendInstall.cs` (consumer flow)

Today: robocopies each `Assets/Packages/*/Backend~/` wholesale into the consumer's `LiveOps/`, then runs `MapCsprojToSolutionFolder` to file every discovered csproj into the right solution folder.

After: iterate manifests, copy slice-by-slice (so an unrelated file accidentally sitting in `Backend~/` doesn't get propagated), use `slices[].solutionFolder` directly when registering csprojs in `LiveOps.Deploy.sln`. `MapCsprojToSolutionFolder`'s hardcoded path-prefix logic becomes a fallback for legacy installs that lack the manifest (removed in phase 4).

### 4.3 `validate-liveops-manifest.ps1` (new)

Hard-fails CI on:

- **Missing source**: a manifest claims `LiveOps/<path>` that doesn't exist on disk.
- **Orphan source**: a folder under `LiveOps/Scaffold/*/` is not claimed by any manifest.
- **Duplicate claim**: two packages list the same `slices[].path`.
- **Case mismatch**: the slice path's casing differs from on-disk casing in either `LiveOps/` or `Backend~/`.
- **csproj drift**: a csproj inside a claimed `Backend~/<path>/` has no twin csproj at `LiveOps/<path>/<same-name>.csproj` (or vice versa).
- **Schema violation**: `package.json` `scaffold.backend` doesn't match the JSON Schema.
- **Unknown `schemaVersion`**: refuses to validate forward-incompatible files instead of silently passing them.

Wired into `validate-changes.ps1` as a non-blocking step in phase 1, blocking from phase 2 onward.

---

## 5. Phased rollout

Each phase is a separate PR that produces demonstrable behavior on its own.

### Phase 0: schema + helper, zero behavior change

- Add `Tools/BackendManifest/scaffold-backend.schema.json` (JSON Schema for the `scaffold.backend` subtree).
- Add `Tools/BackendManifest/package-json-with-scaffold.schema.json` (`$ref`s the above; usable from `$schema` in any `package.json`).
- Add a tiny PowerShell module `.agents/scripts/lib/BackendManifest.ps1` exporting `Read-BackendManifests` (returns a flat list of `{package, path, kind, solutionFolder, packageRoot}` records, one per slice across all packages).
- Add a tiny C# helper `Assets/Packages/com.scaffold.liveops/Editor/LiveOpsBackendManifest.cs` with the same shape, used later by `LiveOpsBackendInstall.cs`.
- Add `Plans/Backend-Manifest-ExecPlan.md` (this file).

**Demonstrable result:** `pwsh -File .agents/scripts/lib/BackendManifest.ps1 -DumpJson` lists every slice across the repo. Today the list is empty (no manifests exist yet); proves the reader works.

### Phase 1: backfill + non-blocking validator

- Add `scaffold.backend` to every `package.json` listed in §6 below. **No code reads these fields yet** — they are descriptive only.
- Add `validate-liveops-manifest.ps1` and wire it into `validate-changes.ps1` with `-WarnOnly` semantics (prints findings, exits 0).

**Demonstrable result:** `pwsh -File .agents/scripts/validate-liveops-manifest.ps1` prints zero findings against the current tree. Manually breaking a slice path in `package.json` produces a clear warning describing what's wrong.

### Phase 2: switch readers to manifest-first with fallback

- Rewrite `refresh-liveops-template.ps1` per §4.1. Add a `-StrictManifest` switch; when off, fall back to folder enumeration with `Write-Warning`. Default: on.
- Rewrite `install-liveops-backend.ps1` and `LiveOpsBackendInstall.cs` per §4.2. Same fallback semantics.
- Validator becomes blocking (exits non-zero on any finding).

**Demonstrable result:** `git rm -r Assets/Packages/com.scaffold.ads/Backend~/Scaffold/Ads`, then `pwsh -File .agents/scripts/refresh-liveops-template.ps1` recreates the folder from `LiveOps/Scaffold/Ads/` purely because the manifest still claims it. Removing the empty shell is no longer a silent registration failure.

### Phase 3: drop fallback, manifest is required

- Delete the folder-enumeration paths in both scripts.
- Delete `MapCsprojToSolutionFolder`'s path-prefix logic; rely on `solutionFolder` from the manifest.
- Update `Docs/LiveOps/Backend-Authoring-Guide.md`, `Docs/Standards/Module-Vertical-Slice.md`, `Tools/BackendTemplate/com.scaffold.example/README.md` to mention the manifest as a required step in module bootstrap.

**Demonstrable result:** removing `scaffold.backend.slices` from a `package.json` causes refresh / install to skip that package (with a clear log line), proving the manifest is now load-bearing.

---

## 6. Backfill inventory

These are the only packages that currently ship `Backend~/` content (`find Assets/Packages -maxdepth 2 -name "Backend~" -type d`):

### `Assets/Packages/com.scaffold.liveops/package.json`

    "scaffold": {
      "backend": {
        "schemaVersion": 1,
        "slices": [
          { "path": "Deploy/Core",             "kind": "host" },
          { "path": "Deploy/LiveOps",          "kind": "host" },
          { "path": "Deploy/Build",            "kind": "host" },
          { "path": "Deploy/Tools",            "kind": "host" },
          { "path": "LiveOps.Deploy.sln",      "kind": "host-sln" },
          { "path": "Directory.Build.props",   "kind": "host-props" }
        ]
      }
    }

### `Assets/Packages/com.scaffold.ads/package.json`

    "scaffold": {
      "backend": {
        "schemaVersion": 1,
        "slices": [
          { "path": "Scaffold/Ads",     "kind": "runtime", "solutionFolder": "Scaffold/Ads" },
          { "path": "Scaffold/Ads.DTO", "kind": "dto",     "solutionFolder": "Scaffold/Ads" }
        ]
      }
    }

### `Assets/Packages/com.scaffold.directpush/package.json`

    "scaffold": {
      "backend": {
        "schemaVersion": 1,
        "slices": [
          { "path": "Scaffold/DirectPush",     "kind": "runtime", "solutionFolder": "Scaffold/DirectPush" },
          { "path": "Scaffold/DirectPush.DTO", "kind": "dto",     "solutionFolder": "Scaffold/DirectPush" }
        ]
      }
    }

### `Tools/BackendTemplate/com.scaffold.example/package.json`

The canonical copy-paste source. Same shape as a real feature so users can rename `Example` → `<Feature>` in one place after copying:

    "scaffold": {
      "backend": {
        "schemaVersion": 1,
        "slices": [
          { "path": "Scaffold/Example",     "kind": "runtime", "solutionFolder": "Scaffold/Example" },
          { "path": "Scaffold/Example.DTO", "kind": "dto",     "solutionFolder": "Scaffold/Example" }
        ]
      }
    }

The vertical-slice walkthrough (`Docs/Standards/Module-Vertical-Slice.md` §2) gains a step "rename also the `path` and `solutionFolder` values in `package.json`". The template README's rename section gains the same.

---

## 7. Open decisions

These are points where the user can redirect before implementation starts. None block phase 0.

1. **`kind` enum granularity.** Current draft has 5 values. Alternatives:
   - **Two values**: `default` + `host`. Drop `runtime`/`dto` distinction (no reader uses it today anyway), drop `host-sln`/`host-props` (treat any path that ends in `.sln` or `.props` as a single-file copy).
   - **Three values**: `feature`, `host`, `host-file`. Compromise.
   - **Five (current)**: most explicit, biggest enum.
2. **Manifest required vs optional in phase 3.** Hard error if a `Backend~/` folder lacks a manifest, or just refuse to sync that package?
3. **Schema location.** In-repo at `Tools/BackendManifest/` (current draft) or hosted on a stable URL so external consumers' IDEs can `$ref` it remotely? In-repo is the lower-friction choice; pick remote only if you publish these packages to a public registry.
4. **`scaffold` namespace name.** Once shipped in a published package this is a breaking-change rename. Alternatives: `scaffoldBackend` (no namespace), `x-scaffold` (npm-extension convention), `com.scaffold` (matches Unity vendor prefix). Recommendation: stick with `scaffold` — matches the rest of the repo's naming.

---

## 8. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Unity's package validator strips unknown top-level fields. | Verify on the active editor (6000.3.x). Standard pattern in npm-derived tooling, very low risk; if it happens, fall back to a sidecar file in `Backend~/`. |
| Phase-2 scripts diverge from Phase-3 scripts during the migration window. | Keep both code paths in one file with `-StrictManifest` switch (default on). Single PR removes the fallback in Phase 3. |
| A consumer who pulled an older version of a package without the manifest hits a "no manifest" error after upgrading their tooling. | Phase 2 fallback handles this. Phase 3 release notes document the requirement. |
| `package.json` becomes a kitchen sink for unrelated metadata. | Reserve the single `scaffold.{subkey}` namespace and review additions in PR. v1 only ships `scaffold.backend`. |

---

## 9. Decision log

- **YYYY-MM-DD** — initial draft. Chose `package.json` `scaffold.backend` over a sidecar `backend.manifest.json` because `package.json` is already in every package's touch-rotation (versioning, dependencies), and a sidecar would just add a *second* file in the same rotation rather than a "set-and-forget" file.
