# Modular UPM packages from Scaffold modules

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Repository policy for ExecPlans is defined in `PLANS.md` at the repository root. This document must be maintained in accordance with that file.

## Purpose / Big Picture

First-party modules now live under **`Assets/Packages/com.scaffold.*/`** as **Unity Package Manager (UPM)**–style trees: each has a **`package.json`** at its root. The **Scaffold** Unity project remains the **holder** (development hub); those folders compile as project assets without `file:` entries in the holder `Packages/manifest.json`. Other Unity projects install individual modules by declaring a **Git dependency with a subpath** (a single Git URL plus a `path=` query that points at one package folder inside this repository).

Someone can add only `com.scaffold.events` (for example) to a foreign `Packages/manifest.json`, resolve dependencies, and compile against the same assemblies as in Scaffold. You can see it working when a clean external project imports one subpath package, the Package Manager shows the package with the expected version and dependencies, and play mode or a minimal compile succeeds.

## Progress

- [x] Author initial ExecPlan (this file) and check it into `Plans/ModulesAsUpmPackages/ModulesAsUpmPackages-ExecPlan.md`.
- [x] Decide package identity and folder convention (naming, which roots are in scope, generators, optional exclusions) and record the decision in `Decision Log`.
- [x] Prototype milestone: **`com.scaffold.types`** lives in **`Assets/Packages/com.scaffold.types/`** with `package.json`, samples entry, and `README.md`. The **holder** does **not** list it in `Packages/manifest.json` (sources compile as project assets). Consumer **`C:\Unity\AITest\AITest`** references the package via **`file:../../../Scaffold/Assets/Packages/com.scaffold.types`**. **Git subpath** proof uses **`?path=/Assets/Packages/com.scaffold.types`** (deferred until remote URL + branch/tag).
- [x] Define the **directed dependency graph** between packages and encode **`dependencies`** in each `package.json` (internal `com.scaffold.*` at `0.1.0`, including **`com.scaffold.schemas`** under `Assets/Packages/com.scaffold.schemas/`, Unity/registry versions aligned with the holder `Packages/manifest.json` where declared). Migration script: `.agents/scripts/migrate-scaffold-packages.ps1` (already executed).
- [x] Migrate **all** listed modules **plus** **`com.scaffold.autopacker`** and **MVVM generators** (`Assets/Generators/MVVM` → `com.scaffold.mvvm/GeneratorsMVVM`). **`Assets/Scripts`** and **`Assets/Generators`** trees removed after move (empty). Assembly **names** unchanged; **GUIDs** preserved via folder moves.
- [x] **Asmdef reference audit** updated: `.agents/scripts/check-scripts-asmdef-references.ps1` accepts optional **`Assets/Scripts`** (if present), root **`Packages`**, and **automatic** **`Assets/Packages/com.scaffold.*`** roots; **asmdefs** without a **`references`** property are handled. Parameter `-ScriptsRoots` still overrides/extends defaults.
- [x] Consumer documentation: **`Docs/ConsumingScaffoldPackages.md`** (Git subpath, `file:`, versioning pointers). **`Architecture.md`**, module **`Docs/*.md`**, **`create-module`** workflow, and **`rewrite-docs-package-paths.ps1`** updated for new paths.
- [x] Tests: existing **Tests** assemblies moved with their packages; empty **Tests** stubs trimmed where applicable. **Samples** entries in `package.json`: added where the migration script wrote manifests; extend with full **`samples`** arrays per package when you want Package Manager sample imports (optional polish).
- [x] Outcomes & Retrospective: see **Outcomes & Retrospective** (summary below).

## Surprises & Discoveries

Document unexpected behaviors, bugs, optimizations, or insights discovered during implementation. Provide concise evidence.

- Observation: `validate-changes.ps1` exits **2** when `check-analyzers.ps1` reports **TOTAL** greater than zero (diagnostic count), even when **BLOCKERS:0**. In this repo most of those diagnostics come from third-party code under `Assets/Packages/AAGen-0.3.0/`, not from the Types package move.
  Evidence: After the prototype, asmdef audit **TOTAL:0**, compilation **PASS**; analyzer summary line: **Analyzers: FAIL (TOTAL:464, BLOCKERS:0)**.

