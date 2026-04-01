# SCA4003 — Asmdef placement and naming *(archived plan text)*

**Back:** [SCA Analyzer Revamp — Exec Plan](SCA-Analyzer-Revamp-ExecPlan.md) · **Disposition:** Category 4 — Full **revamp**

## What it does

Validates **asmdef** file **location** and **name** for a module: discourages asmdef at module root when configured; expects asmdef path/name to match assembly naming; uses **suffix → folder** map for subfolder conventions.

## Diagnostic message

```
Error SCA4003: Assembly '{0}' must declare asmdef at '{1}' with name '{0}'
```

`{0}` — assembly name; `{1}` — expected asmdef path (same message used for several failure modes in this analyzer).

## Implementation (current)

**Source:** `Analyzers/Scaffold/Scaffold.Analyzers/ModuleAsmdefConventionAnalyzer.cs`  
**Tests:** `Scaffold.Analyzers.Tests/ModuleAsmdefConventionAnalyzerTests.cs`

**Report paths:** (1) **asmdef at module root** when disallowed; (2) **no** asmdef at expected candidate path; (3) **JSON name** ≠ assembly / module root:

```csharp
if (disallowModuleRootAsmdef && HasRootAsmdef(moduleContext.ModuleDirectoryPath, fileNames))
{
    context.ReportDiagnostic(Diagnostic.Create(rule, moduleContext.DiagnosticLocation, moduleContext.AssemblyName, expectedPath));
    return;
}

if (string.IsNullOrWhiteSpace(asmdefPath))
{
    context.ReportDiagnostic(Diagnostic.Create(rule, moduleContext.DiagnosticLocation, moduleContext.AssemblyName, candidatePaths.First()));
    return;
}

if (!string.Equals(asmdefName, moduleContext.AssemblyName, StringComparison.Ordinal) &&
    !string.Equals(asmdefName, moduleContext.ModuleRootName, StringComparison.Ordinal))
    context.ReportDiagnostic(Diagnostic.Create(rule, moduleContext.DiagnosticLocation, moduleContext.AssemblyName, asmdefPath));
```

## How it works

- Locates candidate `.asmdef` paths via module context + maps; reads JSON name field; compares to expected assembly naming.

## Configs

| Key | Purpose |
| --- | ------- |
| `scaffold.SCA4003.exempt_assemblies` | Skip these assembly names. |
| `scaffold.SCA4003.suffix_folder_map` | Maps assembly suffixes to folder segments. |
| `scaffold.SCA4003.disallow_module_root_asmdef` | Bool (default true in code path). |
| `scaffold.SCA4003.allow_unknown_suffix_in_any_subfolder` | Bool. |
| `dotnet_diagnostic.SCA4003.severity` | Standard override. |

## StyleCop comparison

- **Equivalent to StyleCop?** No.
- **StyleCop rule(s):** None — no **SA** rule for **asmdef** placement, naming, or Unity assembly definition policy.
- **Difference vs SCA:** **SCA4003** is **Unity/Scaffold** build layout; StyleCop does not replace it.

## Cases it catches

- **Asmdef JSON** `name` does not match assembly / expected path:

```json
{ "name": "WrongName", "references": [] }
```

- **Asmdef at module root** when `disallow_module_root_asmdef` is true.

## Cases it does not catch

- Assembly in **`exempt_assemblies`** list.

```ini
scaffold.SCA4003.exempt_assemblies = Scaffold.Legacy.Editor
```

- Non-Unity / **no** `TryGetModuleContext` — early exit.

## Edge cases / risk

- **Line endings** / path separators on different OS in CI vs local.

## Good

- Keeps Unity-generated projects stable.

## Bad

- Complex config — easy to misconfigure for new modules.

## Overall feedback

Disposition: **revamp** with more **golden** path fixtures.

## Proposal

- Add `TestData/asmdef/` samples referenced by tests; link from `Analyzers.md`.

## Resolution

**Disposition:** Full review / revamp (Category 4).

- **Recorded decision:** *(pending)*
- **Starting point:** Golden asmdef fixtures + config matrix (**SCA4002** / **SCA4003** both archived — see plan stubs).
