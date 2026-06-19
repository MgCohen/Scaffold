# Package versioning workflow for Scaffold UPM packages

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Repository policy for ExecPlans is defined in `PLANS.md` at the repository root. This document must be maintained in accordance with that file.

This plan extends `Plans/ModulesAsUpmPackages/ModulesAsUpmPackages-ExecPlan.md`, which already established the **`Assets/Packages/com.scaffold.*/`** UPM layout, holder-vs-consumer mechanics, and SemVer policy. The work below adds the **author-time workflow and tooling** that turn "I edited a package" into "consumers see a new version".

## Purpose / Big Picture

Today every first-party package under `Assets/Packages/com.scaffold.*/` carries `"version": "0.1.0"` and inter-package dependencies pinned to literal `"0.1.0"`. When we change code inside, for example, `com.scaffold.liveops`, nothing about the package surface advertises that change: the version stays at `0.1.0`, the package's `CHANGELOG.md` is missing, the holder's `Packages/packages-lock.json` does not move, and any consumer that pulls the Git subpath dependency at `#main` either silently picks up the new code (with no version signal) or is stuck on a stale commit if it pinned to a tag. Reverse dependencies (`com.scaffold.model` depends on `com.scaffold.mvvm`, `com.scaffold.liveops` depends on `com.scaffold.cloudcode` and `com.scaffold.appflow`, and so on) never have their `dependencies` versions bumped either, so the dependency graph encoded in `package.json` lies about what was tested together.

After this plan, an author who finishes a change to one (or several) packages can run a **single command from the repository root** that:

- asks them, per touched package, what kind of change it was (patch, minor, major) and a one-line summary,
- bumps each touched package's `package.json` `version`,
- bumps every reverse dependency's matching `dependencies` entry to the new version (and bumps that reverse dependency's own `version` according to the team's chosen propagation policy),
- writes a new entry in each touched package's `CHANGELOG.md` (creating it if missing),
- creates a Git commit per package release (so `git bisect` and history reads cleanly), and
- creates the matching **per-package Git tags** (`com.scaffold.liveops/v0.2.0`) that consumers can pin in their `manifest.json` via `?path=/Assets/Packages/com.scaffold.liveops#com.scaffold.liveops/v0.2.0`.

The user can prove it works by running the command, observing the version diff in the touched `package.json`, the new `CHANGELOG.md` entry, the commit(s), the tag(s), and then in a separate Unity consumer project changing the manifest pin from `#main` to `#com.scaffold.liveops/v0.2.0` and seeing Package Manager show **0.2.0** with the new entry.

## Progress

- [x] Inventory current state: 24 `package.json` files under `Assets/Packages/`, all at `0.1.0`, no `CHANGELOG.md` files, no Git tags, no per-package release tooling.
- [x] Survey industry standards (SemVer 2.0.0, Keep a Changelog, Conventional Commits, Changesets, release-please, semantic-release, Unity UPM Git subpath syntax, Unity package validation rules) and summarize trade-offs in **Options Considered**.
- [x] Recommend a concrete workflow (see **Recommended Workflow**) and pick the tooling shape (see **Decision Log**).
- [ ] Implement Milestone 1: `package.json` schema fixes and propagation rules baseline (canonicalize formatting, normalize versions, document reverse-dependency map).
- [ ] Implement Milestone 2: `bump-package.ps1` PowerShell CLI under `.agents/scripts/release/` with `-Package`, `-Bump {patch|minor|major}`, `-Summary`, `-DryRun`, `-NoCommit`, `-NoTag`.
- [ ] Implement Milestone 3: `/release-package` Cursor workflow file under `.agents/workflows/release-package.md` so the agent can drive the bump from a chat request.
- [ ] Implement Milestone 4: changelog scaffolding + `CHANGELOG.md` template per package, written/appended automatically.
- [ ] Implement Milestone 5: validation: `validate-changes.ps1` (or a dedicated `validate-packages.ps1`) refuses commits where a package's source under `Assets/Packages/<id>/` changed but its `version` did **not** change since the last tag for that package id.
- [ ] Implement Milestone 6: optional CI hook (GitHub Actions) that, on push to `main`, reads the latest tags and surfaces packages whose `HEAD` is ahead of their last release tag.
- [ ] Update `Docs/ConsumingScaffoldPackages.md` and `Plans/ModulesAsUpmPackages/ModulesAsUpmPackages-ExecPlan.md` cross-links once the tooling is in place.

