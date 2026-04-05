# Starting a new project from Scaffold

Scaffold serves two roles: **runtime libraries** (Unity packages under `Assets/Packages/com.scaffold.*`) and **holder tooling** (agents, plans, validation scripts, Roslyn analyzers). A new Unity repo can take any combination; this page lists what to import and how.

## 1. Runtime: Unity packages (most new games start here)

**What:** Only the `com.scaffold.*` packages you need, plus their **transitive UPM dependencies** (VContainer, Addressables, UGS packages, etc.).

**How:** Add Git **subpath** entries to the consumer `Packages/manifest.json` (or `file:` for local clones). See [ConsumingScaffoldPackages.md](ConsumingScaffoldPackages.md) and the package table in [README.md](../README.md).

**You do not copy** the whole `Assets/Packages/` tree unless you are forking the monorepo; UPM pulls only the packages you list.

## 2. AI agent harness (optional)

**What:** Files agents read for rules, workflows, and quality gates.

| Item | Purpose |
|------|--------|
| [AGENTS.MD](../AGENTS.MD) | Primary agent instructions; **edit** for your project name, paths, and stack. |
| [PLANS.md](../PLANS.md) | ExecPlan format for large features. |
| [MILESTONE.md](../MILESTONE.md) | Milestone plan structure when linked from ExecPlans. |
| [.agents/workflows/](../.agents/workflows/) | Reusable workflows (`create-module`, `check-analyzers`, etc.). |
| [.agents/scripts/](../.agents/scripts/) | `validate-changes.ps1`, Unity test runners, analyzer checks, pragma gate, asmdef audit; keep `lib/` with the scripts. |
| [.agents/local.env.example](../.agents/local.env.example) | Optional `UNITY_PATH`; copy to `.agents/local.env` (gitignored). |

**How:** Copy these paths into the new repo root (preserve `.agents/` layout). Update **every reference** inside `AGENTS.MD` to your repo’s doc paths (`Architecture.md`, `Docs/Testing.md`, etc.). If you omit `Docs/Testing.md` or analyzer wiring, trim the “How to Test” / analyzer bullets so agents are not pointed at missing files.

**Relationship:** The harness assumes a Unity project with `ProjectSettings/ProjectVersion.txt` for editor discovery; scripts accept `-ProjectPath` and optional `-UnityPath`.

## 3. Roslyn analyzers (optional, for the same conventions)

**What:**

- `Analyzers/` — analyzer and test projects, especially `Analyzers/Scaffold/Scaffold.Analyzers` and `Generators/Scaffold.Mvvm.Analyzers` if you use MVVM rules.
- [Directory.Build.props](../Directory.Build.props) at the **repository root** — wires `Analyzers/Output/*.dll` into MSBuild as analyzers for generated Unity `.csproj` files.

**How:**

1. Copy the analyzer tree and `Directory.Build.props`.
2. From the analyzer project folder, run `dotnet build -c Release` so DLLs land in `Analyzers/Output/` (see [AGENTS.MD](../AGENTS.MD) “How to Build”).
3. Unity does **not** execute these analyzers; diagnostics appear in the IDE / language server for C# projects that pick up the repo root props file.

If the new repo has different folder or naming conventions, you may need to adjust shared support code (for example path filters) inside the analyzer projects—treat that as a small fork.

## 4. Documentation and architecture (optional)

**What:** [Architecture.md](../Architecture.md), [Docs/Standards/](Standards/), and module doc patterns.

**How:** Copy only if you want the same structural rules. Otherwise replace with your own architecture doc and shorten `AGENTS.MD` so it points only to what exists.

## 5. What usually stays in the Scaffold holder

- **Plans/** — ExecPlans are repository-specific; do not bulk-copy unless you are continuing the same initiative.
- **Entire** `Assets/` **except** consumed UPM packages — game content and app-specific code belong in the new project, not as a clone of the holder.

## 6. Minimal vs full “tooling parity”

| Goal | Import |
|------|--------|
| Use Scaffold libraries in a game | §1 only ([ConsumingScaffoldPackages.md](ConsumingScaffoldPackages.md)). |
| Same AI workflows + validate gate + tests from CLI | §1 + §2; add or mirror `Docs/Testing.md` as needed. |
| Same Roslyn rules as this repo | §1 + §3 (+ §2 if agents should mention the gate). |

## 7. Keeping in sync

Point Git dependencies at a **branch, tag, or commit** you control. When you bump Scaffold packages, re-run your quality gate and fix new analyzer diagnostics if you use §3.
