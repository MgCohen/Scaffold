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
        public const string DiagnosticId = "SCA0004";
        private const string Category = "Style";

        private static readonly LocalizableString Title = "Use curly-bracket bodies for methods in a class";
        private static readonly LocalizableString MessageFormat = "Error SCA0004: Method '{0}' uses a concise expression body `=>`. Convert it to a standard method body surrounded by curly brackets `{ }`.";
        private static readonly LocalizableString Description = "Always use curly-bracket bodies for methods in a class. Expression-body syntax is prohibited for method declarations.";

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
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            if (methodDeclaration.ExpressionBody != null)
            {
                var diagnostic = Diagnostic.Create(Rule, methodDeclaration.ExpressionBody.GetLocation(), methodDeclaration.Identifier.Text);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