- Observation: The **AITest** Unity project path is **`C:\Unity\AITest\AITest`** (nested folder), not `C:\Unity\AITest` at the project root.
  Evidence: `Packages/manifest.json` found only under `AITest/AITest/`.

- Observation: Scanning **all** of **`Assets/Packages/**`** in the asmdef audit pulls in third-party asmdefs (e.g. **AAGen**) and produces false **MissingScriptsGuidReference** issues. **Fix:** auto-include only **`Assets/Packages/com.scaffold.*`** directories as roots, not the parent `Assets/Packages` folder.
  Evidence: `TOTAL:5` failures referencing `AAGen` asmdefs until the script was narrowed.

- Observation: Some **asmdef** files (e.g. **`Scaffold.Autopacker`**) omit a **`references`** property entirely. **Strict** access to `$json.references` in **`check-scripts-asmdef-references.ps1`** threw. **Fix:** treat missing **`references`** as an empty array.
  Evidence: Script failure until the property guard was added; then **TOTAL:0**.

## Decision Log

- Decision: **Module list** — The sixteen first-party module packages listed under **Context and Orientation** (`Assets/Packages/com.scaffold.*`) are **in scope** as one UPM package each, plus **`com.scaffold.autopacker`**.
  Rationale: Confirmed by stakeholder; matches current architecture boundaries.
  Author: Stakeholder (2026-03-30).

- Decision: **In-repo generators** — Ship **AutoPacker** and **MVVM-related generators** as part of packaging. **AutoPacker** is its own package (`com.scaffold.autopacker`) under `Assets/Packages/`, with C# projects under `Generators/AutoPacker/`. **MVVM** tooling ships **together** with `com.scaffold.mvvm`: runtime **and** `GeneratorsMVVM/` (former `Assets/Generators/MVVM`); analyzer build outputs remain under `Generators/MVVMCompositionGenerator` and `Generators/Scaffold.Mvvm.Analyzers` as today.
  Rationale: Generators are first-class deliverables; MVVM infra and MVVM generators are one product surface for consumers.
  Author: Stakeholder (2026-03-30).

- Decision: **Package identity and disk layout (holder)** — Each package uses **`com.scaffold.<module>`** for the `package.json` **name** field (example: `com.scaffold.events`). In **this** repository, package roots with `package.json` live under **`Assets/Packages/<packageName>/`** (example: `Assets/Packages/com.scaffold.events/`) so they are edited as **normal assets** in the holder. The **holder** does **not** require **`file:`** entries in **`Packages/manifest.json`** for those folders. **Consumers** install via UPM (Git subpath or `file:`), e.g. **`?path=/Assets/Packages/com.scaffold.events`**; Unity then resolves the package in the consumer’s package layout (not under `Assets/`).
  Rationale: Develop in-repo like project code; keep consumer install story standard UPM.
  Author: Stakeholder (2026-03-30); holder path revised 2026-03-30 to **`Assets/Packages/`**.

- Decision: **Versioning** — Follow **Semantic Versioning 2.0.0** (industry default): **MAJOR** for breaking API or dependency contract changes, **MINOR** for backward-compatible additions, **PATCH** for backward-compatible fixes. Document **when / where / how** to bump versions in this ExecPlan (see **Versioning** section).
  Rationale: Interoperable with UPM and Git tags; familiar to consumers.
  Author: Stakeholder (2026-03-30).

- Decision: **UPM dependency graph** — Every `package.json` **dependencies** map must reflect **both** internal `com.scaffold.*` edges **and** required Unity/third-party packages (mirror `.asmdef` and runtime needs). See **Package dependency examples**.
  Rationale: Minimal consumer projects resolve everything without hand-editing beyond the Scaffold lines.
  Author: Stakeholder (2026-03-30).

- Decision: **Prototype** — First migrated package: **Types** (`com.scaffold.types`). External validation project path: **`C:\Unity\AITest`** (create or use this Unity project to verify Git URL + subpath install).
  Rationale: Types is a suitable leaf for a vertical slice; fixed path gives a single reproducible consumer for acceptance.
  Author: Stakeholder (2026-03-30).

