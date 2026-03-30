# SCA4002 — Required module folders *(archived plan text)*

**Back:** [SCA Analyzer Revamp — Exec Plan](SCA-Analyzer-Revamp-ExecPlan.md) · **Disposition:** Category 4 — Full **revamp**

## What it does

Ensures each module directory under `Assets/Scripts` contains **required** subfolders (default includes **Runtime**, **Tests** — see parser) unless module or requirement is **exempted**.

## Diagnostic message

```
Error SCA4002: Module '{0}' is missing required folder '{1}' (expected at '{2}')
```

`{0}` — module root name; `{1}` — folder name; `{2}` — full expected path.

## Implementation (current)

**Source:** `Analyzers/Scaffold/Scaffold.Analyzers/ModuleRequiredFoldersAnalyzer.cs`  
**Tests:** `Scaffold.Analyzers.Tests/ModuleRequiredFoldersAnalyzerTests.cs`

**Moment of check:** for each **required** folder name, **`Directory.Exists`** on **`ModuleDirectoryPath + folder`**; if missing, report:

```csharp
foreach (var requiredFolder in requiredFolders)
{
    if (moduleSpecificExemptions.Contains(requiredFolder)) continue;

    var expectedFolderPath = Path.Combine(moduleContext.ModuleDirectoryPath, requiredFolder);
    if (Directory.Exists(expectedFolderPath)) continue;

    context.ReportDiagnostic(Diagnostic.Create(rule, moduleContext.DiagnosticLocation,
        moduleContext.ModuleRootName, requiredFolder, expectedFolderPath));
}
```

## How it works

- `TryGetModuleContext` finds module root from compilation trees; **Directory.Exists** on disk for each required folder (RS1035 suppressed in analyzer).

## Configs

| Key | Purpose |
| --- | --- |
| `scaffold.SCA4002.required_folders` | List of required folder names. |
| `scaffold.SCA4002.exempt_module_roots` | Modules fully exempt. |
| `scaffold.SCA4002.exempt_requirements` | Per-module exempt folder requirements. |
| `dotnet_diagnostic.SCA4002.severity` | Standard override. |

## StyleCop comparison

- **Equivalent to StyleCop?** No.
- **StyleCop rule(s):** None — no **SA** rule for **required module folder** layout.
- **Difference vs SCA:** **SCA4002** is **Scaffold** repository structure; **keep SCA**.

## Cases it catches

- Module root on disk **missing** required subfolder:

```text
Assets/Scripts/Feature/MyModule/
  Runtime/   ✓
  (Tests/ missing) → SCA4002 if Tests required
```

## Cases it does not catch

- Module listed in **`exempt_module_roots`**.

```ini
scaffold.SCA4002.exempt_module_roots = MyModule
```

## Edge cases / risk

- **CI** checkout **sparse** or **shallow** — folder missing on agent though intended in repo:

```yaml
# Pipeline copies only Runtime/ — Tests/ missing → false positive
```

## Good

- Enforces visible module skeleton for onboarding.

## Bad

- Disk I/O in analyzer — performance and environment coupling.

## Overall feedback

Was paired with **SCA4003** (asmdef); both rules later **archived** pending layout policy.

## Proposal

- Revamp tests for **exempt_requirements** matrix; document default folder list in exec config samples.

## Resolution

**Disposition:** Full review / revamp (Category 4).

- **Recorded decision:** *(pending)*
- **Starting point:** Harden disk-based checks + CI assumptions; document defaults and exemptions.