## Surprises & Discoveries

- Observation: Two `package.json` formatting styles coexist. Most files were emitted by `migrate-scaffold-packages.ps1` with PowerShell's `ConvertTo-Json` (deeply indented, `"name":  "..."` with two spaces after the colon). A handful (e.g., `com.scaffold.appflow`, `com.scaffold.ads`, `com.scaffold.ads.levelplay`, `com.scaffold.directpush`) are hand-authored with two-space JSON style. Any tooling that rewrites `package.json` must preserve or canonicalize this, or every release commit will be polluted by formatting churn.
  Evidence: Compare `Assets/Packages/com.scaffold.types/package.json` (PowerShell style) with `Assets/Packages/com.scaffold.appflow/package.json` (standard JSON style).

- Observation: `com.scaffold.appflow/package.json` declares `jp.hadashikick.vcontainer` as a **Git URL** in `dependencies`, which is **not** a valid Unity UPM `dependencies` value. Unity only accepts SemVer ranges in `dependencies`; Git URLs are only legal in the consumer's `Packages/manifest.json` (or as transitive entries Unity already resolved). This will surface the moment we tighten validation; we should fix it as part of Milestone 1 (probably by using `"jp.hadashikick.vcontainer": "1.17.0"` and letting consumers add the Git URL to their root manifest).
  Evidence: `Assets/Packages/com.scaffold.appflow/package.json` line 13. Compare with `com.scaffold.ads`, `com.scaffold.ads.levelplay`, and `com.scaffold.directpush`, which use the SemVer form.

- Observation: There are zero Git tags in the repository today (`git tag` returns empty). That means there is no historical "last release" anchor for any package; the first run of any release tool needs a "treat current `HEAD` as `0.1.0`" bootstrap step.
  Evidence: `git tag | wc -l` → `0`.

## Decision Log

- Decision: Use **independent SemVer per package**, not lockstep.
  Rationale: Sixteen-plus packages with very different change cadences (`com.scaffold.types` is essentially stable; `com.scaffold.liveops` and `com.scaffold.mvvm` change frequently). Lockstep would force consumers of stable packages to re-pin and re-validate on every unrelated change. The `ModulesAsUpmPackages` ExecPlan already named "independent or lockstep" as an open choice; this plan picks independent.
  Author: Plan author (2026-04-25).

- Decision: Adopt **per-package Git tags** of the form `com.scaffold.<id>/v<MAJOR>.<MINOR>.<PATCH>` (for example `com.scaffold.liveops/v0.2.0`).
  Rationale: Unity's Git subpath syntax allows any Git revision after `#`, including tags with slashes. Per-package tags let consumers pin one package without dragging in unrelated changes from a repo-wide tag, and they make `git log com.scaffold.liveops/v0.1.0..com.scaffold.liveops/v0.2.0 -- Assets/Packages/com.scaffold.liveops` produce a clean, package-scoped history. The slash separator is the convention popularized by Lerna, Nx, Changesets, and release-please for monorepos.
  Author: Plan author (2026-04-25).