- Decision: **Tests and samples** — If a module **already** has tests under its tree, **keep** them **inside** the same package. If a module has **no** tests today, **do not add** test folders for migration—**trim** (omit). **Samples** are registered **per package** via `package.json` **samples** (Unity standard) where samples exist.
  Rationale: Avoid empty test cruft; match Unity package conventions for samples.
  Author: Stakeholder (2026-03-30).

- Decision: **Documentation and automation** — Update **`Architecture.md`** and **`Docs/`** for **`Assets/Packages/com.scaffold.*/...`** package roots. **Repository scripts** must accept **`Assets/Scripts/**`, each **`Assets/Packages/com.scaffold.*`** tree, and root **`Packages/**`** (embedded packages, if any). Do **not** treat all of **`Assets/Packages/**`** as first-party (other vendor folders may live there).
  Rationale: Multi-root support without auditing third-party packs under `Assets/Packages`.
  Author: Stakeholder (2026-03-30); refined 2026-03-30 after `AAGen` asmdef noise when scanning the whole `Assets/Packages` tree.

- Decision: **Legal** — Follow **common Unity/open-source package practice**: each publishable package includes a **LICENSE** file (or clear pointer to a top-level license if using a single repo license), and **third-party** notices where bundled binaries or generators require attribution (see **Legal and third-party**).
  Rationale: Industry-standard expectations for redistributable packages.
  Author: Stakeholder (2026-03-30).

## Outcomes & Retrospective

Summarize outcomes, gaps, and lessons learned at major milestones or at completion. Compare the result against the original purpose.

- **Achieved:** All first-party modules listed in **Context and Orientation** now live under `Assets/Packages/com.scaffold.*/` with `package.json` and internal dependency edges; AutoPacker and MVVM generator **assets** are folded into `com.scaffold.autopacker` and `com.scaffold.mvvm` respectively. Holder uses no `file:` entries for these trees. Consumer patterns are documented in `Docs/ConsumingScaffoldPackages.md`; `Architecture.md` and module docs paths were updated via `.agents/scripts/rewrite-docs-package-paths.ps1`.
- **Gaps:** Unity Edit/PlayMode tests are not re-run here; open the project in Unity to refresh the asset database. `validate-changes.ps1` may still report analyzer **TOTAL** greater than zero from third-party code (e.g. AAGen). Optional polish: add full **`samples`** arrays to every `package.json` for one-click sample import in consumers.

## Context and Orientation

**Unity Package Manager (UPM)** is Unity’s built-in system for installing and versioning **packages**. A **package** here means a folder that contains a **`package.json`** file at its root. That manifest declares the package **name** (reverse-DNS, for example `com.scaffold.events`), **version**, optional **dependencies** on other packages by name and version range, Unity version constraints, and metadata such as **displayName** and **description**. Unity’s editor loads package roots from the holder project’s `Packages/manifest.json` and merges them into the same compilation graph as `Assets/`.

**Git URL with subpath** means another project’s `Packages/manifest.json` can depend on a package that lives in a **subfolder** of a Git repository, using a URL form that includes `?path=/path/to/package/root` (and usually a revision such as a branch name or tag after `#`). Package Manager clones the repository and treats only that folder as the package root. This matches the goal of publishing many packages from one Git repo without maintaining separate remotes per module.

**Holder project** means this Scaffold Unity project: the place where all packages are developed together, tested together, and versioned in one Git history. Package sources intended for UPM publication are developed under **`Assets/Packages/<packageName>/`** and compile as **project assets**; the holder **does not** need **`file:`** lines in **`Packages/manifest.json`** for those folders.

**Module** in this repository means a bounded script area that already owns one or more `.asmdef` files. After migration, the **authoritative** location for first-party package trees in the holder is **`Assets/Packages/<packageName>/`**, with documentation updated accordingly. The following **module roots** (all **in scope**) become **one UPM package each**, using **`com.scaffold.<short-name>`** derived from the module (see **Decision Log** for naming):

