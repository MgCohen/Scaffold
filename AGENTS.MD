# Scaffold Unity project — AI agent instructions

This is the **primary context file** for all AI coding agents in this repository. Read it fully before making any changes. It takes precedence over any other instructions.

## Project Summary

This repository is a modular Unity project (C#) with architecture boundaries enforced through documentation, explicit assembly/project dependencies, and custom Roslyn analyzers. First-party assemblies and namespaces use the `Scaffold.*` prefix (see `Plans/PackageableTestingSuite/PackageableTestingSuite-ExecPlan.md`).

## Tech Stack

- **Engine**: Unity
- **Language**: C#
- **Architecture**: MVVM (Model-View-ViewModel)
- **Code Enforcement**: Custom Roslyn Analyzers (`Analyzers/`)
- **Code Generation**: Roslyn Source Generators (if introduced, place them under `Generators/`)
- **Dependency Injection**: VContainer

## Directory and Module Reference

Directory layout, module relationships, and architectural structure are documented in [Architecture.md](Architecture.md). Use that file as the single source of truth for repository structure details.

## Code Rules (Non-Negotiable)

1. **English-Only Outputs**: All generated content (code comments, docs, plans, commit messages, test names, UI placeholder text, and agent replies) must be in English, regardless of the input language (typed or voice).
2. **Separation of Concerns**: Keep App/UI and scene-specific presentation separate from unrelated modules. The `Core/` folder is not “Unity-free by definition”—assemblies such as **`Scaffold.Entities`** intentionally use `UnityEngine` (for example `MonoBehaviour` components for health and lifecycle). Do not treat “under Core” as a ban on engine APIs.
3. **Modular Integrity**: Declare all dependencies explicitly via `.asmdef`/`.csproj`. Never bypass architectural boundaries.
4. **MonoBehaviours**: Use them in composition roots, App/Infra presentation, and in Core modules that own engine-facing gameplay building blocks (for example **`Scaffold.Entities`**). Do not add MonoBehaviours to assemblies that are intentionally Unity-free (`noEngineReferences: true` in their `.asmdef`).
5. **Automated tests**: Add `Tests/` and `*.Tests.asmdef` when it fits the module; add `.cs` tests when you are ready. (**SCA4002** / **SCA4003** folder and asmdef analyzers are archived — see `Plans/SCA-Analyzer-Revamp/SCA4002.md`, `SCA4003.md`.)
6. **Documentation**: Every module needs a `.md` file or folder under `Docs/`.
7. **Plans go in `Plans/`**: All planning documents belong there.
8. **New source generators go in `Generators/`**.
9. **Analyzer compliance**: Fix all Roslyn analyzer warnings/errors before committing.
10. **Bugfixes and tests**: Prefer a regression test when you maintain automated tests; until then, document the fix and verify manually or via analyzer/compile gates.
11. **Pragma suppression approval**: Never introduce a new `#pragma warning disable` unless the user explicitly approves it in the same thread. Approved suppressions must include a one-line reason comment and an immediate matching `#pragma warning restore <ID>`.

## Unity assets and Unity MCP (preferred)

Operations that must match Unity’s asset database format — **especially new folders and their `.meta` files**, GUIDs, and other Unity-managed assets under `Assets/` — **should use Unity MCP** when it is available and the editor bridge is connected. That path produces valid metadata and avoids mistakes (for example GUIDs that are not exactly **32 hexadecimal characters**). Do not default to hand-authoring `.meta` files for new folders or scripts when MCP can create or manage them.

If Unity MCP is unavailable or returns an error (for example Unity not detected / no discovery files), fall back to normal file edits only then, and **validate** folder `.meta` GUIDs are 32 hex chars and match the YAML shape of existing sibling `.meta` files in the same tree.

## How to Build

- **Application**: Standard Unity Editor pipeline (`File > Build Settings`) or Unity CLI batch mode.
- **Analyzers & Generators**: These are standard .NET projects. After modifying them, run `dotnet build -c Release` in their directory. The compiled DLL lands in `Analyzers/Output/` and is picked up by the IDE/language server via `Directory.Build.props` at the repo root. Unity does **not** run the analyzer - diagnostics surface directly in the IDE and to AI tooling.

## How to Test

- **Milestone quality gate (default while Unity tests are not maintained)**: Run `pwsh -NoProfile -File ".agents/scripts/validate-changes.ps1" -SkipTests` from the repository root (or `powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\validate-changes.ps1" -SkipTests` on Windows). **macOS/Linux:** `./.agents/scripts/validate-changes.sh -SkipTests` if `pwsh` is on `PATH`. This runs compilation, asmdef audit, pragma gate, and analyzer checks **without** EditMode/PlayMode. Omit `-SkipTests` when you have restored automated tests. If Windows Application Control blocks `Scaffold.Mvvm.Analyzers.dll` during analyzer tests (0x800711C7), add `-SkipMvvmAnalyzerTests` and run `Scaffold.Mvvm.Analyzers.Tests` on an unblocked machine. On Windows PowerShell 5.x, `&&` is not valid for chaining—use `;`, wrap with `cmd /c "…"`, or run from repo root; see `Docs/Testing.md`.
- **Headless EditMode / PlayMode**: Prefer `.agents/scripts/run-unity-tests.ps1` with `-TestPlatform EditMode` or `-TestPlatform PlayMode`. By default, Unity receives **`-testCategory "!PerformanceBenchmark"`** so heavy `[Performance]` microbenchmarks are skipped; pass **`-IncludePerformanceBenchmarks`** to run them, **`-PerformanceBenchmarksOnly`** to run only that category, or **`-PerformanceTestResultsPath`** (implies benchmarks run so JSON is populated). Pass **`-PerformanceTestResultsPath`** to emit Unity Performance Testing JSON (**`-perfTestResults`**). **Shims** `.agents/scripts/run-editmode-tests.ps1` and `run-playmode-tests.ps1` still forward to the same implementation. The script prints a pass/fail report and deletes its temporary XML/log files before exiting.
- **Unity path resolution**: Test scripts resolve Unity via `-UnityPath`, `UNITY_PATH`, then Unity Hub (Windows / macOS / Linux). See `.agents/scripts/README.md` for `UNITY_PATH` examples and optional fallback behavior.
- **Analyzer diagnostics**: Run `.agents/scripts/check-analyzers.ps1` from the repository root to get deduplicated SCA diagnostics and blockers. If your execution policy blocks scripts, invoke via `powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\check-analyzers.ps1"`. If no `.sln` exists yet at repo root, the script exits with `TOTAL:0` and a skip note.
- **Script documentation**: See `Docs/Testing.md` and `.agents/scripts/README.md` for usage, behavior, parameters, **implementation notes** (paths with spaces on Windows PowerShell 5.x, `lib/UnityProcess.ps1`, `dotnet` quoting), and editing guidance.
- **Test authoring guidance**: When creating or updating automated tests, read `Docs/AutomatedTesting.md` and follow it as the test design/playbook source of truth.

## Workflows

### Creating a New Module
Use the `/create-module` workflow (`.agents/workflows/create-module.md`). Do **not** manually copy-paste folder structures.

### Creating a Custom Analyzer Rule
Use the `/create-custom-analyzer` workflow (`.agents/workflows/create-custom-analyzer.md`).

### Checking Analyzer Diagnostics
Use the `/check-analyzers` workflow (`.agents/workflows/check-analyzers.md`) to report diagnostics without making fixes.

## Where to Put Plans

### ExecPlans

When writing complex features or significant refactors, use an ExecPlan (as described in `PLANS.md`) from design to implementation. Use `MILESTONE.md` for milestone plan structure when a milestone needs its own file.

## Milestone Quality Loop

For every milestone, follow this sequence before marking it complete:

1. Check complexity first. If complex, write a mini milestone plan with concrete steps and sample inputs/outputs.
2. If a bug is discovered or reported, add/update a regression test that reproduces it and verify the test fails before the fix.
3. Implement the milestone or bug fix.
4. Re-run the same regression test and verify it now passes.
5. Run `validate-changes.ps1` (with `-SkipTests` until automated tests are restored) as the milestone quality gate.
6. If the gate fails, fix all reported failures and diagnostics.
7. Re-run the same command until the gate is clean.
8. Commit milestone changes.

## Where to Put Documentation

Every first-party module **must** have a documentation pointer under `Docs/` (path derived from `Architecture.md`) **and** authoritative long-form documentation in `Assets/Packages/<packageId>/README.md` at the package root. The `Docs/` entry links to that README; update the README when you change module documentation.

Documentation standards:

- Module docs standard: `Docs/Standards/Module-Documentation-Standard.md`
- Architecture docs standard: `Docs/Standards/Architecture-Documentation-Standard.md`

Documentation must cover: purpose, public API/interface, usage examples, and any non-obvious design decisions. Keep it up to date as the module evolves.

## Agent Behavior Guidelines

- Read `Architecture.md` for module details before touching any code.
- Read `PLANS.md` before authoring or executing any ExecPlan.
- Read `MILESTONE.md` when writing milestone plan files referenced by an ExecPlan.
- When creating or changing tests, read `Docs/AutomatedTesting.md` before implementation.
- Always fix analyzer warnings before committing.
- Prefer editing existing files over creating new ones.
- For **Unity assets** (folders, `.meta`, Unity-specific create/fix flows under `Assets/`), **prefer Unity MCP** when connected; see **Unity assets and Unity MCP** above.
- Keep changes minimal and focused - no speculative abstractions or premature generalization.
- Use existing modules as reference when unsure about patterns.