- Decision: Build a **PowerShell-first CLI** (`.agents/scripts/release/bump-package.ps1`) rather than adopting Changesets or release-please as a hard dependency.
  Rationale: The repository's tooling baseline is PowerShell (`.agents/scripts/*.ps1`), the `package.json` files are not npm packages (no `node_modules`, no workspaces), and Changesets / release-please both assume a Node toolchain and a publish target like the npm registry. Wrapping our own CLI keeps the workflow native to the existing scripts directory, integrates cleanly with `validate-changes.ps1`, and lets us call it from a Cursor workflow file (`/release-package`). We can still mirror the **mental model** of Changesets (declare intent → bot bumps versions → tags) without inheriting its runtime.
  Author: Plan author (2026-04-25).

- Decision: Use the **Keep a Changelog 1.1.0** structure (`Added`, `Changed`, `Deprecated`, `Removed`, `Fixed`, `Security`) for `CHANGELOG.md`, with version headings of the form `## [0.2.0] - 2026-04-25`.
  Rationale: It is the dominant human-readable changelog standard, plays well with both manual edits and tooling, and matches the section vocabulary already used by Unity's own first-party package changelogs (e.g., `com.unity.addressables`).
  Author: Plan author (2026-04-25).

- Decision: Reverse-dependency propagation policy is **patch by default, configurable per run**.
  Rationale: When `com.scaffold.cloudcode` ships a patch, `com.scaffold.liveops` (which depends on it) needs at minimum a `dependencies` rewrite so its `package.json` truthfully says "I was tested against `cloudcode@0.1.1`". Bumping the dependent's own version one patch level is the conservative SemVer-correct default, since a transitive change is technically a change. The CLI exposes `-PropagateAs {none|patch|minor|major}` so a major bump in a leaf can be propagated as major to its dependents when the API really did break for them.
  Author: Plan author (2026-04-25).

## Outcomes & Retrospective

To be filled at the end of each implemented milestone. After Milestone 6 the plan should record:

- whether the CLI is being invoked manually or via the `/release-package` workflow most of the time,
- how many releases have been cut and whether any consumer reported a broken pin,
- whether the validation gate caught any "forgot to bump" situations,
- whether per-package tags caused any noticeable Git UX issues (for example slow `git fetch --tags`).

## Context and Orientation

The repository is a Unity holder project. First-party modules are UPM packages under `Assets/Packages/com.scaffold.<id>/`, each containing a `package.json` with at minimum `name`, `version`, `displayName`, `description`, `unity`, and `dependencies`. The holder project compiles those folders as ordinary assets and does **not** list them in `Packages/manifest.json`. External Unity projects consume them by adding a Git subpath entry to **their** `Packages/manifest.json`, like:

    "com.scaffold.liveops": "https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.liveops#main"

The 24 packages currently present:

- `com.scaffold.addressables`
- `com.scaffold.ads`
- `com.scaffold.ads.levelplay`
- `com.scaffold.appflow`
- `com.scaffold.autopacker`
- `com.scaffold.cloudcode`
- `com.scaffold.directpush`
- `com.scaffold.entities`
- `com.scaffold.events`
- `com.scaffold.liveops`
- `com.scaffold.maps`
- `com.scaffold.model`
- `com.scaffold.mvvm`
- `com.scaffold.navigation`
- `com.scaffold.pooling`
- `com.scaffold.records`
- `com.scaffold.sceneflow`
- `com.scaffold.schemas`
- `com.scaffold.states`
- `com.scaffold.types`
- `com.scaffold.ugs`
- `com.scaffold.view`
- `com.scaffold.viewmodel`
- (plus the third-party `AAGen-0.3.0` which is **out of scope** for this workflow)

All first-party packages are at `version: 0.1.0`. Inter-package edges that exist today (derived from the `dependencies` blocks):

