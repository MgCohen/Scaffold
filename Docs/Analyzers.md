# Scaffold Analyzers

## Architecture & Locations

### Source Code
The analyzer source and `.csproj` live outside the Unity Assets folder at:
`[Repository Root]/Analyzers/Scaffold/Scaffold.Analyzers/`

This contains one `.cs` file per rule, plus `AnalyzerConfig.cs` for shared configuration utilities.

### Compiled Artifact
When the project is built, the DLL is output to:
`[Repository Root]/Analyzers/Output/Scaffold.Analyzers.dll`

This file is committed to git. It is **not** placed inside `Assets/` — Unity does not process it.

### Integration
A `Directory.Build.props` file at the repo root automatically injects the DLL as a Roslyn analyzer into every `.csproj` in the tree:

```xml
<Project>
  <ItemGroup Condition="'$(MSBuildProjectName)' != 'Scaffold.Analyzers'">
    <Analyzer Include="$(MSBuildThisFileDirectory)Analyzers/Output/Scaffold.Analyzers.dll"
              Condition="Exists('$(MSBuildThisFileDirectory)Analyzers/Output/Scaffold.Analyzers.dll')" />
  </ItemGroup>
</Project>
```

Diagnostics surface in the IDE and to AI tooling via the language server. Unity's compiler never runs them.

### Building
```bash
cd Analyzers/Scaffold/Scaffold.Analyzers
dotnet build -c Release
```

---

## Overview

The Scaffold Analyzer is a custom Roslyn diagnostic analyzer that enforces the project's architectural and code style rules at compile time. Its primary audience is the development environment — IDE inline diagnostics and AI tooling — rather than the Unity runtime.

The rules are designed to produce code that is:
- **Readable by AI**: small, flat, uncommented methods with predictable structure
- **Consistently structured**: naming, ordering, and formatting rules that apply uniformly across all modules
- **Architecturally sound**: namespace alignment, async patterns, and separation of concerns

All rules are warnings by default and can be individually overridden or suppressed via `.editorconfig`.

---

## Rules Reference

### SCA0001 — No Method Comments

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

### SCA0002 — Method Order

Methods must be declared after the methods that call them. If `A` calls `B`, then `B` must appear below `A` in the file. This enforces a top-down reading order.

**Rationale:** Code reads like a newspaper — high-level entry points at the top, implementation details below.

```csharp
// VIOLATION — Initialize calls Setup, but Setup appears first
public void Setup() { }
public void Initialize() { Setup(); }

// COMPLIANT
public void Initialize() { Setup(); }
public void Setup() { }
```

---

### SCA0003 — No Nested Calls

Function calls and object constructions must not be nested as arguments. Extract intermediate values to named local variables.

**Exception:** `nameof()` expressions are allowed to be nested.

```csharp
// VIOLATION
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

### SCA0004 — Curly-Bracket Bodies Only

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

### SCA0005 — No Multi-Line Signatures or Statements

Method signatures must fit on a single line. Statements inside a method body must not span multiple lines.

**Exception:** Fluent/builder chains using member access (`.Method().Method()`) are permitted to span lines.

```csharp
// VIOLATION — signature spans multiple lines
public void Register(
    string name,
    int priority)
{ }

// COMPLIANT
public void Register(string name, int priority) { }

// VIOLATION — statement spans multiple lines
var result =
    someValue +
    otherValue;

// ALLOWED — fluent chain
builder
    .WithName("test")
    .WithPriority(1)
    .Build();
```

---

### SCA0006 — Small Functions

Methods must not exceed 8 lines of code (configurable). Refactor by extracting steps into well-named helper methods.

**Configuration:** Override the threshold in `.editorconfig`:
```ini
scaffold.SCA0006.max_lines = 12
```

```csharp
// VIOLATION — 9 lines
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

// COMPLIANT — extracted
public void ProcessOrder(Order order)
{
    ValidateOrder(order);
    var items = PrepareItems(order);
    FinalizeOrder(order, items);
}
```

---

### SCA0009 — Namespace Must Match Folder Structure

Namespaces must be prefixed with the project name and match the file's folder path. Unity-specific segments (e.g., `Assets`, `Scripts`) are excluded from the expected namespace.

```csharp
// File: Assets/Scripts/Infra/Events/EventBus.cs
// VIOLATION
namespace Utilities.Events { }

// COMPLIANT
namespace Scaffold.Infra.Events { }
```

---

### SCA0010 — No Underscore or Hungarian Prefixes on Private Fields

Private fields must not use `_` or `m_` prefixes.

```csharp
// VIOLATION
private int _count;
private string m_name;

// COMPLIANT
private int count;
private string name;
```

---

### SCA0011 — Private Fields Must Be camelCase

Private fields must start with a lowercase letter.

```csharp
// VIOLATION
private int Count;

// COMPLIANT
private int count;
```

---

### SCA0012 — Public Members Must Be PascalCase

Public fields, properties, and methods must start with an uppercase letter.

**Exceptions:** Unity's built-in members `gameObject` and `transform` are exempt. Override methods and operator overloads are also skipped.

```csharp
// VIOLATION
public int count;
public void processData() { }

// COMPLIANT
public int Count;
public void ProcessData() { }
```

---

### SCA0013 — Prefer Unity's Awaitable Over Task/ValueTask

Methods should return Unity's `Awaitable` type instead of `Task` or `ValueTask`. Use `Task`/`ValueTask` only when `Awaitable` is not applicable (e.g., non-Unity libraries, interfaces crossing module boundaries).

```csharp
// VIOLATION
public async Task LoadSceneAsync(string name) { }
public async ValueTask<int> FetchIdAsync() { }

// COMPLIANT
public async Awaitable LoadSceneAsync(string name) { }
```

---

## Configuration

All rules support per-rule severity override via `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.SCA0001.severity = none      # suppress
dotnet_diagnostic.SCA0006.severity = error     # escalate to error
dotnet_diagnostic.SCA0013.severity = suggestion
```

Valid severity values: `error`, `warning`, `suggestion`, `info`, `hidden`, `silent`, `none`.

The `AnalyzerConfig.cs` class handles all config parsing. It caches overridden descriptors and provides:
- `GetEffectiveDescriptor()` — reads and applies severity from editorconfig
- `ShouldSuppress()` — returns true if severity is set to `none`
- `GetInt()` — reads integer config values (used by SCA0006 for line threshold)

---

## Adding a New Rule

1. Create `[RuleName]Analyzer.cs` in `Analyzers/Scaffold/Scaffold.Analyzers/`
2. Extend `DiagnosticAnalyzer`, declare a `DiagnosticDescriptor` with the next available `SCA{id}`
3. Override `Initialize(AnalysisContext context)` and register your syntax/symbol action
4. Use `AnalyzerConfig.GetEffectiveDescriptor()` and `ShouldSuppress()` at the call site so the rule respects editorconfig
5. Build: `dotnet build -c Release` from `Analyzers/Scaffold/Scaffold.Analyzers/`
6. Add a test in `Analyzers/Scaffold/Scaffold.Analyzers.Tests/`

Use the `/create-custom-analyzer` workflow (`.agents/workflows/create-custom-analyzer.md`) to scaffold the boilerplate automatically.
