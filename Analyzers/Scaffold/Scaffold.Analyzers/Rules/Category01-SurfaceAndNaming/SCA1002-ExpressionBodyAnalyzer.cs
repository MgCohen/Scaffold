using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scaffold.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ExpressionBodiedMethodAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SCA1002";
        private const string Category = "Style";

        private static readonly LocalizableString Title = "Use curly-bracket bodies for methods and constructors";
        private static readonly LocalizableString MessageFormat = "Error SCA1002: '{0}' uses a concise expression body `=>`. Convert it to a block body surrounded by curly brackets `{{ }}`.";
        private static readonly LocalizableString Description = "Always use block bodies for methods and constructors. Expression-body syntax is prohibited (including override, explicit interface implementation, and constructors).";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath)) return;

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule)) return;

            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            if (methodDeclaration.ExpressionBody != null)
            {
                var diagnostic = Diagnostic.Create(rule, methodDeclaration.ExpressionBody.GetLocation(), methodDeclaration.Identifier.Text);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
        {
            if (ModuleConventions.IsExcludedThirdPartyVendorPath(context.Node.SyntaxTree.FilePath)) return;

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            if (!AnalyzerConfig.TryGetEffectiveDescriptor(options, DiagnosticId, Rule, out var rule)) return;

            var ctor = (ConstructorDeclarationSyntax)context.Node;

            if (ctor.ExpressionBody != null)
            {
                var diagnostic = Diagnostic.Create(rule, ctor.ExpressionBody.GetLocation(), ctor.Identifier.Text);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