- `Assets/Packages/com.scaffold.bootstrap` → `com.scaffold.bootstrap`
- `Assets/Packages/com.scaffold.view` → `com.scaffold.view`
- `Assets/Packages/com.scaffold.addressables` → `com.scaffold.addressables`
- `Assets/Packages/com.scaffold.entities` → `com.scaffold.entities`
- `Assets/Packages/com.scaffold.liveops` → `com.scaffold.liveops`
- `Assets/Packages/com.scaffold.viewmodel` → `com.scaffold.viewmodel`
- `Assets/Packages/com.scaffold.cloudcode` → `com.scaffold.cloudcode`
- `Assets/Packages/com.scaffold.events` → `com.scaffold.events`
- `Assets/Packages/com.scaffold.mvvm` → `com.scaffold.mvvm` (**includes** `GeneratorsMVVM/` from the former `Assets/Generators/MVVM`; analyzer **build** projects remain under repo `Generators/` as today)
- `Assets/Packages/com.scaffold.model` → `com.scaffold.model`
- `Assets/Packages/com.scaffold.navigation` → `com.scaffold.navigation`
- `Assets/Packages/com.scaffold.sceneflow` → `com.scaffold.sceneflow`
- `Assets/Packages/com.scaffold.scope` → `com.scaffold.scope`
- `Assets/Packages/com.scaffold.ugs` → `com.scaffold.ugs`
- `Assets/Packages/com.scaffold.maps` → `com.scaffold.maps`
- `Assets/Packages/com.scaffold.records` → `com.scaffold.records`
- `Assets/Packages/com.scaffold.types` → `com.scaffold.types`

**Additional in-scope package (generators):**

- **AutoPacker** — `com.scaffold.autopacker` at `Assets/Packages/com.scaffold.autopacker/`; C# projects for builds remain under `Generators/AutoPacker/` at the repo root.

**General-purpose Roslyn analyzers** under `Analyzers/Scaffold/Scaffold.Analyzers` (non-MVVM) are **not** part of the sixteen-module list; treat them as **optional follow-up** unless product policy requires shipping them as a UPM package. **Repository scripts** under `.agents/scripts/` stay repo tooling, not consumer packages.

**Current coupling:** Assembly references are already explicit in `.asmdef` files (by assembly name or GUID). After packaging, those assembly names stay the same unless you intentionally rename. The **package graph** must **not** introduce cycles; mirror the existing acyclic `.asmdef` graph and declare **UPM** dependencies so Package Manager orders resolution correctly.

## Versioning

Follow **Semantic Versioning 2.0.0** (the public SemVer specification: MAJOR.MINOR.PATCH with the usual compatibility rules).

**When to bump**

- **MAJOR** — Breaking changes: removed or renamed public APIs, changed behavior that forces consumer code changes, or a **breaking** change in declared `dependencies` (for example dropping support for an older `com.unity.*` line).
- **MINOR** — Backward-compatible new features, new optional dependencies, or additive APIs.
- **PATCH** — Bug fixes and internal refactors that do not change the public contract.

**Where to update**

- **Primary:** each package’s own `Assets/Packages/<packageName>/package.json` **version** field (in this repo’s holder layout).
- **Changelog:** `Assets/Packages/<packageName>/CHANGELOG.md` (recommended for published packages), with a new entry for every version consumers might pull.
- **Git:** tag the repository when you cut a release consumers should pin (common monorepo patterns: either **one tag** for the whole repo at a point in time, or **per-package tags** if you adopt them—document the team’s chosen tag scheme in `Docs/` when implementation picks one).

**How to update**

1. Edit `CHANGELOG.md` under that package (Added / Changed / Fixed / Removed as appropriate).
2. Bump `version` in `package.json` for that package.
3. Commit with a message that names the package id and new version (example: `Release com.scaffold.types 1.2.0`).
4. Push and (if used) create or move the Git tag that consumers reference in `manifest.json` (`#v1.2.0` or branch name per team policy).

**Monorepo note:** Many teams either version all `com.scaffold.*` packages **in lockstep** (same version number everywhere) or **independently** per package. Pick one policy when rollout begins and record it in `Decision Log` if it differs from independent versioning.

## Package dependency examples

**Purpose:** `package.json` **dependencies** tell UPM which other packages to install first. They must cover **Scaffold packages** and **Unity/registry** packages your assemblies need.

**Rules of thumb**