- `com.scaffold.events` → `com.scaffold.types`
- `com.scaffold.maps` → `com.scaffold.records`
- `com.scaffold.mvvm` → `com.scaffold.maps`, `com.scaffold.records`
- `com.scaffold.model` → `com.scaffold.mvvm`
- `com.scaffold.addressables` → `com.scaffold.types`, `com.scaffold.maps`, `com.scaffold.appflow`, `com.unity.addressables`
- `com.scaffold.liveops` → `com.scaffold.cloudcode`, `com.scaffold.appflow`
- `com.scaffold.ads.levelplay` → `com.scaffold.ads`, `com.unity.services.levelplay`
- `com.scaffold.cloudcode` → `com.unity.services.cloudcode`, `com.unity.nuget.newtonsoft-json`
- `com.scaffold.directpush` → `jp.hadashikick.vcontainer`
- `com.scaffold.appflow` → `jp.hadashikick.vcontainer` (as Git URL — see **Surprises & Discoveries**)

This is the graph the bump tool must walk to propagate a release.

## Industry standards research

Three families of practice are relevant. The plan picks pieces from each rather than adopting any one wholesale.

**Semantic Versioning 2.0.0** (`MAJOR.MINOR.PATCH`) is the only version-number contract Unity Package Manager understands in `dependencies`. The rules: increment MAJOR on incompatible API changes, MINOR on backward-compatible feature additions, PATCH on backward-compatible bug fixes. Pre-1.0 packages are allowed to break compatibility on MINOR bumps, but it is still polite to flag breakage in the changelog. All `com.scaffold.*` packages are pre-1.0 today.

**Keep a Changelog 1.1.0** specifies that every release gets a `## [version] - date` heading and groups changes under fixed verbs (`Added`, `Changed`, `Deprecated`, `Removed`, `Fixed`, `Security`). It also reserves `## [Unreleased]` at the top for in-progress notes. Unity's own first-party packages (e.g., `com.unity.addressables`) follow this format, so consumers will find it familiar.

**Conventional Commits** (`feat:`, `fix:`, `chore:`, `BREAKING CHANGE:`) is a complementary convention that lets tools infer SemVer impact from commit messages. We do **not** require it here, but the CLI accepts a `-FromConventionalCommits` flag in a stretch milestone for users who want it.

**Monorepo release tools** in the wider ecosystem split into two camps:

- *Intent-declared* (Changesets): the author writes a small markdown file (`.changeset/<random>.md`) that says "package X, minor, summary" at the time of the change. A bot later collects all pending changesets, computes the version bumps, updates `package.json`s and changelogs, and opens a "Version Packages" PR. This decouples *making the change* from *cutting the release*. It scales well to teams with many contributors and a release manager.
- *Commit-derived* (release-please, semantic-release): the tool reads commit history since the last tag, infers SemVer impact from Conventional Commits, and produces release PRs or direct releases. No human intent file needed, but commit hygiene becomes load-bearing.

For a small team where the author of the change is also the releaser, a **direct CLI** (`bump-package.ps1`) is simpler than either: declare intent and cut the release in one step, locally. The Changesets-style intent file becomes an in-memory prompt; the release-please-style git history walk becomes optional via `-FromConventionalCommits`.

## Options Considered

Four shapes of workflow were considered. They are listed roughly in order of increasing ceremony and infrastructure.

### Option A — Lockstep + manual edit

Pick one repository-wide version. Every change to any package bumps every package's `version` and the matching `dependencies` entries. Tags are repository-wide (`v0.2.0`).

- Pros: trivial mental model; one tag per release; consumers can pin `#v0.2.0` once and get a coherent set.
- Cons: penalizes stable packages with churn; consumer projects re-validate everything on every bump; defeats the point of having independently consumable packages; the `Plans/ModulesAsUpmPackages` ExecPlan already flagged this trade-off.
- Tooling needed: a one-shot `bump-all.ps1` that walks every `package.json` and replaces the version. Trivial.
- Verdict: **Rejected** in **Decision Log**.

### Option B — Independent SemVer + manual edit

Each package owns its own version. Authors hand-edit `package.json` and `CHANGELOG.md`, hand-edit reverse dependencies' `dependencies` versions, hand-create the Git tag.

