using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class StaticMethodScopeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA2004";
        private const string Category = "Design";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Restrict static methods in non-static classes",
            "Error SCA2004: Static method '{0}' in non-static class '{1}' is not allowed. Convert it to an instance method, or rename it to an allowed parsing/conversion (`Parse*`, `TryParse*`, `From*`, `To*`) or factory (`Create*`, `Build*`, `New*`) pattern; extension methods are also allowed.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Static methods should be limited to extension methods, parsing/conversion helpers, and factory methods.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath)) return;

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule)) return;

            var methodDeclaration = (MethodDeclarationSyntax)context.Node;
            if (!methodDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword)) return;

            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
            if (methodSymbol == null) return;
            if (methodSymbol.MethodKind != MethodKind.Ordinary) return;
            if (methodSymbol.IsExtensionMethod) return;
            if (IsInStaticClass(methodSymbol)) return;
            if (IsAllowedByName(methodSymbol.Name)) return;

            var containingTypeName = methodSymbol.ContainingType?.Name ?? "UnknownType";
            var diagnostic = Diagnostic.Create(rule, methodDeclaration.Identifier.GetLocation(), methodSymbol.Name, containingTypeName);
            context.ReportDiagnostic(diagnostic);
        }

        private static bool IsInStaticClass(IMethodSymbol methodSymbol)
        {
            return methodSymbol.ContainingType != null && methodSymbol.ContainingType.IsStatic;
        }

        private static bool IsAllowedByName(string methodName)
        {
            return
                methodName.StartsWith("Parse", StringComparison.Ordinal) ||
                methodName.StartsWith("TryParse", StringComparison.Ordinal) ||
                methodName.StartsWith("From", StringComparison.Ordinal) ||
                methodName.StartsWith("To", StringComparison.Ordinal) ||
                methodName.StartsWith("Create", StringComparison.Ordinal) ||
                methodName.StartsWith("Build", StringComparison.Ordinal) ||
                methodName.StartsWith("New", StringComparison.Ordinal);
        }
    }
}