1. If assembly **A** in package **P** has an `.asmdef` reference to assembly **B** shipped from package **Q**, then **P**’s `package.json` must list **`"com.scaffold.q": "x.y.z"`** (or a compatible range) for **Q**’s actual package name.
2. If **A** references **`Unity.Addressables`**, declare **`"com.unity.addressables": "..."`** in **P**’s `package.json` (match holder versions unless you document a wider range).
3. If **A** references **`jp.hadashikick.vcontainer`**, declare it explicitly—consumers do not inherit it automatically from another Scaffold package unless that package re-exports it (prefer explicit dependencies for clarity).
4. **Transitive resolution:** UPM merges dependencies; avoid declaring duplicates with conflicting version ranges. Prefer aligning with `Packages/manifest.json` in the holder until you publish compatibility matrices.

**Illustrative snippets (versions are placeholders—replace with real versions from the holder at implementation time)**

Example A — a leaf-style package that only needs Unity baseline and one Unity module:

    {
      "name": "com.scaffold.types",
      "version": "1.0.0",
      "unity": "2022.3",
      "displayName": "Scaffold Types",
      "description": "…",
      "dependencies": {
        "com.unity.nuget.newtonsoft-json": "3.2.1"
      }
    }

Example B — a package that depends on another Scaffold package **and** VContainer:

    {
      "name": "com.scaffold.events",
      "version": "1.0.0",
      "unity": "2022.3",
      "displayName": "Scaffold Events",
      "description": "…",
      "dependencies": {
        "com.scaffold.scope": "1.0.0",
        "jp.hadashikick.vcontainer": "1.17.0"
      }
    }

Example C — consumer project pulling one Git subpath package (prototype target **Types**):

    {
      "dependencies": {
        "com.scaffold.types": "https://github.com/YourOrg/Scaffold.git?path=/Assets/Packages/com.scaffold.types#main"
      }
    }

**Authoritative graph work:** During implementation, derive the real **com.scaffold.*** edges by walking each module’s `.asmdef` **references** that point at other **Scaffold** assemblies and map assembly → owning package id. Record any edge case (GUID-only references, precompiled DLLs) in **Surprises & Discoveries**.

## Plan of Work

**Naming and layout.** Use **`com.scaffold.<module>`** and **`Assets/Packages/<packageName>/`** in the holder as in **Decision Log**. Migrate each module’s runtime, container, tests, editor, and samples subtrees into that folder preserving internal structure (`Runtime/`, `Container/`, etc.).

**Prototype first.** Implement **`com.scaffold.types`** first; validate the consumer at **`C:\Unity\AITest`** with Git URL + subpath. Then proceed in dependency order.

**Dependency manifests.** Fill **`dependencies`** per **Package dependency examples** and the derived graph.

**Guid and asset moves.** Preserve **GUIDs** in `.meta` files when moving; fix hard-coded asset paths if any.

**Documentation and architecture.** Update `Architecture.md` and `Docs/` for **`Assets/Packages/...`** locations.

**Automation.** Update scripts so validation and audits treat **`Assets/Scripts/**`, **`Assets/Packages/**`, and root **`Packages/**`** as valid roots for assemblies and package layouts.

## Concrete Steps

Work from the repository root (example: `C:\Unity\Scaffold`).

**Prototype (Types + AITest)**

    1. Create `Assets/Packages/com.scaffold.types/package.json` with name `com.scaffold.types`, initial version (for example `0.1.0`), `unity` constraint aligned with `ProjectSettings/ProjectVersion.txt`, and `dependencies` derived from `Scaffold.Types` asmdefs.
    2. Move `Assets/Packages/com.scaffold.types` content into `Assets/Packages/com.scaffold.types/`, preserving `Runtime/`, `Editor/`, `Samples/`, `Tests/` as applicable. Apply **Tests and samples** policy (keep existing tests; trim if none).
    3. **Holder:** do **not** add `com.scaffold.types` to `Packages/manifest.json` (optional: consumers only).
    4. Open Unity in Scaffold, let it compile, fix missing references.
    5. Run: powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests
    6. In a consumer project, add `"com.scaffold.types": "https://<your-remote>/Scaffold.git?path=/Assets/Packages/com.scaffold.types#<branchOrTag>"` (or a `file:` URL to the same folder for local testing).

