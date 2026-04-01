# SCA2006 — Same-layer initialization usage

**Back:** [SCA Analyzer Revamp — Exec Plan](SCA-Analyzer-Revamp-ExecPlan.md) · **Disposition:** Category 4 — Full **revamp**

## What it does

For types implementing **`Scaffold.Scope.Contracts.IAsyncLayerInitializable`**, analyzes **initialization** call chains to block **same-layer** dependency **instance member** usage unless attributes **`AllowSameLayerInitializationUsage`** / **`AllowInitializationCallChain`** exempt (see source for full rules).

## Diagnostic message

```
Error SCA2006: Initialization call chain for '{0}' cannot use same-layer dependency member '{1}' from '{2}'.
```

`{0}` — initialization entry **method name**; `{1}` — same-layer **member** used; `{2}` — **dependency type** name.

## Implementation (current)

**Source:** `Analyzers/Scaffold/Scaffold.Analyzers/InitializationSameLayerUsageAnalyzer.cs`  
**Tests:** `Scaffold.Analyzers.Tests/InitializationSameLayerUsageAnalyzerTests.cs`

Entry: **`InitializeAsync`** on a type implementing **`IAsyncLayerInitializable`**, with layer from path. **`CallChainAnalysis`** walks operations and reports **same-layer member usage** via:

```csharp
private void ReportUsageViolation(Location location, IMethodSymbol entryMethod, string memberName, string dependencyType)
{
    Diagnostic diagnostic = Diagnostic.Create(rule, location, entryMethod.Name, memberName, dependencyType);
    reportDiagnostic(diagnostic);
}
```

(Operation-block analysis invokes this when a **tainted** same-layer dependency’s instance member is used — see **`OperationBlockAnalysis`** in the same file.)

## How it works

- Large **operation-graph** style analysis (`CallChainAnalysis`) — see file for details.

## Configs

| Key | Purpose |
| --- | ------- |
| `dotnet_diagnostic.SCA2006.severity` | Standard override. |

## StyleCop comparison

- **Equivalent to StyleCop?** No.
- **StyleCop rule(s):** None — no **SA** rule for **same-layer initialization** usage patterns.
- **Difference vs SCA:** **SCA2006** is **Scaffold** initialization policy; **keep SCA**.

## Cases it catches

- During **initialization** call chain, use of a **same-layer** dependency’s **instance member** without allow-attribute (message shape: `Initialization call chain for '{0}' cannot use same-layer dependency member '{1}' from '{2}'.`):

```csharp
// Conceptual — exact symbols from Scaffold.Scope.Contracts.*
// IAsyncLayerInitializable entry calls into same-layer field.Method() → may report
```

## Cases it does not catch

- **`IAsyncLayerInitializable`** not in compilation → analyzer **never** registers symbol actions.

```csharp
// Project without Scaffold.Scope.Contracts reference
```

## Edge cases / risk

- **Attributes** `AllowSameLayerInitializationUsage` / `AllowInitializationCallChain` exempt paths — mis-apply → wrong silence or noise.

```csharp
[AllowSameLayerInitializationUsage]
public async Task InitAsync() { /* ... */ }
```

## Good

- Encodes a subtle startup correctness constraint.

## Bad

- Hard for newcomers; needs diagrams.

## Overall feedback

Prioritize **integration tests** and **documentation** over micro-syntax tests.

## Proposal

- Add architecture diagram to `Analyzers.md` or a dedicated `Docs/Analyzers/SCA2006-Initialization.md` when revamping (optional; only if team wants).

## Resolution

**Disposition:** Full review / revamp (Category 4).

- **Recorded decision:** *(pending)*
- **Starting point:** More integration-style cases + docs; optional architecture diagram for DI/layer model.