- Pros: no new tooling; full control; matches what the `ModulesAsUpmPackages` plan documents today.
- Cons: high toil per release; easy to forget a reverse-dependency bump or a tag; impossible to enforce; commit history fragments because each release is a hand-crafted change.
- Tooling needed: none.
- Verdict: this is the *current* state, and it is exactly what the user wants to leave behind.

### Option C — Independent SemVer + custom CLI **(Recommended)**

Each package owns its own version. A PowerShell CLI under `.agents/scripts/release/` does the bump, the changelog write, the reverse-dependency propagation, the commit, and the tag. The Cursor workflow file `/release-package` lets the agent invoke the CLI conversationally ("bump liveops as minor, summary 'add deferred event dispatch'") and the CLI does the rest. CI optionally surfaces packages whose source moved without a release.

- Pros: native to the existing PowerShell tooling baseline; no Node dependency; one command from a finished change to a tagged release; agent-friendly via the workflow file; can be invoked headlessly from CI later.
- Cons: we own and maintain the script; we have to handle the two `package.json` formatting styles already in the tree (see **Surprises & Discoveries**); the transitive bump algorithm is ours to get right.
- Tooling needed: ~300–500 lines of PowerShell, one workflow markdown file, optional GitHub Actions job.
- Verdict: **Selected.**

### Option D — Adopt Changesets

Drop a `.changeset/` folder, add a Node toolchain (`pnpm` or `npm`), install `@changesets/cli`, configure it to treat each `Assets/Packages/com.scaffold.*` directory as a workspace package, and rely on the `changeset version` and `changeset publish` commands. Skip publishing to npm by setting `"private": true` per package and using a custom `publish` step that only tags and pushes.

- Pros: battle-tested tool; rich ecosystem of GitHub Actions; release notes generation; familiar to anyone with JS background.
- Cons: introduces a Node dependency to a Unity/C# repository; Changesets' workspace model assumes `package.json` files are npm packages with `workspaces:` in a root `package.json`, which would either require a fake root `package.json` or an unsupported configuration; the `publish` step would have to be heavily customized because we are not publishing to a registry; tag format is configurable but requires plugin work to match the `com.scaffold.<id>/v<x.y.z>` shape Unity consumers want.
- Tooling needed: Node + pnpm, `@changesets/cli`, a custom publisher, GitHub Action.
- Verdict: **Rejected** for the initial rollout, but the Changesets *mental model* (declare intent → tool computes versions → tool writes changelog → tool tags) is preserved in Option C. We can revisit if the team grows.

### Option E — Adopt release-please

Google's release-please reads Conventional Commits, opens PRs that bump versions and update `CHANGELOG.md`, and creates GitHub Releases on merge. Has first-class monorepo support and per-package tagging.

- Pros: zero local tooling; pure GitHub Action; handles tags and release notes; first-class monorepo support including per-package `<package>-v<x.y.z>` tag style.
- Cons: requires strict Conventional Commits discipline (every commit must be prefixed correctly or it does not count); the release PR is opened *eventually*, so the link between "I finished the change" and "version is bumped" is async; harder to drive from a Cursor agent in the middle of a feature branch; still needs us to teach it about our `Assets/Packages/com.scaffold.*` layout via its `release-please-config.json`.
- Tooling needed: GitHub Action, `release-please-config.json`, `.release-please-manifest.json`, commit message linting.
- Verdict: **Worth revisiting later** as a CI complement to Option C, especially once commit hygiene is good. Not the starting point.

## Recommended Workflow

The agreed shape, end-to-end, looks like this. Steps in **bold** are implemented by the new tooling; steps in plain text are normal authoring.