**Rollout**

    Repeat for each remaining package in **dependency order**. After each step, re-run the validate script until clean. Add **`com.scaffold.autopacker`** and expand **`com.scaffold.mvvm`** with generator trees per **Decision Log**.

## Validation and Acceptance

**Holder project:** Unity compiles with zero errors. `validate-changes.ps1 -SkipTests` passes with scripts recognizing **`Assets/Packages/**`** (and other configured roots).

**Consumer (`C:\Unity\AITest`):** `com.scaffold.types` resolves via Git subpath; a minimal script compiles against a documented type.

**Documentation:** `Architecture.md` and `Docs/` reflect package ids and paths; **Versioning** and dependency examples remain accurate.

## Idempotence and Recovery

Use version control commits per package migration; rollback via `git revert`. Prefer **one package per commit** to bisect failures.

## Artifacts and Notes

**Consumer `file:` pattern (local path to repo folder):**

    "com.scaffold.types": "file:../path/to/Scaffold/Assets/Packages/com.scaffold.types"

**Consumer Git subpath pattern:**

    "com.scaffold.types": "https://github.com/YourOrg/Scaffold.git?path=/Assets/Packages/com.scaffold.types#main"

## Interfaces and Dependencies

**Required artifacts per package**

- `package.json` with **name**, **version**, **displayName**, **description**, **unity**, **dependencies** as needed.
- `.asmdef` layout preserved inside the package; assembly **names** unchanged unless a deliberate rename is approved.
- **CHANGELOG.md** recommended when publishing versions consumers track.
- **Samples** registered in `package.json` **samples** when the package ships sample content (per-package).

**Package dependency graph**

- Must match **.asmdef** edges between Scaffold-owned assemblies, expressed as **com.scaffold.*** dependencies, plus **Unity/third-party** packages per **Package dependency examples**.

**External Unity packages**

- Declare **`com.unity.*`**, **`jp.hadashikick.vcontainer`**, and other registry packages explicitly where the code references them.

## Legal and third-party

- Include a **LICENSE** file in each published package folder (or a single repo **LICENSE** referenced consistently if the org uses one license for all packages—still document which applies).
- For **bundled binaries** (generator DLLs, CommunityToolkit, Newtonsoft, etc.), follow common practice: **Third Party Notices** or **NOTICE** file in the package when redistribution obligations apply; list versions where useful.
- Do not strip existing license headers from source files when moving trees.

---

Revision history:

- 2026-03-30: Initial ExecPlan authored. Captures holder-project workflow, Git subpath consumption, candidate module list from `Assets/Scripts/`, validation via `validate-changes.ps1`, and a prototype-first rollout to reduce migration risk.
- 2026-03-30: Stakeholder decisions recorded (naming, `Packages/<packageName>/`, SemVer, dependency graph, prototype Types + `C:\Unity\AITest`, tests/samples policy, dual-path automation, legal). Added AutoPacker (`com.scaffold.autopacker`) and MVVM+generators scope; added **Versioning**, **Package dependency examples**, **Legal and third-party**.
- 2026-03-30: Prototype executed: `com.scaffold.types` embedded under `Packages/`, holder manifest wired, `check-scripts-asmdef-references.ps1` dual-root, `Docs/Tools/Types.md` and `Docs/Testing/Testing.md` updated, AITest consumer manifest updated with `file:` dependency.
- 2026-03-30: Holder layout changed to **`Assets/Packages/com.scaffold.types/`** (no `file:` entry in holder `manifest.json`); `packages-lock.json` and `manifest.json` updated; asmdef audit includes **`Assets/Packages`**; AITest `file:` path and ExecPlan paths updated to **`/Assets/Packages/...`** for Git subpaths.
- 2026-03-30: Full rollout: all modules + **`com.scaffold.autopacker`** moved to **`Assets/Packages/`**; **`Docs/ConsumingScaffoldPackages.md`** added; **`rewrite-docs-package-paths.ps1`** and **`migrate-scaffold-packages.ps1`** added; **`Assets/Scripts`** and empty **`Assets/Generators`** removed; asmdef script handles missing **`references`**; **`Architecture.md`** and ExecPlan aligned.
