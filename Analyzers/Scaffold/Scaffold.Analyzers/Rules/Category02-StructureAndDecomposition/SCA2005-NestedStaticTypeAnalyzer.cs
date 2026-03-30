using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    /// <summary>
    /// Flags nested static types declared inside non-static types (prefer instance-owned collaborators).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NestedStaticTypeInInstanceAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA2005";
        private const string Category = "Design";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Avoid nested static types inside instance types",
            "Error SCA2005: Nested static type '{0}' is not allowed inside instance type '{1}'. Prefer a private instance handler (or nested non-static type) owned by '{1}'.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Nested static types inside instance types encourage static pipelines; prefer collaborators owned by the enclosing instance.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(
                AnalyzeTypeDeclaration,
                SyntaxKind.ClassDeclaration,
                SyntaxKind.StructDeclaration,
                SyntaxKind.InterfaceDeclaration,
                SyntaxKind.RecordDeclaration);
        }

        private static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is not TypeDeclarationSyntax nested)
            {
                return;
            }

            if (nested.Parent is not TypeDeclarationSyntax containing)
            {
                return;
            }

            if (!nested.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
            {
                return;
            }

            if (containing.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
            {
                return;
            }

            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath))
            {
                return;
            }

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule))
            {
                return;
            }

            var diagnostic = Diagnostic.Create(
                rule,
                nested.Identifier.GetLocation(),
                nested.Identifier.Text,
                containing.Identifier.Text);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