1. Author makes code changes inside one or more `Assets/Packages/com.scaffold.<id>/` trees on a feature branch.
2. Author runs the existing milestone gate: `pwsh -NoProfile -File ".agents/scripts/validate-changes.ps1" -SkipTests`.
3. Author runs **`pwsh .agents/scripts/release/bump-package.ps1 -Package com.scaffold.liveops -Bump minor -Summary "Add deferred event dispatch"`**. The CLI:
    - reads `Assets/Packages/com.scaffold.liveops/package.json`,
    - bumps `0.1.0` → `0.2.0`,
    - prepends an entry to `Assets/Packages/com.scaffold.liveops/CHANGELOG.md` under a new `## [0.2.0] - YYYY-MM-DD` heading with the supplied summary slotted into a section the user picks (`Added` / `Changed` / `Fixed` / etc., default `Changed` for `minor`, `Fixed` for `patch`, `Changed` + a `BREAKING:` line for `major`),
    - walks the dependency graph to find every package whose `dependencies` map contains `com.scaffold.liveops`,
    - rewrites those `dependencies` entries to `0.2.0`,
    - bumps each of those reverse-dependent packages by the propagation policy (default `patch`), recursively,
    - writes one Git commit per released package with the message `release(com.scaffold.liveops): 0.2.0` (and similarly for cascaded patches),
    - creates one annotated Git tag per released package (`git tag -a com.scaffold.liveops/v0.2.0 -m "..."`),
    - prints a summary of every package that moved and its new version,
    - exits non-zero if any step failed (the user can re-run with `-DryRun` first to see what would happen).
4. Author runs **`git push --follow-tags`** (or the CLI does it when invoked with `-Push`).
5. **`/release-package`** Cursor workflow lets the agent perform steps 3 and 4 from a chat instruction such as "release a minor for liveops with summary X" — it just calls the CLI under the hood.
6. Consumer projects pin to the new tag in their own `Packages/manifest.json`:

        "com.scaffold.liveops": "https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.liveops#com.scaffold.liveops/v0.2.0"

7. CI (Milestone 6, optional) runs on every push to `main` and prints any package whose `HEAD` source has changed since its last tag without a corresponding version bump, as a soft warning at first and a hard failure once the team is comfortable.

## Plan of Work

The work is split into six milestones. Each is independently mergeable.

**Milestone 1 — Baseline cleanup.** Rewrite the two formatting styles in `Assets/Packages/com.scaffold.*/package.json` to a single canonical shape (two-space JSON, alphabetized `dependencies`). Fix the `com.scaffold.appflow` Git-URL-in-`dependencies` issue noted in **Surprises & Discoveries**. Bootstrap-tag the current `HEAD` as `com.scaffold.<id>/v0.1.0` for every first-party package so the release CLI has an "anchor" tag to diff against. No behavior change for consumers; all packages still resolve to the same code at `0.1.0`.

**Milestone 2 — `bump-package.ps1`.** Create `.agents/scripts/release/bump-package.ps1`. Required parameters: `-Package <id>`, `-Bump {patch|minor|major}`. Optional: `-Summary "..."`, `-Section {Added|Changed|Deprecated|Removed|Fixed|Security}`, `-PropagateAs {none|patch|minor|major}` (default `patch`), `-DryRun`, `-NoCommit`, `-NoTag`, `-Push`. The script must:

- Load all `Assets/Packages/com.scaffold.*/package.json` files into an in-memory model.
- Compute the reverse-dependency closure of `<id>`.
- Apply version bumps and rewrite `dependencies` entries.
- Emit canonical JSON (matching Milestone 1 formatting) so commits stay clean.
- Append/create `CHANGELOG.md` per touched package using Keep a Changelog 1.1.0.
- Create one commit per released package and one annotated tag per released package.

**Milestone 3 — `/release-package` Cursor workflow.** Add `.agents/workflows/release-package.md` modeled on `.agents/workflows/create-module.md`. It instructs the agent to: gather the package id, the bump kind, and a one-line summary from the user; run `bump-package.ps1 -DryRun` first; show the user the planned diff; then run it for real; then push.

