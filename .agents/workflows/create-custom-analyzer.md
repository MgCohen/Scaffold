---
description: create a custom roslyn analyzer linter rule
---

When the user asks you to create a new custom C# linter rule or analyzer, follow these step-by-step instructions carefully to ensure the analyzer is compatible with the project and acts as an "agent-friendly" tool.

The analyzers live in the `.NET Standard 2.0` class library `Scaffold.Analyzers` inside the `Scaffold` project.

### Step 1: Core Rules for Agent-Friendly Analyzers

To design analyzers that are truly "agent-friendly", you must follow these rules when formatting the warning/error message. The agent relies heavily on this output to fix the user's code later!

1. **Let Roslyn Handle Spatial Data (Path, Line, and Column)**
   - Never embed the file path or line number directly in your static message text.
   - When you call `Diagnostic.Create(Rule, node.GetLocation())`, Roslyn automatically outputs the absolute file path, line number, and column index during the CLI build step (e.g., `C:\ProjectPath\File.cs(17,14): warning SCA0104:...`). The agent will parse this.

2. **Be Explicit About the Error Title**
   - Every `MessageFormat` string must explicitly list `Error [ID]:` as the start of the message.
   - *Example*: `"Error SCA0104: ..."`

3. **Be Descriptive and Actionable**
   - Never just describe the error. You must tell the agent *exactly how* to fix it.
   - *Bad*: `"Do not use '$' string interpolation."`
   - *Good*: `"Error SCA0104: Do not use '$' string interpolation. Replace $\"...\" with string.Format(\"...\", args)."`

### Step 2: Create the Analyzer Class

In the `Scaffold.Analyzers/Scaffold.Analyzers` subfolder, create a new C# class file (e.g., `MyNewRuleAnalyzer.cs`).

- The class must inherit from `DiagnosticAnalyzer`.
- The class must be decorated with `[DiagnosticAnalyzer(LanguageNames.CSharp)]`.
- The namespace must be `Scaffold.Analyzers`.

### Step 3: Define the Diagnostic Descriptor

You need to define a unique Diagnostic ID (e.g., `SCA00XX` — check existing files for the next number) and a `DiagnosticDescriptor`. 

```csharp
public const string DiagnosticId = "SCA0020";
private const string Category = "Style";

// Apply the Agent-friendly rules here:
private static readonly LocalizableString MessageFormat = 
    "Error SCA0020: The node '{0}' is invalid. Replace it with the correct syntax `correctSyntax()` immediately.";

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

// turbo
Run `dotnet build` inside the `Scaffold.Analyzers` directory. Address any compilation errors immediately. Ensure you test it on standard syntax components to be certain the diagnostic behaves as designed.
