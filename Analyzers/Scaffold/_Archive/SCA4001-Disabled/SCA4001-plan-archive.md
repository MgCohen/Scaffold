# SCA4001 — Cross-module `*.Runtime` references *(archived plan text)*

**Back:** [SCA Analyzer Revamp — Exec Plan](SCA-Analyzer-Revamp-ExecPlan.md) · **Disposition:** Category 4 — Full **revamp**

## What it does

At **compilation** scope: if the assembly references another **`Scaffold.*.Runtime`** (or configured module runtime) **outside** the same module root, reports — enforces **composition** boundaries (bootstrap exempt; infrastructure assemblies skipped).

## Diagnostic message

```
Error SCA4001: Assembly '{0}' references runtime assembly '{1}'. Non-bootstrap modules must avoid cross-module '*.Runtime' references.
```

`{0}` — current assembly name; `{1}` — referenced runtime assembly name.

## Implementation (current)

**Source:** `Analyzers/Scaffold/Scaffold.Analyzers/RuntimeAssemblyBoundaryAnalyzer.cs`  
**Tests:** `Scaffold.Analyzers.Tests/RuntimeAssemblyBoundaryAnalyzerTests.cs`

**Moment of check:** each **`ReferencedAssemblyNames`** entry that is a **module `*.Runtime`**, not same module, not exempt:

```csharp
foreach (var reference in context.Compilation.ReferencedAssemblyNames)
{
    var referenceName = reference?.Name;
    if (!IsRuntimeAssemblyName(referenceName)) continue;
    if (IsSameModule(currentModuleRoot, referenceName)) continue;
    if (ShouldSkipForNoContractsException(referenceName, assetsScriptsRoot, moduleRootsWithoutContracts)) continue;

    context.ReportDiagnostic(Diagnostic.Create(rule, GetDiagnosticLocation(context.Compilation),
        assemblyName, referenceName));
}
```

## How it works

- Uses **assembly name** graph + `ModuleConventions.GetModuleRootName`.
- **`scaffold.SCA4001.no_contract_modules`** (see source for exact key usage) exempts modules without separate contracts.

## Configs

| Key | Purpose |
| --- | --- |
| `scaffold.SCA4001.no_contract_modules` | Listed module roots allowed to reference certain runtimes (no-contract exception path). |
| `dotnet_diagnostic.SCA4001.severity` | Standard override. |

## StyleCop comparison

- **Equivalent to StyleCop?** No.
- **StyleCop rule(s):** None — no **SA** rule for **cross-module `*.Runtime` assembly references** or module boundaries.
- **Difference vs SCA:** **SCA4001** is **Scaffold** module architecture; StyleCop does not replace it.

## Cases it catches

- Assembly `Scaffold.Foo.Runtime` references `Scaffold.Bar.Runtime` where **Bar** ≠ **Foo** module root (simplified):

```text
// Foo.Runtime assembly references Scaffold.Bar.Runtime.dll → diagnostic
```

## Cases it does not catch

- Reference **within** same module (`Scaffold.Foo.Runtime` ↔ `Scaffold.Foo.Contracts` is not this rule’s focus — see `IsSameModule`).

- **Bootstrap** / **test** / **infrastructure** assemblies (see `IsBootstrapAssembly`, `IsInfrastructureAssembly`).

## Edge cases / risk

- **`no_contract_modules`** misconfiguration allows a reference that should be blocked:

```ini
# Too broad — hides real cross-module issues
scaffold.SCA4001.no_contract_modules = SomeModule
```

## Good

- Protects modular compilation and dependency hygiene.

## Bad

- Misconfiguration of exceptions can hide real issues.

## Overall feedback

Disposition: **revamp** with extra tests around **no_contract** scenarios.

## Proposal

- Document **every** exempt path in `Analyzers.md` with examples; add structural tests for multi-assembly graphs.

## Resolution

**Disposition:** Full review / revamp (Category 4).

- **Recorded decision:** *(pending)*
- **Starting point:** Validate `no_contract_modules` + bootstrap skips; expand structural graph tests.