**Milestone 4 — Changelog scaffolding.** Add a `CHANGELOG.md` template (a starter `## [Unreleased]` block plus the existing `## [0.1.0] - <bootstrap-date>` line written by Milestone 1) to every package as part of Milestone 1's bootstrap commit so Milestone 2 has somewhere to prepend.

**Milestone 5 — Validation gate.** Extend `.agents/scripts/validate-changes.ps1` (or add a sibling `validate-packages.ps1` it calls) that, for each `com.scaffold.*` package, computes whether any tracked file under that package's directory differs from the commit pointed to by its latest `com.scaffold.<id>/v*` tag. If so, the package's current `version` must not equal the version embedded in that tag. Emit a `PackageNotReleased` diagnostic per offender. Initially soft (warning), then promoted to a blocker once the workflow is established.

**Milestone 6 — CI surface.** Add a GitHub Actions workflow (`.github/workflows/package-release-status.yml`) that runs the Milestone 5 check on every push and posts a comment / status check listing packages that are "ahead of last release". This is informational; the local CLI remains the actual release path.

## Concrete Steps

These are the commands a future implementer (or the user) runs. Working directory is the repository root unless stated otherwise.

**Verify the current state (read-only):**

    git tag --list 'com.scaffold.*/*'
    # expected: empty before Milestone 1; one tag per package after.

**After Milestone 2 — release a single package:**

    pwsh -NoProfile -File ".agents/scripts/release/bump-package.ps1" `
        -Package com.scaffold.liveops `
        -Bump minor `
        -Summary "Add deferred event dispatch" `
        -DryRun

Expected output (illustrative):

    Planned releases:
      com.scaffold.liveops: 0.1.0 -> 0.2.0  (minor, Changed: Add deferred event dispatch)
      (no reverse dependents)
    Planned tags:
      com.scaffold.liveops/v0.2.0
    Planned commits:
      release(com.scaffold.liveops): 0.2.0
    DRY RUN: nothing written.

Then re-run without `-DryRun` to apply, then `git push --follow-tags`.

**After Milestone 2 — release a package with reverse dependents:**

    pwsh -NoProfile -File ".agents/scripts/release/bump-package.ps1" `
        -Package com.scaffold.types `
        -Bump patch `
        -Summary "Fix nullable annotation on TypeReference" `
        -PropagateAs patch

Expected output (illustrative):

    Planned releases:
      com.scaffold.types: 0.1.0 -> 0.1.1  (patch, Fixed: Fix nullable annotation on TypeReference)
      com.scaffold.events: 0.1.0 -> 0.1.1  (patch, Changed: Bump com.scaffold.types to 0.1.1)
      com.scaffold.addressables: 0.1.0 -> 0.1.1  (patch, Changed: Bump com.scaffold.types to 0.1.1)
    Planned tags:
      com.scaffold.types/v0.1.1
      com.scaffold.events/v0.1.1
      com.scaffold.addressables/v0.1.1
    Planned commits: 3
    DRY RUN: nothing written.

**Driving it from Cursor (after Milestone 3):** in chat, say `/release-package liveops minor "Add deferred event dispatch"` and let the agent run the CLI in dry-run, show the diff, and apply on confirmation.

## Validation and Acceptance

The workflow is considered working when, starting from a clean checkout of `main`:

1. A user changes a single line inside `Assets/Packages/com.scaffold.liveops/Runtime/...`.
2. Runs the recommended CLI for `com.scaffold.liveops -Bump minor -Summary "..."`.
3. Sees, in `git status`, **only** the expected files modified: `Assets/Packages/com.scaffold.liveops/package.json`, `Assets/Packages/com.scaffold.liveops/CHANGELOG.md`, plus the same pair for any reverse dependent that was touched. No formatting drift in untouched packages.
4. Sees `git log --oneline -5` showing one `release(com.scaffold.liveops): 0.2.0` commit per released package.
5. Sees `git tag --list 'com.scaffold.*/*'` listing the new annotated tag(s).
6. After `git push --follow-tags`, opens an external Unity consumer project, edits its `Packages/manifest.json` to pin `com.scaffold.liveops` to `#com.scaffold.liveops/v0.2.0`, opens Unity, and the Package Manager UI shows version `0.2.0` with the new changelog entry.
7. Running `pwsh -NoProfile -File ".agents/scripts/validate-changes.ps1" -SkipTests` continues to pass.

