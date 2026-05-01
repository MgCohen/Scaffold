# SCA5001 — Public runtime methods: entry validation *(archived plan text)*

**Back:** [SCA Analyzer Revamp — Exec Plan](../../../Plans/SCA-Analyzer-Revamp/SCA-Analyzer-Revamp-ExecPlan.md) · **Disposition:** Category 5 — Full **revamp** *(superseded by removal — see live [SCA5001.md](../../../Plans/SCA-Analyzer-Revamp/SCA5001.md))*

## What it does

Requires **public** runtime methods with **parameters** to show **invariant validation at entry** (guard `if`, or calls matching allowed prefixes like `Validate*`, `Ensure*`, etc.). Excludes overrides, interface impl quirks, Unity event system edge cases — see archived source.

## Diagnostic message

```
Error SCA5001: Public runtime method '{0}' does not validate invariants at entry. Add a leading guard clause (`if (...) return/throw`) or call a validation method (`Validate*`, `TryValidate*`, `Ensure*`, `Guard*`) before business logic.
```

`{0}` — method name.

## Implementation (archived)

**Source (archive):** `Analyzers/Scaffold/_Archive/SCA5001-Disabled/InvariantEntryPointAnalyzer.cs`; **`InvariantUsageScope.cs`** is under `_Archive/SCA5002-Disabled/` (shared; **SCA5002** disabled)  
**Tests (archive):** `Analyzers/Scaffold/_Archive/SCA5001-Disabled/InvariantEntryPointAnalyzerTests.cs`

**Moment of check:** after `IsCandidateEntryPoint` + `ShouldValidateForType`, if **`HasEntryValidation`** returns **false**, report:

```csharp
var methodDeclaration = (MethodDeclarationSyntax)context.Node;
var symbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
if (!IsCandidateEntryPoint(methodDeclaration, symbol)) return;
if (symbol == null) return;
if (!InvariantUsageScope.ShouldValidateForType(context.Compilation, symbol.ContainingType)) return;

var allowedPrefixes = GetAllowedPrefixes(options);
if (HasEntryValidation(methodDeclaration, allowedPrefixes)) return;

context.ReportDiagnostic(Diagnostic.Create(rule, methodDeclaration.Identifier.GetLocation(),
    methodDeclaration.Identifier.Text));
```

`HasEntryValidation` inspects the **first executable** statement after leading locals for guard / null-coalescing assignment / `Validate*`-prefixed call (see archived source).

## How it works

- Resolves symbol; filters to candidate **public** ordinary methods with body and parameters on **runtime** paths.
- Uses `InvariantUsageScope.ShouldValidateForType` to scope which types need validation.
- Parses leading statements for guard/validation patterns vs allowed prefix calls.

## Configs

| Key | Purpose |
| --- | ------- |
| `scaffold.SCA5001.allowed_prefixes` | Comma-separated custom prefixes (extends defaults). |
| `dotnet_diagnostic.SCA5001.severity` | Standard override. |

## StyleCop comparison

- **Equivalent to StyleCop?** No.
- **StyleCop rule(s):** None — no **SA** rule for **public runtime method entry validation** / required call patterns.
- **Difference vs SCA:** **SCA5001** was **Scaffold** contract behavior; StyleCop does not replace it.

## Cases it catches

- **Public** instance method with **parameters**, runtime path, no leading guard / `Validate*` / `Ensure*` pattern at entry:

```csharp
public void Move(int dx, int dy)
{
    x += dx;  // flagged: no guard at entry
}
```

## Cases it does not catch

- **No parameters** — not a candidate:

```csharp
public void Tick() { }
```

- **Override** / **explicit interface** implementations (filtered out).

```csharp
public override string ToString() => Name;
```

## Edge cases / risk

- Thin **forwarder** still expected to validate by rule heuristics:

```csharp
public void Save(User u) => _repo.Save(u);  // may still flag
```

## Good

- Pushes defensive public API surface in runtime code.

## Bad

- Heuristic “leading validation” can miss valid patterns or flag wrong ones.

## Overall feedback

Disposition: **revamp** — tests and implementation need joint review.

## Proposal

- Shared test fixtures with **SCA5002** for prefix lists.
- Document **exact** recognized patterns in code comments + `Analyzers.md`.

## Resolution *(historical)*

**Disposition:** Full review / revamp (Category 5).

- **Recorded decision:** *(pending)*
- **Starting point:** Revamp implementation + tests; align prefix config story with **SCA5002**; update disposition Notes when stable.
