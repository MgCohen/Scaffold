# SCA5002 — Public runtime constructors: parameter validation *(archived plan text)*

**Back:** [SCA Analyzer Revamp — Exec Plan](SCA-Analyzer-Revamp-ExecPlan.md) · **Disposition:** Category 5 — Revamp (**SCA5001** method rule disabled — see [SCA5001.md](SCA5001.md))

## What it does

Requires **public** runtime **constructors** with parameters to perform **entry validation** (guard or `Validate*`/`Ensure*`/`Guard*` patterns). Includes **primitive semantic token** heuristics for “obvious” names.

## Diagnostic message

```
Error SCA5002: Public runtime constructor '{0}' does not validate constructor parameters at entry. Add a leading guard clause (`if (...) throw ...`) or call a validation method (`Validate*`, `TryValidate*`, `Ensure*`, `Guard*`) before assignments/business logic.
```

`{0}` — type name for the constructor.

## Implementation (current)

**Source:** `Analyzers/Scaffold/Scaffold.Analyzers/ConstructorInvariantAnalyzer.cs`  
**Tests:** `Scaffold.Analyzers.Tests/ConstructorInvariantAnalyzerTests.cs`

**Moment of check:** candidate public runtime ctor with parameters, after **`ShouldSkipForPrimitiveLikeParameters`**, if **`HasEntryValidation`** is **false**:

```csharp
if (!IsCandidateEntryPoint(constructorDeclaration, symbol)) return;
if (!InvariantUsageScope.ShouldValidateForType(context.Compilation, symbol.ContainingType)) return;
if (ShouldSkipForPrimitiveLikeParameters(symbol, options)) return;

if (HasEntryValidation(context, constructorDeclaration, allowedPrefixes)) return;

context.ReportDiagnostic(Diagnostic.Create(rule, constructorDeclaration.Identifier.GetLocation(),
    constructorDeclaration.Identifier.Text));
```

`HasEntryValidation` allows guard, coalesce assignment, **`ArgumentNullException.ThrowIfNull`**, or **`Validate*`**-style calls (see source).

## How it works

- Mirrors the **former SCA5001** method-entry policy but for constructors; additional logic for **primitive semantic** parameter names (may reduce need for explicit guards). **SCA5001** is not shipped; see [SCA5001.md](SCA5001.md).

## Configs

| Key | Purpose |
| --- | --- |
| `scaffold.SCA5002.allowed_prefixes` | Custom validation method prefixes. |
| `scaffold.SCA5002.primitive_semantic_tokens` | Token list for semantic primitive parameters. |
| `dotnet_diagnostic.SCA5002.severity` | Standard override. |

## StyleCop comparison

- **Equivalent to StyleCop?** No.
- **StyleCop rule(s):** None — no **SA** rule requires **constructor parameter validation** on public runtime types.
- **Difference vs SCA:** **SCA5002** is **Scaffold** contract behavior; StyleCop does not replace it.

## Cases it catches

- **Public** ctor with parameters, no **guard** / validation prefix at entry:

```csharp
public Player(int health)
{
    this.health = health;  // flagged if no Validate*/if throw
}
```

## Cases it does not catch

- **Private** ctor or **parameterless** public ctor (see `IsCandidateEntryPoint` in source).

```csharp
private Player(int id) { }
public Player() { }
```

## Edge cases / risk

- **Primitive semantic** tokens (e.g. `count`, `index`) may reduce pressure to guard — still policy-heavy.

```csharp
public Grid(int width, int height) // width/height may be treated as semantic primitives
```

## Good

- Closes the “constructors bypass method guards” hole.

## Bad

- Duplicate mental load with 0012 for maintainers.

## Overall feedback

Keep prefixes **synchronized** in docs and `.editorconfig` examples.

## Proposal

- Consider **shared config** type for 0012+0017 prefixes to avoid drift (single key or merged parser).

## Resolution

**Disposition:** Full review / revamp (Category 5); **SCA5001** companion rule is disabled.

- **Recorded decision:** *(pending)*
- **Starting point:** Unify prefix / primitive-token policy with any future **SCA5001** replan; update disposition when aligned.