For Milestone 5's gate: deliberately edit a file inside `Assets/Packages/com.scaffold.events/Runtime/` without running the CLI, run the validation script, and observe the `PackageNotReleased: com.scaffold.events` diagnostic.

## Idempotence and Recovery

The CLI must be idempotent in the sense that re-running with the **same** arguments after a successful run is a no-op (it detects the version already matches and exits zero with a "nothing to do" message). If a run is interrupted between commit and tag, re-running detects the missing tag and creates only that. `-DryRun` is always safe.

Recovery from a bad release: `git tag -d com.scaffold.<id>/v<x.y.z>` and `git reset --hard HEAD~N` on the local branch before pushing. After pushing, a bad release is reverted by a follow-up release (do not delete remote tags consumers may have already pinned).

## Artifacts and Notes

Canonical `package.json` shape we will converge on (two-space JSON, alphabetized `dependencies`, no Git URLs in `dependencies`):

    {
      "name": "com.scaffold.liveops",
      "version": "0.2.0",
      "displayName": "Scaffold LiveOps",
      "description": "LiveOps services and DTOs.",
      "unity": "6000.0",
      "author": {
        "name": "Matheus Gandolf Cohen",
        "email": "matheus@duckduckweasel.com",
        "url": "https://github.com/MgCohen/Scaffold"
      },
      "dependencies": {
        "com.scaffold.appflow": "0.1.0",
        "com.scaffold.cloudcode": "0.1.0"
      }
    }

Canonical `CHANGELOG.md` shape:

    # Changelog

    All notable changes to `com.scaffold.liveops` will be documented in this file.

    The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
    and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

    ## [Unreleased]

    ## [0.2.0] - 2026-04-25

    ### Changed
    - Add deferred event dispatch.

    ## [0.1.0] - 2026-03-30

    ### Added
    - Initial UPM package extracted from Scaffold monorepo.

## Interfaces and Dependencies

`bump-package.ps1` public surface (PowerShell-style parameter contract):

    param(
        [Parameter(Mandatory=$true)][string]$Package,
        [Parameter(Mandatory=$true)][ValidateSet('patch','minor','major')][string]$Bump,
        [string]$Summary = '',
        [ValidateSet('Added','Changed','Deprecated','Removed','Fixed','Security')]
        [string]$Section = '',
        [ValidateSet('none','patch','minor','major')][string]$PropagateAs = 'patch',
        [switch]$DryRun,
        [switch]$NoCommit,
        [switch]$NoTag,
        [switch]$Push
    )

The script depends only on:

- PowerShell 5.1+ (already required by the rest of `.agents/scripts/`).
- `git` on `PATH`.
- A canonical JSON read/write helper that preserves key order; this can be a small inline implementation since the schema is fixed.

It does **not** depend on Node, npm, pnpm, Changesets, release-please, or semantic-release.

`/release-package` workflow file lives at `.agents/workflows/release-package.md` and is referenced from `AGENTS.MD` under the **Workflows** section once Milestone 3 lands.

---

Revision history:

- 2026-04-25: Initial ExecPlan authored. Captures the four real options (lockstep, manual independent, custom CLI, Changesets, release-please), recommends Option C (custom CLI + Cursor workflow), enumerates the six implementation milestones, and pins decisions on independent SemVer, per-package Git tags, Keep a Changelog 1.1.0, and patch-by-default propagation.
