---
description: create a custom roslyn analyzer linter rule
---

When the user asks you to create a new custom C# linter rule or analyzer, follow these step-by-step instructions carefully to ensure the analyzer is compatible with the project and acts as an "agent-friendly" tool.

The analyzers live in this repository's `.NET Standard 2.0` class library `Scaffold.Analyzers`.

### Step 1: Core Rules for Agent-Friendly Analyzers

To design analyzers that are truly "agent-friendly", you must follow these rules when formatting the warning/error message. The agent relies heavily on this output to fix the user's code later!

1. **Let Roslyn Handle Spatial Data (Path, Line, and Column)**
   - Never embed the file path or line number directly in your static message text.
   - When you call `Diagnostic.Create(Rule, node.GetLocation())`, Roslyn automatically outputs the absolute file path, line number, and column index during the CLI build step (e.g., `<absolute-path-to-file>.cs(17,14): warning SCA0104:...`). The agent will parse this.

2. **Be Explicit About the Error Title**
   - Every `MessageFormat` string must explicitly list `Error [ID]:` as the start of the message.
   - *Example*: `"Error SCA0104: ..."`

3. **Be Descriptive and Actionable**
   - Never just describe the error. You must tell the agent *exactly how* to fix it.
   - *Bad*: `"Do not use '$' string interpolation."`
   - *Good*: `"Error SCA0104: Do not use '$' string interpolation. Replace $\"...\" with string.Format(\"...\", args)."`

### Step 2: Create the Analyzer Class

In `Analyzers/Scaffold/Scaffold.Analyzers/Rules/`, add the file under the **category folder** that matches the rule’s disposition (e.g., `Category01-SurfaceAndNaming/`, `Category03-OrganizationAndPlacement/`, `Category07-Hygiene/` for hygiene policy rules). Name it after the rule (e.g., `MyNewRuleAnalyzer.cs`).

- The class must inherit from `DiagnosticAnalyzer`.
- The class must be decorated with `[DiagnosticAnalyzer(LanguageNames.CSharp)]`.
- The namespace must be `Scaffold.Analyzers` (folder path does not change the namespace).

### Step 3: Define the Diagnostic Descriptor

You need to define a unique Diagnostic ID: **`SCA` + category digit (1–8) + three-digit index** within that category (e.g. **SCA1008** for the next rule in category 1). See `Docs/Analyzers/Analyzers.md` → Rule IDs and [SCA-Rule-Disposition.md](../../Docs/Analyzers/SCA-Rule-Disposition.md). Then define a `DiagnosticDescriptor`. 

```csharp
public const string DiagnosticId = "SCA3003";
private const string Category = "Style";

// Apply the Agent-friendly rules here:
private static readonly LocalizableString MessageFormat = 
    "Error SCA3003: The node '{0}' is invalid. Replace it with the correct syntax `correctSyntax()` immediately.";

private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
    DiagnosticId,
    "Brief title of the rule",
    MessageFormat,
    Category,
    DiagnosticSeverity.Warning, // Or Error if critical
    isEnabledByDefault: true);

public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);
```

### Step 4: Register the Analyzer & Implement Logic

Override the `Initialize` method to register your analyzer, usually to a `SyntaxKind`.

```csharp
public override void Initialize(AnalysisContext context)
{
    // Required setup
    context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
    context.EnableConcurrentExecution();

    // Register node action
    context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
}

private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
{
    var methodDeclaration = (MethodDeclarationSyntax)context.Node;
    
    // Check your logic
    if (methodDeclaration.Identifier.Text.StartsWith("BadPrefix"))
    {
        // Report the diagnostic! The location provides path/line/col automatically.
        var diagnostic = Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation(), methodDeclaration.Identifier.Text);
        context.ReportDiagnostic(diagnostic);
    }
}
```

### Step 5: Verify the Analyzer

Run `dotnet build -c Release` inside `Analyzers/Scaffold/Scaffold.Analyzers/`. Address any compilation errors immediately. Ensure you test it on standard syntax components to be certain the diagnostic behaves as designed.
