# Scaffold Analyzers

Rule examples refer to `Scaffold.*` assembly names where they match modules in this tree. Clone path and Unity install location are host-specific.

**This file is the canonical hub** for Scaffold analyzer behavior, layout, testing, authoring, and archives. Deeper audits and StyleCop notes remain in linked docs but are optional reading once you have read the sections below.

## Contents

1. [TL;DR](#tldr)
2. [Rule IDs and categories](#rule-ids-and-categories)
3. [Where everything lives (paths)](#where-everything-lives-paths)
4. [Architecture](#architecture)
5. [Configuration](#configuration)
6. [Testing rules](#testing-rules)
7. [Creating a new rule](#creating-a-new-rule)
8. [Archived / disabled rules](#archived--disabled-rules)
9. [Rules reference](#rules-reference) — per-rule behavior and good/bad snippets (this section)

**Also useful:** [Audit-2026-03.md](Audit-2026-03.md) (overlap matrix), [StyleCop-Audit.md](StyleCop-Audit.md) (third-party StyleCop vs SCA), [SCA-Rule-Disposition.md](SCA-Rule-Disposition.md) (policy table). **Namespace layout (SCA3005 + SCA3006 + SCA3004):** design notes in [SCA3005-SCA3006-Namespace-Split-Spec.md](SCA3005-SCA3006-Namespace-Split-Spec.md).

## TL;DR

- Purpose: custom Roslyn analyzer package that enforces style, architecture, and MVVM/runtime-boundary rules.
- Location: source under `Analyzers/Scaffold/Scaffold.Analyzers/`; compiled DLL under `Analyzers/Output/`.
- Depends on: Roslyn APIs + repository `Directory.Build.props` analyzer wiring.
- Used by: all repository `.csproj` projects through analyzer injection.
- Runtime/Editor: IDE/build-time diagnostics only (Unity runtime does not execute analyzers).

**Milestone D1 audit (rules, overlap, config knobs):** [Audit-2026-03.md](Audit-2026-03.md).

**StyleCop.Analyzers (optional third-party) vs SCA — conflicts and install steps:** [StyleCop-Audit.md](StyleCop-Audit.md).

## Rule IDs and categories

- **SCA rules:** IDs are **`SCA` + one category digit (1–8) + three-digit index** within that category (e.g. **SCA1001** = category 1, first rule; **SCA3005** / **SCA3006** = category 3, namespace layout). See [SCA-Rule-Disposition.md](SCA-Rule-Disposition.md) for the eight buckets and per-rule disposition.
- **SCM rules (MVVM):** **SCM001**–**SCM003** ship from **`Generators/Scaffold.Mvvm.Analyzers`** (separate assembly), not from `Scaffold.Analyzers`.
- **Retired / special:** **SCA0009** (merged into **SCA1004**), **SCA0011** (removed), **SCA0025** (unused ID — no descriptor). Legacy `SCA0001`-style numeric-only IDs are fully superseded.

When adding a **new SCA** rule, pick the next free index **in the correct category** (e.g. new surface rule → **SCA1008** if **SCA1007** is the last in category 1), update `AnalyzerReleases.Unshipped.md`, and add an entry to [SCA-Rule-Disposition.md](SCA-Rule-Disposition.md).

## Where everything lives (paths)

| What | Path |
|------|------|
| SCA analyzer source | `Analyzers/Scaffold/Scaffold.Analyzers/` |
| Rule implementations | `Analyzers/Scaffold/Scaffold.Analyzers/Rules/CategoryNN-*/` (see category list under [Architecture](#architecture)) |
| Shared helpers | `Analyzers/Scaffold/Scaffold.Analyzers/Support/` — `AnalyzerConfig.cs`, `ModuleConventions.cs`, `ScriptPathFilters.cs`, `NamespacePathResolution.cs`, `NamespaceLayoutAnalysis.cs`, `NamespaceLayoutDescriptors.cs` |
| Built analyzer DLL (committed) | `Analyzers/Output/Scaffold.Analyzers.dll` |
| MVVM analyzer source | `Generators/Scaffold.Mvvm.Analyzers/` |
| MVVM analyzer DLL (committed) | `Analyzers/Output/Scaffold.Mvvm.Analyzers.dll` |
| SCA unit tests | `Analyzers/Scaffold/Scaffold.Analyzers.Tests/` |
| Test fixtures (snippets, golden files) | `Analyzers/Scaffold/Scaffold.Analyzers.Tests/TestData/` |
| Repo-wide analyzer injection | `Directory.Build.props` (repo root) |
| Per-rule deep dives (plans) | `Plans/SCA-Analyzer-Revamp/SCA*.md` |
| Disabled/removed rule **archives** (code + plan dumps) | `Analyzers/Scaffold/_Archive/` |
| Analyzer gate script | `.agents/scripts/check-analyzers.ps1` |
| Agent workflow (optional) | `.agents/workflows/create-custom-analyzer.md`, `.agents/workflows/check-analyzers.md` |

**Test folder mirror:** `Scaffold.Analyzers.Tests/Tests/CategoryNN-*/` mirrors rule categories; `Tests/Golden/` holds `ScaRuleGoldenTests` (one baseline per rule).

## Responsibilities

- Owns SCA diagnostic rules and rule metadata.
- Owns analyzer config parsing (`AnalyzerConfig.cs`) and severity override behavior.
- Owns architecture enforcement diagnostics (for example namespace alignment and runtime assembly boundaries).
- Does not own Unity runtime behavior or gameplay logic.

## Public API

| Symbol | Purpose | Inputs | Outputs | Failure behavior |
|---|---|---|---|---|
| `Scaffold.Analyzers.dll` | Analyzer distribution artifact | analyzer source + build pipeline | Roslyn diagnostics surfaced in IDE/build | missing artifact means no diagnostics in consumers |
| `AnalyzerConfig` | Central config reader and descriptor override helper | `.editorconfig` values + analyzer options | effective rule descriptors/config values | invalid values fall back to defaults |
| `SCA1001`-`SCA2005` descriptors | Rule contracts consumed by IDE/build tooling | C# syntax/semantic model | diagnostics with code fixes/remediation guidance | suppressed/disabled severities skip reporting |

## Setup / Integration

1. Build analyzer project:

```bash
cd Analyzers/Scaffold/Scaffold.Analyzers
dotnet build -c Release
```

2. Ensure compiled artifact exists at `Analyzers/Output/Scaffold.Analyzers.dll`.
3. Keep `Directory.Build.props` analyzer include active so all projects receive diagnostics.
4. Run analyzer diagnostics from repo root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\.agents\scripts\check-analyzers.ps1"
```

The script runs `dotnet test` on the analyzer unit test project and `dotnet build` on the solution at the repo root when a `.sln` is present. On **Windows PowerShell 5.x**, solution and project paths are **quoted** when passed to `dotnet` so paths with spaces are not split. See [Testing.md](../Testing.md) → "Implementation notes".

## How to Use

1. Run `check-analyzers.ps1`.
2. Fix reported diagnostics in top-down file order.
3. Re-run analyzer checks.
4. Run full gate:

```powershell
& ".\.agents\scripts\validate-changes.cmd"
```

## Examples

### Minimal

```ini
[*.cs]
dotnet_diagnostic.SCA2003.severity = error
```

### Realistic

1. Hit an active guard or correctness rule (for example **SCA2004**).
2. Apply the fix suggested by the diagnostic message.
3. Re-run analyzer checks to confirm the diagnostic is cleared.

### Guard / Error path

```text
If Analyzers/Output/Scaffold.Analyzers.dll is missing, analyzer diagnostics will not load in consumer projects.
```

## Best Practices

- Keep rule IDs stable once published.
- Keep rule docs synchronized with current analyzer IDs and behavior.
- Prefer conservative diagnostics with precise messages and examples.
- Respect `.editorconfig` severity overrides via `AnalyzerConfig`.
- Add/update analyzer tests for every rule behavior change.
- For analyzers that need multi-file or cross-assembly fixtures, prefer `StructuralTestGraph` (`Scaffold.Analyzers.Tests/Support/StructuralTestGraph.cs`) over ad-hoc per-test temp workspaces.

## Anti-Patterns

- Updating analyzer behavior without updating docs/tests.
- Introducing rule ID gaps or undocumented renumbering.
- Depending on analyzer diagnostics as runtime safety checks.

## Architecture

### Rule layout (categories)

Rules live under `Scaffold.Analyzers/Rules/`:

| Folder | Scope |
|--------|--------|
| `Category01-SurfaceAndNaming` | Comments, braces, expression bodies, line breaks, naming, loop/conditional braces |
| `Category02-StructureAndDecomposition` | Method order, nesting depth, method length, static scope in instance types, nested static types |
| `Category03-OrganizationAndPlacement` | Namespace vs folder, type-per-file, member order, one namespace per file |
| `Category04-ModuleLayoutAndBoundaries` | *(reserved — archived analyzers used this bucket)* |
| `Category05-GuardsAndValidation` | *(reserved — e.g. disabled invariant rules)* |
| `Category06-UnityAndSerialization` | SerializeField patterns, TMPro vs legacy UI |
| `Category07-Hygiene` | Dead code in runtime, pragma disables |

All analyzer types use namespace **`Scaffold.Analyzers`** (folder name does not change namespace).

### `AnalyzerConfig` and shared helpers

- **`AnalyzerConfig`** (`Support/AnalyzerConfig.cs`): reads `dotnet_diagnostic.{ID}.severity` and custom `scaffold.{ID}.*` keys; **`TryGetEffectiveDescriptor`** / **`ShouldSuppress`** / **`GetInt`** — use these at report sites so `.editorconfig` overrides apply.
- **`ScriptPathFilters`**: single place for `Assets/Scripts` normalization, Tests/Samples skips, generated paths — **do not** copy-paste path checks in new rules.
- **`ModuleConventions`**: module roots, infrastructure assembly detection, related path helpers.

### Extended analyzer rules and release tracking

- **`Scaffold.Analyzers.csproj`** sets `<EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>` (stricter Roslyn analyzer authoring checks).
- **`AnalyzerReleases.Shipped.md`** and **`AnalyzerReleases.Unshipped.md`** are additional files for [release tracking](https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md). Add new or changed IDs under **Unshipped**; move to **Shipped** when you cut a release.
- **RS1032** (message format) is suppressed project-wide until a message-format pass; **RS2007** may be suppressed when Unshipped mixes **Changed Rules** and **New Rules** (tool quirk).

### MVVM analyzer (`Scaffold.Mvvm.Analyzers`)

The MVVM project **`Generators/Scaffold.Mvvm.Analyzers`** **links** shared sources from `Scaffold.Analyzers/Support/`: `AnalyzerConfig.cs`, `ModuleConventions.cs`, `ScriptPathFilters.cs`. If you add a new shared file under `Support/` and use it from linked MVVM code, **add the same `<Compile Include=... Link=...>`** entry to `Scaffold.Mvvm.Analyzers.csproj`.

### Build output and integration

Built DLLs are committed under **`Analyzers/Output/`** (`Scaffold.Analyzers.dll`, `Scaffold.Mvvm.Analyzers.dll`). They are **not** under Unity `Assets/`.

Repo root **`Directory.Build.props`** injects both analyzers into consuming `.csproj` files (excludes the four analyzer test projects). Diagnostics appear in IDE and `dotnet build`; Unity Player does not run analyzers.

```xml
<Project>
  <ItemGroup Condition="'$(MSBuildProjectName)' != 'Scaffold.Analyzers' and '$(MSBuildProjectName)' != 'Scaffold.Analyzers.Tests' and '$(MSBuildProjectName)' != 'Scaffold.Mvvm.Analyzers' and '$(MSBuildProjectName)' != 'Scaffold.Mvvm.Analyzers.Tests'">
    <Analyzer Include="$(MSBuildThisFileDirectory)Analyzers/Output/Scaffold.Analyzers.dll"
              Condition="Exists('$(MSBuildThisFileDirectory)Analyzers/Output/Scaffold.Analyzers.dll')" />
    <Analyzer Include="$(MSBuildThisFileDirectory)Analyzers/Output/Scaffold.Mvvm.Analyzers.dll"
              Condition="Exists('$(MSBuildThisFileDirectory)Analyzers/Output/Scaffold.Mvvm.Analyzers.dll')" />
  </ItemGroup>
</Project>
```

### Diagnostic messages (authoring)

- Start static text with **`Error {ID}:`** so fixes are grep-friendly.
- Do **not** embed file paths or line numbers in the message — Roslyn supplies location.
- Prefer actionable text: what to change and how.

## Testing rules

### Projects

| Project | Role |
|---------|------|
| `Scaffold.Analyzers.Tests` | All **SCA** rules |
| `Scaffold.Mvvm.Analyzers.Tests` | **SCM001–SCM003** only |

### Running tests and repo gate

```bash
cd Analyzers/Scaffold/Scaffold.Analyzers.Tests
dotnet test
```

From repo root, **`check-analyzers.ps1`** runs **`dotnet test`** on the SCA test project and **`dotnet build`** on the solution (`--no-incremental`), deduplicates diagnostics, and prints `RULE:<code>:<count>` summaries. See [.agents/workflows/check-analyzers.md](../../.agents/workflows/check-analyzers.md) for flags (e.g. including test assemblies).

### Fixtures and harness

- **`TestData/`** — copied next to the test assembly. Load text with **`AnalyzerTestHarness.LoadTestDataText`** (paths like `Category07/SCA8002-PragmaRuntime.cs` or `Golden/SCA1001-MethodComment.cs`).
- **`AnalyzerTestHarness.GetDiagnosticsAsync`** — compile a single synthetic tree with a fake path (e.g. `C:\Repo\Assets\Scripts\...`) and optional **UnityEngine** reference; pass **`IDictionary<string,string>`** analyzer options to mimic `.editorconfig`.
- **`StructuralTestGraph` + `AnalyzerTestHarness.GetDiagnosticsAsync`/`GetDiagnosticsByIdAsync`** — multi-file or multi-assembly scenarios; prefer this over ad-hoc temp workspaces.
- **Golden baselines** — `TestData/Golden/{Name}.cs` + `{Name}.golden.txt`; optional `{Name}.options.txt` (`key=value`, including `__include_unity_engine_reference__=true`). **`DiagnosticGoldenFixture`** formats diagnostics in a stable sort order; **`ScaRuleGoldenTests`** asserts one golden per shipped SCA rule.

### Regression workflow

1. Add or adjust a test (or golden) that **fails** on the old behavior.
2. Fix the analyzer.
3. Confirm tests pass; update **`Docs/Analyzers/Analyzers.md`** rule section if behavior or messaging changed.

## Configuration

Per-rule severity uses **`.editorconfig`**:

```ini
dotnet_diagnostic.SCA1001.severity = warning   # or: error | suggestion | none
```

Custom integer/string keys use the **`scaffold.{DiagnosticId}.{key}`** pattern (examples: `scaffold.SCA2002.max_nesting_depth`, `scaffold.SCA2003.max_lines`, `scaffold.SCA3005.allowed_roots`, `scaffold.SCA3006.content_roots`, `scaffold.SCA6002.forbidden_types`, `scaffold.SCA8001.exempt_method_names`). **`none`** suppresses the rule.

**`AnalyzerConfig`** caches overridden descriptors and exposes **`GetEffectiveDescriptor`**, **`ShouldSuppress`**, **`GetInt`**, and list parsing helpers.

Example **`[*.cs]`** block showing several overrides (trim to what you need):

```ini
[*.cs]
dotnet_diagnostic.SCA1001.severity = none
dotnet_diagnostic.SCA2003.severity = error
dotnet_diagnostic.SCA5001.severity = none
dotnet_diagnostic.SCA6001.severity = warning
dotnet_diagnostic.SCA2004.severity = warning
dotnet_diagnostic.SCA3002.severity = warning
dotnet_diagnostic.SCA6002.severity = warning
dotnet_diagnostic.SCA5002.severity = none
dotnet_diagnostic.SCM001.severity = warning
dotnet_diagnostic.SCM002.severity = warning
dotnet_diagnostic.SCA3003.severity = warning
dotnet_diagnostic.SCM003.severity = warning
dotnet_diagnostic.SCA4001.severity = none
dotnet_diagnostic.SCA4002.severity = none
dotnet_diagnostic.SCA4003.severity = none
dotnet_diagnostic.SCA2006.severity = none
dotnet_diagnostic.SCA1006.severity = warning
dotnet_diagnostic.SCA1007.severity = warning
dotnet_diagnostic.SCA8001.severity = suggestion
dotnet_diagnostic.SCA8002.severity = suggestion
```

Valid severities include `error`, `warning`, `suggestion`, `info`, `hidden`, `silent`, `none`.

## Creating a new rule

1. **Choose ID** — Next free index in the correct **category** (see [Rule IDs and categories](#rule-ids-and-categories)). Implement under the matching **`Rules/CategoryNN-*/`** folder.
2. **Implement** — `[DiagnosticAnalyzer(LanguageNames.CSharp)]`, `DiagnosticDescriptor`, register actions in **`Initialize`**. At each report site, use **`AnalyzerConfig.TryGetEffectiveDescriptor`** (or **`ShouldSuppress`** + **`GetEffectiveDescriptor`**) so **`dotnet_diagnostic.{ID}.severity`** and **`none`** work.
3. **Messages** — Prefix with **`Error {ID}:`**; describe the fix (see [Diagnostic messages](#diagnostic-messages-authoring)).
4. **Path filtering** — Use **`ScriptPathFilters`** / **`ModuleConventions`** where appropriate; do not duplicate `Assets/Scripts` checks.
5. **Tests** — Add tests under **`Scaffold.Analyzers.Tests/Tests/{same CategoryNN}/`**; add **`TestData`** snippets and/or **`Golden`** pair; register in **`ScaRuleGoldenTests`** if the rule is a stable one-diagnostic baseline.
6. **Release tracking** — Add the ID to **`AnalyzerReleases.Unshipped.md`** (and **Shipped** when releasing).
7. **Docs** — Add a **### SCAxxxx** section under [Rules reference](#rules-reference) in **this file**; add or update **`Plans/SCA-Analyzer-Revamp/SCAxxxx.md`** and **[SCA-Rule-Disposition.md](SCA-Rule-Disposition.md)**.
8. **Build** — `dotnet build -c Release` in **`Scaffold.Analyzers`**; commit updated **`Analyzers/Output/Scaffold.Analyzers.dll`**.

**SCM (MVVM) rules** — implement in **`Generators/Scaffold.Mvvm.Analyzers`**, tests in **`Generators/Scaffold.Mvvm.Analyzers.Tests`**, release tracking in that project’s shipped/unshipped files, and commit **`Analyzers/Output/Scaffold.Mvvm.Analyzers.dll`** when the MVVM analyzer changes.

Optional scaffolding steps: [.agents/workflows/create-custom-analyzer.md](../../.agents/workflows/create-custom-analyzer.md) (same rules; use **SCA** + category digit + three digits for new IDs, not `SCA00XX`).

## Archived / disabled rules

These IDs are **not emitted** by the shipping analyzer; sources and plan snapshots live under **`Analyzers/Scaffold/_Archive/`**. Canonical stubs live in **`Plans/SCA-Analyzer-Revamp/`**.

| ID | Status | Archive folder | Plan stub |
|----|--------|----------------|-----------|
| **SCA5001** | Disabled — invariant entry points | [`_Archive/SCA5001-Disabled/`](../../Analyzers/Scaffold/_Archive/SCA5001-Disabled/README.md) | [`SCA5001.md`](../../Plans/SCA-Analyzer-Revamp/SCA5001.md) |
| **SCA5002** | Disabled — constructor invariants | [`_Archive/SCA5002-Disabled/`](../../Analyzers/Scaffold/_Archive/SCA5002-Disabled/README.md) | [`SCA5002.md`](../../Plans/SCA-Analyzer-Revamp/SCA5002.md) |
| **SCA4001** | Disabled — runtime boundary | [`_Archive/SCA4001-Disabled/`](../../Analyzers/Scaffold/_Archive/SCA4001-Disabled/README.md) | [`SCA4001.md`](../../Plans/SCA-Analyzer-Revamp/SCA4001.md) |
| **SCA4002** | Disabled — required folders | [`_Archive/SCA4002-Disabled/`](../../Analyzers/Scaffold/_Archive/SCA4002-Disabled/README.md) | [`SCA4002.md`](../../Plans/SCA-Analyzer-Revamp/SCA4002.md) |
| **SCA4003** | Disabled — asmdef layout | [`_Archive/SCA4003-Disabled/`](../../Analyzers/Scaffold/_Archive/SCA4003-Disabled/README.md) | [`SCA4003.md`](../../Plans/SCA-Analyzer-Revamp/SCA4003.md) |
| **SCA2006** | Removed — same-layer init | [`_Archive/SCA2006-Removed/`](../../Analyzers/Scaffold/_Archive/SCA2006-Removed/README.md) | [`SCA2006.md`](../../Plans/SCA-Analyzer-Revamp/SCA2006.md) |

Each archive **`README.md`** lists files inside (e.g. former analyzer `.cs`, tests, **`*-plan-archive.md`** full plan text). **`SCA0025`** has no implementation — see [`SCA0025.md`](../../Plans/SCA-Analyzer-Revamp/SCA0025.md).

## AI Agent Context

- Invariants:
  - analyzer output DLL path and `Directory.Build.props` wiring remain valid.
  - rule IDs remain unique and documented.
  - analyzer changes are covered by analyzer tests.
- Allowed Dependencies:
  - Roslyn analyzer APIs and repo-shared analyzer infrastructure.
- Forbidden Dependencies:
  - Unity runtime APIs in analyzer execution logic.
- Change Checklist:
  - update rule docs and examples for changed diagnostics.
  - run analyzer project build.
  - run `.agents/scripts/check-analyzers.ps1`.
- Known Tricky Areas:
  - rule ID renumbering drift between docs and code.
  - stale analyzer DLL not rebuilt after source updates.

## Related

- **`Docs/Analyzers/Analyzers.md` (this file)** — primary hub; keep in sync when rules change.
- [SCA-Rule-Disposition.md](SCA-Rule-Disposition.md) — policy table per ID.
- [Audit-2026-03.md](Audit-2026-03.md) — overlap and friction notes.
- [StyleCop-Audit.md](StyleCop-Audit.md) — optional StyleCop package vs SCA.
- `Architecture.md` (repo root) — broader repo architecture.
- [Testing.md](../Testing.md) — general test strategy; analyzer specifics are under [Testing rules](#testing-rules) above.
- `Docs/Standards/Module-Documentation-Standard.md`
- [.agents/workflows/check-analyzers.md](../../.agents/workflows/check-analyzers.md)

## Changelog

- Added architecture/location details and rule quick-start guidance.
- Added module-standard top sections while preserving full rules reference below.
- Single-document hub: paths, architecture, testing, authoring, archives, archive table, expanded configuration sample.

## Quick Start (AI + Dev Workflow)

Use this section when you need fast fixes, then use the full rule catalog below for exact behavior and examples.

### Most frequently hit rules (fix-first order)

1. `SCA2003`: split long methods into smaller named steps.
2. `SCA2002`: remove nested calls by introducing intermediate locals.
3. `SCA3003`: reorder class members to expected layout.
4. `SCA1004` / `SCA1005`: normalize naming (`camelCase` when not public/internal — prefixes + leading case; `PascalCase` public or internal).
5. `SCA1002` / `SCA1003`: use block bodies and keep method/constructor signatures single-line.
6. `SCM003`: in MVVM descendants, use bind APIs instead of manual `PropertyChanged` wiring.

### Rule lookup tip

When you see `SCAxxxx` or `SCMxxx` in diagnostics:

1. Jump to [Rules reference](#rules-reference).
2. Find `### SCAxxxx` or `### SCMxxx` (`SCM` rules ship from **Scaffold.Mvvm.Analyzers**).
3. Apply the compliant example pattern directly.

### Rule ID map

Active rule IDs in this repository:

- `SCA1001`–`SCA2005` (**SCA0011** removed; **SCA5001** / **SCA5002** / **SCA4001**–**SCA4003** disabled; **SCA0025** unused ID) — core **Scaffold.Analyzers**
- `SCM001`–`SCM003` — MVVM pack (**Scaffold.Mvvm.Analyzers**; former **SCA0018** / **SCA0019** / **SCA0021**)

---

## Rules reference

Sections below list **what each rule does** and **short good/bad snippets**. For resolution notes and proposals, see the matching file under **`Plans/SCA-Analyzer-Revamp/`** (e.g. `SCA2003.md`).

### SCA1001 - No Method Comments

Methods must not have XML documentation comments or inline comments. The only exceptions are comments containing `todo` or `sample` (case-insensitive).

**Rationale:** Comments often compensate for poorly named or overly complex methods. Method names and structure should be self-documenting.

```csharp
// VIOLATION
/// <summary>Loads the player data from disk.</summary>
public void LoadPlayer()
{
    // read file
    var data = File.ReadAllText(path);
}

// COMPLIANT
public void LoadPlayer()
{
    var data = File.ReadAllText(path);
}

// ALLOWED (todo exception)
public void LoadPlayer()
{
    // todo: add error handling
    var data = File.ReadAllText(path);
}
```

---

### SCA2001 - Method Order

Instance methods must be declared after the methods that call them. If `A` calls `B`, then `B` must appear below `A` in the file (`calleeIndex > callerIndex` for each direct caller→callee edge). This enforces a top-down reading order.

Other methods may appear between caller and callee; only the **relative order** of caller and callee is checked.

Static methods are not evaluated by this rule (class-level placement for static members is covered by `SCA3003`).

**Rationale:** Code reads like a newspaper — high-level entry points at the top, implementation details below.

**Algorithm (implementation in `MethodOrderAnalyzer`):**

1. **Scope:** Consider only **non-static** instance methods with no **explicit interface** implementation specifier. **Declaration order** in the type is the order of `MethodDeclarationSyntax` members that pass this filter.
2. **Direct dependencies:** For each such method, walk its body for `InvocationExpressionSyntax` nodes. Resolve the invoked `IMethodSymbol` (using `OriginalDefinition`). If the callee is **another** instance method on the **same** type (and not a self-call), add a directed edge **caller → callee**.
3. **Callee below caller:** For each edge, if the callee appears **above or at** the caller in the file (`calleeIndex <= callerIndex`), report the **callee** — it must move **below** the caller.

```csharp
// VIOLATION - Initialize calls Setup, but Setup appears first
public void Setup() { }
public void Initialize() { Setup(); }

// COMPLIANT
public void Initialize() { Setup(); }
public void Setup() { }
```

---

### SCA2002 - No Nested Calls

Function calls and object constructions must not be nested as arguments beyond the configured depth. Extract intermediate values to named local variables when the depth limit is exceeded.

**Configuration:** `scaffold.SCA2002.max_nesting_depth` (default `1`). The analyzer reports when the computed nesting depth of an argument expression is **greater than or equal to** this value. Use `1` for strict mode (no call/object-creation in argument position). Use `2` to allow one level (for example `Outer(new Inner())` or `Outer(GetValue())`).

```ini
scaffold.SCA2002.max_nesting_depth = 1
```

**Depth algorithm (`NestedCallAnalyzer`):** Depth is computed only for **`InvocationExpressionSyntax`** and **`BaseObjectCreationExpressionSyntax`**. For an invocation, depth = `1 + max(depth of each argument expression)`; for a `new`, same using constructor arguments. Any other expression kind (including **lambda expressions**) yields depth **0** at that node—the analyzer does **not** recurse into lambda bodies, so calls inside `x => ...` do not increase depth. **Fluent** chains in argument position are usually still invocations, so they contribute depth like any other call.

**Exception:** `nameof()` expressions are treated as depth **0** (special-cased).

```csharp
// VIOLATION (default max_nesting_depth = 1)
var result = Process(GetInput());
var obj = new Handler(new Config());

// COMPLIANT
var input = GetInput();
var result = Process(input);

var config = new Config();
var obj = new Handler(config);

// ALLOWED
Debug.LogError(nameof(MyClass));
```

---

### SCA1002 - Curly-Bracket Bodies Only

Methods in a class must use block bodies with curly brackets. Expression-body syntax (`=>`) is not allowed on method declarations.

```csharp
// VIOLATION
public int GetCount() => items.Count;

// COMPLIANT
public int GetCount()
{
    return items.Count;
}
```

---

### SCA1003 - No Multi-Line Method/Constructor Signatures

Method and constructor signatures must fit on a single line. Method signatures include trailing generic constraints (`where T : ...`) and those constraints must stay on the same line as the signature.

For constructors, the initializer (`: base(...)` or `: this(...)`) is part of the signature:
- the initializer must start on the same line as the closing `)` of the parameter list
- the initializer itself must remain on a single line (no line breaks inside initializer arguments)

Multiline initializers are allowed (for example object/collection initializers in local declarations or assignments).

**Exception:** Fluent/builder chains using member access (`.Method().Method()`) are permitted to span lines.
**Exception:** Local declarations that use object/collection initializers are permitted to span lines.

```csharp
// VIOLATION - signature spans multiple lines
public void Register(
    string name,
    int priority)
{ }

// COMPLIANT
public void Register(string name, int priority) { }

// VIOLATION - constructor initializer starts on next line
public Sample(string value)
    : base(value)
{ }

// VIOLATION - constructor initializer arguments span lines
public Sample(string value) : base(
    value)
{ }

// COMPLIANT - constructor initializer stays on one line with signature
public Sample(string value) : base(value) { }

// VIOLATION - statement spans multiple lines
var result =
    someValue +
    otherValue;

// ALLOWED - fluent chain
builder
    .WithName("test")
    .WithPriority(1)
    .Build();

// ALLOWED - multiline collection initializer local declaration
List<int> values = new List<int>
{
    1,
    2
};
```

---

### SCA2003 - Small Functions

Methods must not exceed **15** non-empty lines of code by default (configurable). Blank lines are ignored. Lines that only continue a fluent/builder member chain (trimmed line starts with `.`) do not count. Refactor by extracting steps into well-named helper methods.

**Configuration:** Override the threshold in `.editorconfig`:
```ini
scaffold.SCA2003.max_lines = 15
```

```csharp
// VIOLATION - exceeds configured non-empty line limit (default 15)
public void ProcessOrder(Order order)
{
    ValidateOrder(order);
    var items = FetchItems(order);
    ApplyDiscounts(items);
    CalculateTotals(items);
    SaveToDatabase(order);
    SendConfirmation(order);
    UpdateInventory(items);
    NotifyWarehouse(order);
    LogAuditTrail(order);  // line 9
}

// COMPLIANT - extracted
public void ProcessOrder(Order order)
{
    ValidateOrder(order);
    var items = PrepareItems(order);
    FinalizeOrder(order, items);
}
```

---

### SCA2004 - Restrict Static Methods in Non-Static Classes

Static methods declared inside non-static classes are disallowed unless they are:
- extension methods
- parsing/conversion helpers (`Parse*`, `TryParse*`, `From*`, `To*`)
- factory methods (`Create*`, `Build*`, `New*`)

Static methods in static classes are always allowed.

```csharp
// VIOLATION
public sealed class Game
{
    private static void EnsureWaitTimeout(TimeSpan timeout) { }
}

// COMPLIANT - instance method
public sealed class Game
{
    private void EnsureWaitTimeout(TimeSpan timeout) { }
}

// COMPLIANT - static utility class
public static class TimeUtility
{
    public static bool IsNegative(TimeSpan timeout) => timeout < TimeSpan.Zero;
}

// COMPLIANT - parsing/conversion helper
public sealed class LevelId
{
    public static LevelId Parse(string raw) => new LevelId();
}

// COMPLIANT - factory method
public sealed class LevelFactory
{
    public static LevelFactory CreateDefault() => new LevelFactory();
}
```

---

### SCA2005 - No Nested Static Types Inside Instance Types

Nested `static` classes (and other static nested types) declared inside **non-static** enclosing types are flagged. Prefer a private instance collaborator or nested non-static type owned by the outer type.

```csharp
// VIOLATION
public sealed class MyService
{
    private static class Pipeline { }
}

// COMPLIANT — nested instance helper
public sealed class MyService
{
    private sealed class Pipeline { }
}
```

---

### SCA3005 - Namespace root segment must be allowed

**Implementation:** `NamespaceRootAnalyzer` (`SCA3005-NamespaceRootAnalyzer.cs`). Shared path/syntax logic lives in `NamespaceLayoutAnalysis.cs` with `NamespacePathResolution.cs`.

The **first** segment of every top-level namespace must be one of the configured allowed roots. Missing namespace (types in the global namespace) is also reported when roots are configured.

**Keys (read from per-tree options first, then global options):**

- `scaffold.SCA3005.root` — optional single root segment included in the allowed set (often `Scaffold`).
- `scaffold.SCA3005.allowed_roots` — semicolon-separated extra first-segment names (e.g. `GameModule;GameModuleDTO`).

**SCA3006** below uses the same allowed-root union when checking the full declared namespace against the folder-derived path.

---

### SCA3006 - Namespace must match folder path

**Implementation:** `NamespacePathAnalyzer` (`SCA3006-NamespacePathAnalyzer.cs`). Same shared helpers as **SCA3005**.

For files under a matched **content root**, the declared namespace must equal **`R` · folder suffix** for some allowed root **`R`**: one namespace segment for the root, then one segment per folder in the derived suffix (see `NamespacePathResolution`). Folder segments come from the path after the content root (file name excluded). **`Runtime`** and **`Implementation`** folder segments are omitted from the suffix; other segments are kept (including `Contracts`).

Diagnostics show the **full** expected namespace. The root segment used for that display string is chosen deterministically (`scaffold.SCA3005.root` when it appears in the allowed set; otherwise the lexicographically first allowed root).

**Keys:**

- `scaffold.SCA3006.content_roots` — semicolon-separated path markers (default `Assets/Scripts`). This repo also lists `LiveOps/Project` for Cloud Code sources.
- `scaffold.SCA3006.first_segment_ignore` — optional. When **omitted**, the first folder segment under the content root is skipped (legacy). When **set**, it is a semicolon-separated list of first-segment folder names to remove once (match `*` to always skip the first segment). An empty value can be supplied to disable legacy skip when tests or a folder layout need every segment.
- `scaffold.SCA3006.suffix_ignore_globs` — optional semicolon-separated globs; paths matching a glob skip **SCA3006** only (not **SCA3005**).

Generated paths (`obj/`, `bin/`, `*.g.cs`, etc.) are skipped. `Assets/Packages/com.scaffold.records/Runtime/IsExternalInit.cs` is exempt. Files that are not under any configured content root are skipped for both **SCA3005** and **SCA3006**. Assembly/module–only compilation units (metadata-only `AssemblyInfo.cs`) are skipped.

**Multiple top-level namespaces** and **types outside a block `namespace { }`** are **SCA3004** (`SingleTopLevelNamespaceAnalyzer`), not **SCA3005**/**SCA3006**.

```csharp
// File: Assets/Packages/com.scaffold.navigation/Container/NavigationInstaller.cs
// VIOLATION (namespace does not match folder-derived path for an allowed root)
namespace Utilities.Container.Navigation { }

// COMPLIANT
namespace Scaffold.Navigation.Container { }
```

```csharp
// File: Assets/Packages/com.scaffold.mvvm/Runtime/Binding/BindSet.cs
// COMPLIANT (Runtime/Implementation skipped from folder suffix)
namespace Scaffold.MVVM.Binding { }
```

```csharp
// File: Assets/Packages/com.scaffold.navigation/Runtime/Contracts/INavigation.cs
// COMPLIANT (Contracts kept in suffix)
namespace Scaffold.Navigation.Contracts { }
```

```ini
dotnet_diagnostic.SCA3005.severity = warning
dotnet_diagnostic.SCA3006.severity = warning
scaffold.SCA3005.root = Scaffold
scaffold.SCA3005.allowed_roots = GameModule;GameModuleDTO;Scaffold
scaffold.SCA3006.content_roots = Assets/Scripts;LiveOps/Project
```

---

### SCA3004 - One Top-Level Namespace Per File

**Implementation:** `SingleTopLevelNamespaceAnalyzer` (`SCA3004-SingleTopLevelNamespaceAnalyzer.cs`); shares `NamespaceLayoutAnalysis` with **SCA3005**/**SCA3006** for the same file-path gating.

Files under `Assets/Scripts/` must follow **one** of these layout rules:

1. **At most one** top-level `namespace` declaration (nested namespaces inside it are fine).
2. If the file uses a **block** `namespace { }` at compilation-unit scope, **types, records, enums, and delegates** must not be declared as **siblings** of that block at file scope—move them inside the namespace. **File-scoped** `namespace X;` keeps declarations under that namespace in the syntax tree, so this rule does not apply the same way. **Top-level statements** / implicit entry files are skipped.

This prevents sibling namespace declarations from bypassing namespace/folder conventions, and prevents “split” files that close a namespace block then declare more types at file scope.

`Assets/Packages/com.scaffold.records/Runtime/IsExternalInit.cs` is explicitly exempted for C# record compatibility.
Assembly/module-attributes-only files (for example `AssemblyInfo.cs`) are exempt as metadata-only files.

```csharp
// VIOLATION — two top-level namespaces
namespace Scaffold.Navigation.Contracts { }
namespace Scaffold.Navigation { }

// VIOLATION — type at file scope after a block namespace
namespace SampleNamespace
{
    public class MyClass { }
}
public class MyOtherClass { }

// COMPLIANT
namespace Scaffold.Navigation.Contracts { }

// COMPLIANT — file-scoped namespace; types belong to that namespace
namespace Scaffold.Infra.Events;

public class EventBus { }
```

---

### SCA1004 - Private Fields Must Be camelCase (prefixes + leading letter)

**Former SCA0009** was merged into **SCA1004** — one rule for fields that are **not** **`public`** or **`internal`** ( **`internal`** fields follow **SCA1005** like **`public`** ):

- No **leading `_`** or **single-letter `x_`** Hungarian-style prefix (`m_`, `s_`, `k_`, …).
- The name must **start with a lowercase letter** (camelCase).

```csharp
// VIOLATION
private int _count;
private string m_name;
private int s_count;
private int Count;

// COMPLIANT
private int count;
private string name;
```

---

### SCA1005 - Public or Internal Members Must Be PascalCase

**`public`** or **`internal`** fields, properties, and methods must start with an uppercase letter (**`internal`** is treated like **`public`** for naming; **`protected internal`** counts because it includes **`internal`**).

**Exceptions:** Unity's built-in members `gameObject` and `transform` are exempt. Override methods and operator overloads are also skipped.

```csharp
// VIOLATION
public int count;
internal void processData() { }

// COMPLIANT
internal int Count;
public void ProcessData() { }
```

---

### SCA5001 - *(disabled)*

**Not emitted.** The former **public runtime method entry validation** analyzer was removed pending replan. Keep `dotnet_diagnostic.SCA5001.severity = none` in `.editorconfig`. Archived source, tests, and prior plan notes: [`Analyzers/Scaffold/_Archive/SCA5001-Disabled/`](../../Analyzers/Scaffold/_Archive/SCA5001-Disabled/README.md). Status: [SCA5001.md](../../Plans/SCA-Analyzer-Revamp/SCA5001.md).

---

### SCA6001 - SerializeField backing for `public get / private set` (Unity types)

**Unity-only** (`Scaffold.Analyzers/Rules/Category06-UnityAndSerialization/`). Applies when the compilation references `UnityEngine*` and the file is under `Assets/Scripts/` (excluding `Tests/` and `Samples/`).

**Containing type** must be **`[Serializable]`** or inherit **`UnityEngine.Object`** (e.g. **`MonoBehaviour`**, **`ScriptableObject`**).

**Only** reports **`public` instance properties** with **`public get; private set;`** that are not expressed as a **private `[SerializeField]` field** plus a **public getter-only** property. Other shapes (`{ get; set; }`, getter-only, `init`, etc.) are not flagged.

```csharp
// VIOLATION — public get / private set without SerializeField-backed getter-only pattern
[Serializable]
public class ViewModel
{
    public int Count { get; private set; }
}

public sealed class Hud : MonoBehaviour
{
    public int Score { get; private set; }
}

// Not flagged — public setter
[Serializable]
public class ViewModel
{
    public int Count { get; set; }
}

// COMPLIANT
[Serializable]
public class ViewModel
{
    [SerializeField] private int count;
    public int Count => count;
}
```

---

### SCA3002 - One Top-Level Type per File (Unity Scripts)

Each file under `Assets/Scripts/` may contain **at most one top-level** type (class, struct, interface, enum, record). **Nested** types inside a type body are allowed and do not count toward the limit.

**Exception — generic arity variants:** several top-level types may share the **same simple name** if they differ only by **type parameter arity** (e.g. `IRequestHandler`, `IRequestHandler<TResponse>`, `IRequestHandler<TRequest, TResponse>` in `IRequestHandler.cs`). This keeps non-generic and generic “overloads” in one file.

**Primary** type: name matches the file name (e.g. `Game.cs` → `Game`); if no match, the **first** top-level type in the file is primary. Every **other** top-level type is a violation (move to its own file, or nest inside the primary type), unless it is part of a **same-name generic arity group** (another top-level type in the file has the same simple name but a different number of type parameters).

Skips `Tests/`, `Samples/`, and typical generated paths.

```csharp
// VIOLATION — two top-level types in Game.cs
public sealed class Game { }
internal enum GameState { A, B }  // SCA3002: move to GameState.cs or nest inside Game

// COMPLIANT — one top-level type; helper nested
public sealed class Game
{
    private enum LocalState { A, B }
    private LocalState state;
}

// COMPLIANT — same simple name, distinct type parameter counts (single file IRequestHandler.cs)
public interface IRequestHandler { }
public interface IRequestHandler<TResponse> : IRequestHandler { }
public interface IRequestHandler<TRequest, TResponse> : IRequestHandler<TResponse> where TRequest : class { }
```

---

### SCA6002 - Use TextMeshProUGUI Instead of UnityEngine.UI.Text

**Unity-only** (`Scaffold.Analyzers/Rules/Category06-UnityAndSerialization/`). By default, legacy `UnityEngine.UI.Text` is disallowed in favor of `TMPro.TextMeshProUGUI`. Configure additional forbidden metadata names and replacements:

```ini
scaffold.SCA6002.forbidden_types = UnityEngine.UI.Text=>TMPro.TextMeshProUGUI;Legacy.UI.OldLabel=>MyApp.UI.NewLabel
```

Entries are `MetadataName=>Replacement`, separated by `;`. If `=>` is omitted for an entry, the replacement defaults to `TMPro.TextMeshProUGUI`.

```csharp
// VIOLATION
using UnityEngine.UI;
public sealed class MainMenuView
{
    private Text currentLevelLabel;
}

// COMPLIANT
using TMPro;
public sealed class MainMenuView
{
    private TextMeshProUGUI currentLevelLabel;
}
```

---

### SCA5002 - *(disabled)*

**Not emitted.** The former **public runtime constructor parameter validation** analyzer was removed pending replan with **SCA5001**. Keep `dotnet_diagnostic.SCA5002.severity = none` in `.editorconfig`. Archived source (`ConstructorInvariantAnalyzer`, `InvariantUsageScope`), tests, and prior plan notes: [`Analyzers/Scaffold/_Archive/SCA5002-Disabled/`](../../Analyzers/Scaffold/_Archive/SCA5002-Disabled/README.md). Status: [SCA5002.md](../../Plans/SCA-Analyzer-Revamp/SCA5002.md).

---

### SCM001 - MVVM Classes Should Use Module Base Types

Classes inside the MVVM module should not manually implement MVVM notifier interfaces.  
Prefer inheriting from `Scaffold.MVVM.Model` or `Scaffold.MVVM.ViewModel` so shared behavior and source-generated features stay consistent.

This rule checks files under `Assets/Packages/com.scaffold.viewmodel/`, `Assets/Packages/com.scaffold.model/`, `Assets/Packages/com.scaffold.mvvm/`, and `Assets/Packages/com.scaffold.view/`, and skips `Tests/` and `Samples/`.

```csharp
// VIOLATION
public class InventoryViewModel : IViewModel
{
}

// COMPLIANT
public partial class InventoryViewModel : ViewModel
{
}
```

For `IViewModel`, this diagnostic is raised when a class directly inherits from `object` and implements `IViewModel` without inheriting from `ViewModel`.

---

### SCM002 - MVVM Generator Attributes Must Resolve

Known MVVM source-generator attributes must resolve to actual referenced types:
- `ObservableProperty`
- `NestedObservableObject`
- `BindSource`

If these attributes are unresolved, add references to:
- `CommunityToolkit.Mvvm`
- `CommunityToolkit.Mvvm.SourceGenerators`
- `MVVMCompositionGenerator`

```csharp
// VIOLATION (attribute unresolved)
public partial class InventoryViewModel
{
    [ObservableProperty]
    private int amount;
}

// COMPLIANT (attribute type available through references)
public partial class InventoryViewModel
{
    [ObservableProperty]
    private int amount;
}
```

---

### SCA3003 - Class Member Order

Class and struct members must follow this order:
- static properties
- const fields
- constructors
- indexers
- properties
- fields
- events
- instance methods
- static methods
- private nested types (`class`, `struct`, `enum`)
- operators

Special exception:
- a backing field may appear directly below its related property (for example `MyValue => myValue;` followed by `private int myValue;`).

```csharp
// VIOLATION
public sealed class Game
{
    public int Value => value;
    public Game() { }
    private int value;
}

// COMPLIANT
public sealed class Game
{
    public Game() { }
    public int Value => value;
    private int value;
}
```

---

### SCM003 - MVVM Descendants Must Use Bind APIs (No Manual PropertyChanged Wiring)

Classes that inherit from `Scaffold.MVVM.Model`, `Scaffold.MVVM.ViewModel`, or `Scaffold.MVVM.ViewElement` must use the framework's bind APIs and base notification flow.

Manual `PropertyChanged` wiring/declarations in these descendants is disallowed, including:
- subscribing/unsubscribing to `PropertyChanged` with `+=`/`-=`
- declaring a `PropertyChanged` event
- manually calling `OnPropertyChanged(...)`
- manually calling `UpdateBinding(...)`
- manually calling `RegisterNestedProperties()`

```csharp
// VIOLATION
public sealed class InventoryView : ViewElement<InventoryViewModel>
{
    public void Attach(INotifyPropertyChanged vm)
    {
        vm.PropertyChanged += OnChanged;
    }
}

// COMPLIANT
public sealed class InventoryView : ViewElement<InventoryViewModel>
{
    protected override void OnBind()
    {
        Bind(() => viewModel.Amount, amount => amountLabel.text = amount.ToString());
    }
}
```

---

### SCA4001 - *(disabled)*

**Not emitted.** The former **cross-module `*.Runtime` reference** analyzer was removed pending **contract/boundary** replan. Keep `dotnet_diagnostic.SCA4001.severity = none` in `.editorconfig`. Archived source, tests, and prior plan notes: [`Analyzers/Scaffold/_Archive/SCA4001-Disabled/`](../../Analyzers/Scaffold/_Archive/SCA4001-Disabled/README.md). Status: [SCA4001.md](../../Plans/SCA-Analyzer-Revamp/SCA4001.md).

---

### SCA4002 - *(disabled)*

**Not emitted.** The former **required module folders** analyzer was removed pending **folder/module layout** replan. Keep `dotnet_diagnostic.SCA4002.severity = none` in `.editorconfig`. Archived source, tests, and prior plan notes: [`Analyzers/Scaffold/_Archive/SCA4002-Disabled/`](../../Analyzers/Scaffold/_Archive/SCA4002-Disabled/README.md). Status: [SCA4002.md](../../Plans/SCA-Analyzer-Revamp/SCA4002.md).

---

### SCA4003 - *(disabled)*

**Not emitted.** The former **module asmdef placement and naming** analyzer was removed pending **asmdef/module layout** replan (with **SCA4001** / **SCA4002**). Keep `dotnet_diagnostic.SCA4003.severity = none` in `.editorconfig`. Archived source, tests, and prior plan notes: [`Analyzers/Scaffold/_Archive/SCA4003-Disabled/`](../../Analyzers/Scaffold/_Archive/SCA4003-Disabled/README.md). Status: [SCA4003.md](../../Plans/SCA-Analyzer-Revamp/SCA4003.md).

---

### SCA2006 - *(removed)*

**Not emitted.** The former **same-layer initialization usage** analyzer was removed; initialization ordering is expected to be handled by runtime composition. Keep `dotnet_diagnostic.SCA2006.severity = none` in `.editorconfig`. Archived source, tests, and prior plan notes: [`Analyzers/Scaffold/_Archive/SCA2006-Removed/`](../../Analyzers/Scaffold/_Archive/SCA2006-Removed/README.md). Status: [SCA2006.md](../../Plans/SCA-Analyzer-Revamp/SCA2006.md).

---

### SCA1006 - Loop Bodies Require Braces and Next-Line Formatting

`for`, `foreach`, `while`, and `do-while` must use braces, and the opening brace must be on the next line after the loop header.

```csharp
// VIOLATION
for (int i = 0; i < 10; i++) Tick();

// VIOLATION
while (ready) { Tick(); }

// COMPLIANT
for (int i = 0; i < 10; i++)
{
    Tick();
}
```

---

### SCA1007 - Conditional Braces and Inline If Exception

Only one pattern may omit braces:
- single-line `if (...) statement;`
- no `else` branch
- exactly one embedded statement

All other `if/else` forms must use braces and next-line block formatting.

```csharp
// ALLOWED
if (ready) Tick();

// VIOLATION
if (ready)
    Tick();

// VIOLATION
if (ready) Tick();
else Tick();

// COMPLIANT
if (ready)
{
    Tick();
}
else
{
    Pause();
}
```

---

### SCA8001 - Runtime Dead Code for Non-Public Methods/Constructors

In `Runtime/` paths, non-public methods/constructors that are not referenced by non-test code are flagged as dead code. Default descriptor severity is **Info** (suggestion-style); prefer `dotnet_diagnostic.SCA8001.severity = suggestion` in `.editorconfig` for repo policy.

This rule skips:
- public/protected public API members
- interface implementations
- override members
- files under `Tests/` and `Samples/`
- common Unity engine callback names (for example `OnEnable`, `Awake`, `Update`) and any extra names listed in `scaffold.SCA8001.exempt_method_names` (semicolon-separated)

```csharp
// VIOLATION (unused private runtime helper)
private void NormalizeState() { }

// COMPLIANT (used in non-test runtime flow)
public void Run()
{
    NormalizeState();
}

private void NormalizeState() { }
```

---

### SCA8002 - Runtime Code Must Not Use `#pragma warning disable`

In `Runtime/` paths under `Assets/Scripts/`, `#pragma warning disable` is discouraged. Default descriptor severity is **Info** (suggestion-style); prefer `dotnet_diagnostic.SCA8002.severity = suggestion` in `.editorconfig`.

This rule skips:
- files under `Tests/` and `Samples/`
- generated/build paths (`obj/`, `bin/`, `*.g.cs`)
- a `#pragma warning disable` that is immediately followed on the **next source line** by `#pragma warning restore` for the **same** warning code set (tightly scoped pair)

```csharp
// VIOLATION (long-lived disable with restore far away)
#pragma warning disable CS0168
public sealed class Game
{
    public void Run() { }
}
#pragma warning restore CS0168

// COMPLIANT — fix issue instead of suppression
public sealed class Game
{
    public void Run() { }
}

// COMPLIANT — consecutive disable/restore for the same codes (exempt from reporting)
#pragma warning disable CS0168
#pragma warning restore CS0168
public sealed class Game2 { }
```

---
